---
tags: [Unity, Framework, ExpandComponent, UnitMover, 决策, 重构]
created: 2026-08-03
updated: 2026-08-04
status: 已实施，待 Unity 验证
---

# UnitMover 层级职责重构决策

> [!important] 已确认的生命周期决策
> 生命周期调用不再以策略虚方法散落在 `UnitMover` 中。`UnitMover` 唯一持有 `UnitMoverLifecycleContainer`，策略在 `OnRegisterLifecycle()` 中按自身模块编排顺序注册无参回调；`UnitMover` 不读取黑板、不识别悬浮胶囊、接地、跳跃、边缘保护或斜坡模块。

## 容器边界

- `UnitMover`：解析 Unity 引用和 `IDataProvider`、创建 `IUnitBody` / `IPhysicsQuery`、维护策略缓存、清空容器并触发阶段。
- `UnitMovementStrategy`：持有配置和纯 C# 模块、解释自身 Blackboard 契约、把模块流程包装为 `void` 无参回调并注册到容器。
- 模块：不得持有 `UnitMover`、不得调用 `GetComponent` 或自行订阅 Unity 生命周期；只能接收稳定依赖和策略传入的数据。

该容器是单个 `UnitMover` 实体内部的调用表，不是全局事件中心，也不向其他实体广播。策略切换保留缓存实例状态，但必须在 `Deactivated` 回调归还自己修改过的共享 Unity 组件；需要清空实例状态时由业务层显式调用 `ClearMovementStrategyState<TStrategy>()`。

## 背景

本次重构将 `UnitMover` 收紧为 Unity 组件承载器与策略管理器。移动策略决定需要哪些模块、如何组合它们以及最终如何驱动刚体；功能模块仅完成各自的纯 C# 计算。

本决策不使用 ScriptableObject 配置。策略实例通过 `[SerializeReference]` 序列化，策略直接持有各模块和 Settings 的 `[SerializeField]` 字段，由现有自定义 Editor 递归显示。配置与运行时状态继续留在各自模块实例中，避免产生额外资产、引用空缺和运行时策略切换时的配置来源问题。

## 一、最终层级

```text
UnitMover（MonoBehaviour）
  - Unity 生命周期、Unity 组件解析、适配器创建
  - 策略缓存、切换与释放
  - 向策略显式传入已获取的具体依赖
                │
                ▼
UnitMovementStrategy（纯 C# 抽象基类）
  - 输入解释、模块持有、模块编排、速度提交
  - 普通默认实现：NormalGroundMovementStrategy
                │
                ▼
功能模块（纯 C# 具体类）
  - 独立配置、运行时状态与单一功能计算
```

`UnitMover` 不把自身、根 `GameObject` 或万能 Context 交给策略。策略只接收已经解析好的具体组件、适配器和数据引用；策略与模块均不得反向访问 `UnitMover`。

## 二、UnitMover 职责

### 承担

- 持有并解析同对象的 `Rigidbody`、唯一主 `CapsuleCollider`、`IDataProvider` 与可选移动参考相机。
- 创建并持有 `RigidbodyUnitBody`、`UnityPhysicsQuery` 等 Unity 适配器。
- 保留 Inspector 初始策略：`[SerializeReference] UnitMovementStrategy _movementStrategy`。
- 维护 `Type -> UnitMovementStrategy` 的运行时缓存、当前激活策略和策略生命周期。
- 在 `Awake` / `OnEnable` 获取依赖并初始化初始策略；在 `FixedUpdate` 仅调用当前策略的固定步；在 `OnDisable` / `OnDestroy` 释放策略与刚体接管状态。
- 将编辑器形状同步和 Gizmo 绘制请求转交给当前初始策略或当前激活策略。
- 暴露 `UseMovementStrategy<TStrategy>()`、`ClearMovementStrategyState<TStrategy>()`、状态诊断和检查点等对外 API。

### 不承担

- 不持有 Jump、Gravity、Hover、GroundProbe、EdgeProtection、FloatingCapsule、SteepSlope 等功能模块字段。
- 不读取或转换 Blackboard，不构建默认移动命令，不消费跳跃事件。
- 不决定模块调用顺序、接地/空中行为、限速空间或运动贡献规则。
- 不保留 `UnitMovementRuntime`、`UnitMovementProfile` 或命令来源注册表。

### 显式初始化参数

策略初始化时由 `UnitMover` 逐项传递需要的依赖。基础策略接口采用显式参数，而不是重新包装成万能 Context：

```csharp
strategy.Initialize(
    rigidbody,
    movementCollider,
    transform,
    unitBody,
    physicsQuery,
    dataProvider,
    movementReference);
```

