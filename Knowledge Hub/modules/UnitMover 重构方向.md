---
tags: [Unity, Framework, ExpandComponent, UnitMover, Rigidbody]
created: 2026-07-28
updated: 2026-07-29
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
      Rigidbody + Collider + Physics
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
  Physics/GroundProbeModule.cs       接地与支撑查询
  Physics/EdgeProtectionModule.cs    边缘约束与诊断快照
  Gizmos/UnitMoverGizmoRenderer.cs   只读 Scene 诊断绘制
  Strategies/                         纯 C# 移动策略
  UnityAdapters/                      Rigidbody 和 Physics 适配
```

`UnitMovementProfile` 只保留 `LocomotionSettings` 与 `GroundSettings`。`JumpModule`、`GravityModule` 和 `EdgeProtectionModule` 各自直接包含 Inspector 参数以及不参与序列化的运行时状态；它们在创建运行时时初始化，并在销毁运行时时调用 `ResetRuntimeState()`。

`FloatingCapsuleModule` 聚合浮动胶囊参数和 `FloatingCapsuleAuthoringState`。`ColliderShapeModule` 只负责根据这份数据同步实际 `CapsuleCollider` 与自动生成的脚底 `BoxCollider`，不再把 Authoring 状态放回 `UnitMover` 或 Profile。

## 物理能力边界

`GroundProbeModule` 负责接地、坡面和支撑查询，供 `HoverModule` 与 `EdgeProtectionModule` 复用。它们不能合并：检测结果需要独立消费，合并后会让悬浮响应和边缘保护相互耦合。

`HoverModule` 根据有效脚底和 `BottomClearance + HoverHeight` 调节上下速度：脚底过低向上修正，过高但仍在探测范围时向下回拉。阻尼按当前修正方向投影，避免越过目标后持续抵消错误方向的速度。

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

`UnitMovementStrategy` 通过 `[SerializeReference]` 保存 Inspector 初始策略，运行时由 `UseMovementStrategy<TStrategy>()` 选择。Runtime 缓存 `Type -> UnitMovementStrategy` 实例，切换回已使用策略时保留必要的实例数据。

每个策略必须实现 `ClearState()`；业务层可通过 `ClearMovementStrategyState<TStrategy>()` 显式清空，Runtime 销毁时统一清理所有缓存策略。命令源 API 继续用于 AI、网络回放等纯 C# 生产者，但最基础的实体输入由 `UnitMover` 每个固定步直接从 Provider Blackboard 读取。

## Gizmo 规则

`UnitMover.OnDrawGizmosSelected()` 只调用 `UnitMoverGizmoRenderer.DrawAll(...)`。渲染器接收形状模块、浮动胶囊模块、接地设置和边缘诊断快照，不接收完整 Runtime，也不触发 Physics 查询或组件写入。

Scene 预览含义：黄色体积是底部无碰撞区，黄色线是期望支撑距离，绿色射线是可行走支撑，红色射线是无支撑危险点，红色箭头是危险外法线，青色箭头是约束后速度。

## 验证记录

- `2026-07-29`：删除 `StepSettings` 与 `StepAssistModule`，台阶能力改由 `BottomClearance` 和自动脚底 `BoxCollider` 提供。
- `2026-07-29`：`UnitMovementProfile` 收口为 `LocomotionSettings`、`GroundSettings`；跳跃、重力、边缘防跌落迁入对应的状态型模块；浮动胶囊 Authoring 数据迁入 `FloatingCapsuleModule`。
- `2026-07-29`：Scene Gizmo 绘制从 `UnitMover` 抽离到 `UnitMoverGizmoRenderer`，绘制逻辑只读模块快照。
- 静态编译通过后，仍须在 Unity 中验证台阶、坡面、窄桥、短缝、异常回退、移动平台和浮动手感。Unity 正常重新导入项目后会生成新脚本 `.meta` 并重建 `.csproj`。
