# Presentation

主包0.4.0内置模块，保持独立程序集，依赖 UniTask 2.5.11。显式 `builder.UsePresentation(factory)`；不匹配的 View 可由 IPresenterFactory.Create 返回 null。重复注册拒绝。

Presenter 是普通 C# 对象，每 Session 创建一次。Create/Bind 位于 Awake 后、OnOpenAsync 前；Bind 为同步工作。异步数据准备留给 OnOpenAsync 或业务服务，不能在 Bind 内等待同一 Context 命令。

返回绑定后由框架在终结/回滚时幂等 Dispose。Bind 失败由观察器释放已创建 Presenter；Create 抛错前的部分资源由工厂清理。Dispose 抛错保留在关闭结果，不阻断其他回收。

外部传入 Model 默认由调用方拥有，Presenter 不擅自 Dispose；自建 Model 由 Presenter 释放。Instance/Session/Foreground 三种寿命分开管理订阅。缓存重开新建 Presenter，不复用旧会话绑定。工厂自身由调用方拥有。

不强制 MVC/MVP/MVVM，不引入 DI 容器或 ServiceLocator。
