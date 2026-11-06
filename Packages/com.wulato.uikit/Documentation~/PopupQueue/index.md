# Popup Queue

主包内置模块，与基础 Runtime 共用程序集并默认装配，依赖 UniTask 2.5.11；不依赖 Dialogs。Build 后已有 IUIPopupQueue。2.4 起普通业务使用泛型 PopupAsync，无需实现 IPopupRequest。

```csharp
var result = await ui.PopupAsync<RewardView, RewardArgs>(args,
    priority: 10, deduplicationKey: "reward", cancellationToken: ct);
// 无参数 View：await ui.PopupAsync<NoticeView>(cancellationToken: ct);
```

在统一 UIRegistry 中注册非 Page View，队列统一调用核心 OpenAsync；参数应为不可变快照。Page 会被拒绝，避免队列隐式重建导航历史。高级调用可通过 `ui.GetService<IUIPopupQueue>()` 使用 key/UIArguments 或 IPopupRequest 描述符；不用增加单独 Registry。

优先级高者先执行，同优先级 FIFO，不抢占当前项。key 覆盖等待与显示期，Ordinal 比较；null 不去重，空字符串是有效 key。重复项独立返回 Duplicate。失败保存在 PopupQueueResult，处理后继续下一条。完成 UICompletion 可能仍有隔离清理，不能当作资源已实际释放。

AcquirePause 返回幂等租约；所有来源释放后才恢复出队。暂停不取消已开始打开的项，不隐藏/关闭当前 Session。下一条等待上一条 Closed，不等待对象销毁；CacheOne 也会继续出队。

await StopAsync() 主动注销并等待当前关闭/打开回滚屏障，结果 QueueStopped；Context 退出为 ContextStopped。Dispose 幂等且不阻塞，需要屏障时显式 await StopAsync。晚释放暂停租约不能重新启动。

停止回调不等待同一 Context 队列。队列按 Context 隔离，不提供自定义异步打开、抢占、LatestWins 或关闭重开式的伪恢复。


每项请求有独立 CancellationToken：等待（包括暂停中）取消会移除该项并释放 key；打开中取消沿用核心回滚及迟到隔离清理；显示后取消通过 Owner 关闭，不受 CanClose 否决，等待 Closed 后抛 OperationCanceledException。在此屏障前 key 保持占用、下一项不启动；取消一项不会停止整条队列或取消其他请求。注册回调可来自后台线程，队列/Unity 操作回到主线程执行。

Context/Queue 停止覆盖尚未完成的取消，返回 ContextStopped/QueueStopped。普通完成仍携带实际 UICompletion 的错误与 HasPendingCleanup；调用方取消抛标准取消异常，清理错误继续由核心 ErrorSink 报告，未决隔离可从 Diagnostics 查看。逻辑 Closed/回滚不代表迟到资源已物理释放，队列不复用隔离中的对象。每次返回的操作只 await 一次。

钩子内触发的 token 取消会等钩子退出后再处理，避免在 CanClose 中嵌套 Owner 关闭。业务仍不得在生命周期钩子中提交同 Context 公共命令。
