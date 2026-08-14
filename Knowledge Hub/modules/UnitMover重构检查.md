---
tags: [Unity, Framework, ExpandComponent, UnitMover, 重构, 审计]
created: 2026-07-29
updated: 2026-08-04
status: 结构重构已实施，待 Unity 编译与场景验证
---

# UnitMover 重构检查

> [!important] 2026-08-04 容器审计补充
> 本检查文档中旧的 `OnActivated -> Simulate -> OnDeactivated -> OnDispose` 生命周期描述已失效。当前策略只通过 `OnRegisterLifecycle()` 向 `UnitMoverLifecycleContainer` 注册无参回调；`UnitMover` 只更新上下文、调用容器、管理缓存与策略切换。

## 生命周期容器审计

| 检查项 | 结果 | 说明 |
| --- | --- | --- |
| 策略注入入口 | 通过代码审计 | `UnitMovementStrategy` 仅保留首次 `OnInitialized()`、`OnRegisterLifecycle()` 和显式 `ClearState()`。|
| 策略切换 | 通过代码审计 | 旧策略 `Deactivated` 后清空容器；目标策略重新注入，并依次执行依赖刷新与激活。|
| 固定步热路径 | 通过代码审计 | `UnitMover` 只更新容器时间上下文并调用 `InvokeFixedUpdate()`，无组件查找、反射或 LINQ。|
| 编辑器 Authoring | 通过代码审计 | 同步、恢复、重捕获与 Gizmo 均由策略注入 Authoring / Gizmo 回调，Inspector 切策略前调用 `RestoreInitialStrategyAuthoring()`。|
| Unity 编译 | 待验证 | Unity CLI 在启动前遭遇 Unity Hub 日志目录 `EEXIST`，未进入项目重编译；需在已打开的 Unity 编辑器中确认 Console 零错误。|

## 当前结果

2026-08-04 已完成 `UnitMover` 层级职责重构。一切运动逻辑不再集中在 `UnitMovementRuntime`，该类、`UnitMovementProfile`和命令来源注册表已删除。

```text
UnitMover (MonoBehaviour)
  - 解析 Rigidbody / CapsuleCollider / IDataProvider / 参考相机
  - 创建 RigidbodyUnitBody / UnityPhysicsQuery
  - 策略缓存、切换、释放与 Gizmo 转发
                 |
                 v
UnitMovementStrategy (纯 C#)
  - 解释输入、组装模块、编排固定步、提交速度
                 |
                 v
功能模块 (纯 C#)
  - 独立配置、运行时状态与单一计算职责
```

## 职责审计

| 检查项 | 结果 | 说明 |
| --- | --- | --- |
| Unity 组件解析 | 通过 | `UnitMover` 仅在组装阶段解析同对象 `Rigidbody`、唯一 `CapsuleCollider` 和可选 `IDataProvider`。 |
| Blackboard 解释 | 通过 | `UnitMover` 不访问 `IDataProvider.Blackboard`；默认策略自行转为 `IUnitMovementInput`。 |
| 模块归属 | 通过 | `NormalGroundMovementStrategy` 持有 Settings 与可序列化模块，创建所需的运行时模块。 |
| 策略缓存 | 通过 | `UnitMover` 以具体 `Type` 缓存实例，切换不自动调用 `ClearState()`。 |
| 策略生命周期 | 通过 | 实现 `Initialize -> OnActivated -> Simulate -> OnDeactivated -> OnDispose`，并保留显式 `ClearState()`。 |
| 主体碰撞体 | 通过 | 仅支持唯一 `CapsuleCollider`，其它 Collider 模式已移除。 |
| 序列化迁移 | 待 Unity 验证 | `NormalGroundMovementStrategy` 以 `MovedFrom` 接收旧 `DefaultRigidbodyMovementStrategy`的已序列化类型。 |
| Inspector 扩展 | 通过代码审计 | `UnitMoverEditor` 递归枚举当前策略的一级序列化字段，将每个字段绘制为独立全宽模块。 |
| FixedUpdate 热路径 | 通过代码审计 | 只转发当前策略 `Simulate` ；无 `GetComponent`、反射、LINQ 或新建物理命中缓冲区。 |

## 待验证清单

1. 在 Unity 编辑器中触发导入和脚本重编译，确认 Console 为零错误。
2. 打开包含旧策略序列化数据的场景，确认该策略迁移为 `NormalGroundMovementStrategy`。
3. 验证 Inspector 能显示默认策略的全宽模块面板，并且 `BottomClearance` 滑条上限符合胶囊直径约束。
4. 验证普通移动、跳跃、浮动胶囊、台阶、斜坡、边缘保护、检查点与策略切换后的实例状态保留。

## 调试边界

- `UnitMover` 只在 `Awake` / `OnEnable` / 重新组装时解析组件和 Provider；不在 `FixedUpdate` 扫描组件。
- 模块不持有 `UnitMover` 引用，不自行注册 Unity 生命周期回调。
- 现有 `Preload.unity` 中保留旧类型名是有意的待迁移数据，不保留旧类型别名。
