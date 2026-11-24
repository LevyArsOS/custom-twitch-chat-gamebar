using System.Net.Http.Headers;
using System.Text.Json;
using Windows.System;

namespace CustomTwitchChat.GameBar.App;

public sealed partial class SettingsWidgetPage
{
    private static readonly HttpClient AuthHttp = new();
    private CancellationTokenSource? _authCts;

    private async void OpenTwitchDevConsoleAsync()
    {
        await Launcher.LaunchUriAsync(new Uri("https://dev.twitch.tv/console/apps"));
        _statusText.Text = "Opened dev.twitch.tv/console/apps. Create a Public client app, copy its Client ID and paste it here.";
    }

    private async void StartTwitchDeviceLoginAsync()
    {
        ReadControlsIntoWorkingConfig();
        var clientId = (_clientIdBox.Text ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(clientId))
        {
            _statusText.Text = "Paste your Twitch Public Client ID first (click Dev Console to create one).";
            return;
        }

        _authCts?.Cancel();
        _authCts = new CancellationTokenSource();
        var ct = _authCts.Token;

        var scopes = new List<string>();
        if (_authPointsCheck.IsChecked == true) scopes.Add("channel:read:redemptions");
        if (_authFollowsCheck.IsChecked == true) scopes.Add("moderator:read:followers");
        if (scopes.Count == 0)
        {
            scopes.Add("channel:read:redemptions");
            _authPointsCheck.IsChecked = true;
        }

        _authEnabledCheck.IsChecked = true;
        ApplyToOverlay();

        try
        {
            var form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["scopes"] = string.Join(' ', scopes),
            });
            var devRes = await AuthHttp.PostAsync("https://id.twitch.tv/oauth2/device", form, ct);
            var devBody = await devRes.Content.ReadAsStringAsync(ct);
            if (!devRes.IsSuccessStatusCode)
            {
                _statusText.Text = $"Twitch device auth failed ({(int)devRes.StatusCode}): check your Client ID.";
                return;
            }

            using var devDoc = JsonDocument.Parse(devBody);
            var deviceCode = devDoc.RootElement.GetProperty("device_code").GetString() ?? "";
            var userCode = devDoc.RootElement.GetProperty("user_code").GetString() ?? "";
            var verifyUri = devDoc.RootElement.TryGetProperty("verification_uri", out var vu)
                ? vu.GetString() ?? "https://www.twitch.tv/activate"
                : "https://www.twitch.tv/activate";
            var intervalSec = devDoc.RootElement.TryGetProperty("interval", out var iv) ? Math.Max(3, iv.GetInt32()) : 5;

            try
            {
                var pkg = new Windows.ApplicationModel.DataTransfer.DataPackage();
                pkg.SetText(userCode);
                Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(pkg);
            }
            catch
            {
                // Clipboard optional.
            }

            _statusText.Text = $"Twitch Code: {userCode} (copied to clipboard!) — Authorize in browser; waiting for confirmation...";
            var launchUrl = verifyUri.Contains('?') ? verifyUri : $"{verifyUri}?public=true&device-code={Uri.EscapeDataString(userCode)}";
            await Launcher.LaunchUriAsync(new Uri(launchUrl));

            await PollDeviceTokenAsync(clientId, deviceCode, scopes, intervalSec, ct);
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer login attempt.
        }
        catch (Exception ex)
        {
            _statusText.Text = $"Twitch login error: {ex.Message}";
        }
    }

    private async Task PollDeviceTokenAsync(
        string clientId,
        string deviceCode,
        List<string> requestedScopes,
        int intervalSec,
        CancellationToken ct)
    {
        var nonce = _workingConfig.TryGetValue("authNonce", out var n) && n is double d ? (int)d : 1;
        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(intervalSec), ct);
            var form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["device_code"] = deviceCode,
                ["grant_type"] = "urn:ietf:params:oauth:grant-type:device_code",
            });
            var tokRes = await AuthHttp.PostAsync("https://id.twitch.tv/oauth2/token", form, ct);
            var tokBody = await tokRes.Content.ReadAsStringAsync(ct);
            using var tokDoc = JsonDocument.Parse(tokBody);

            if (tokRes.IsSuccessStatusCode && tokDoc.RootElement.TryGetProperty("access_token", out var atProp))
            {
                var accessToken = atProp.GetString() ?? "";
                var refreshToken = tokDoc.RootElement.TryGetProperty("refresh_token", out var rt) ? rt.GetString() ?? "" : "";
                var expiresIn = tokDoc.RootElement.TryGetProperty("expires_in", out var ei) ? ei.GetInt64() : 14400;

                using var valReq = new HttpRequestMessage(HttpMethod.Get, "https://id.twitch.tv/oauth2/validate");
                valReq.Headers.Authorization = new AuthenticationHeaderValue("OAuth", accessToken);
                var valRes = await AuthHttp.SendAsync(valReq, ct);
                var valBody = await valRes.Content.ReadAsStringAsync(ct);
                using var valDoc = JsonDocument.Parse(valBody);
                var userId = valDoc.RootElement.TryGetProperty("user_id", out var ui) ? ui.GetString() ?? "" : "";
                var login = valDoc.RootElement.TryGetProperty("login", out var lg) ? lg.GetString() ?? "" : "";

                var sessionJson = JsonSerializer.Serialize(new
                {
                    access_token = accessToken,
                    refresh_token = refreshToken,
                    expires_at = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + expiresIn * 1000,
                    scopes = requestedScopes,
                    user_id = userId,
                    login,
                    nonce,
                });

                _hostApp.SaveAuthSession($"ctc.auth.v1.{clientId}", sessionJson);
                _statusText.Text = $"Twitch connected as @{login}!";
                return;
            }

            var msg = tokDoc.RootElement.TryGetProperty("message", out var m) ? m.GetString() : "";
            if (msg == "slow_down") intervalSec += 5;
            else if (msg is "expired_token" or "access_denied")
            {
                _statusText.Text = $"Twitch login {msg}; click Login Twitch to retry.";
                return;
            }
        }
    }
}
