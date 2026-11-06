# Theme 与 Focus 的项目侧接入

## Theme：保留现有 Styler，不建立主题引擎

现有 Evo 1.x（项目已用 1.9.9）或项目自选 Styler 继续负责颜色、字体、Sprite 和控件视觉状态。UIKit 不升级 Evo、不安装商业资源、不迁移主题资产，也不为不同 Styler 再建统一适配接口。只有具体项目证明需要时才加局部桥接脚本；不要为了主题增加运行时必需依赖。Evo 2 的 Unity 6 要求不能套用到当前 Unity 2022 项目。

将样式施加到 **View 的内容子节点**，沿用 Workbench Page/Modal/Dialog 模板。Styler 不得重新设置 Shell 上由 UIKit 管理的 CanvasGroup alpha/interactable/blocksRaycasts，不得通过 ignoreParentGroups、独立 Canvas overrideSorting、额外 GraphicRaycaster 或私建 EventSystem/InputModule 绕过仲裁。调整内容动画使用已有 Transition/DOTween Presets；不要让主题系统与 Transition 同时写同一属性。自定义控件还需真实门控验证，不能仅凭原生 Button 的通过推断所有商业控件安全。

主题变化用项目已有事件/配置刷新；手工订阅用 `scope.Track` 并捕获当前 scope，异步结果回主线程后检查 `IsCurrent`。静态样式不需要 Presenter。不要改变 Localization 全局表加载所有权；本地化字体变化和主题字体覆盖应在项目里指定单一写入方。

## Focus：原生 Selectable + UIKit 现有门控

只使用宿主已有 EventSystem 和对应输入驱动。Legacy 与 Input System 均通过同一 UIKit 输入仲裁域；Input System 模块配置见现有 InputSystem 指南。没有独立焦点服务、焦点栈或控件级 Back 路由。

- 使用 Button、Toggle、Slider、Dropdown 等原生 Selectable，配置显式 Navigation 邻居；避免隐藏、不可交互控件进入导航图。
- 当前门控在新输入 owner 恢复时优先使用该 View 上次保存且仍在其层级内、激活的对象，否则选择层级遍历中第一个可交互 Selectable。首次默认焦点可通过 Prefab 层级顺序安排；本次不新增默认焦点公共 API。
- Modal 接管时清空前页选择并禁止前页 CanvasGroup 交互。关闭 Modal 后恢复前页保存的选择；保存对象已不激活时回退原生可交互控件。
- 现有保存对象有效性检查以 owner/层级/激活状态为准，不会替项目判断保存控件是否后来被禁用或设置 interactable=false。动态改变这些状态时，项目在稳定交互阶段选择另一个活跃可交互目标；不要在 OnOpen 中抢选或绕过锁。销毁/隐藏的默认焦点可按上述回退路径验证。
- 锁和 owner 切换会隔离持有输入；完全释放后才恢复处理。不要每帧调用 SetSelectedGameObject、在解锁时模拟 Submit/Cancel，或用主题脚本强行打开门控。

复杂列表用项目控件的导航能力；虚拟化项不可长期保存为业务选中模型。项目保存 item ID，数据/布局稳定后把焦点映射到当前可见实例；租用 Cell 的回收不得冒充页面 Session。

## 最小项目检查

使用两个原生按钮的 Page 和一个 Modal，分别在两输入后端检查 Modal 获得焦点、关闭恢复原按钮、原按钮隐藏时回退，以及锁内持有 Submit 解锁不点击，释放再按恰好点击一次。仓库 `focus-controls` 专项覆盖这些路径。Gamepad 显式 Navigation、主题厂商控件、RTL 和平台手柄映射仍需项目专项，不据此宣称全部控件或移动端已验收。
