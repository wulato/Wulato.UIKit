# Navigation

内置主包 0.4.0，保留 Wulato.UIKit.Navigation 程序集；无需旧 Navigation 包。开发默认 Unity 2022.3.62f2，其他兼容目标显式验证，UniTask 2.5.11。

```csharp
using Wulato.UIKit.Navigation;
var ui = new UIContextBuilder(root, registry).Build();
var nav = ui.GetService<IUINavigator>();
await nav.PushAsync<HomeView>(ct);
await nav.PushAsync<SettingsView, SettingsArgs>(args, ct);
bool moved = await nav.BackAsync(ct);
```

仅导航 UILayer.Page；Modal 不入历史。历史包含当前页面，只保存 key 与调用方保证不可变的参数。CacheOne 不是展示状态持久化；外部 Model 由项目保存。首页 Back=false；Single 同类型重复 Push 报 AlreadyOpen。

请求按 Context FIFO，普通 Open/Close 不插入切换。提交前取消/失败恢复原页；提交后调用方取消不撤销切换，等待旧页有界关闭。关闭错误进入 UICompletion/错误接收器，不能据此重试已提交的 Push。Shutdown 仍可从队列外停止。当前页被外部关闭后历史清空。

自定义 INavigationTransition 只修改声明的动画写集；Stop 同步终止写入并完成活跃操作，Reset 幂等复位。默认无动画，NavigationSettings 的超时默认2秒并在组合时复制。不得在生命周期或转场回调中同步等待同 Context 命令；外部完成源会同步执行续体，应在回调之外提交流程。

组合样例 BusinessFlow 从主包导入；导入后使用菜单 Wulato/UIKit/Prepare Business Flow Sample，再打开 BusinessFlowScene。Author 只补缺失资源，不覆盖已存在消费场景。
