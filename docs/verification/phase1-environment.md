# 第一阶段 T11 验收环境（2026-11-01）

**当前完整验收已通过，无未解决环境阻塞。** 结果与原始数据见 [T11 报告](../../Verification/Reports/2026-11-01-t11.md)。

| 项目 | 本轮实测 |
| --- | --- |
| Unity | 2022.3.62f2、6000.3.22f1；未使用 2022.3.62f2c1 替代 |
| Build Support | 两版本均有 AndroidPlayer/iOSSupport，实际位于与 Unity.app 同级的 PlaybackEngines；已通过真实平台构建 |
| Android | ARM64 实体设备，两个版本 × 两后端 4/4 构建、安装、启动、触摸 smoke 通过 |
| iOS | iPhone 13、iOS18.4，两个版本 × 两后端 4/4 完成 Unity 导出、Xcode 编译、签名、安装及真机启动/触摸 smoke |
| Xcode | 26.0.1 / 17A400 |
| 签名 | 用户指定田茂弟个人开发证书，实际 Team K4332PY5B7；凭据保留在执行环境，未写入仓库 |
| clean | 两编辑器 Android Input System 消费样例构建 2/2，实际 Player 程序集清单无验证/Test 程序集 |
| Git 安装 | 4 个新消费宿主，从当前包内容建立独立、固定 commit 的本地 Git 快照安装，无旧 Library 借用 |

## 本轮已解决的问题

- 原“缺少 Build Support”是脚本只查 app bundle 内路径的误判，现已撤回并增加两种目录布局的回归；真实 8 组合构建与设备证据优先。
- 普通项目测试错误引用仓库 Verification 夹具已修复，包内测试自包含；当前根 Unity Console 0 编译错误。
- Android 新 Input System 测试应用曾被手机拒绝安装；用户允许后安装及真实 smoke 通过，失败尝试已归档。
- 验证入口最初无摄像机清屏，关闭后闪烁；加入固定背景与完成提示，用户复验确认画面稳定。
- 早期批处理许可 IPC 失败不代表用户无法打开 Unity；后续全部指定版本批处理与真实构建已正常完成。
- Xcode 曾在 provisioning 阶段崩溃；独立复验报个人账号登录被拒绝和缺少 profile。用户在 Xcode 恢复账号后，四项个人签名/实体设备验收均通过。

## 执行与后续复现

按 Verification/README.md 使用既有 runner。构建/测试/设备命令由同一 artifact-root 锁串行协调，保持当前源指纹、依赖锁和原始证据索引。每个 smoke build-id 对应实际包及设备报告；更换编辑器版本的同包名 iOS smoke 应用时先归档结果，再干净重装本任务生成的验证应用，避免旧报告干扰。

本地不可变 Git 快照证明固定 commit 的干净接入，不宣称远端发行 tag 的权限或网络可达性。远端 GitHub Actions 未触发；本轮在此 macOS 宿主执行同等验证命令。性能基线限于记录的 Editor 条件，未宣称 Player 性能测量。

未自动推送、合并、打 tag 或发布；后续发行另行按授权处理。
