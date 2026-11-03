# 生命周期与资源所有权

每次成功打开创建一个 Session；CacheOne 重用 View 实例，但不会重用 Session、Session scope 或 foreground scope。句柄不暴露 View，旧句柄不控制后续重开。

顺序为：未激活父节点下加载实例 → 序列化绑定已就绪时 OnCreate（每实例一次）→ 建立 Session/参数 → 隐藏激活并进入 Awake/OnEnable → 扩展 OnPreparing → OnOpenAsync → 下一 Update 的布局屏障 → Enter 转场 → 输入/前景提交 → OpenAsync 返回。业务自行调用 WaitLayoutAsync 时，仍遵循框架协调的准备过程。

正常关闭：接受请求并取消 Session/前景能力 → OnCloseAsync → Exit → 去激活与输入移除 → OnClosed → Release 释放或健康实例进入 CacheOne → 完整终态提交后通知 Closed。强制退出绕过业务关闭否决；失败打开及超时回调的隔离清理遵循框架契约，不能假设每条失败路径都执行完整正常关闭钩子。OnClosed/OnDispose 必须允许部分初始化和幂等清理。

OnCreate 适合绑定实例级按钮事件；每次参数和订阅初始化放在 OnOpenAsync，订阅租约用 session.Track 管理。临时输入/前景资源用 OnForegroundEntered 收到的 scope.Track；失去前景即释放。资源作用域不等于全局单例或业务架构要求，项目可独立选择 MVC/MVP/MVVM。

Release 关闭后由租约销毁实例；CacheOne 是受容量限制的 LRU，不保证永久驻留。不要缓存旧 SessionToken 或把异步写入延续到下一会话。异步工作在 scope token 取消后停止访问 View；忽略取消的迟到任务会进入隔离/待清理诊断。Shutdown 有统一预算，返回 PendingCleanupCount 不为零时不等于所有迟到工作已经完成。

## UIContextHost 所有权

宿主只装配并持有自身成功构建的 Context；同 Root 已被占用时失败，不关闭外部 Context。初始化前 Context 为 null，停止后保留引用用于诊断。重复 Shutdown 共享同一终态，单次 UniTask 不重复消费。Host/Root 同帧销毁复用已有关闭机制及原因；禁用不是 Shutdown。

Inspector 设置只在 Build 时映射为现有验证快照。超时默认 30/10/2/2/2 秒，ShutdownBudget 5 秒，缓存 8，Basic 诊断。运行时修改 Inspector 不改变已建 Context。ShutdownResult 中的 PendingCleanup 不是“资源已全部物理释放”。禁用 Reload 时新 PlaySession 重新装配，不延续旧 Context。
