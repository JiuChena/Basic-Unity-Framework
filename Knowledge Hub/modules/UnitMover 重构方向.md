---
tags: [Unity, Framework, ExpandComponent, UnitMover, Rigidbody]
created: 2026-07-28
updated: 2026-08-04
status: 策略层级重构已实施，待 Unity 场景验证
---

# UnitMover 重构方向

> [!important] 生命周期容器为当前实现
> 本文中出现的旧策略回调 `OnActivated`、`Simulate(float, float)`、`OnDeactivated`、`OnDispose`、`OnRuntimeDependenciesChanged` 与 `RestoreAuthoring(...)` 均为历史描述。当前实现以 `UnitMoverLifecycleContainer` 为准：`UnitMover` 只持有一份容器，并且只负责更新上下文、触发阶段和管理策略切换；策略通过 `OnRegisterLifecycle()` 注入自己的无参包装方法。

## 当前策略切换流程

```text
首次策略：Initialize(依赖注入) -> BindLifecycle -> Initialize -> DependenciesChanged -> Activated
缓存策略切换：旧策略 Deactivated -> 容器 Clear -> 目标 BindLifecycle -> DependenciesChanged -> Activated
新策略切换：旧策略 Deactivated -> 容器 Clear -> 创建并 Initialize(依赖注入)
           -> BindLifecycle -> Initialize -> DependenciesChanged -> Activated
完整释放：活动策略 Deactivated -> Disposed -> 容器 Clear；其余缓存策略临时绑定 Disposed 后释放
```

容器阶段包括 `Initialize`、`Activated`、`Update`、`FixedUpdate`、`LateUpdate`、`DependenciesChanged`、`Deactivated`、`Disposed`、`AuthoringValidate`、`AuthoringRestore`、`AuthoringRecapture` 与 `DrawGizmosSelected`。固定步只由 `UnitMover` 调用容器；模块不订阅 Unity 生命周期，也不持有 `UnitMover`。

## 当前结构

`UnitMover` 是 `Framework.ExpandComponent` 中的 Unity 外壳与策略管理器。它仅解析 Unity 组件和 `IDataProvider`，创建适配器，并维护移动策略的缓存与生命周期；它不保存具体运动模块，也不读取 Provider 黑板。

```text
UnitMover (MonoBehaviour)
  - Rigidbody / CapsuleCollider / IDataProvider 解析
  - RigidbodyUnitBody / UnityPhysicsQuery 创建
  - 策略缓存、切换、激活、释放
                 |
                 v
UnitMovementStrategy (纯 C#)
  - 自行解释 Blackboard 所需契约
  - 自行持有 Settings 与功能模块
  - 自行编排固定步并提交最终速度
                 |
                 v
功能模块 (纯 C#)
  - 单一功能、配置与运行时状态
```

当前默认实现为 `NormalGroundMovementStrategy`。该类型以 `[SerializeReference]` 保存于 `UnitMover`，直接持有 `LocomotionSettings`、`GroundSettings`、`JumpModule`、`GravityModule`、`FloatingCapsuleModule` 与 `EdgeProtectionModule`。没有 ScriptableObject 配置资产，也没有 `UnitMovementProfile`。

旧 `DefaultRigidbodyMovementStrategy` 使用 Unity `MovedFrom` 类型迁移到 `NormalGroundMovementStrategy`。旧 `UnitMovementRuntime`、`UnitMovementProfile`、`IUnitMovementCommandSource` 与 Runtime 命令来源注册表已删除。

## UnitMover 职责

`UnitMover` 只承担以下职责：

- 解析同对象 `Rigidbody`、唯一主 `CapsuleCollider`、可选 `IDataProvider` 和可选移动参考相机。
- 自动解析 Provider 时只接受唯一候选；同对象存在多个 Provider 且未手动指定时记录一次歧义错误，不按组件顺序选择。
- 创建一次性的 `RigidbodyUnitBody` 与 `UnityPhysicsQuery`，并将 `Rigidbody`、`CapsuleCollider`、`Transform`、适配器、Provider 和移动参考 `Transform` 显式传入策略。
- 维护 `Type -> UnitMovementStrategy` 缓存；切换时先执行旧策略 `OnDeactivated`，再首次 `Initialize` 目标策略或激活缓存策略，停用或销毁时统一调用 `OnDispose`。
- 提供 `UseMovementStrategy<TStrategy>()` 与 `UseMovementStrategy(Type)`；前者供业务代码以泛型切换，后者供 Inspector 或运行时类型选择器切换。另提供 `ClearMovementStrategyState<TStrategy>()`、`SetCheckpoint()`、`RestoreCheckpoint()` 与浮动基础形状重捕获入口。
- 将编辑模式形状同步和 Scene Gizmo 请求转交给当前策略。

它不解释 `IDataProvider.Blackboard`、不消费 `IUnitMovementInput`、不组装 Jump/Hover/Gravity/Edge/Slope 模块，也不决定模块调用顺序。固定步只调用当前策略的 `Simulate(Time.fixedDeltaTime, Time.time)`。

## 策略生命周期

```text
首次使用: 创建 -> Initialize -> 缓存 -> OnActivated
切换缓存策略: 当前 OnDeactivated -> 目标 RefreshRuntimeDependencies -> 目标 OnActivated
切换新策略: 当前 OnDeactivated -> 创建 -> Initialize -> 缓存 -> OnActivated
重复选择当前策略: 返回已有实例，不重复激活
显式重置: ClearMovementStrategyState<T> -> ClearState
组件停用: 当前 OnDeactivated -> 全部 OnDispose -> 清空缓存
```

