# Custom Twitch Chat — Xbox Game Bar Widget

[![CI](https://github.com/LevyArsOS/custom-twitch-chat-gamebar/actions/workflows/ci.yml/badge.svg?branch=develop)](https://github.com/LevyArsOS/custom-twitch-chat-gamebar/actions/workflows/ci.yml)
[![Package MSIX](https://github.com/LevyArsOS/custom-twitch-chat-gamebar/actions/workflows/package-msix.yml/badge.svg?branch=develop)](https://github.com/LevyArsOS/custom-twitch-chat-gamebar/actions/workflows/package-msix.yml)
[![License: MIT](https://img.shields.io/github/license/LevyArsOS/custom-twitch-chat-gamebar)](LICENSE)
[![Languages: EN | PT-BR](https://img.shields.io/badge/docs-EN%20%7C%20PT--BR-green)](README.pt-BR.md)

*[Leia em Português](README.pt-BR.md)*

Native Xbox Game Bar widget (`Microsoft.Gaming.XboxGameBar`) that embeds [`custom-twitch-chat`](https://github.com/LevyArsOS/custom-twitch-chat) (`overlay.html` and `index.html`) so you can pin a 100% transparent, click-through Twitch chat overlay directly above exclusive fullscreen DirectX/Vulkan games (`Win+G`).

## Features

- **Exclusive Fullscreen Overlay**: Registered via `microsoft.gameBarUIExtension` so Windows renders the widget inside the Xbox Game Bar overlay plane on top of exclusive fullscreen games.
- **Transparent Pin State & Click-Through**: Responds to `Pinned`, `ClickThroughEnabled`, `GameBarDisplayMode`, and `RequestedOpacity`. When pinned outside the Game Bar menu, window chrome hides and pointer input passes straight through to the game (`gb-pinned-clickthrough`).
- **Embedded Configurator & Live Sync**: Includes both the **Overlay View** (`CustomTwitchChat.Overlay`) and **Settings View** (`CustomTwitchChat.Settings`). Editing settings or clicking **Apply to Game Bar Overlay** updates the pinned chat in real time via `BroadcastChannel` and the host bridge.
- **Full `custom-twitch-chat` Compatibility**: Supports `#c=1.<base64url>` Config Hashes, `config.json`, exported `chat.html` (`window.__CTC_CONFIG__`), legacy StreamElements `chat.data.json`, and in-widget Device Code OAuth for Channel Points and Follows.

## Install (End Users — No Developer Mode Required)

1. Download `CustomTwitchChat.GameBar-v0.1.0-x64.zip` from [GitHub Releases](https://github.com/LevyArsOS/custom-twitch-chat-gamebar/releases).
2. Extract the ZIP archive and double-click **`Install.cmd`**:
   - Accept the one-time Administrator prompt so Windows trusts the signing certificate (`CustomTwitchChat.cer`).
   - Click **Install** in the native Windows App Installer window.
3. Open **Custom Twitch Chat** from the Start Menu to set your channel, import a config from the web configurator (`.json` / `.html` / `#c=1...` URL), or sign in with Twitch OAuth.
4. Press `Win+G` in any game, open **Widget Menu** -> **Custom Twitch Chat Overlay**, click the **Pin** icon and the **Click-through** icon to keep chat transparent above your game.

## Build & Sideload on Windows (Developers)

1. Sync web assets from a sibling `custom-twitch-chat` checkout (or generate standalone fallback assets):
   ```bash
   ./scripts/sync-web-assets
   ```
2. Build the sideloadable `.msix` package on Windows (.NET 9 + Windows 10 SDK `10.0.19041.0`):
   ```powershell
   .\scripts\package-msix.ps1 -Configuration Release -Platform x64
   ```
3. Install the package locally:
   ```powershell
   .\scripts\install-dev-msix.ps1
   ```
4. Press `Win+G` to open Xbox Game Bar, open **Widget Menu** -> **Custom Twitch Chat Overlay**, position it, click the **Pin** icon, and enable **Click-through** if desired. Click the **Settings (gear)** icon in the widget title bar to open the embedded Configurator.

## Development & Verification

```bash
./scripts/sync-web-assets   # Refresh WebAssets from ../custom-twitch-chat/dist
./scripts/check             # Content scan + Release build (-warnaserror) + xUnit + >= 90% coverage gate
```

## License

[MIT](LICENSE) © 2026 LevyArsOS
