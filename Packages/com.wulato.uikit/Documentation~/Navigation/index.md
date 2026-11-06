# Navigation

主包默认装配 Navigator，与基础模块共用 `Wulato.UIKit` 程序集。开发默认 Unity 2022.3.62f2、UniTask 2.5.11；其他目标须显式验证。

```csharp
var ui = new UIContextBuilder(root, registry).Build();
var home = await ui.OpenAsync<HomeView>(ct); // 新根历史
await ui.PushAsync<InventoryView, InventoryArgs>(args, ct);
await ui.ReplaceAsync<SettingsView>(ct);    // 只替换顶项
bool moved = await ui.PopToAsync<HomeView>(ct);
bool backed = await ui.BackAsync(ct);
```

Open 的泛型/类型擦除入口均按 Registry Layer 分流：Page 建立新根，其他层走普通打开，不改变历史或关闭导航页。Push/Replace 只接受 Page；Replace 在空历史中等价首开。三者尝试打开当前仍存活的同一 Single 时返回 AlreadyOpen，不隐式重载。

PopTo 恢复最近匹配历史项及其原参数，裁掉后续描述；只重建目标，不重放中间页面生命周期。当前已匹配或无匹配（包括未注册目标）返回 false。历史只保存 key 和调用方保证不可变的参数；CacheOne 不等于展示状态持久化。高级历史查询仍可用 `ui.GetService<IUINavigator>().History`，其快照不可修改。

所有历史读取与拟议修改在同一 Context FIFO 中执行，沿用 Prepare/AnimatePair/CommitReplacement：准备或提交前取消/失败恢复旧页、历史与输入；提交后才关闭旧页，旧页关闭错误不撤销已提交历史，caller 取消也不撤销提交。Shutdown 仍可接管有界关闭；不合作动画按既有隔离/迟到清理协议处理，不复活仍被动画写入的对象。

`ui.CloseAsync(session)` 或 Session 自己关闭当前页时清空历史；关闭旧句柄不影响新会话。显式 Open/Push/Replace/PopTo 不调用用户返回确认。当前 BackAsync 仍只处理页面历史；Modal 优先及 CanClose/输入 Cancel 的统一路由按 2.3 交付，不改变本阶段输入驱动。

通过 `WithNavigationTransition(pair, settings)` 配置页面配对动画，默认立即完成；普通非 Page 界面继续使用 IUITransition。Stop 必须同步停止写入并终结活跃操作，Reset 幂等恢复声明写集。禁止在生命周期/转场回调中同步等待同 Context 公共命令。

组合样例 BusinessFlow 从主包导入，运行菜单 Wulato/UIKit/Prepare Business Flow Sample，再打开 BusinessFlowScene；Author 只补缺失资源，不覆盖已存在消费场景。
