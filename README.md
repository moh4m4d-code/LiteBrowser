# Lite Browser

A clean, dark, tabbed browser for Windows built to stay light on RAM and CPU.
WPF + .NET 8 on top of the system Edge WebView2 runtime (already installed on Windows 10/11), so there is no bundled Chromium.

## Download
Grab the latest build from the **Releases** page:
- `LiteBrowser-standalone.exe` - runs anywhere, nothing to install
- `LiteBrowser.exe` - much smaller, needs the .NET 8 Desktop Runtime

## How it stays light
- Shared WebView2 runtime, capped renderer processes, low-end-device mode
- Background tabs are hidden, set to a low memory target, then **suspended** after 30 s (not if playing audio)
- Background networking, component updates, sync and autofill are off
- Workstation non-concurrent GC, no telemetry, no extensions engine

## Shortcuts
Ctrl+T new tab, Ctrl+W close tab, Ctrl+L address bar, F5 reload

## Build
```
dotnet run
```
Every push to `main` is built by GitHub Actions and published to Releases.
