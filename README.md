# Wulato.UIKit 0.5.0

面向 Unity uGUI 的轻量独立 UPM 框架，提供 Session、异步打开关闭、缓存、导航、Dialog、Popup 和可选 Presenter。简单 UI 可直接编写，不强制 MVC/MVP/MVVM。

只安装一个 UIKit 包：`com.wulato.uikit`。基础 Runtime 为 `Wulato.UIKit`，必需依赖 uGUI1.0.0 / UniTask2.5.11；Addressables/Input System/DOTween 适配在主包内条件程序集，可选绑定、列表和本地化通过样例接入。

- [安装与能力选择](Packages/com.wulato.uikit/Documentation~/Installation.md) · [QuickStart](Packages/com.wulato.uikit/Documentation~/QuickStart.md)
- [0.5 Breaking Changes / 迁移](Packages/com.wulato.uikit/Documentation~/Migration-0.5.md) · [包内索引](Packages/com.wulato.uikit/README.md)
- [Workbench](Packages/com.wulato.uikit/Documentation~/Authoring-0.5.md) · [独立预览](Packages/com.wulato.uikit/Documentation~/Preview.md)
- [0.5计划与验收记录](docs/plans/2026-11-05-wulato-uikit-0.5-optimization.md) · [验证入口](Verification/README.md) · [历史0.4记录](docs/superpowers/plans/2026-11-03-wulato-uikit-master-optimization.md)

开发/本轮验收使用 Unity **2022.3.62f2**。0.5.0 是 Phase1–5 功能的文档/安装检查点；远端 CI、完整 Editor/Reload、移动/IL2CPP、真机和 Release Gate 尚须后续验收。其他编辑器 profile 与历史证据不代表当前支持声明。

开发采用 GitHub Flow：main 为默认分支与稳定基线，任务使用 feat/*、fix/*、refactor/*、chore/*，专项通过后提交 PR，main 合并由用户确认。保护禁止强推/删除及管理员绕过；不清理 master、原分支或用户工作区。

日常修改先检查 `git status`，再运行建议（不执行测试）：

```sh
python3 Tools/Verification/ci_suites.py --base HEAD --plan
```

具体专项使用 `Tools/Verification/run.py`，默认只选相关 suite 与准确编辑器/后端。全量、远端与移动门禁按计划执行，不因版本号升级而宣称完成。
