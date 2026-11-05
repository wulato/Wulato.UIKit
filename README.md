# Wulato.UIKit

面向 Unity uGUI 的独立 UPM 框架，提供 Session 生命周期、异步打开关闭、缓存、导航、Dialogs、PopupQueue 和 Presenter 扩展点，不强制 MVC/MVP/MVVM。

- 主包：`com.wulato.uikit`，当前为 0.5 开发树，基础 Runtime 已收敛为 `Wulato.UIKit`。package.json 暂保持 0.4.0，不表示已发布 0.5；见 [开发迁移](Packages/com.wulato.uikit/Documentation~/Migration-0.5.md)。
- Addressables/Input System 适配已内置主包条件程序集；只按需安装 Unity SDK，无需额外 UIKit 适配包。
- [接入指南](Packages/com.wulato.uikit/Documentation~/QuickStart.md) · [升级迁移](Packages/com.wulato.uikit/Documentation~/Migration-0.5.md) · [独立预览](Packages/com.wulato.uikit/Documentation~/Preview.md)
- [0.5 当前实施计划](docs/plans/2026-11-05-wulato-uikit-0.5-optimization.md) · [0.4 历史计划与证据](docs/superpowers/plans/2026-11-03-wulato-uikit-master-optimization.md) · [验证入口](Verification/README.md)

0.5 计划已批准，Phase 0 两项 PR 已合并，默认分支为 main。Phase 1 已合并，当前任务 2.1 收敛 Context 入口与默认装配；host.UI 为主入口，Registry 来源驱动默认加载。导航与 Dialog/Popup 便捷入口按后续阶段交付。

开发采用简化 GitHub Flow：main 为稳定基线，独立任务使用 feat/*、fix/*、refactor/*、chore/* 短分支，专项验证后提交 PR，由用户确认合并。main 已要求通过 PR，禁止强推和删除，管理员同样受约束；暂不要求额外审批账号或远端 CI 状态，人工合并确认仍然必需。不清理 master、原开发分支及未提交工作区。每项依赖任务从更新的 main 开始，不自动合并。

开发默认使用 Unity 2022.3.62f2。其他编辑器按明确兼容目标配置和验证；开发默认、已有历史证据与新版支持声明是不同概念。包的最低版本声明保持不变，不宣称所有更新版本均已验证。

日常修改先查看建议：

```sh
python3 Tools/Verification/ci_suites.py --base HEAD --plan
```

该命令包含本地暂存、未暂存和相关未跟踪文件，只输出建议。执行具体专项使用现有 `Tools/Verification/run.py`；`--editor all` 仅选择 `editors.json` 中的 targets，`--backend all` 选择两种输入后端。正式移动发布需另过独立门禁。
