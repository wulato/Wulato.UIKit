# Localization Session

可选 Unity Localization 1.4.5 示例。先安装 SDK、创建原生 Settings/Locales/String Table，再导入本示例并添加 `WULATO_UIKIT_LOCALIZATION`。缺 SDK 或缺 define 均不编译，不影响主包。

在 Registry 注册 `LocalizationView`（参数类型 `UnityEngine.Localization.LocalizedString`），然后用配置好表和条目的引用调用 `ui.OpenAsync<LocalizationView, LocalizedString>(reference)`。Prefab 可绑定自己的 Text；留空会创建最小标签。示例不创建 EventSystem、表或第二套生命周期，也不改变 Shell 门控。

订阅随每次 Session 结束移除；缓存重开重新订阅。SDK 自己处理语言切换与事件句柄，不手工释放其句柄。详细配置、所有权和验证范围见包内 `Documentation~/Integrations/Localization.md`；Theme/Focus 见同目录 `ThemeAndFocus.md`。

Tests 需 Unity Test Framework/UNITY_INCLUDE_TESTS；仅导入 SDK 和示例不自动运行测试。生产项目可不导入 Tests 文件夹。
