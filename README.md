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

## Download and run

This release provides a **self-contained Windows x64 build**, targeting 64-bit Windows 10 / 11 devices. A separate .NET runtime installation is not required.

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

This release does not collect system hardware inventories or automatically upload diagnostic logs.

## Current limitations

- Verified minute-level precipitation data is unavailable. There is **no rain-start or rain-stop countdown**, and hourly forecasts are not converted into minute estimates.
- Weather alert notifications, a first-run wizard and developer mode are not included.
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
