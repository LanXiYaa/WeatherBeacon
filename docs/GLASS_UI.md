# Sunset Glass UI — 1.0.0.0 / x64

将用户 CSS 的暖色渐变、半透明背景、内卡片、圆角描边、位置胶囊与日预报高亮转换为 WPF XAML。
模糊仅作用于应用内部天气背景层，不是桌面实时背景模糊。未加入示例 AQI、温度曲线或虚构数据。当前源码的桌面行为保持原样，本次没有改动嵌入/回退实现。
已通过 x64 自包含编译及晴天/夜间/雨天离屏渲染检查；未验证真实桌面启动、拖拽和长时间模糊渲染性能。
程序：release/glass-ui-win-x64/WeatherBeacon.exe。已验证可运行的旧版仍保存在 release/verified-working-win-x64。
参考 CSS：docs/theme-reference/sunset-glass.css。样例数据预览：docs/previews/glass-widget.png。
