# 升级到 0.4

主包内含 Navigation、Presentation、Dialogs、PopupQueue；类型、命名空间、程序集名和资产 GUID 保持不变，使用方式仍然显式组合。Core 不依赖这些业务模块。

## 消费项目迁移

1. 保存项目并将当前版本及本地修改纳入自己的版本管理。
2. 在同一次 manifest 修改中移除四个旧业务包 `com.wulato.uikit.navigation`、`.presentation`、`.dialogs`、`.popup-queue` 的依赖及 testables，升级 `com.wulato.uikit` 到同一确定提交中的 0.4.0。
3. 使用 Input System 时升级对应适配到 0.4.0；使用 Addressables 时升级对应适配到 0.2.0。它们继续独立安装，按编辑器兼容目标显式配置 SDK 版本，UniTask 仍为 2.5.11。
4. 让 Unity 重新解析依赖锁，不手工保留旧业务包或复制旧 Runtime 目录；否则会产生重复程序集。程序集名称未变，消费者 asmdef 无需改名。
5. BusinessFlow 从主包导入；已有导入样例保留，不覆盖消费者改过的脚本、场景或 GUID。先在单独工程验证迁移，再更新实际业务工程。

Git 安装将主包及所选适配器固定到同一提交，路径分别为 `Packages/com.wulato.uikit`、`Packages/com.wulato.uikit.input-system`、`Packages/com.wulato.uikit.addressables`。此文档不代表已有公开 tag 或已发布 registry 版本。

旧四包不保留兼容空壳。验证命令中的旧单业务包安装范围会返回迁移错误，改用 main/addressables/all；main 现在包含所有业务模块，不能以旧单模块隔离结果替代新版安装证据。回退应恢复升级前的整组依赖和锁，避免新主包与旧业务包混用。

## API 与版本维护

现有 IUIContext、View/Session 和四模块契约保持不变。新增 UIContextHost 是可选装配层，高级组合继续直接用 Builder。新增 Editor Profile/Adapter 仅用于开发工具，Player 不引用 Editor 程序集。

公共 API 破坏变更必须有明确设计、版本说明和迁移步骤；弃用先文档说明并保留过渡 API，再在明确版本移除。0.x 版本也不隐式改变生命周期及所有权语义。每次版本更新同步主包、受影响适配依赖与 Changelog；不为了版本号统一而升级未使用 SDK。
