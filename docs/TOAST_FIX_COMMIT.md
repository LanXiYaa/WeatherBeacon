# Windows 通知修复

版本 **1.0.0.0**，仅 Windows x64。建议提交标题：`Fix portable Windows toast identity and alert lifecycle`。

## 诊断证据

- 用户当前配置为通知开启、蓝色阈值。旧 EXE 的系统通知权限返回 Enabled；最初读取历史为 0 条。
- 从旧版实际设置窗口点击黄色调试按钮后，历史立即变为 1 条，正文与内容属于该 EXE。未复现“所有提交均失败”，也不能把横幅未出现认定为提交失败。此前仅测试测试宿主的证据不足以证明实际产品正常。
- 旧实现的 AUMID 由 EXE 路径生成，且没有匹配的开始菜单快捷方式；调试通知五分钟过期，真实通知人为设置六小时保留。旧 Tag 使用内容指纹，更新会产生新条目；解除只删本地记录，没有清除系统历史。异步发送错误还会被两秒轮询状态覆盖。
- 验证固定身份首次注册时，原先的“先查权限再发送”逻辑在读取 Setting 时返回 **0x80070490 / Element not found**，从而阻止首次 Show。直接提交带调试标记的测试 Toast 后，历史出现 1 条，之后 Setting 返回 Enabled，证实 Windows 此时尚未生成该应用的通知设置。修复对此特定结果允许尝试 Show；明确的禁用状态仍阻止发送，最终权限由 Windows 自己执行。
- 不能证明首次初始化问题是用户此前每次看不到横幅的唯一原因；系统横幅／勿扰状态未完成目视核验，没有擅自修改系统通知设置。

## 当前实现

1. 固定 AUMID：`WeatherBeacon.Desktop`；显示名：`WeatherBeacon`；固定 COM 激活器 GUID。进程、发送 API、注册表和快捷方式使用同一身份。
2. 创建 `%APPDATA%\Microsoft\Windows\Start Menu\Programs\WeatherBeacon.lnk`，设置 AppUserModelID 和 ToastActivatorCLSID。当前用户注册，无需管理员权限。每次启动更新当前 EXE 路径，移位后通知身份保持不变。
3. 图标保存在 `%LOCALAPPDATA%\WeatherBeacon\Assets`，注册名称、快捷方式图标和 Toast 图标统一使用天气像素图。测试宿主不能修改正式程序的快捷方式或激活命令。
4. 使用通知兼容库中支持显式 AUMID 的 DesktopNotificationManagerCompat 注册激活器，并通过原生 ToastNotificationManager 发送。该兼容 API 标记为旧接口，但能满足显式稳定身份；已实测本项目 .NET 10 x64 下的注册、发送和 COM 激活。未引入独立 Windows App SDK 安装要求。
5. 标题示例：`兰州市 · 大风黄色预警`；等级文字本身明确区分四级。正文为最多两行、最多 90 个文字单元的摘要；使用原生 `displayTimestamp` 对应发布时间，不再把时间塞入正文。
6. 增加系统 `dismiss` 类型的“知道了”按钮。通知正文的激活请求打开设置；程序关闭时由 COM 启动当前 EXE，已运行时通过现有实例响应。按钮不需要启动应用。
7. 不设置 Alarm / Reminder / Urgent 场景，不改变勿扰或通知权限，标准横幅由 Windows 控制。不再人为给调试通知设置五分钟有效期；留存、折叠和分组遵守 Windows 规则。实测系统为未指定过期的测试通知提供三天历史保留期。
8. 同一 AUMID 由系统按应用分组。真实预警以城市和类型生成稳定 Tag，同类型取最新发布时间、同时间取较高等级。更新／升级替换旧条目，不堆叠；确认解除时主动移除系统历史，失败后下次刷新重试。降级低于阈值时清除旧高等级通知。
9. 本地原 alert ID 去重记录迁移到稳定类型槽位，保持已推送指纹，避免升级重放。重启后继续去重；不完整接口列表不能判断解除。
10. 调试使用相同内容格式，标题带 `[调试]`；使用独立 debug 分组和随机 Tag，不写真实通知记录。总开关与调试节流继续生效。
11. 系统权限与最后一次发送结果分开维护。保留异常 HRESULT，轮询不再抹去发送错误。没有增加日志收集、首次引导或空壳设置页。

## 验收

| 实际观察 | 自动检查 | 未验证 |
|---|---|---|
| 操作了实际 EXE 的开发者按钮，状态变为“已提交 Windows” | 该 EXE 的固定 AUMID 历史中出现了含正确调试标题、图标 URI、系统时间和关闭按钮的通知 | 屏幕右下角横幅、通知中心应用名／图标的最终肉眼显示、折叠布局 |
| 实际设置窗口可以正常打开 | 快捷方式属性读取：AUMID、激活器 GUID、目标 EXE 均正确；HKCU 应用名称和图标注册正确 | 所有 Windows 10／11 版本与系统通知策略组合 |
| — | 真正调用 Windows 通知中心：首次 1 条；重复不发送；升级后仍 1 条且内容为红色；解除后 0 条 | 实际点击“知道了”按钮、点击通知正文；系统 dismiss XML 与实际 COM 激活回调已分别检查，但不等同于人工点击通过 |
| COM 激活后设置窗口响应 | 通过 Windows COM 实际调用正式激活器成功 | 应用完全退出后的通知点击冷启动、多进程同时激活竞争 |
| — | 去重、更新、解除、重启重读、旧记录迁移、降级清理、字段异常、阈值等检查通过 | 断电时去重占位与 Show 之间的极小窗口仍可能漏送一次 |
| — | 原有视觉回归：帧数 16→34、暂停、0 CheckBox、页面切换、材质、位置约束和遮挡通过；单位、数据错误保留和自启回归通过 | Win+D 反复切换、托盘全部手势、实际多屏拖动；本次未修改这些功能的核心实现 |

注意：历史存在证明 Windows 已收下通知，不证明横幅出现。应用完全被系统禁用通知时无法保证进入通知中心；勿扰与横幅开关仅由系统决定，不强行覆盖。

## 复现命令

在源码目录，先启动正式 `WeatherBeacon.exe` 使其注册通知身份，再运行：

```powershell
dotnet run --project tests/WeatherBeacon.Stage4 -c Release -- --inspect-notifications WeatherBeacon.Desktop
dotnet run --project tests/WeatherBeacon.Stage4 -c Release -- --native-lifecycle output/notification-test
dotnet run --project tests/WeatherBeacon.Stage4 -c Release -- --activate-notification
dotnet run --project tests/WeatherBeacon.Stage4 -c Release -- tests/WeatherBeacon.Stage4/Fixtures/xiaomi-fuzhou-alert.json output/stage4-check
dotnet run --project tests/WeatherBeacon.VisualCheck -c Release -- output/visual-check
```

原生生命周期测试发出标记为“调试”的临时通知，复用正式 AUMID 但不修改注册，使用独立测试记录，并清除自己创建的生命周期条目；不会修改真实预警去重记录。

参考：[微软桌面 Toast 与快捷方式说明](https://learn.microsoft.com/en-us/windows/apps/design/shell/tiles-and-notifications/send-local-toast-desktop-cpp-wrl)、[ToastNotificationHistory](https://learn.microsoft.com/en-us/uwp/api/windows.ui.notifications.toastnotificationhistory)、[Toast displayTimestamp](https://learn.microsoft.com/en-au/uwp/schemas/tiles/toastschema/element-toast)。
