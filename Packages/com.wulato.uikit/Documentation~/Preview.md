# 独立 PlayMode 预览

打开 Tools → Wulato → UIKit → Workbench 的“预览”页，创建并保存 UIPreviewProfile，配置 Registry、已注册 View Key 和输入驱动。先完成现有生成、编译、写回及只读校验，再启动预览。Legacy 可使用默认驱动；Input System 项目必须明确指定已安装适配器脚本。

工具保存用户确认后的场景布局与选择，在独立临时场景中通过 UIContextHost 和现有 OpenAsync 运行。使用“关闭界面”“再次打开”观察同一 Context 中的新 Session；CacheOne 可以复用实例，Session 身份仍会改变。普通业务启动场景不参与运行。

## 参数及业务依赖

无参 View 无需 Adapter。带参或外部 Loader/服务的 View 创建 Editor ScriptableObject，继承 `Wulato.UIKit.Editor.Authoring.UIPreviewAdapter`：

```csharp
public override void Configure(UIContextBuilder builder) { /* 显式注入依赖 */ }
public override UIArguments CreateArguments() => UIArguments.From<MyArgs>(null);
public override void Release() { /* 结束自己持有的共享依赖 */ }
```

每次预览克隆一次 Adapter，Configure 在 Build 前调用，参数按 Registry 验证。typed null 必须有明确类型；不会自动构造任意业务对象。Release 至多调用一次，失败也会清理，主错误不会被清理异常覆盖。

工厂服务继续由 Context 释放。共享依赖归注入方所有：Release 内查看 `ShutdownResult`。若结果存在 PendingCleanup，或 Shutdown 本身未正常完成，不得立即 Dispose 仍被迟到任务使用的共享资源；应由适配器自己的任务/租约所有者在真实完成后释放。工具不能用终态诊断快照推测迟到工作已经物理完成，也不强制销毁仍可能被迟到工作持有的 Adapter。

## 编辑现场与恢复

脏场景保存对话框取消即不启动；未保存的 Untitled 场景需先保存，Prefab Stage 需先退出。Root 模板仅允许一个 UIRoot 和标准 Unity UI 组件，不带启动脚本、其他 Host 或 EventSystem。

临时资产位于 `Assets/Wulato.UIKit.Preview/<request-id>`，恢复状态独立保存在 `Library/WulatoUIKit/preview.json`。停止、关闭窗口、编译中断和退出 PlayMode 请求同一清理路径，保留 Shutdown 错误及 PendingCleanup。恢复前重新核对当前布局和原场景 GUID，删除前核对工具目录 GUID、文件清单和哈希。

用户修改临时资产、切换场景布局或原场景丢失时，工具保留现场并提示。崩溃/重启后使用“恢复原场景”或“保留现场”，不会自动再次进入 PlayMode；损坏记录也保留供人工查看。归档记录在 Library/WulatoUIKit/PreviewHistory。预览不修改 Build Settings 或项目输入设置，不替代 Player、移动设备或完整业务场景验收。
