# P0 Authoring 工作台验收与实际交互记录（2026-11-02）

环境：Unity 2022.3.62f2，Wulato.UIKit 根工程；Both 输入设置，显式使用 Legacy 驱动。结论：本节列明范围通过，含工具替代与未验证项。

## 修复

- 样例场景无Camera时创建幂等背景Camera，消除Game中遮挡UI的“No cameras rendering”提示，保留已有相机。
- 移除跨编译重载失效的缓存Editor，使用公开SerializedProperty绘制Marker；修复NullReference与GUILayout异常。
- 完成创建后同步Prefab及View脚本；已有Prefab选择正确回填。
- 工作台重试/生成先保存资产，防止表单内存值与磁盘Prefab绑定不一致。
- 工作台最小尺寸720×440。

## 实际通过

- QuickStart默认版本目录识别、预览应用、编译续跑与保持样例场景打开的重复配置。
- 无参View创建、已有Prefab配置与GUID保留；空Target错误提示、移除无效项后重试完成。
- 真实Game键盘Submit触发选中的Close按钮；Session/租约从1降到0。
- 同Context重开：Session数量1→0→1，身份1→2，Context ID相同。重开使用验证菜单调用Demo公开OpenAsync，非鼠标Inspector菜单操作。
- 空测试场景Apply/Undo/Redo：Root数量0→2→0→2，磁盘场景未自动保存；随后显式保存测试场景。
- 等待编译阶段点击取消：脚本保留、目标Prefab未创建；编译重载后仍为已取消，场景文件字节及Root/EventSystem保留。
- 双编辑器legacy authoring40项、samples12项，共4组合52项通过，原始证据已按当前快照/索引/XML/trace/指纹复核。
- 根ProjectSettings、manifest、packages-lock与本轮开始时SHA-256一致，未提交或发布。

## 限制与未验证

- CUA对Unity主窗口/UIElements内容区坐标点击报windowNotFoundAtPosition。样例导入由验证菜单调用[Unity官方Sample.Import API](https://docs.unity.cn/Packages/com.unity.package-manager-ui%402.1/api/UnityEditor.PackageManager.UI.Sample.html)完成。
- 未验证鼠标Close点击和Inspector上下文菜单重开；对应行为使用真实Submit与公开OpenAsync验证。
- 未在Unity 6、新输入驱动下人工操作工作台；未人工重走“等待时切换并编辑另一脏场景后取消”的完整竞态，相关逻辑由既有专项覆盖。
- 未运行Player、移动端、IL2CPP或完整发布门禁，也未为本轮刷新完整历史矩阵。

原始本地证据位于 `Artifacts/uikit/authoring-interactive/`：acceptance.json、reopen-check.json、scene-undo-check.json、cancel-check.json、observed-*.json、game-*.png。测试资产及辅助菜单保留在Assets/WorkbenchValidation，便于复现；辅助菜单仅前置导入、场景准备、调用既有Demo入口和读诊断，工作台动作由CUA界面执行。

收尾：Unity自动创建的ProjectSettings/SceneTemplateSettings.json已备份到本证据目录并移除；根设置和依赖保留原状。验证场景/脚本/Prefab及辅助菜单保留，Editor已退出PlayMode。


## 实现与回归范围

主包新增Editor工作台及场景、界面、生成和样例操作服务。复用现有Builder、Marker、全量注册生成、Pending和指纹检查，不新增运行时API或主包依赖。验证入口增加authoring专项与独立QuickStart消费编排；原有批处理入口保留。

实现阶段已完成四宿主editor/samples/generated-integration及干净QuickStart接入；交互修复后仅运行直接受影响的双编辑器legacy专项，未将较早完整矩阵作为修复后完整验收证明。

| 最终专项 | 2022.3.62f2 legacy | 6000.3.22f1 legacy |
| --- | --- | --- |
| EditMode authoring | 20 PASS | 20 PASS |
| PlayMode samples | 6 PASS | 6 PASS |

当前源码的4组合52项原始证据在提交前再次复核通过：

- `Artifacts/uikit/20261102T140625649381-tests-747385`（authoring）
- `Artifacts/uikit/20261102T141753304047-tests-394264`（samples）

Unity资产与meta成对保留。根工程中的导入样例、验证脚本/场景/Prefab和生成注册一同归档，保留GUID。被取消请求的脚本保留，缺少对应Prefab是已验证的预期状态。已有验证场景可直接打开；辅助准备菜单拒绝覆盖已有文件。

## Git归档

2026-11-02 用户在完成验收后明确授权本地Git提交，覆盖此前“本轮不提交”的范围约定；不授权推送或发布。排除根 `Packages/packages-lock.json`、根 `ProjectSettings`、`AGENTS.md` 与 `.vscode` 既有差异。原始截图/日志留在本地Artifacts，此报告保存验收结论、证据位置和实际限制。
