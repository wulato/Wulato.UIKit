# 性能采样与资源基线

T10 的 Editor 基线由仓库的 `Verification/Reports/2026-11-01-t10.md` 汇总，原始每次耗时和 ProfilerRecorder 数据保存在该报告链接的 `performance-*.trace.json`。本包没有承诺零 GC、固定毫秒预算或移动端性能；Player/IL2CPP 数据尚未采集，留给 T11。

## 条件与复现

使用 Unity 2022.3.62f2 / 6000.3.22f1，分别在 legacy 与 Input System 宿主运行；执行模式为 Editor PlayMode、诊断 Basic、CacheCapacity=1。代表 Modal 使用 InventoryView 的真实 Prefab、uGUI Text、普通 ScrollRect，参数固定为 `baseline-player / Equipment`。采样在专门的空 Scene 中进行，所有实例来自真实 PrefabUIViewLoader。

从仓库根运行：

```sh
python3 Tools/Verification/run.py prepare-samples --editor all --backend all
python3 Tools/Verification/run.py tests --editor all --backend all --mode PlayMode --suite samples
python3 Tools/Verification/run.py tests --editor all --backend all --mode PlayMode --suite performance
```

prepare-samples 先导入三个 Samples 并确认业务程序集编译，再安装独立样例 acceptance 包。未导入样例的宿主不装此包，普通测试也不引用样例类型。CLI 保留已有消费端 Prefab/Scene 和 GUID，源脚本存在非预期差异则报错；语义正确性由生成校验和实际样例测试证明。

每种模式记录 50 次 open/close，逐次保存 openMs、closeMs、elapsedMs，包含异步等待的经过时间，不把它混同于同步 CPU 耗时：

| 模式 | 准备方式 | 资源验收预期 |
| --- | --- | --- |
| first-load | 每次新建 Context，首次加载/实例化后 Release | 创建 50 个；每次稳定后实例/租约 0 |
| warm-release-rebuild | 一次预热 Release，同 Context 再重建 50 次 | 含预热创建 51 个；每次稳定后 0 |
| cache-one-reopen | 一次预热后 CacheOne 重开 50 次 | 共创建 1 个；关闭最多保留 1 个，ClearCache 后 0 |

这里的 first-load 是 **Editor 中该 Context 的首次加载与实例化**。注册 Prefab 已由 AssetDatabase 加载，不模拟冷磁盘或清空操作系统资源缓存；不可把这一列称为设备冷启动加载耗时。每个成功关闭结果无清理错误，PendingCleanup/IsolatedLeaseCount 为 0，并等待延迟 Destroy 稳定后用实际实例计数和独立租约计数交叉检查。另以容量 0 验证 CacheOne 不保留资源。

## 分配与 Canvas

GC Allocated In Frame 记录整个采样窗口中真实帧分配，并保存帧数、总量和最大帧；Profiler.GetTotalAllocatedMemoryLong 记录窗口首尾总内存。GC 数据包含 Editor/Test Runner 开销，既不是线程分配差，也不归因于单条异步命令。不会以 GC 必须显著降低作为硬性通过条件。

Canvas 指标通过当前编辑器公开的 ProfilerRecorderHandle 枚举，按顺序选 Canvas.BuildBatch、UGUI.Rendering.UpdateBatches、Canvas.SendWillRenderCanvases；报告明确记录实际名称、可用性、样本数、纳秒总量和最大帧。不同标记含义/平台不能直接合并比较；不可用时显式标记 unavailable，绝不以 0 伪装采样成功。

`Wulato.UIKit.Load.Sync`、`Layout.Sync`、`Hooks.Sync`、`Transition.Sync`、`Cache.Sync` 仅包围同步片段：加载/钩子/转场调用返回 UniTask 就结束作用域，布局等待之后的 ForceUpdateCanvases 单独包围，不跨 await/跨帧持有 ProfilerMarker。Hooks 包括实例创建及 open/close 的同步入口；它不宣称覆盖异步续体内的全部业务工作。采样测试检查五种标记实际产生样本，异步经过时间使用独立 Stopwatch 记录。

解释差异时应比较相同编辑器、输入后端、参数、诊断配置与 Recorder 名称；首次/预热还会包含 JIT、字体/UI 缓存和 Editor 调度噪声。若缓存重开分配收益不足，应根据原始记录定位，不捏造阈值。目标项目可在真实设备上设置自己的耗时、分配和重建预算。
