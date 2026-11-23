# Glossary

Shared vocabulary of this project. Specs, code names, commits and discussions use these terms
exactly; a concept that needs a sentence to explain gets an entry here instead.

| Term | Meaning | Not to be confused with |
|---|---|---|
| Game Bar Widget | Native Xbox Game Bar extension (`Microsoft.Gaming.XboxGameBar`) hosting the transparent chat overlay over exclusive fullscreen games | Standalone desktop window |
| Overlay View | Primary widget window rendering `overlay.html` from `custom-twitch-chat` with transparent background and click-through support | Settings View |
| Settings View | Secondary Game Bar widget window rendering `index.html` (Configurator) and syncing config updates to the Overlay View | Overlay View |
| Game Bar Bridge | Lightweight script and host message protocol (`gamebar-bridge.js` / `WidgetBridge`) connecting `custom-twitch-chat` with the Game Bar host state | Twitch IRC client |
| Pin State | Game Bar widget state (`Pinned` + `ClickThroughEnabled`) where chrome is hidden, background is transparent, and pointer input passes to the game | Interactive mode |
| Config Sync | Live propagation of normalized config or `#c=1.<base64url>` Config Hash from the Settings View to the Overlay View | Static file export |
| Seam | Public interface where automated tests observe behavior without coupling to internals | Internal helper |
