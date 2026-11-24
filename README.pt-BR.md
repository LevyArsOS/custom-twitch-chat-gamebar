# Custom Twitch Chat — Widget para Xbox Game Bar

[![CI](https://github.com/LevyArsOS/custom-twitch-chat-gamebar/actions/workflows/ci.yml/badge.svg?branch=develop)](https://github.com/LevyArsOS/custom-twitch-chat-gamebar/actions/workflows/ci.yml)
[![Package MSIX](https://github.com/LevyArsOS/custom-twitch-chat-gamebar/actions/workflows/package-msix.yml/badge.svg?branch=develop)](https://github.com/LevyArsOS/custom-twitch-chat-gamebar/actions/workflows/package-msix.yml)
[![Licença: MIT](https://img.shields.io/github/license/LevyArsOS/custom-twitch-chat-gamebar)](LICENSE)
[![Idiomas: EN | PT-BR](https://img.shields.io/badge/docs-EN%20%7C%20PT--BR-green)](README.md)

*[Read in English](README.md)*

Widget nativo para Xbox Game Bar (`Microsoft.Gaming.XboxGameBar`) que embute o [`custom-twitch-chat`](https://github.com/LevyArsOS/custom-twitch-chat) (`overlay.html` e `index.html`), permitindo fixar o chat da Twitch com fundo 100% transparente e *click-through* por cima de jogos em tela cheia exclusiva (DirectX/Vulkan) via `Win+G`.

## Recursos

- **Overlay sobre Fullscreen Exclusivo**: Registrado via `microsoft.gameBarUIExtension` para rodar na camada de overlay do Xbox Game Bar acima de jogos em tela cheia exclusiva.
- **Pin State Transparente e Click-Through**: Reage aos eventos `Pinned`, `ClickThroughEnabled`, `GameBarDisplayMode` e `RequestedOpacity`. Quando fixado fora do menu da Game Bar, o chrome oculta automaticamente e os cliques atravessam direto para o jogo (`gb-pinned-clickthrough`).
- **Configurador Embutido com Sincronização em Tempo Real**: Traz a **Overlay View** (`CustomTwitchChat.Overlay`) e a **Settings View** (`CustomTwitchChat.Settings`). Alterações no configurador ou o botão **Apply to Game Bar Overlay** atualizam o chat fixado instantaneamente via `BroadcastChannel` e bridge do host.
- **Compatibilidade Total com `custom-twitch-chat`**: Aceita Config Hashes `#c=1.<base64url>`, `config.json`, `chat.html` exportado (`window.__CTC_CONFIG__`), `chat.data.json` do StreamElements e login OAuth Device Code para Channel Points e Follows.

## Instalação (Usuário Final — Sem Modo Desenvolvedor)

1. Baixe o arquivo `CustomTwitchChat.GameBar-v0.1.1-x64.zip` em [GitHub Releases](https://github.com/LevyArsOS/custom-twitch-chat-gamebar/releases).
2. Extraia o ZIP e clique duas vezes em **`Install.cmd`**:
   - Aceite a permissão de Administrador (1 única vez) para o Windows confiar no certificado de assinatura (`CustomTwitchChat.cer`).
   - Clique em **Instalar** na janela nativa do Instalador de Aplicativos do Windows.
3. Abra **Custom Twitch Chat** no Menu Iniciar para escolher seu canal, importar uma configuração do configurador web (`.json` / `.html` / link `#c=1...`) ou fazer login com a Twitch.
4. Pressione `Win+G` em qualquer jogo, abra **Menu de Widgets** -> **Custom Twitch Chat Overlay**, clique em **Fixar (Pin)** e ative o ícone de **Click-through** para manter o chat transparente por cima do jogo.

## Build e Instalação (Sideload) no Windows (Desenvolvedores)

1. Sincronize os assets web do repositório irmão `custom-twitch-chat`:
   ```bash
   ./scripts/sync-web-assets
   ```
2. Gere o pacote `.msix` no Windows (.NET 9 + Windows 10 SDK `10.0.19041.0`):
   ```powershell
   .\scripts\package-msix.ps1 -Configuration Release -Platform x64
   ```
3. Instale o pacote localmente:
   ```powershell
   .\scripts\install-dev-msix.ps1
   ```
4. Pressione `Win+G` para abrir a Xbox Game Bar, abra **Menu de Widgets** -> **Custom Twitch Chat Overlay**, posicione na tela, clique em **Fixar (Pin)** e ative o **Click-through** se desejar. Clique na engrenagem do widget para abrir o configurador embutido.

## Desenvolvimento e Verificação

```bash
./scripts/sync-web-assets   # Atualiza WebAssets a partir de ../custom-twitch-chat/dist
./scripts/check             # Checagem de política + build Release (-warnaserror) + xUnit + cobertura >= 90%
```

## Licença

[MIT](LICENSE) © 2026 LevyArsOS
