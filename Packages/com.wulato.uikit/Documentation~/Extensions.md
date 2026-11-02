# 可选扩展接入

Core 不依赖 uGUI 输入后端、TMP 或业务框架。IUIViewLoader 的返回租约只拥有其克隆实例；加载器必须遵守未激活父节点和取消契约。自定义 IUILayoutReady / IUITransition / IUIErrorSink 通过 Builder 显式传入，Core 不扫描程序集发现适配器。

IUITransition 的 Enter/Exit 单次消费，Stop 必须同步停止后续写入，Reset 恢复其声明的动画状态。需要完整页面事务的扩展通过现有事务能力接入；外部转场所有者与默认 Enter/Exit 不重复运行。当前已提供独立 Navigation、Presentation、Dialogs、Popup Queue、Addressables 和 Input System 包；按项目显式安装并装配。Navigation 的 UseNavigation() 负责注册导航服务和事务能力；其他扩展详见 phase2-migration.md、phase3-migration.md。R3 数据绑定、DOTween 转场适配和虚拟列表尚未交付。

IUISessionLifecycleObserver.OnPreparing 返回的幂等租约纳入 Session scope，注册顺序准备，逆序释放；只有成功返回绑定的观察者收到对应终结通知。WithServiceFactory 按注册顺序创建，仅能查询已创建服务；Context 拥有这些工厂产物并在 Shutdown/失败回滚时逆序释放。Builder 注入的外部共享依赖由调用方持有。

多 Context 共享输入时，调用方显式创建 UIInputDomain/UIInputArbiter 并注入匹配当前 EventSystem 的 arbiter；每个 Context 独立 FIFO。只有一个当前输入 owner，Modal 屏障及按住跨移交的隔离由框架处理。业务无需自行切换 InputModule，也不应旁路 CanvasGroup/overrideSorting 控制权。
