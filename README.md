# WeatherBeacon 1.0.0.0 — Visible-window rollback (x64)

Desktop embedding is disabled. This is a conservative rollback of the embedding path, not an exact historical binary. Weather, settings and tray remain; no other features are advanced.

Latest failing embedded sessions reported visible=true, nonzero on-screen geometry and userHidden=false. The specific Explorer layering/composition cause remains unproven. A matching WorkerW parent does not prove actual display.

The rollback uses a normal non-topmost WPF window. Startup and tray show restore/activate it. Settings explicitly show rollback mode. Win+D may hide it; restore from tray. Desktop persistence is NOT fixed and no desktop-activation fallback is used.

Automated check on this machine: parent=0, attached=false, visible=true, minimized=false, rectangle (24,938)-(444,1368), centerBelongsToWidget=true. x64 build and version checks passed. No visual or multi-monitor-disconnection acceptance test performed.

Exit all previous instances; run the complete release/rollback-visible-win-x64 folder. Diagnostic JSONL files remain in executable-directory/logs.

Build with .NET 10: dotnet publish src/WeatherBeacon.App/WeatherBeacon.App.csproj -c Release -r win-x64 --self-contained true -o output/rollback-visible-win-x64
