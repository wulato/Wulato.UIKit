# T2 P1 修复验收：根工程 UniTask 依赖

2026-10-31。仅修复根开发工程的 UPM 接入；T3 未开始。

## 修复

根工程 `Packages/manifest.json` 按 [UniTask 官方 UPM 安装说明](https://github.com/Cysharp/UniTask#upm-package) 添加 Git URL，并固定批准版本：

```json
"com.cysharp.unitask": "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask#2.5.11"
```

`Packages/packages-lock.json` 由已打开的根工程 Unity Package Manager 自动生成：UniTask source=git、depth=0、提交 `2e993ff18f28c931602a07292df0b0804eebef99`；同时登记主包 source=embedded、version=file:com.wulato.uikit。原有其他依赖条目保持一致。主包 package.json 的 UniTask 数字依赖仍为 2.5.11。

## 验收证据

完整目录：[20261031T125159262468](/Users/tian/Projects/Tian/Wulato.UIKit/Artifacts/uikit/root-dependencies/20261031T125159262468)，原始文件由 [evidence-index.json](/Users/tian/Projects/Tian/Wulato.UIKit/Artifacts/uikit/root-dependencies/20261031T125159262468/evidence-index.json) 保存 SHA-256。

| 项目 | 结果 | 原始证据 |
| --- | --- | --- |
| 修复前根 manifest/lock 校验 | 预期 FAIL：缺少 UniTask Git 来源/锁定 | [red.json](/Users/tian/Projects/Tian/Wulato.UIKit/Artifacts/uikit/root-dependencies/20261031T125159262468/red.json) |
| 修复前实际 Unity 错误 | com.cysharp.unitask@2.5.11 cannot be found | [red-editor-excerpt.log](/Users/tian/Projects/Tian/Wulato.UIKit/Artifacts/uikit/root-dependencies/20261031T125159262468/red-editor-excerpt.log) |
| 修复后实际根工程 | Unity 2022.3.62f2；UniTask 2.5.11 / Git；UIKit 0.1.0 / Embedded；uGUI 1.0.0 | [root-resolution.json](/Users/tian/Projects/Tian/Wulato.UIKit/Artifacts/uikit/root-dependencies/20261031T125159262468/root-resolution.json) |
| 根工程实际程序集 | UniTask、Wulato.UIKit.Core、Loading、UGUI 四份 Player 编译产物均存在；验证脚本成功引用 IUIContext 和 UniTask | [root-editor-resolution.log](/Users/tian/Projects/Tian/Wulato.UIKit/Artifacts/uikit/root-dependencies/20261031T125159262468/root-editor-resolution.log) |
| Python 回归 | 76/76 PASS | [python-tests.log](/Users/tian/Projects/Tian/Wulato.UIKit/Artifacts/uikit/root-dependencies/20261031T125159262468/python-tests.log) |
| G0 schema 3 | PASS，现有四宿主证据指纹仍有效 | [g0-preflight-report.json](/Users/tian/Projects/Tian/Wulato.UIKit/Artifacts/uikit/root-dependencies/20261031T125159262468/g0-preflight-report.json) |
| 清理与变更范围 | 临时 Editor 探针源码、meta 和 DLL 已移除；主包及 ProjectSettings 指纹不变 | [acceptance.json](/Users/tian/Projects/Tian/Wulato.UIKit/Artifacts/uikit/root-dependencies/20261031T125159262468/acceptance.json) |

验证通过根工程内临时 Editor-only 程序集进行，使用 Unity PackageInfo 和 CompilationPipeline 读取实际解析与编译结果；不是手填版本或复制四宿主的锁文件。探针源与 asmdef 副本保留在证据目录，正式工程不保留探针。日志片段排除了许可等认证信息。

四宿主的 T2 契约测试和编译探针沿用既有验收；本轮只改根 manifest/lock，未声称重跑其 Unity 测试，也未进入会话执行或任何 T3 实现。

本记录补充并更新原 T2 报告中“根配置无变更”的历史状态：该描述属于原提交 7d110c8，本次按用户明确授权补齐根工程依赖。