- `Rigidbody`：允许受信任策略实现飞行、游泳、击飞等特殊行为。
- `CapsuleCollider` 与 `Transform`：用于碰撞形状、物理探测和辅助碰撞体管理。
- `IUnitBody`：提供统一的速度提交、检查点恢复和刚体设置恢复边界。
- `IPhysicsQuery`：提供预分配的物理查询适配。
- `IDataProvider`：仅作为数据源引用，由策略自行解释其 Blackboard。
- `Transform movementReference`：提供相机平面输入转换所需的可选参考。

## 三、策略职责与生命周期

### UnitMovementStrategy

`UnitMovementStrategy` 是唯一可替换的移动编排点。它是受信任的框架扩展代码，拥有实现自身移动行为所需的已解析组件与适配器；框架不为策略建立权限沙箱。

策略必须提供以下生命周期：

```text
Initialize(...)  首次加入 UnitMover 缓存时执行一次，缓存依赖并创建运行时模块。
RestoreAuthoring(...) Inspector 替换初始策略前执行，归还该策略的编辑期共享 Unity 组件状态；默认空实现。
OnActivated()    每次真正切换到该策略时执行，建立当前策略需要的活动状态。
Simulate(...)    当前激活时每个 FixedUpdate 执行一次。
OnDeactivated()  切换离开时执行，恢复本策略修改过的共享状态。
ClearState()     业务显式要求时清空本策略的所有运行时状态。
OnDispose()      UnitMover 禁用或销毁时执行最终解绑与清理。
```

策略切换规则：

```text
首次使用：创建实例 -> Initialize -> 缓存 -> OnActivated
切换缓存策略：旧策略 OnDeactivated -> 目标策略 RefreshRuntimeDependencies -> 目标策略 OnActivated
切换新策略：旧策略 OnDeactivated -> 创建实例 -> Initialize -> 缓存 -> 目标策略 OnActivated
重复选择当前策略：直接返回缓存实例，不重复激活
显式重置：ClearMovementStrategyState<T>() -> 策略 ClearState
组件释放：当前策略 OnDeactivated -> 所有策略 OnDispose -> 清空缓存
```

缓存策略在一次 UnitMover 启用周期内保留实例状态。切换不会自动调用 `ClearState()`；否则缓存复用没有意义。每个策略仍必须实现 `ClearState()`，由业务层或完整释放时明确调用。任何修改共享 Unity 组件的策略，必须在 `OnDeactivated()` 中归还；这样首次初始化的新策略不会把旧策略的临时形状或附加组件当作基础状态。

### 速度提交

策略在 `Simulate` 内从当前刚体速度构造本帧累计结果，调用自己的模块计算普通贡献、方向约束和最终限速，然后通过 `IUnitBody.Commit(finalVelocity)` 完成一次最终提交。

- 普通贡献采用增量语义：移动加速度、重力、跳跃补速、悬浮修正和陡坡下滑均累加到当前速度结果。
- 约束模块可以移除累计结果中的危险方向分量。
- 策略决定最终 Clamp 的空间：地面策略通常限制支撑面切向速度，飞行策略可以限制完整三维速度。
- 策略直接调用 `Rigidbody` API 属于显式绕过累计器的高级行为；该策略必须自行保证调用顺序与最终结果。

## 四、功能模块与配置

### 模块归属

模块直接由策略持有。默认地面策略至少包含：

```text
NormalGroundMovementStrategy
  - LocomotionSettings
  - GroundSettings
  - JumpModule
  - GravityModule
  - FloatingCapsuleModule
  - EdgeProtectionModule
  - 运行时创建：ColliderShapeModule、GroundProbeModule、HoverModule、SteepSlopeSlideModule
```

`LocomotionSettings`、`GroundSettings` 与各模块的 Inspector 参数全部作为策略内部的 `[SerializeField]` 递归显示。模块内部的 `[NonSerialized]` 字段只保存计时器、命中缓存、依赖引用、检查点、刚体基准重力等运行时数据。

### 模块依赖

允许模块通过构造函数或一次性 `Initialize` 持有稳定、单向、只读的下层依赖。例如：

- `GroundProbeModule` 持有 `ColliderShapeModule`、`IPhysicsQuery`、宿主 `Transform` 与 `GroundSettings`。
- `HoverModule` 持有 `GroundProbeModule` 与 `GroundSettings`。
- `EdgeProtectionModule` 持有 `ColliderShapeModule` 与 `GroundProbeModule`。

禁止事项：

