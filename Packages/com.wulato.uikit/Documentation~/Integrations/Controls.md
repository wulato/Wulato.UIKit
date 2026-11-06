# 列表与扩展控件边界

默认使用 uGUI 的 ScrollRect、LayoutGroup、Button 等控件。少量数据无需虚拟列表。UIKit 只管理 View / Session / 输入归属；业务直接使用控件 API，不提供统一控件控制器、列表引擎或额外生命周期。

## FancyScrollView（可选）

[官方 v1.9.0](https://github.com/setchi/FancyScrollView/tree/v1.9.0) 支持池化、位置驱动的 Cell 呈现及原生 Scroller。项目需要固定尺寸大数据列表时，可另行安装官方 UPM：

`https://github.com/setchi/FancyScrollView.git#v1.9.0`

导入 **FancyScrollView Catalog** 示例并为使用的构建目标设置 `WULATO_UIKIT_FANCY_SCROLL`。SDK 安装需项目自行授权；UIKit 不自动安装，不增加必需依赖。仅安装 SDK 不会启用 UIKit 示例；缺 SDK 时保持符号关闭。原来的 Inventory / ScrollRect 示例不变。

示例直接继承 `FancyScrollView<int, CatalogContext>` / `FancyCell`，使用 SDK 原样的池与 Scroller。每行固定 64，视口 384；使用示例内 Resources/UIKitFancyCatalogDemo.prefab；也可自行在 Inspector 绑定 CatalogView.list 与 Scroller.viewport。SDK 没有公开 viewport setter，采用正常 Prefab 序列化，不修改 SDK 或运行时反射私有字段。Cell 的缩放由 SDK 给出的滚动位置驱动，滚动和缓动交给 SDK；这不是新的 Tween 引擎。需要复杂动画可按官方样例使用 Animator，不强制安装 DOTween。

实例化 Cell 数与视口及 SDK 保留的池高水位相关，不随 1万→10万数据量增长。SDK 扩大视口时增池，缩小后保留已有实例；此示例不修改池策略，不支持可变行高、通用 Grid、远程分页或任意布局虚拟化。不要据此宣称所有数据量、设备或列表零 GC。

### 异步与 Session

`CatalogArgs` 传入 ID 列表及 `LoadCaption(id, ct)`。数据由业务持有，打开期间不要原地修改列表；需要更新时由业务构造新快照并明确刷新/重开，示例没有通用响应式数据源。

每个 Cell 在换项、隐藏或禁用时取消前次请求。Session.Token 关联所有请求；整个列表的清理由 Session.Track 管理。结果切回主线程后检查：对象存活、取消状态、捕获的 Session.IsCurrent、Cell 绑定序号。序号避免同一 ID 再次出现时旧请求写入新绑定；身份校验也覆盖不遵守取消的业务 Loader。

示例只加载字符串，没有资源释放责任。若改为 Texture / Addressables 等资源，业务 Loader 必须明确每次结果的所有权，并在取消或迟到结果被拒绝时释放其 Lease；不要仅丢弃 GPU 资源引用。Session 自动解绑不能代替资源所有权协议。活动请求的业务错误输出 Console；业务可在 Loader 内实现降级/重试策略，不应无限重试或吞掉当前错误。

### 输入与性能

把整个列表放在所属 View 下，沿用 UIKit InputArbiter，不自行创建 EventSystem 或绕过 InputDomain。正式输入验证使用真实驱动与 ScrollRect，覆盖拖拽中 Modal 抢占、一次 endDrag、无 drop、按住不重放和释放后新按下；两后端分别验证。原生 Scroller 的基础拖拽/位置呈现另在真实 SDK 专项验证。

局部性能仅记录 Editor 内 300 次跳转耗时、GC.GetTotalMemory 差值与 Cell 数；计时不含每帧渲染/异步网络或真实设备性能，不设跨机器阈值、不代表 Player 分配和帧预算。

## Unity UI Extensions（直接使用，仅指南）

[官方仓库与文档](https://github.com/Unity-UI-Extensions/com.unity.uiextensions) 提供扩展控件。先确认目标控件确实解决项目需求，再按官方发布版本和依赖说明安装；不默认安装整套 SDK，不复制控件源码进 UIKit，不添加 UIKit 控制器包装。Evo 等商业控件也遵循同一边界，不假设项目已购买或安装。

接入检查限于所选控件：Unity 2022.3 / uGUI 版本、TMP 等附加依赖、实际程序集引用、是否新增 EventSystem/InputModule、Mask/Raycast 和 CanvasGroup 支持、拖拽/Modal 切换及 Session 结束后回调。其订阅/事件解绑归 Session.Track；控件内部池化由控件自身管理。UI Extensions 本任务未安装/编译，不宣称该 SDK 或全部控件已经兼容。

撤回本功能时 revert 示例与专项即可继续使用原 ScrollRect，不自动卸载用户 SDK。
