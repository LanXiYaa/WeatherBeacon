# 思源字体与视觉对齐

版本 1.0.0.0，x64。模式 full；范围为 WPF 小组件与全部现有设置页，沿用 XAML 样式；不修改业务逻辑。系统 Toast、标题栏、托盘和原生确认框字体由 Windows 管理。

## 检查范围
| 类别 | 检查证据 | 结果 |
| --- | --- | --- |
| Typography | 两个 Window XAML、字体实际解析及截图 | 内嵌思源黑体 Regular/Medium、思源宋体 Regular；Tabular 数字 |
| Surfaces | 小组件边距、设置控件与预报条目截图 | 对齐与文字高度调整 |
| Animations | 现有视觉回归程序 | 帧变化、暂停、页面切换通过；未新增动画 |
| Icons | 导航 XAML 与截图 | 固定 26 DIP 图标列；天气图标沿用 |
| Performance | 视觉回归程序 | 动画计时与暂停检查通过；未开展长期性能测试 |

## 修改
| Severity | Location | Before | After | Why |
| --- | --- | --- | --- | --- |
| MEDIUM | Features/Widget/WidgetWindow.xaml:1、Features/Settings/SettingsWindow.xaml:1、WeatherBeacon.App.csproj | 系统字体与 Georgia | 正文思源黑体，品牌与展示标题思源宋体；嵌入未修改官方字体并附许可证 | 用户指定字体；保证跨电脑一致 |
| LOW | Features/Settings/SettingsWindow.xaml 导航 | 空格对齐图标文字 | 固定图标列与统一垂直居中 | 字形宽度不再影响对齐 |
| LOW | Features/Settings/SettingsWindow.xaml 控件样式 | 按钮、选择器高度不同，右侧控件缺少间距 | 最小高度 40、垂直居中、20 DIP 间距 | 统一基线与边缘 |
| LOW | Features/Widget/WidgetWindow.xaml 预报和当前天气 | 小偏移、小时/每日列宽不同 | 相同 72 DIP 列节奏、统一左边缘、紧凑条目间距；温度 72 DIP | 新字体避免裁切并保持视觉对齐 |
| LOW | 两个 Window 根元素 | 默认像素与数字排布 | LayoutRounding、SnapsToDevicePixels、Tabular numerals | 避免细线模糊与数字跳动 |

## 考虑后未采用
| 位置 | 方案 | 原因 |
| --- | --- | --- |
| 天气背景与页面切换 | 新增动效 | 本轮只做小幅对齐与字体替换 |
| 系统 Toast/原生确认框 | 自绘取代系统控件 | 会改变系统交互和通知行为 |
| 字体资源 | 依赖用户安装字体 | 便携分发无法保证一致 |

## 验收
| 实际观察 | 自动检查 | 未验证 |
| --- | --- | --- |
| 检查设置外观、小组件雨夜渲染图；修正逐日条目底部空间 | 实际 GlyphTypeface 字体来源为应用 pack 资源，包含“晴”字符；编译发布通过 | 不同显示器 DPI 人工测试 |
| 对齐与字体层次符合小幅修改范围 | 8 图标、9 场景、动画帧变化、暂停、页面切换、0 CheckBox、材质调用、位置与遮挡测试通过 | Win+D、登录自启、托盘交互和真实通知本轮未重测 |

命令：dotnet run --project tests/WeatherBeacon.VisualCheck -c Release -- <output>；最终条目间距调整后再次使用 glass-only 生成三种天气截图。
发布：release/source-han-win-x64；安装包：release/source-han-setup/WeatherBeacon-1.0.0.0-x64-Setup.exe。
Verdict: Approve（本次对齐和字体范围无剩余可操作问题；上述人工检查未验证）。

## GitHub 提交建议
内嵌思源字体并微调小组件与设置布局。源码与 push 同步，未自动提交 Git。
字体来自 Adobe 官方 source-han-sans / source-han-serif release 分支；SIL OFL 1.1 原文及来源随软件分发。
