# WeatherBeacon

**A desktop weather widget for Windows · First official release · 1.0.0.0**

English | [简体中文](README_CN.md)

WeatherBeacon brings current conditions, hourly forecasts and a five-day outlook into a compact glass-style card. Place it on your desktop and keep the weather within reach.

> **Made with AI: the project's code, interface and documentation were primarily generated and revised with AI assistance. The maintainer defines requirements, tests the application and makes release decisions.**

## Features

- **Weather at a glance:** current temperature, conditions, hourly forecasts, daily highs and lows, detailed weather descriptions and a highlighted current day.
- **Glass-style interface:** translucent cards, soft gradients, weather icons and animated backgrounds, with animation, opacity and corner-radius controls.
- **Flexible placement:** drag, lock or reset the widget and restore its saved position on launch.
- **City selection:** search manually or request an approximate IP-based location and confirm the result.
- **Temperature units:** Celsius and Fahrenheit.
- **Automatic updates:** a 15-minute default interval, with 5, 10, 15, 30 and 60-minute options. Failed requests retain valid data from the current session and retry automatically.
- **System tray:** single-click to show or hide, double-click for settings, or right-click for visibility, settings and exit controls.
- **Startup support:** launch at the current user's sign-in without administrator privileges or automatically opening settings.
- **Single instance:** another launch activates the existing instance.
- **Weather alerts:** actual Xiaomi alerts delivered through Windows Toast, with a blue/yellow/orange/red threshold and persistent deduplication. Disabled by default; enable on the Notifications page.
- **Developer previews:** confirm activation on the About page to preview weather and day/night scenes, restore real conditions, and send clearly labeled test notifications.

## Download and run

This release provides a **self-contained Windows x64 build**, targeting 64-bit Windows 10 version 1903 or later / Windows 11 devices. A separate .NET runtime installation is not required.

1. Download and extract the complete release package.
2. Run `WeatherBeacon.exe`.
3. Double-click its tray icon, open the Weather page and select a city through search or IP lookup.
4. Adjust appearance, refresh interval and temperature units, then save settings.

Keep the executable together with its accompanying files. Exit the previous version through the tray before updating.

## Usage notes

- The widget is a **normal, non-topmost window**. Other applications can cover it, and Win+D may hide it. Use the tray to show it again. Persistent desktop-layer visibility is not guaranteed.
- The widget's glass effect uses internal translucent layers and background blur; it does not blur desktop content in real time. Settings can use supported system materials, with fallback rendering where necessary.
- If startup is disabled in Task Manager, re-enable it there. WeatherBeacon respects the system-disabled state.
- After moving the application folder, launch it manually from the new location once. An enabled startup entry is updated only when it has not been disabled by the system.
- VPNs, proxies and ISP routing can affect IP location accuracy. Confirm the city before applying it.

## Data and privacy

Weather and city search use Xiaomi weather services. Clicking IP lookup contacts `ipwho.is` for an approximate location, then matches it through Xiaomi's city service. These network services receive the information required for the requests.

Settings are stored in `%LOCALAPPDATA%\WeatherBeacon\settings.json`. Existing desktop diagnostics are written locally to the executable's `logs` directory and include window state, placement, display layout and animation status. Review files before sharing them for troubleshooting.

Alert fingerprints are stored by city and alert type in alert-delivery.json alongside settings; test notifications never modify that ledger. A fixed AppUserModelID, WeatherBeacon.Desktop, matches a current-user Start menu shortcut, application name, icon and COM activator. Administrator privileges are not required. Starting from a new location updates the shortcut and activation command while preserving notification identity. Legacy path-based identities may remain in Windows.

Updates replace the same notification. Resolved alerts, or alerts downgraded below the selected threshold, remove the old notification. Toasts use a two-line summary, native publication time, an acknowledgment button and body activation. Test titles begin with [调试]. Notification Center retention follows Windows policy; test notifications no longer expire after five minutes.

Weather overrides are in-memory only. Restore, disabling developer mode, or restarting returns to real weather. Test Toasts respect the master notification switch but bypass the level threshold.

This release does not collect system hardware inventories or automatically upload diagnostic logs. See the [original stage-four notes](docs/STAGE4_COMMIT.md) and the [latest notification fix and verification report](docs/TOAST_FIX_COMMIT.md).

## Current limitations

- Verified minute-level precipitation data is unavailable. There is **no rain-start or rain-stop countdown**, and hourly forecasts are not converted into minute estimates.
- A first-run wizard and system hardware inventory logging are not included.
- Alerts are polled with weather refreshes rather than delivered as a real-time feed. Unsupported or missing Xiaomi alert fields are reported explicitly.
- Windows notification permission does not guarantee a visible banner. Focus Assist / Do Not Disturb may send notifications only to Notification Center.
- Service availability and coverage depend on third-party endpoints. Offline sessions cannot receive new forecasts, and weather snapshots are not retained after exit.
- Startup-state handling, materials and multi-monitor DPI behavior can vary across Windows versions. Real sign-in startup, network recovery and all hardware combinations have not been comprehensively validated.

## Build from source

Built with **C#, WPF and .NET 10**. Install the .NET 10 SDK and run from the repository directory:

```powershell
dotnet publish src/WeatherBeacon.App/WeatherBeacon.App.csproj -c Release -r win-x64 --self-contained true -o output/win-x64
```

Run `WeatherBeacon.exe` from the complete `output/win-x64` directory.

```text
src/WeatherBeacon.App/              WPF interface, settings and Windows integration
src/WeatherBeacon.Core/             Weather models and interfaces
src/WeatherBeacon.Infrastructure/   Weather and city services
tests/                             Automated checks and UI regression tools
docs/                              Design references and development records
```

## Feedback

Include the application version, reproduction steps, expected behavior and actual behavior. For invisible-window issues, you may attach the matching diagnostic file from `logs`. For weather-service issues, include the selected city and approximate time.

**WeatherBeacon 1.0.0.0 — Made with AI, improved through real needs and feedback.**

The Art Deco interface pairs geometric detailing and ocean-blue/champagne colors with Windows Mica, Mica Alt and Acrylic. Weather information stays legible, with warning levels identified by both text and color.
