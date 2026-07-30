---
tags: [Unity, Framework, ExpandComponent, UnitMover, 重构, FloatingCapsule]
created: 2026-07-29
updated: 2026-07-30
status: 核心职责拆分已实施，待 Unity 场景验证
---

# FloatingCapsuleModule 职责分析

## 问题

`FloatingCapsuleAuthoringState`（浮动胶囊的形状快照捕获、恢复、同步计算）当前定义在 `ColliderShapeModule.cs` 内部，而非 `FloatingCapsuleModule.cs`。两个类的职责分配倒置：

| 类 | 当前行数 | 当前实际内容 |
|------|------|------|
| `FloatingCapsuleModule.cs` | 51 | 纯数据容器，4个 `[SerializeField]` 参数 + 快照引用，零算法 |
| `ColliderShapeModule.cs` | 410 | 含 `FloatingCapsuleAuthoringState`（形状捕获/恢复/同步计算）+ 脚底 BoxCollider 管理 + 边界查询 |

核心算法全部在 `ColliderShapeModule.cs` 内部：

```csharp
// FloatingCapsuleAuthoringState 定义于 ColliderShapeModule.cs:13-116

CaptureBaseShape(capsule)   // 记录基础 center/height/radius/direction 快照
RestoreBaseShape(capsule)   // 从快照恢复到 CapsuleCollider
Synchronize(capsule, enabled, clearance)
  ├── 浮动启用:
  │     effectiveCenter = baseCenter + axis * (clearance * 0.5)   ← 顶部对齐
  │     effectiveHeight = baseHeight - clearance (≥ diameter)      ← 底部削除
  │     写回 capsule.center/height/radius
  └── 浮动关闭:
        恢复基础形状或接收作者最新编辑
```

## 实施结论

已按以下边界完成职责拆分：`FloatingCapsuleModule` 负责数学规则，`ColliderShapeModule` 负责 Unity 组件操作。

```
FloatingCapsuleModule.cs  ← 包含 FloatingCapsuleAuthoringState
  ├── [SerializeField] _enabled / _bottomClearance / _footBoxHeight / _footBoxSupportWidthScale
  ├── [SerializeField] _authoringState : FloatingCapsuleAuthoringState
  ├── CaptureBaseShape(capsule) → 快照
  ├── GetEffectiveShape(capsule) → 返回基础或浮动后的纯形状计算结果
  └── GetFloatingBottomClearance(capsule) → 计算实际世界留空高度

ColliderShapeModule.cs  ← 拆掉 FloatingCapsuleAuthoringState 的定义
  ├── Synchronize() → 调 FloatingCapsuleModule.GetEffectiveShape()，结果写入 CapsuleCollider
  ├── EnsureFootCollider / DestroyFootCollider  → 操作 Unity 组件
  └── GetHorizontalExtent / GetFootSupportRadius / GetFootSupportProbeHalfExtents → 边界查询
```

## 职责对照

| 方法 | 改前归属 | 改后归属 | 原因 |
|------|---------|---------|------|
| `CaptureBaseShape` | `ColliderShapeModule.cs` | `FloatingCapsuleModule.cs` | 形状建模规则 |
| 基础形状恢复 | `ColliderShapeModule.cs` | `FloatingCapsuleModule.cs` | 返回基础形状数据，不直接写组件 |
| `GetEffectiveShape(胶囊, 启停, 留空)` | `ColliderShapeModule.cs` | `FloatingCapsuleModule.cs` | 只算数，不操作组件 |
| 写入 `capsule.center/height/radius` | `ColliderShapeModule.cs` | `ColliderShapeModule.cs` | 操作 Unity CapsuleCollider |
| 脚底 Box 创建/更新/销毁 | `ColliderShapeModule.cs` | `ColliderShapeModule.cs` | 操作 Unity BoxCollider |
| `GetHorizontalExtent` 等查询 | `ColliderShapeModule.cs` | `ColliderShapeModule.cs` | 基于 Unity Bounds 的查询 |

`FloatingCapsuleAuthoringState` 随 `FloatingCapsuleModule` 保持序列化，因此既有 Prefab 和场景中的快照数据字段不改名、不丢失。脚底 `BoxCollider` 的引用仅用于跨编辑器重载恢复形状模块缓存；它的创建、更新和销毁仍完全由 `ColliderShapeModule` 执行。

## 验证记录

- `2026-07-30`：移动 `FloatingCapsuleAuthoringState` 的类型定义与全部形状建模算法至 `FloatingCapsuleModule.cs`；`ColliderShapeModule` 仅保留 CapsuleCollider 写入、脚底 BoxCollider 生命周期和 Bounds 查询。
- 静态编译与 Unity 场景验证待执行。
