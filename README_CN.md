# WeatherBeacon 1.0.0.0 — 第三步（x64）

本轮实现自启、IP/手动城市选择、单实例、限频刷新恢复、温度单位与每日天气名称。保持第二步普通非置顶窗口、托盘、拖动、动态背景和设置页动画。未增加预警通知、日志功能或开发者模式。

## 使用
退出旧版，运行 release/stage3-win-x64/WeatherBeacon.exe，保留全部文件。
设置→天气：开机自启、刷新间隔、使用华氏温度、IP 定位和手动搜索。外观/单位/间隔修改后点击保存；选定城市自动保存。自启开关立即操作系统状态。
IP 定位会在点击按钮后访问 ipwho.is，并用小米 geo 接口匹配城市，需要确认搜索结果再使用。VPN/运营商出口可能不准确；失败可手动搜索。

## 自启实现与限制
使用当前用户 HKCU\Software\Microsoft\Windows\CurrentVersion\Run 的 WeatherBeacon 值，命令为带引号的当前 EXE 路径加 --autostart。无需管理员权限。每次首次实例启动检查路径，仅已启用且未被系统禁用时修复。
设置每 2 秒重新读取 Run 与 Explorer\StartupApproved\Run，系统禁用时显示关闭及原因，不擅自重新启用。需用户在任务管理器重新启用。StartupApproved 是 Windows 实现细节，遇到未知状态保守显示无法识别，停止自动路径修复，不能保证所有 Windows 版本的兼容性。
移动目录后需手动运行一次新路径。系统禁用时保留旧路径，用户重新启用后再运行新路径才修复。真实重启登录未测试。

## 刷新策略
默认 15 分钟，可选 5/10/15/30/60。单次网络请求最多三次尝试，间隔 1/2 秒；刷新周期失败按 30/60/120/240/300 秒退避。网络恢复事件安排 15 秒后刷新。同城市请求限频 15 秒，并禁止并发；主动切换到不同城市立即请求。
请求/解析失败保留上一份有效数据并标出旧城市和更新时间。进程关闭后不保存天气快照，目前旧数据保留限于本次运行。

## 降雨与天气名称
每日保留真实天气代码，显示如“中雨”“晴转多云”，今日仍高亮。小时数据不用于估算分钟。
本次实际天气样本的 preHour 是上小时实况，未验证可用分钟降水序列。因此显示“暂无分钟级降水预报”，未实现降雨/雨停倒计时。这是明确的接口能力限制，不做虚假倒计时。

## 验收报告
| 实际观察 | 自动检查 | 未验证 |
|---|---|---|
| 未进行桌面交互目视验收 | WPF 动画帧 16→35，关闭/不活跃暂停；0 CheckBox；导航切换结束状态通过；位置恢复和遮挡检测通过 | 托盘真实单击/双击/右键、拖动手势、Win+D 10轮 |
| 未实机登录重启 | 注册项启用/关闭、路径变更修复、模拟系统禁用不修复通过；测试后恢复原值 | 真实任务管理器操作、登录自启、其他 Windows 版本 |
| 未目视确认原实例响应 | 首实例运行，第二实例在 5 秒内退出；自启参数启动未崩溃 | 无城市配置时自启不弹设置的端到端检查 |
| 没有真实断开网络 | 模拟 HTTP 故障、解析失败保留城市/温度/每日数据；单位往返转换、刷新默认值/非法值校验通过 | 断网重连事件端到端、长时间退避、真实超时 |
| 未验证地理准确性 | IP 服务与小米 geo 返回有效 locationKey | VPN、异地出口、IP 服务不可用地区 |

已有普通窗口模式下 Win+D 可能隐藏小组件，本轮没有改变该行为。没有将进程存活当作桌面验收通过。

## 构建和测试
.NET 10 SDK：
```powershell
dotnet publish src/WeatherBeacon.App/WeatherBeacon.App.csproj -c Release -r win-x64 --self-contained true -o output/stage3-win-x64
dotnet run --project tests/WeatherBeacon.Stage3 -- <小米天气响应JSON文件>
dotnet run --project tests/WeatherBeacon.Stage3 -- --startup-tests
dotnet run --project tests/WeatherBeacon.VisualCheck -- output/previews
```
startup-tests 临时改动 WeatherBeacon 自启值，并用 finally 恢复，请关闭软件后运行。
版本所有位置固定 1.0.0.0。
