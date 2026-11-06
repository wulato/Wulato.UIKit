# Dialogs

主包内置模块，与基础 Runtime 共用程序集并默认装配，依赖 UniTask 2.5.11；无需 Navigation/Presentation/Popup Queue。

请求实现 IDialogRequest<TResult>。对应 View 继承 UIView<DialogArguments<TRequest,TResult>>，在 OnOpenAsync 接收 Request 与 Responder；通过 UIRegistryBuilder.AddDialog<TView,TRequest,TResult>(viewKey, asset) 一次注册 View 和请求/结果映射，不再单独装配 DialogRegistry。注册集合在服务创建时复制校验。

```csharp
try {
    DialogResult<bool> result = await ui.DialogAsync(new ConfirmRequest(), ct);
    if (result.Status == DialogStatus.Submitted) Apply(result.Value);
    // result.Completion 保留实际关闭原因、清理错误和未决隔离标记。
} catch (OperationCanceledException) { /* 调用方取消，打开已回滚或关闭屏障已完成 */ }
```

Status 只有 Submitted、UserCanceled、ContextStopped。false/null/default 都是有效提交；非 Submitted 不可读取 Value。未打开即 Context 退出时 Completion 可为 null。

Token 覆盖排队、打开和整个等待期，打开后取消走 Owner 关闭，不受 CanClose 用户否决。用户按钮/Back/Mask 仍遵守核心关闭策略。调用方取消抛标准 OperationCanceledException；打开失败保持原异常，关闭错误进入 UICompletion。

TrySubmit/TryCancel 只接受一次终结意图，实际结果在成功打开及 Closed 后发布。Context 退出覆盖尚未完成的意图。缓存重开使用新 responder，旧 responder 失效；固定监听应在每次 OnOpen 更新会话绑定，不能用旧异步回调读取新 Session 后投递。

2.4 起普通调用直接使用 `ui.DialogAsync(request, ct)`，转发默认 IUIDialogs，没有新结果通道或生命周期。高级服务入口仍可用；无需额外装配或单独注册 DialogRegistry。

若 CanClose 等钩子内触发 caller 取消或结果意图，服务会等钩子退出后再提交 Owner 关闭；公共命令的 nested-command 防护保持有效。