- 模块不得持有 `UnitMover` 或策略引用。
- 模块不得形成循环依赖。
- 模块不得在固定步自行执行 `GetComponent`、`Find`、`Camera.main` 或其他场景查找。
- 模块不得自行注册 Unity 生命周期回调。

涉及多个大类模块协同的复杂顺序由策略决定；单模块内部的基础判断保留在模块本身。

## 五、默认策略：NormalGroundMovementStrategy

`NormalGroundMovementStrategy` 取代 `DefaultRigidbodyMovementStrategy`，作为 Inspector 新建 UnitMover 的默认策略。它提供普通角色在刚体、胶囊碰撞体和 DataProvider 输入下的标准地面移动能力，不包含 Player、Enemy、NPC 等业务逻辑。

固定步默认流程由该策略自身定义：

```text
同步有效胶囊形状
-> 接地与坡面探测
-> 从 IDataProvider.Blackboard 获取 IUnitMovementInput
-> 计算地面或空中的水平移动
-> Jump / Hover / Gravity 贡献
-> SteepSlope / EdgeProtection 约束
-> 按默认规则限速
-> IUnitBody.Commit
-> 保存只读诊断状态
```

这只是默认地面策略的行为，不是 `UnitMover` 对其他策略施加的模板。飞行、游泳、遁地、Root Motion 或 AI 策略可以只使用其中一部分模块，或实现完全不同的组合顺序。

## 六、DataProvider 归属

- `UnitMover` 只解析同对象 `IDataProvider` 引用；显式 Inspector 引用优先。
- `UnitMover` 不读取 `IDataProvider.Blackboard`，不判断 Blackboard 是否实现 `IUnitMovementInput`。
- 策略在 `Initialize` 时从 Provider 的 Blackboard 获取并缓存自己需要的契约。
- `NormalGroundMovementStrategy` 缓存 `IUnitMovementInput` 与可选 `IUnitMovementReferenceFrame`，在输入不可用时执行中性命令。
- 特殊策略可缓存飞行、游泳、攀爬等专用契约，框架不预先翻译业务层输入。
- 未显式指定且同对象存在多个 Provider 时，`UnitMover` 记录一次歧义错误并拒绝按组件顺序随机选择。

## 七、Inspector 与编辑模式

`UnitMoverEditor` 保留当前深色全宽折叠栏、相关组件引用和运行时诊断风格。

- 策略类型选择器继续使用 `TypeCache` 与 `[SerializeReference]`。
- 策略的一级可序列化字段由 Editor 自动枚举；每个字段在当前模块面板风格中递归调用 `PropertyField(..., true)`。
- 新策略或策略新增模块字段后，无需为该字段再写专用 Editor；满足 Unity 可序列化规则和 Tooltip 即可显示。
- 仅保留真正需要专用交互的功能：策略类型选择、浮动胶囊底部留空动态上限、只读运行时诊断和 Gizmo 开关。
- `OnValidate` 与 `OnDrawGizmosSelected` 只转交给策略的 Authoring/Gizmo 方法；编辑模式不写刚体速度、不运行物理步。

## 八、旧结构清理

以下旧结构在本次重构中直接删除，不保留兼容 API：

- `UnitMovementRuntime`
- `UnitMovementProfile`
- `DefaultRigidbodyMovementStrategy`
- `IUnitMovementCommandSource` 与 Runtime 命令来源注册表
- `UnitMover.Runtime`、`SubmitCommand` 等仅服务于 Runtime 的对外链路
- UnitMover 上直接保存的 Jump、Gravity、EdgeProtection、FloatingCapsule、Profile 配置字段

现有场景和 Prefab 中序列化的 `DefaultRigidbodyMovementStrategy` 通过 Unity 类型迁移标记迁移为 `NormalGroundMovementStrategy`，不保留旧类型别名。

## 九、性能与验收

- `FixedUpdate` 不执行组件查找、反射、LINQ、数组扩容或新的物理命中缓冲区分配。
- 物理模块继续复用预分配 `RaycastHit[]`。
- 策略缓存只在首次使用某类型时分配；切换回缓存策略不创建实例。
- `UnitMover` 固定步只转发给当前策略，不承担逐模块分支。
- 编译必须零错误；重构后检查所有旧 Runtime/Profile/DefaultStrategy 引用均已清理。
- Inspector 验收：初始策略显示为 `NormalGroundMovementStrategy`，其模块字段在现有折叠栏风格下完整显示，无 SO 资产引用。
- 行为验收：默认策略仍支持移动、跳跃、浮动胶囊、台阶边界、接地、陡坡约束、边缘保护、检查点恢复、摄像机参考和运行时策略缓存切换。
