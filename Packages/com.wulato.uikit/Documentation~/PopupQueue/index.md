# Popup Queue

主包内置模块，与基础 Runtime 共用程序集并默认装配，依赖 UniTask 2.5.11；不依赖 Dialogs。Build 后已有 IUIPopupQueue。泛型 PopupAsync 入口按 2.4 交付。

IPopupRequest 仅提供 ViewType 和 UIArguments；业务先完成数据准备，队列统一调用核心 OpenAsync。参数应为不可变快照。

优先级高者先执行，同优先级 FIFO，不抢占当前项。key 覆盖等待与显示期，Ordinal 比较；null 不去重，空字符串是有效 key。重复项独立返回 Duplicate。失败保存在 PopupQueueResult，处理后继续下一条。完成 UICompletion 可能仍有隔离清理，不能当作资源已实际释放。

AcquirePause 返回幂等租约；所有来源释放后才恢复出队。暂停不取消已开始打开的项，不隐藏/关闭当前 Session。下一条等待上一条 Closed，不等待对象销毁；CacheOne 也会继续出队。

await StopAsync() 主动注销并等待当前关闭/打开回滚屏障，结果 QueueStopped；Context 退出为 ContextStopped。Dispose 幂等且不阻塞，需要屏障时显式 await StopAsync。晚释放暂停租约不能重新启动。

停止回调不等待同一 Context 队列。队列按 Context 隔离，不提供自定义异步打开、抢占、LatestWins 或关闭重开式的伪恢复。
