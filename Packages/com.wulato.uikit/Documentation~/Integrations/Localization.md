# Unity Localization

推荐使用 Unity Localization 的 String Table、Asset Table 和原生组件，不建立 UIKit 语言包或加载器。候选/本次验证版本为 **1.4.5，Unity 2022.3.62f2**；新版兼容性另行验证。核心 UIKit 不引用或安装 Localization。

## 接入

1. 项目按官方流程安装 `com.unity.localization` 1.4.5，创建 Localization Settings、Locales 和 String Table Collection，填写各语言翻译。包会依赖 Addressables/Newtonsoft Json；这些是选择 Localization 后的依赖，不是 UIKit 必需依赖。
2. 普通静态标签优先使用官方 `LocalizeStringEvent`/`LocalizeAssetEvent`，在 Inspector 指定表、条目和更新目标。不要同时让组件和手工订阅写同一标签。
3. 需要以 **Session** 为作用域的标签，从 Package Manager 导入 **Localization Session** 示例，添加项目 define `WULATO_UIKIT_LOCALIZATION`。示例 asmdef 还通过包版本 define 要求 Localization `[1.4.5,2.0.0)`；该范围只用于隔离，不表示全部版本已验收。无 SDK 或无显式 define 时示例不编译。
4. 给 Prefab 添加 `LocalizationView`，在 Registry 注册为带 `LocalizedString` 参数的 View，资源来源配置沿用 UIKit。每个标签使用自己的引用：

```csharp
var greeting = new UnityEngine.Localization.LocalizedString("UI", "greeting");
await ui.OpenAsync<LocalizationView, UnityEngine.Localization.LocalizedString>(greeting);
```

示例只演示 Text；TMP 可通过同样的回调赋值接入，不要求核心安装 TMP。字体、布局膨胀、RTL、Smart Strings、资源本地化、表打包/预加载及缺失翻译策略按官方工作流配置。示例不自动创建或迁移项目表，不调用同步 WaitForCompletion。

## 生命周期与所有权

`OnOpenAsync` 捕获本次 `UISessionScope`，先 Track 退订对象，再订阅 `LocalizedString.StringChanged`。回调检查捕获的 `scope.IsCurrent`。关闭、取消、打开回滚和 Shutdown 均由现有 Session 终结清理；CacheOne 的 GameObject 还活着也必须退订。不能只等 OnDestroy；重开必须重新建立本次订阅。官方事件在 Unity 主线程派发；自定义后台计算必须回到主线程后再次检查 scope。

Localization 1.4.5 自己管理该事件的加载句柄：更换语言时撤下旧完成回调；最后一个订阅移除时撤下完成回调并释放自己 Acquire 的引用。示例**不手工 Release** `CurrentLoadingOperationHandle`、不销毁业务提供的 LocalizedString、不调用全局 StringDatabase 清缓存。底层共享表加载仍可能完成；取消 UI 更新不等于取消所有 SDK 工作。

官方组件订阅通常跟随 OnEnable/OnDisable，不能笼统视为 Session 订阅：如果项目配置使组件在 Session 结束后仍启用，需使用本示例的手工 Session 订阅或在现有 View 钩子中明确停用组件。不要额外建立一套生命周期。

语言切换是项目级 `LocalizationSettings.SelectedLocale` 操作；UIKit 不代管全局设置或回滚其他页面的语言。表加载失败/缺失条目反馈按官方策略处理，避免用空字符串吞掉全局配置错误。语言变化只更新内容，不重新 Open 页面。

## 验证与边界

`localization` 专项使用真实 SDK 的 LocalizedString/表条目查找/格式化/语言事件及引用计数，通过可控表来源验证语言迟到、缓存重开、回滚及 Shutdown。它不代替项目 Addressables 表内容构建、真实翻译、字体/RTL 或移动端验收。`package-install` 检查主程序集依赖隔离；无 SDK、SDK 关闭/启用组合独立验证。

参考：[官方 1.4 手册](https://docs.unity3d.com/Packages/com.unity.localization@1.4/manual/index.html)、[LocalizedString API](https://docs.unity3d.com/Packages/com.unity.localization@1.4/api/UnityEngine.Localization.LocalizedString.html)。
