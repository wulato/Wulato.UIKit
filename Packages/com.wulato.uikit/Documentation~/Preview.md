# 独立 PlayMode 预览

打开 Tools → Wulato → UIKit → Workbench 的“预览”页，创建并保存 UIPreviewProfile，配置 Registry、已注册 View Key 和输入驱动（留空按当前启用后端；Both 必须明确选择），可选已有转场 Preset。先完成现有生成、编译、写回及只读校验，再启动预览。Legacy 可使用默认驱动；Input System 项目必须明确指定已安装适配器脚本。

工具保存用户确认后的场景布局与选择，在独立临时场景中通过 UIContextHost 和现有 OpenAsync 运行。使用“关闭界面”“再次打开”观察同一 Context 中的新 Session；CacheOne 可以复用实例，Session 身份仍会改变。普通业务启动场景不参与运行。

## 参数及业务依赖

Prefab、Resources、已安装 SDK 的 Addressables 按 Registry 默认路由，无参 View 无需 Loader Adapter。参数或业务服务需显式构造时，创建 Editor ScriptableObject，继承 `Wulato.UIKit.Editor.Authoring.UIPreviewAdapter`：

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

## 只读 Diagnostics

Workbench 的 Diagnostics 页在 PlayMode 中以约4Hz采样活跃 Context，以及仍存在的 Host 的终态。按 Context 显示 Session、页面历史键（不含业务参数）、缓存 LRU、输入 Owner/Generation、锁、租约/隔离数、PendingCleanup 与错误摘要。暂停采样和清除只影响面板，不调用运行时修改 API；最多保留32个 Context，每组最多显示128行。

UIDiagnosticsSnapshot.History 仅复制类型键；Errors 只保留值摘要，最近32条、消息最多1024字符，TotalErrorCount 为该 Context 已报告错误总数。ErrorSink 收到同一个原异常且仍由原所有者管理；未报告、已被业务捕获的异常不计入。Off 不记录历史或错误。快照不暴露 View、Context、Session 对象、业务参数或 Exception。

“运行中采样”“终态快照”“已脱离运行集的最后采样”有明确区别。停止后的 SampleTime / PendingCleanup 不更新，不能据此判断迟到工作的物理释放。正常预览停止保存纯值记录，跨 Domain Reload 仍显示终态及原采样 UTC；中断期间没有取得样本时只保留恢复错误，不伪造实时数据。新一次 Play 清除旧面板采样；重新预览仍通过原生命周期产生新 Session。恢复记录附加诊断数据不改变原场景/资产的 GUID、文件清单和哈希保护。
