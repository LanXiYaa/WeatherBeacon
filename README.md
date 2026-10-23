# WeatherBeacon 1.0.0.0 — Stage 2 (x64)

Stage 2 adds free desktop-widget positioning, opaque animated weather scenes, animation lifecycle control, settings-only system materials, page transitions and animated toggle switches. It does not add onboarding, startup registration, alert notifications, logging features or developer mode.

The WorkerW desktop embedding and tray interaction paths from stage 1 remain in place. The widget remains non-topmost. Its background is an opaque weather scene and is never Mica or Acrylic.

## Verification summary

| Actual observation | Automated checks | Not verified |
|---|---|---|
| WPF-generated screenshots show distinct clear day/night, cloud, overcast, rain, snow, fog and thunderstorm scenes; text remains readable on dark backdrops. | Release starts attached=true, parent=host=WorkerW, visible=true, minimized=false, at the saved 420x430 position. | Live mouse dragging and tray clicks while the desktop was covered by another foreground app. |
| Two rain frames captured about 0.85 s apart visibly differ; frame count advanced 16 to 35. | Tracked to about 30 fps; animation stops when disabled, hidden/inactive, or fully covered; partially covered remains active. | Long-duration thermal and battery behavior. |
| Final x64 self-contained build has FileVersion/ProductVersion 1.0.0.0 for all WeatherBeacon assemblies. | Zero CheckBox controls; rapid page switching settled correctly; negative-coordinate, disconnected-monitor and occlusion cases passed. | Windows 10 runtime material fallback and a real mixed-DPI monitor transition; fallback logic is implemented, but those systems were not available. |

Animation sampling in the WPF visual-check process: 10.54% of one core (0.527% of total 20-core capacity), 194.9 MiB working set with animation on; 0% CPU, 193.9 MiB with animation off. The covered release process used 0.047 CPU-seconds over 5.03 s (0.94% of one core, 0.047% of total capacity) and 206.6 MiB working set.

Run `release/stage2-win-x64/WeatherBeacon.exe`. Keep the complete directory together. Build with .NET 10:

```powershell
dotnet publish src/WeatherBeacon.App/WeatherBeacon.App.csproj -c Release -r win-x64 --self-contained true -o release/stage2-win-x64
```
