# 第四步：预警通知与开发者模式

> 此文件保留首次第四步交付的实现与验收记录。通知实现已更新，当前行为及后续测试请见 [通知修复说明](TOAST_FIX_COMMIT.md)。

版本：**1.0.0.0**。交付架构：**Windows x64**。

建议提交标题：`Add persistent weather alerts and developer weather previews`

## 本次实现

- 通知页：默认关闭的总开关、蓝／黄／橙／红阈值滑块、系统通知状态、数据支持状态和发送状态。通知设置自动保存。
- 小米解析：实测 `alerts` 数组，读取 `alertId / title / type / level / detail / pubTime`。2026-10-24 查询的福州响应含台风黄色预警（发布时间 2026-10-23 10:32 +08:00）；北京、广州、上海、兰州样本为空数组。测试样本是公开服务实际响应，不用于自动制造真实预警。
- 缺失或不支持的字段明确提示；跳过未知等级和不完整条目。字段缺失、解析失败及部分解析不会被当作“预警已解除”。
- 按城市保存预警 ID 和内容指纹到 `%LOCALAPPDATA%\WeatherBeacon\alert-delivery.json`。更新内容可重新通知；完整有效列表中消失的预警视为解除。重启不会重放相同内容，损坏的记录会阻止真实通知并提示。
- 通知通过 `CommunityToolkit.WinUI.Notifications 7.1.2` 注册当前用户身份和 COM 激活器，不要求安装包或管理员权限。Windows API 目标为 10.0.18362.0；无额外 Windows App SDK 运行时安装要求。保留 x64 自包含发布方式。
- Toast 显示类型、等级、简要说明、城市和发布时间；长说明截取 220 字。系统禁用或发送失败不会中断天气窗口。
- 在“关于”页确认开启开发者模式，明确提示“非专业人士最好不要打开该选项”，再显示开发者页。提供晴、多云、阴、小雨、中雨、大雨、雷暴、雪、雾、未知天气及昼夜预览。小雨／大雨的雨丝密度、速度和长度不同。
- 预览显示“调试中”，温度、小时和每日预报仍是真实数据。天气刷新不会覆盖预览；恢复按钮、关闭模式或重启恢复真实天气。覆盖不序列化。
- 开发者可选择 8 类预警和 4 个等级发送真实测试 Toast；带【调试】标记，不进入真实去重记录。测试遵守总开关，绕过阈值，2 秒节流。
- README 中英文、像素图标与 GitHub 提交目录同步。没有添加首次引导、硬件日志或空壳页面。

## 已知边界

1. 预警跟随用户设置的天气刷新周期轮询，并非实时推送。只有已验证字段能被解析；没有推断、编造预警等级或发布时间。
2. 当前真实样本没有到期时间。不会虚构解除时间；以之后成功读取的完整列表判断解除。Toast 在通知中心的 6 小时保留时间只是显示有效期，不表示气象预警有效期。
3. Windows `ToastNotifier.Setting` 可报告应用、用户和策略禁用。勿扰／专注助手无法可靠判断；系统允许通知、Show 调用成功及通知历史存在，都不证明横幅实际出现。
4. 为避免崩溃后重复推送，先持久化发送占位，再向 Windows 提交。若在两者之间断电，可能漏掉一次通知；明确发送失败会恢复记录，等待下一轮重试。系统异步拒绝会显示错误，但不保证重发。
5. 便携版从新路径启动会按工具包机制注册通知身份；旧路径身份可能残留。天气预警去重仍使用同一用户配置目录，不因移动目录重置。
6. 保留第三步的普通非置顶窗口路径，未重新引入桌面嵌入。原有 Win+D 行为边界仍在，不能将本轮测试描述为桌面常驻问题已解决。

## 验收报告

