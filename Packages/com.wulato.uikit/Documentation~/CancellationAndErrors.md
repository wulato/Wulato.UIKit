# 取消与错误

OpenAsync 的调用方取消或 Session 取消返回 OperationCanceledException；配置超时返回带 Stage 的 UIException/Timeout。参数不匹配、缺注册、已停止或 Single 重复打开通过返回的操作报告，不提前创建实例/交付半完成 Session。

```csharp
try
{
    var session = await ui.OpenAsync<MyView>(cancellationToken);
    var result = await session.CloseAsync();
    foreach (var error in result.Errors) UnityEngine.Debug.LogException(error);
}
catch (System.OperationCanceledException) { /* 本次打开已取消 */ }
catch (UIException error)
{
    UnityEngine.Debug.LogError($"{error.Code} / {error.Stage} / {error.SessionId}");
}
```

默认 ErrorSink 也会记录框架错误；自定义 sink 可接项目日志，保持非阻塞且不要抛出。OnCloseAsync/清理错误写入 UICompletion.Errors；同时检查 HasPendingCleanup。异常不是资源已释放的证明，要结合 Diagnostics.PendingCleanupCount、IsolatedLeaseCount 及实际租约。

关闭被接受后没有调用方取消；重复 CloseAsync/Closed/Shutdown 共享固定结果，可由多个观察者等待。其它单次消费 UniTask 不重复 await、不要同时 Forget 和 await 同一原始操作。用户关闭请求由 CanClose 同步判定；异步确认属于外部业务流程，先 await 确认，再提交 Close/Open 命令。不要在 CanClose 或生命周期中等待同一 FIFO 的命令，否则队列无法前进。

Root 销毁触发框架清理，完成通知由独立运行时宿主泵送。主动退出应 await ShutdownAsync 并检查 Errors/PendingCleanupCount；不要把销毁 GameObject 当成已经观察到 Shutdown 完成。
