# Local Addressables Inventory

安装主包、UniTask 和 Unity Addressables SDK 后，从主包导入本样例，无需独立 UIKit 适配包。开发验收固定 Unity 2022.3.62f2 / Addressables 1.21.21 / SBP 1.21.25；其他编辑器需显式验证。

1. 显式调用 `Wulato.UIKit.Samples.Addressables.Editor.LocalInventorySetup.Author()` 创建缺失 Prefab 与本地组，不覆盖已有资源。
2. 执行 UIKit Generate，等待编译，Complete writeback，再调用 `FinalizeSample()`。
3. 在 Addressables Groups 中 Build New Build / Default Build Script，Play Mode Script 选择 Use Existing Build。
4. 打开 LocalInventoryScene。样例依次展示带参打开、缓存重开、ClearCache、预取消，再显示 Inventory: 24。

External 注册不将 Prefab 硬引用写入注册资产。组、catalog 和 Bundle 属于宿主；普通构建只校验，不自动生成代码或构建内容。样例的三个原有主包样例和 BusinessFlow 可与本例共存。
