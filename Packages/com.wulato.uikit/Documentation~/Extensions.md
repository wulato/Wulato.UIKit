# 业务模块与可选适配

Navigation、Presentation、Dialogs、PopupQueue 已内置主包，并与 Core/Loading/UGUI 共用 `Wulato.UIKit` Runtime 程序集；当前仍显式装配，不自动注册服务。Addressables/Input System 保持独立可选包，后续任务 1.2 才合并其包位置。

# 可选扩展接入

基础 Runtime 依赖 uGUI/UniTask，不引用可选 Input System、Addressables、TMP 或业务框架。IUIViewLoader 的返回租约只拥有其克隆实例；加载器必须遵守未激活父节点和取消契约。自定义 IUILayoutReady / IUITransition / IUIErrorSink 通过 Builder 显式传入，不扫描程序集发现适配器。

IUITransition 的 Enter/Exit 单次消费，Stop 必须同步停止后续写入，Reset 恢复其声明的动画状态。页面事务由内置 Navigator 管理，低层事务接口已转为 internal，业务不再直接提交事务步骤。INavigationTransition 及其配对上下文继续公开，可提供自定义动画；外部转场所有者与默认 Enter/Exit 不重复运行。Navigation 的 UseNavigation() 当前仍负责显式组合；升级见 [0.5 迁移](Migration-0.5.md)，模块协议见各模块文档。R3 数据绑定、DOTween 适配和虚拟列表尚未交付。

IUISessionLifecycleObserver.OnPreparing 返回的幂等租约纳入 Session scope，注册顺序准备，逆序释放；只有成功返回绑定的观察者收到对应终结通知。WithServiceFactory 按注册顺序创建，仅能查询已创建服务；Context 拥有这些工厂产物并在 Shutdown/失败回滚时逆序释放。Builder 注入的外部共享依赖由调用方持有。

多 Context 共享输入时，调用方显式创建 UIInputDomain/UIInputArbiter 并注入匹配当前 EventSystem 的 arbiter；每个 Context 独立 FIFO。只有一个当前输入 owner，Modal 屏障及按住跨移交的隔离由框架处理。业务无需自行切换 InputModule，也不应旁路 CanvasGroup/overrideSorting 控制权。
