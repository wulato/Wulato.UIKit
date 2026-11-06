# DOTween transitions

先按 Documentation~/Integrations/DOTween.md 安装官方 DOTween 1.3.045 并启用 `WULATO_UIKIT_DOTWEEN`；导入本示例不安装 SDK。

1. 复用 QuickStart 或项目已有 UIRoot、UIRegistry、EventSystem/驱动和 UIContextHost。
2. Create → Wulato → UIKit → Transitions → DOTween Single，创建 Fade（默认）、Scale 或 Slide preset；组合可选多个 Effects。Duration 默认 0.2 秒，Scale 默认 0.9，Offset 默认 (0,-60)。
3. 同菜单创建 DOTween Page Pair；Incoming / Outgoing 可各自选择效果、时长和曲线，页面动画同时播放。
4. 将两资产赋给 Host 的 Transition Preset / Navigation Preset；空字段保持 Immediate。
5. 在 Registry 以非 Page 注册带关闭按钮的 Notice prefab，key 为 notice。把本组件挂到业务对象，赋 Host，按钮分别绑定 Open / Back。
6. 切换三个 preset 观察 Fade、Scale、Slide；timeScale=0 时仍按非缩放时间播放。页面示例使用 ui.OpenAsync / PushAsync / BackAsync，自动走配对 preset。

只改 AnimationRoot / AnimationOpacity，业务布局与输入门禁由 UIKit 管理。关闭按钮、取消、超时和 Shutdown 复用原生命周期；不要自行 KillAll 或同时控制同一动画表面。
