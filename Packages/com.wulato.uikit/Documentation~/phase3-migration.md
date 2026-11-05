# 历史：0.3.0 迁移

当前升级请使用 [0.4 迁移](Migration-0.4.md)。以下保留历史记录。

# 0.3.0 迁移：可选 Addressables

主包和 Input System 适配为 0.3.0；Navigation/Presentation/Dialogs/Popup Queue 为 0.1.1，依赖核心 0.3.0，公共行为不变。Addressables 可选包首版 0.1.0。消费宿主将所选 Wulato 包锁定到相同 commit；不要依赖未发布的 registry 版本。

原 UIViewAsset.Direct/Resources 保留，枚举数值保持 0/1；追加 External=2 与 External(providerId,assetId)。旧序列化资产默认仍为 Direct，不需要迁移 View/Session 或生命周期。默认加载器不会解释 External，必须显式 WithLoader 注入对应适配器。

Marker 新增 externalProviderId/externalAssetId，均为空继续生成 Direct；填写时必须成对有效。重新执行生成、等待编译和写回；新字段进入指纹，旧生成产物预期失效。External 注册不直接持有 Prefab 引用，宿主负责把资源加入 Addressables catalog 并显式构建内容。

2022.3.62f2 宿主锁 Addressables 1.21.21 / SBP 1.21.25，6000.3.22f1 锁 2.9.1 / 2.6.1；UniTask 2.5.11 与 Input System 1.17.0 不变。未安装适配包的项目不新增任何后端依赖。
