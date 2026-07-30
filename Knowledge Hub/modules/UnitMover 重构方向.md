---
tags: [Unity, Framework, ExpandComponent, UnitMover, Rigidbody]
created: 2026-07-28
updated: 2026-07-30
status: 核心结构已实施，待 Unity 场景验证
---

# UnitMover 重构方向

## 当前结论

`UnitMover` 是 `Framework.ExpandComponent` 中的通用运动扩展组件。它组装 Unity 引用和纯 C# 运动管线，不承担 Player、Enemy、NPC 等业务实体的控制职责。

```text
DataProvider / AI / Root Motion / Network
                  |
                  v
          UnitMover (MonoBehaviour)
                  |
                  v
       UnitMovementRuntime (纯 C#)
                  |
                  v
 IUnitBody / IPhysicsQuery (Unity 适配边界)
                  |
                  v
      Rigidbody + CapsuleCollider + Physics
```

Provider 仅写入自己的 Blackboard。`UnitMover` 主动消费同对象 `IDataProvider` 暴露的 `IUnitMovementInput`，不反向驱动 Provider，也不依赖具体的 Player Blackboard 或 Input Attribute。

## 已实施的模块划分

```text
Assets/Scripts/C#/Framework/Expand Component/UnitMover/
  UnitMover.cs                       生命周期、组装和对外 API
  Core/UnitMovementRuntime.cs        固定步运动管线
  Profiles/UnitMovementProfile.cs    LocomotionSettings + GroundSettings
  Motor/JumpModule.cs                跳跃参数与瞬态状态
  Motor/GravityModule.cs             重力参数与瞬态状态
  Motor/HoverModule.cs               悬浮运动响应
  Physics/ColliderShapeModule.cs     有效碰撞形状与脚底 Box 同步
  Physics/FloatingCapsuleModule.cs   浮动配置与组件专属形状快照
  Physics/GroundProbeModule.cs       接地、支撑查询和内部陡坡下滑修正
  Physics/EdgeProtectionModule.cs    边缘约束与诊断快照
  Gizmos/UnitMoverGizmoRenderer.cs   只读 Scene 诊断绘制
  Strategies/                         纯 C# 移动策略
  UnityAdapters/                      Rigidbody 和 Physics 适配
```

`UnitMovementProfile` 只保留 `LocomotionSettings` 与 `GroundSettings`。`JumpModule`、`GravityModule` 和 `EdgeProtectionModule` 各自直接包含 Inspector 参数以及不参与序列化的运行时状态；它们在创建运行时时初始化，并在销毁运行时时调用 `ResetRuntimeState()`。

`UnitMover` 只支持一个同对象 `CapsuleCollider` 作为主碰撞体；`[RequireComponent]`、Inspector 字段、自动引用解析、运行时依赖校验和 Gizmo 全部使用该强类型契约。旧的 `BoxCollider` 主碰撞体兼容路径已移除。

`FloatingCapsuleModule` 聚合浮动胶囊参数和 `FloatingCapsuleAuthoringState`，并独占基础形状快照、顶部对齐、留空限幅和实际留空计算；它只返回纯形状数据，不写 Unity 组件。`ColliderShapeModule` 只负责将形状结果写入实际 `CapsuleCollider`、管理自动生成的脚底 `BoxCollider` 和提供 Bounds 查询，不再保存浮动胶囊建模算法或 AuthoringState 类型定义。脚底 Box 仅在启用浮动胶囊时由模块内部创建，用于精确台阶物理边界与接地探测，不是可配置的第二种 UnitMover 碰撞体模式。

`UnitMovementRuntime.Create(...)` 是运行时唯一的组装入口：它创建 `RigidbodyUnitBody`、`UnityPhysicsQuery` 和 `GroundProbeModule`，并复用 UnitMover 为编辑模式预览保留的 `ColliderShapeModule`。`UnitMover` 不再保留命令源或策略的单行代理，外部系统通过只读 `Runtime` 属性访问相应纯 C# API。

`UnitMovementRuntime` 是跨模块运行时的组装和协同中心，不是无逻辑的转发器：当跳跃、地面探测、边缘回退、速度提交等多个模块必须共享同一步的结果并决定下一阶段时，由 Runtime 保留该组合顺序与协同判断。相反，单模块可根据传入上下文独立决定的基础规则必须封装在模块内部，例如 `GroundProbeModule` 判断接触与可行走性、`HoverModule` 判断是否支撑回正、`SteepSlopeSlideModule` 判断是否施加下坡修正。模块之间不相互调用；Runtime 传入必要的只读上下文并接收结果。