| 项目 | 实际观察 | 自动检查 | 未验证 |
|---|---|---|---|
| 小米预警 | 读取真实福州台风黄色预警及多个城市空数组 | 真实样本字段、缺失字段、无效结构检查通过 | 所有地区、未来新增结构和未知等级 |
| 通知 | 未观察到屏幕横幅 | 未安装测试程序调用真实 Toast 成功；Windows 通知历史返回 1 条 | 最终 EXE 的横幅、点击激活；系统禁用／勿扰各场景 |
| 去重 | — | 首次、重复、重建实例读取磁盘、内容更新、解除重发、城市隔离、发送失败重试、记录损坏、过期过滤通过 | 真实断电和系统异步失败恢复 |
| 新页面 | 已查看 WPF 渲染生成的通知与开发者页截图，修正天气下拉框名称 | 默认隐藏开发者页、开启显示、关闭退出该页、阈值边界通过 | 人工点击确认对话框、全部 DPI／系统主题 |
| 天气预览 | — | 10 类天气 × 2 种昼夜状态；真实数据保留、刷新时覆盖保持、恢复真实状态、重建 VM 无覆盖通过 | 发布程序逐项肉眼观察、标签位置在所有缩放下的效果 |
| 原有视觉与位置 | — | VisualCheck：8 种图标、9 种场景、帧数 15→34、动画暂停、无 CheckBox、页面切换、材质、位置约束／遮挡通过 | 实际拖动、多显示器切换、托盘单／双／右键、Win+D→还原连续 10 轮 |
| 第三步稳定性 | — | 单位、解析／网络失败保留数据、天气名称、刷新间隔；自启开关、路径修复、尊重系统禁用通过，测试后恢复注册表 | 真正注销／登录、拔网线恢复、实际 IP 定位 |
| 启动与单实例 | — | 已退出旧版后独立启动第四步发布包；现有诊断报告可见、未最小化、460×626、位于屏幕内、中心命中小组件，动画初始化帧数 4。再次启动第四步包在 5 秒内退出，进程数保持 1 | 不将窗口状态检查等同于人工桌面验收；同时启动竞争未验证 |
| 发布 | — | x64 自包含发布成功，文件／产品版本 1.0.0.0，当前 NuGet 源无已知漏洞包 | 其他机器 Windows 10／11 |

视觉检查 CPU 样本：动画开启约占总 CPU 0.449%，关闭约 0.019%，是测试进程 4 秒采样，不代表所有机器或实际产品长期性能。

当前桌面工具无法定位正在运行的小组件窗口，因此未完成上述人工交互项；没有把进程存活作为通过证据。

## 可复现检查

在仓库根目录运行（需要 .NET 10 SDK、Windows）：

```powershell
dotnet run --project tests/WeatherBeacon.Stage4 -c Release -- tests/WeatherBeacon.Stage4/Fixtures/xiaomi-fuzhou-alert.json output/stage4-check
dotnet run --project tests/WeatherBeacon.VisualCheck -c Release -- output/visual-check
dotnet run --project tests/WeatherBeacon.Stage3 -c Release -- tests/WeatherBeacon.Stage4/Fixtures/xiaomi-fuzhou-alert.json
dotnet run --project tests/WeatherBeacon.Stage3 -c Release -- --startup-tests
dotnet run --project tests/WeatherBeacon.Stage4 -c Release -- --toast
dotnet publish src/WeatherBeacon.App/WeatherBeacon.App.csproj -c Release -r win-x64 --self-contained true -o output/win-x64
```

`--startup-tests` 暂时修改 WeatherBeacon 当前用户启动项并在 finally 中恢复；`--toast` 会注册测试程序的通知身份并发出一条明确标记的测试通知。

参考：[微软 ToastNotificationManagerCompat 文档](https://learn.microsoft.com/en-us/dotnet/api/communitytoolkit.winui.notifications.toastnotificationmanagercompat?view=win-comm-toolkit-dotnet-7.0)、[ToastNotifier.Setting](https://learn.microsoft.com/en-us/uwp/api/windows.ui.notifications.toastnotifier.setting)。
