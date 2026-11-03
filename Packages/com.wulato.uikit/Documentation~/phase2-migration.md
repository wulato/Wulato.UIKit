# 历史：0.1 → 0.2 迁移

当前升级请使用 [0.4 迁移](Migration-0.4.md)。以下为历史记录。

# 从0.1迁移到0.2

主包和 Input System 适配包升级到0.2.0；四个业务扩展0.1.0明确依赖核心0.2.0。消费 manifest 应将全部选装包固定到相同 Git commit，不依赖尚未发布的 registry 版本。主包不安装任何业务扩展或可选输入后端。

现有 View、Session、Open/Close 入口保持兼容。原 IUITransactionSteps 成员保留；新增 IUIPageTransactionAccess/IUIPageTransactionSteps 用于成对动画与受控替换，不要求第三方实现旧接口时增加成员。

WithTransactions 默认关闭，Navigation 显式启用。工厂运行前可查询 IUIContextLifetime 和本次 UIRegistrySnapshot；内建服务不能覆盖。其余工厂仍只能查询已创建依赖，不得在构造期打开 UI。

Stopping 先提交状态再发信号；回调不能等待本 Context 队列。服务逆序释放，外部注入依赖不归 Context 所有。导航提交后忽略调用方取消，普通 OpenAsync 的 Token 仍只控制打开阶段。

Dialogs 的 Token 覆盖整个等待期，调用方取消抛 OperationCanceledException；业务提交/用户取消/Context退出通过 DialogResult 区分。不要把业务结果塞进 UICompletion。

新增公共协议、所有权和组合例子见四个可选包 Documentation~/index.md。BusinessFlow 样例位于 Navigation 包，需要四扩展全部安装。没有新增 Addressables、R3、DOTween、虚拟列表或导航抢占。

Build 工厂失败时始终回滚所有已取得的资源并解除 Root 占用；单一错误保持原异常，多项原始/停止回调/Dispose 错误以 AggregateException 一并返回。