DataProvider 的解析只在创建 Runtime 时执行一次，并且不把 Unity `OnEnable` 期间暂未激活的 Provider 误判为不兼容。固定步只消费缓存的 `IUnitMovementInput`；缓存 Provider 缺失或禁用时，UnitMover 只记录一次错误并阻断运动物理步，不会在热路径轮询组件。已绑定 Provider 重新启用后可直接恢复消费。

## 物理能力边界

`GroundProbeModule` 负责接地、坡面和支撑查询，并将自己的 `GroundContact` 转换为包含接地、稳定支撑与地面几何的 `UnitMovementState`。浮动胶囊接地优先使用脚底 `BoxCast` 覆盖真实占地；仅当该检测没有有效命中时，才从脚底辅助体顶部向下执行一条长度受同一 `GroundCheckDistance` 限制的中心射线兜底，并把结果换算为脚底底面距离。它供 `HoverModule` 与 `EdgeProtectionModule` 复用，但不直接调用它们；Runtime 将状态传给后续模块。稳定支撑以中心点和四个周向点确认，要求中心命中且至少两个周向点有可行走地面；单个边缘命中不能更新异常跌落回退位置。

`FloatingCapsuleModule` 只保存 Inspector 序列化的形状与配置数据；`HoverModule` 是浮动胶囊的运行时核心，独占有效接触、起跳豁免和支撑修正的判定。Runtime 每个固定步将本步接触和跨模块跳跃结果传入 `HoverModule.Apply(...)`，由 HoverModule 决定是否生效。`HoverModule` 根据任何有效脚底接触和 `BottomClearance + HoverHeight` 调节沿接触法线的速度：脚底过低向上修正，过高但仍在探测范围时向下回拉。陡坡同样保留这份悬浮支撑；阻尼按当前修正方向投影，避免越过目标后持续抵消错误方向的速度。

`GroundContact` 将“命中有效地面”与“允许作为站立地面”分开记录，并缓存坡度角。超过 `GroundSettings.SlopeLimit` 的斜面仍保留接触法线和接触点，会继续驱动浮动胶囊的支撑回正，但不会进入 Ground 模式或记录边缘回退安全位置。Runtime 只将“本步是否因接地且未起跳而使用地面法线”的组合结果传入 `GravityModule`；重力模块自行决定是否写入重力。`SteepSlopeSlideModule` 则自行根据 `GroundContact` 决定是否下滑，并将世界向下方向投影到斜面上；它计算 `inverseSlopeRatio = InverseLerp(SlopeLimit, 90, SlopeAngle)`，以 `inverseSlopeRatio * SteepSlopeSlideSpeed` 作为目标下坡速度，同时至少抵消当前全部上坡速度。因此哪怕只超过坡度限制一点，上坡分量也会被清零；当配置速度大于零时，实际下坡分量还会被直接补足到该目标，避免被静摩擦逐帧抵消。

旧的 `StepSettings`、`StepAssistModule` 和前方探针自动向上助推已删除。最大可通过台阶高度只由 `FloatingCapsuleModule.BottomClearance` 决定，脚底 `BoxCollider` 提供实际的精确物理边界。

`EdgeProtectionModule` 只在候选运动可能离开支撑面时执行额外检测：先做前缘三点支撑预测，失败后才扫描局部危险方向，并移除外向速度分量。它保存 `EdgeProtectionDebugState` 供 Gizmo 读取，不把调试绘制混入运动逻辑。

## 浮动胶囊规则

对于默认 Y 轴胶囊：

```text
基础：radius = 0.5, height = 2.0, center.y = 1.0
clearance = 0.4：radius = 0.5, height = 1.6, center.y = 1.2
```

公式为：

```text
effectiveHeight = baseHeight - clearance
effectiveCenter = baseCenter + capsuleAxis * (clearance * 0.5)
maximumClearance = baseHeight - baseRadius * 2
```

这保证胶囊顶部不移动，底部向上留出无碰撞空间；动态上限保证有效高度不小于直径。`[ExecuteAlways]` 仅在编辑模式同步形状并绘制 Gizmos，运动与 Rigidbody 写入只发生在播放模式。

## 策略与命令

`UnitMovementStrategy` 通过 `[SerializeReference]` 保存 Inspector 初始策略，运行时由 `unitMover.Runtime.UseMovementStrategy<TStrategy>()` 选择。Runtime 缓存 `Type -> UnitMovementStrategy` 实例，切换回已使用策略时保留必要的实例数据。

每个策略必须实现 `ClearState()`；业务层可通过 `unitMover.Runtime.ClearMovementStrategyState<TStrategy>()` 显式清空，Runtime 销毁时统一清理所有缓存策略。命令源 API 继续用于 AI、网络回放等纯 C# 生产者，但最基础的实体输入由 `UnitMover` 每个固定步直接从 Provider Blackboard 读取。

