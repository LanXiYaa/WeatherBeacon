# WeatherBeacon 1.0.0.0 — Stage 3 / x64

Adds current-user startup registration, IP-assisted/manual city selection, a per-user/session single instance, bounded retries and refresh intervals, Celsius/Fahrenheit display, and detailed daily weather names. Existing ordinary-window behavior, tray, dragging, animations and settings transitions are retained.

Run the entire release/stage3-win-x64 folder after exiting older builds. Settings > Weather contains the new controls. Save unit/refresh changes explicitly; city selection persists automatically. Startup changes apply immediately.

Startup uses HKCU Run with --autostart. It repairs a moved executable path on manual launch only when enabled and not system-disabled. StartupApproved state is read conservatively and refreshed every two seconds in settings; its undocumented format may vary by Windows version. System-disabled entries require re-enabling through Task Manager. No administrator rights required.

Refresh defaults to 15 minutes (5/10/15/30/60 available). Network attempts retry after 1/2 seconds, then failed refresh cycles back off 30/60/120/240/300 seconds. Network availability recovery schedules a refresh. Old valid in-memory weather remains visible on failures, including its original city/time. No cross-session weather cache.

IP lookup calls ipwho.is only on user action, then Xiaomi geo; user confirms the matched city. Minute-level rain data has not been validated: explicitly reports unavailable, never estimates minutes from hourly data.

Automated checks passed: unit conversion, parsing/network-failure state preservation, weather names, interval validation, registry enable/disable/path repair/system-disabled preservation (original values restored), second-process exit, WPF animation frames/pause/navigation/toggles/placement/occlusion. Real login startup, Task Manager UI actions, tray gestures, 10 Win+D cycles, network reconnection and mixed-DPI interaction remain unverified. Ordinary windows may be hidden by Win+D, unchanged from accepted stage 2.

Build: dotnet publish src/WeatherBeacon.App/WeatherBeacon.App.csproj -c Release -r win-x64 --self-contained true -o output/stage3-win-x64
See README_CN.md for the full acceptance matrix and test commands. Version stays 1.0.0.0. No alert, logging or developer-mode features added.
