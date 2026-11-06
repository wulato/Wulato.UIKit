# DOTween transitions (0.5 / task 3.1)

默认不需要 DOTween，空 Host preset 使用 Immediate。主包不自动下载 SDK，不包含其 DLL、源码或 Pro；业务不需要第二个 UIKit 包。

已验证格式：官方 DOTween **1.3.045** 核心 DLL + 官方运行时 Modules，Unity 2022.3.62f2。按[官方安装说明](https://dotween.demigiant.com/download.php)完成 SDK 安装/Modules 设置；使用 asmdef 的项目按官方 Utility Panel 配置程序集。UIKit 本身只通过独立程序集显式引用 `DOTween.dll`，不使用 UI 快捷模块。DLL 或 SDK 运行时模块不完整时不能启用集成；不以 DLL 替身或伪造版本验证。

在消费项目 Scripting Define Symbols 明确加入 **WULATO_UIKIT_DOTWEEN**。全局 DOTWEEN / DOTWEENPRO 不会自动启用 UIKit。已安装 SDK 但未开启符号时，UIKit 适配/测试/示例程序集仍不编译；基础程序集的实际 IL 不引用 DOTween。Version Defines 无法可靠识别 Asset Store/DLL 形式的 SDK，故此处使用 Define Constraints + overrideReferences/precompiledReferences；Addressables/Input 的 Version Defines 保持原样。

Create → Wulato → UIKit → Transitions：创建 DOTween Single 和 DOTween Page Pair 资产，分别赋给 Host 的 Transition Preset 与 Navigation Preset。每个 Context 创建自己的播放 owner，设置在创建时复制。Single 作用于普通界面，Page Open/Push/Back/Replace/PopTo 使用配对动画；Incoming/Outgoing 同时播放，按最长时长结束。Fade、Scale、Slide 可组合；默认 0.2 秒、OutCubic、非缩放 Update，timeScale=0 仍播放。也可直接 WithTransition / WithNavigationTransition 注入；Host 的 configure 最后执行，可覆盖 preset。

转场只写 AnimationRoot 的 scale/anchoredPosition 和 AnimationOpacity.alpha；不改业务布局、Rotation、Shell 输入门禁或可见性。UIKit 在布局后捕获声明属性，Reset 仅复位这些属性。不要让业务 Tween 同时写入同一表面。所有转场调用在主线程，后台 token 取消回到主线程并绑定原播放身份。

每次播放独立持有 Sequence，Stop 同步 Kill(false)，然后终结等待；Kill/Complete 的旧回调不会移除重入播放。外部 Kill 被视为取消，不把取消当成功。UIKit 不调用 KillAll，不修改 DOTween 全局时标/容量/回收/默认 Ease/自动播放；只对自己创建的 Sequence 配置 AutoKill(true)、非回收与 unscaled Update；即使业务将 defaultAutoKill 设为 false，完成后的自有 Sequence 也会释放。Session、输入锁、超时、迟到隔离及 Shutdown 继续由原 UIKit 生命周期管理。

通用 UITransitionRunner 已移除；自定义实现 IUITransition/INavigationTransition 仍受支持。原 Runner 四项运行行为验收迁到真实 DOTween 专项，基础生命周期/写集/取消/超时测试保留。示例在 Samples~/DOTween，导入不安装 SDK。

本地验证使用既有 run.py 的 `dotween` 专项，必须先显式授权并用 `Tools/Verification/dotween_sdk.py` 将固定哈希的官方 ZIP 安装到忽略的验证宿主；安装器只复制 DLL 和 Utils/Physics/Physics2D 必需运行时文件，不导入 SDK Editor 安装器，不修改 SDK 源码或消费项目。receipt 保存归档与逐文件哈希。Host Reload 执行器在适配测试可用时额外验证两个实际 Play 退出的播放终结。已验证版本不代表其他版本、Pro 或移动 Player 已通过。

ci_suites 可以选择此可选专项，仍不自动下载 SDK；运行前须具备显式安装授权与已准备的宿主。远端 SDK 来源和发布 CI 的实际配置留到 Phase 6，本轮只验证本地选择/证据规则。
