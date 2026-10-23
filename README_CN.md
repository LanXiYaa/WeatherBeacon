# WeatherBeacon 1.0.0.0 — 可见窗口回退版（x64）

本轮停止桌面嵌入尝试，恢复普通非置顶 WPF 窗口。不是历史二进制原样复制，而是在现有功能上禁用故障嵌入路径。天气、设置、托盘保留。不推进其他功能。

## 原因证据
用户最新两次诊断均 attached=true、visible=true、userHidden=false，窗口 420×430 且位于主屏内。因此这些会话不是零尺寸、离屏或主动隐藏。WorkerW 父句柄一致不能证明实际桌面呈现正常；具体遮挡/合成原因仍未证实。

## 回退行为
- 不调用桌面 Attach，不创建 WorkerW、不更改父子关系。
- 启动显示普通非置顶窗口；托盘显示操作恢复窗口并激活。
- 设置明确显示回退模式。
- Win+D 可能将它一起隐藏；可用托盘恢复。此版本没有解决桌面常驻，不使用桌面激活检测替代方案。
- 版本统一 1.0.0.0，仅发布 x64。

## 验收
| 实际观察 | 自动检查 | 未验证 |
|---|---|---|
| 未进行目视确认 | 本机启动 parent=0、attached=false、visible=true、minimized=false；矩形 (24,938)-(444,1368)；窗口中心命中属于小组件 centerBelongsToWidget=true | 用户机器上的实际显示与托盘交互 |
| 未进行 Win+D 验收 | x64 编译与版本检查通过 | Win+D 常驻明确不支持；断开副屏未测试 |

## 运行
退出之前所有 WeatherBeacon，再运行 release/rollback-visible-win-x64/WeatherBeacon.exe。保留整个目录。
诊断仍放在程序同目录 logs 文件夹。若仍看不到，请提供最新 JSONL，不必继续尝试嵌入诊断旧版。

源码 source，GitHub 副本 push。构建：
```powershell
dotnet publish src/WeatherBeacon.App/WeatherBeacon.App.csproj -c Release -r win-x64 --self-contained true -o output/rollback-visible-win-x64
```