## Gizmo 规则

`UnitMover.OnDrawGizmosSelected()` 只调用 `UnitMoverGizmoRenderer.DrawAll(...)`。渲染器接收形状模块、浮动胶囊模块、接地设置和边缘诊断快照，不接收完整 Runtime，也不触发 Physics 查询或组件写入。

Scene 预览含义：黄色体积是底部无碰撞区，黄色线是期望支撑距离，绿色射线是可行走支撑，红色射线是无支撑危险点，红色箭头是危险外法线，青色箭头是约束后速度。

## 临时调试

`GroundProbeModule.cs` 内 `GroundProbeModule.LogGroundProbeDebug(...)` 和 `SteepSlopeSlideModule.LogSlopeDebug(...)` 是本次陡坡静止问题的临时 Editor 日志。前者在脚底 `BoxCast` 与中心射线后逐条输出原始命中表面的名称、Collider 类型、Collider 世界旋转、命中法线、坡度、距离、有效地面过滤结果和最终选中状态；后者输出最终有效接触、可行走状态、坡度角、坡度限制、当前速度、下坡方向和本帧下坡速度增量。确认问题原因后必须删除两个方法及全部调用；`UnitMover.cs` 中的依赖缺失 `Debug.LogError` 属于正式错误提示，不在本次清理范围。

## 验证记录

- `2026-07-29`：删除 `StepSettings` 与 `StepAssistModule`，台阶能力改由 `BottomClearance` 和自动脚底 `BoxCollider` 提供。
- `2026-07-29`：`UnitMovementProfile` 收口为 `LocomotionSettings`、`GroundSettings`；跳跃、重力、边缘防跌落迁入对应的状态型模块；浮动胶囊 Authoring 数据迁入 `FloatingCapsuleModule`。
- `2026-07-29`：Scene Gizmo 绘制从 `UnitMover` 抽离到 `UnitMoverGizmoRenderer`，绘制逻辑只读模块快照。
- `2026-07-30`：UnitMover 主碰撞体收紧为 `CapsuleCollider`；删除外部 `BoxCollider` 的引用解析、运行时校验与接地兼容，保留浮动胶囊内部脚底 Box 的专用探测。
- `2026-07-30`：Runtime 组装收口至 `UnitMovementRuntime.Create(...)`；移除 UnitMover 的命令源与策略透传 API，固定步不再扫描 DataProvider，稳定支撑改为中心加四向采样。
- `2026-07-30`：陡坡不再在接地探测阶段被直接丢弃；保留斜面接触并区分可站立性，超过坡度限制时按超限角度施加可配置的下坡加速度。
- `2026-07-30`：浮动胶囊的悬浮支撑扩展到不可站立斜面；陡坡修正先强制清除上坡速度，再按逆坡参数叠加下坡速度。
- `2026-07-30`：浮动胶囊是否参与本步支撑的接触、起跳和时间判断收回 `HoverModule`；Runtime 仅保留模块调度顺序，不再持有悬浮条件分支。
- `2026-07-30`：明确 Runtime 负责跨模块的组合顺序和协同决策，基础模块负责可由自身上下文独立完成的规则判断；禁止功能模块之间相互调用。
- `2026-07-30`：接地、稳定支撑和地面状态快照创建收回 `GroundProbeModule`；Runtime 只消费接地模块输出，并保留策略、跳跃和边缘模块之间的协同决策。
- `2026-07-30`：浮动胶囊的基础形状快照与顶部对齐计算迁入 `FloatingCapsuleModule`；`ColliderShapeModule` 收口为 Unity 碰撞体写入、脚底 Box 生命周期和边界查询。
- `2026-07-30`：浮动胶囊脚底 BoxCast 未命中时增加有界中心射线兜底，持续提供斜面接触法线；射线长度仍受既有接地距离限制，不引入远距离吸附。
- `2026-07-30`：陡坡下推从按物理步累积的加速度改为目标下坡速度，避免微小单步速度被坡面静摩擦清零；已有场景值通过 `FormerlySerializedAs` 保留。
- `2026-07-30`：为定位陡坡静止问题，在 `SteepSlopeSlideModule` 增加临时 Editor 日志；问题确认后必须清理。
- `2026-07-30`：临时接地日志前移至 `GroundProbeModule` 的脚底 BoxCast 与中心射线，输出原始坡面名称、坡度和过滤结果；问题确认后必须清理。
- 静态编译通过后，仍须在 Unity 中验证台阶、坡面、窄桥、短缝、异常回退、移动平台和浮动手感。Unity 正常重新导入项目后会生成新脚本 `.meta` 并重建 `.csproj`。
