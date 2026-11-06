# 安装 Wulato.UIKit 0.5.0

UIKit 只有一个包：`com.wulato.uikit`。必需依赖为 uGUI 1.0.0、UniTask 2.5.11；Addressables、Input System、DOTween、R3、FancyScrollView、Localization、TMP 和主题资源均按需选择。项目不必采用 MVC/MVP/MVVM。

开发/本轮验收版本为 **Unity 2022.3.62f2**。存在其他编辑器 profile 不等于本次兼容验收。0.5.0 为代码与文档检查点；远端 CI、完整 Editor/Reload、移动/IL2CPP 与 Release Gate 按后续阶段验收。

## 固定提交安装

先从仓库已审核 PR/交付记录获取包含 0.5.0 的 **40位提交 SHA**，替换下面的 `REVIEWED_40_CHARACTER_SHA` 后再写入项目 `Packages/manifest.json`。占位符不可直接使用；不要把 main、任务分支或可移动 tag 当固定版本。

```json
{
  "dependencies": {
    "com.wulato.uikit": "https://github.com/wulato/Wulato.UIKit.git?path=Packages/com.wulato.uikit#REVIEWED_40_CHARACTER_SHA",
    "com.cysharp.unitask": "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask#2.5.11",
    "com.unity.ugui": "1.0.0"
  }
}
```

这是**需要合并到已有 dependencies 的片段**，不能覆盖项目其他依赖。UPM 不会代替你配置 Git 来源的 UniTask；根 manifest 显式声明其固定源。使用内部镜像时保持同版本、记录实际提交与解析结果。Git URL 依赖需要消费机器的 Git/访问权限；不要把凭据写入 manifest。

也可将已审核提交检出到稳定目录，在 Package Manager 选择 Add package from disk → `Packages/com.wulato.uikit/package.json`；业务 asmdef 引用 `Wulato.UIKit`，无 asmdef 的脚本不需额外配置。磁盘安装目录需随项目环境保持可访问，适合本地联调；团队交付优先使用固定 Git 源。两种方式不要同时安装两份主包。

打开 Unity 等待解析和编译，在 Package Manager 确认主包为0.5.0、UniTask为2.5.11。让 UPM 正常生成/更新项目 `packages-lock.json`，检查后提交该项目自己的 manifest/lock。不要复制本仓库验证宿主的绝对 file: 路径或锁文件到业务项目。

## 选择能力

以下为本计划使用的候选/验证版本，版本范围条件只决定编译参与，不宣称范围内全部版本兼容。SDK 安装是项目显式选择；导入 Samples 不会安装 SDK。

| 能力 | 项目额外安装/配置 | UIKit 启用方式 | 指南 |
| --- | --- | --- | --- |
| 普通 UI / 页面 / Dialog / Popup | 无额外 SDK | 主包默认装配；Host 或 Builder | [QuickStart](QuickStart.md) |
| Legacy 输入 | Active Input Handling 支持旧输入 | 主包 LegacyUIInputDriver | [初始化](Authoring-0.5.md) |
| Input System | Unity Input System1.17.0，明确输入设置 | 主包 InputSystemUIInputDriver；包版本条件程序集 | [初始化](Authoring-0.5.md) |
| Addressables | Unity Addressables1.21.21 / SBP1.21.25 | Registry External/addressables 来源自动路由；不传 Loader | [Addressables](Addressables/index.md) |
| 动画 | DOTween1.3.045 核心 DLL 与必需 Runtime Modules | WULATO_UIKIT_DOTWEEN，Host Preset；未启用时 Immediate | [DOTween](Integrations/DOTween.md) |
| 响应式绑定 | R3/R3.Unity1.3.1及指南列出的 DLL 依赖 | 导入 R3 Session Binding；WULATO_UIKIT_R3 | [R3](Integrations/R3.md) |
| 大列表 | FancyScrollView1.9.0 | 导入 FancyScrollView Catalog；WULATO_UIKIT_FANCY_SCROLL | [控件](Integrations/Controls.md) |
| 本地化 | Unity Localization1.4.5及必需依赖；本次实际闭包含 Addressables1.21.21 / SBP1.21.25 / Newtonsoft3.2.1 | 原生组件或 Localization Session；示例需 WULATO_UIKIT_LOCALIZATION | [Localization](Integrations/Localization.md) |
| Theme / Focus | 项目已有 Styler / 原生 Selectable | 不安装新 UIKit 包，不升级 Evo，不增加焦点服务 | [Theme/Focus](Integrations/ThemeAndFocus.md) |

DOTween Pro、其他版本/厂商控件、具体字体/RTL和真机不包含在这些候选版本的通过声明中。DOTween/R3/Fancy 的显式宏只有在所需 SDK 完整安装后才能启用；Localization 示例额外使用包 Version Define 隔离缺席 SDK。关闭某能力应先移除业务直接引用并关闭宏，再移除不再被其他功能使用的 SDK；Localization 自己也依赖 Addressables，不要误删共享依赖。

## 初始化与样例

推荐先走 [QuickStart](QuickStart.md)：导入一个样例 → Workbench配置输入、Root和Registry → 安装/更新 Host → 运行。默认单一 EventSystem/输入模块；Both 输入项目须明确选择驱动。不要用 Styler、自建模块或 Canvas 覆盖排序绕过 UIKit 门控。

Package Manager Samples 共九项：QuickStart、Modals、Inventory、BusinessFlow、LocalInventory、DOTween、R3 Session Binding、FancyScrollView Catalog、Localization Session。普通开发先导入 QuickStart；BusinessFlow 演示页面导航/Dialog/Popup/可选 Presenter。LocalInventory 与其他可选示例按各指南先准备 SDK。已经导入到 Assets/Samples 的目录是消费项目文件，升级 Package 不自动更新这些副本；有改动时先比较，不覆盖源码、Prefab、场景或 GUID。

生成器写入 `Assets/Wulato.UIKit.Generated`，模板创建的业务源/Usage由项目持有。升级后重新生成/编译/写回 Registry；不要直接编辑生成产物来绕过校验。详细入口见 [Workbench](Authoring-0.5.md)、[预览](Preview.md)、[迁移](Migration-0.5.md)。

## 安装问题与撤回

- 旧 UIKit 业务/适配包造成重复类型或程序集：按迁移文档移除旧 dependencies/testables，保留按需使用的 Unity SDK；不要复制 Runtime 补齐。
- 找不到 UniTask：检查根 manifest 的 Git 源、2.5.11解析及 Git访问；不要改 UIKit 核心异步依赖。
- 可选 SDK类型缺失：检查 SDK版本/Modules/DLL与显式宏，缺 SDK 时先关闭宏；不把 SDK 引用加入基础 Runtime。
- Source/Registry/输入配置失败：先看 Workbench Validate及 Diagnostics，不通过放宽指纹、删除测试或清理用户资产处理。

本任务不创建发布 tag、不推送 release、不改消费项目。撤回时消费端继续使用旧审核 SHA；如已迁移，在项目任务分支恢复其 manifest/lock、生成与业务调用改动，再做相应专项。历史0.4文档与测试证据保留，但不作为0.5验收证明。