策略切换不会调用 `ClearState()`，因此缓存实例可以保留自身状态。策略必须实现 `ClearState()`，并只在业务层显式要求或完整释放时清理运行时数据。策略若修改了共享 Unity 组件，必须在 `OnDeactivated()` 中归还；首次创建目标策略会在旧策略归还后才执行 `Initialize()`，不得捕获旧策略留下的组件状态。

## NormalGroundMovementStrategy

默认策略在 `Initialize(...)` 时：

1. 确保自身序列化模块完整。
2. 创建或复用 `ColliderShapeModule`，同步浮动胶囊和脚底辅助 `BoxCollider`。
3. 创建 `GroundProbeModule`、`HoverModule` 与 `SteepSlopeSlideModule`，初始化 Gravity 和 EdgeProtection。
4. 从 `IDataProvider.Blackboard` 缓存 `IUnitMovementInput`，并把可选移动参考传给 `IUnitMovementReferenceFrame`。

默认策略在 `OnActivated()` 立即重新同步浮动形状；在 `OnDeactivated()` 和 `OnDispose()` 调用 `ColliderShapeModule.RestoreAuthoringShape()`，将主 `CapsuleCollider` 恢复至自身 Authoring 基础快照，并只移除自身登记的脚底辅助 `BoxCollider`。

固定步顺序属于策略而不是 `UnitMover`：

```text
同步有效胶囊
-> 接地/坡面探测
-> Provider Blackboard 转标准移动命令
-> 陡坡上坡输入约束
-> Jump 解析
-> 地面或空中候选速度
-> EdgeProtection 速度约束
-> 跳跃、重力、陡坡下滑、Hover 速度贡献
-> IUnitBody.Commit
-> 保存只读状态与速度诊断
```

无 `IUnitMovementInput` 时默认策略使用中性命令继续执行物理步，允许重力、悬浮或外部策略逻辑正常工作。`UnitMover` 不会因为 Provider 黑板缺少该契约而阻断运行。

## 功能模块边界

- `FloatingCapsuleModule`：保存顶部对齐的浮动胶囊配置和基础形状快照，只生成纯形状数据。
- `ColliderShapeModule`：将有效形状写入主 `CapsuleCollider`、维护内部脚底 `BoxCollider`、提供 Bounds 与探测尺寸。
- `GroundProbeModule`：进行无分配接地、坡面和支撑查询，统一过滤层、Trigger 与自身碰撞体。
- `HoverModule`：对任意有效地面接触执行沿法线的浮动弹簧和阻尼修正，跳跃主动阶段豁免。
- `SteepSlopeSlideModule`：锁定不可行走坡面，约束上坡输入并按曲线、坡度差和上限叠加下滑速度。
- `EdgeProtectionModule`：对候选速度预测支撑、移除危险外向速度并保存 Gizmo 快照；检查点只能通过 `SetCheckpoint` / `RestoreCheckpoint` 显式使用。
- `JumpModule`、`GravityModule`：分别维护跳跃事件与重力状态，不持有或查询 Unity 组件。

模块可持有稳定的单向下层依赖，例如 `HoverModule -> GroundProbeModule`；不得持有 `UnitMover` 或策略引用，不得在固定步执行 `GetComponent`、`Find`、`Camera.main` 或场景查询。

## 浮动胶囊规则

对于默认 Y 轴胶囊：

```text
基础: radius = 0.5, height = 2.0, center.y = 1.0
clearance = 0.4: radius = 0.5, height = 1.6, center.y = 1.2
```

```text
effectiveHeight = baseHeight - clearance
effectiveCenter = baseCenter + capsuleAxis * (clearance * 0.5)
maximumClearance = baseHeight - baseRadius * 2
```

顶部保持不动，只有底部上移形成无碰撞空间。`BottomClearance` 同时是精确最大台阶高度；Inspector 以基础胶囊高度动态限制其最大值。`[ExecuteAlways]` 仅在编辑模式同步形状和绘制 Gizmo，不写刚体速度或执行物理步。

## Inspector 与 Gizmo

`UnitMoverEditor` 保留全宽深色折叠栏风格：

- Unity 引用、策略选择、运行时只读诊断和 Gizmo 开关由 Editor 专门绘制。
- 当前策略的一级序列化字段自动枚举，每个字段独立显示在模块折叠栏中；新策略或新模块字段不需要新增专用 Editor 代码。
- 浮动胶囊的 `BottomClearance` 是唯一专用字段控件，用动态合法上限绘制滑条。

`UnitMoverGizmoRenderer` 仅读取策略暴露的 `ColliderShapeModule`、`FloatingCapsuleModule`、`GroundSettings` 和边缘调试快照，不触发组件写入或 Physics 查询。

## 验证状态

- `2026-08-04`：策略层级重构已实施，旧 Runtime/Profile/命令来源代码已删除，默认策略改为 `NormalGroundMovementStrategy`，Inspector 改为策略字段递归显示。
- `2026-08-04`：播放模式下 Inspector 的“运行时移动策略”会调用 `UseMovementStrategy(Type)` 真实切换活动策略，不再仅替换初始策略的序列化配置；需在 Unity 编辑器中确认类型迁移、脚本零编译错误，以及台阶、跳跃、斜坡、边缘保护、检查点和运行时策略切换行为。
