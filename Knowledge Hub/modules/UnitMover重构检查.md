---
tags: [Unity, Framework, ExpandComponent, UnitMover, 重构]
created: 2026-07-29
updated: 2026-07-29
status: 全部已实施
---

# UnitMover 重构检查

## 问题一：适配器创建在 UnitMover 内部 ✅ 已修复

**改前：** `UnitMover.CreateRuntime()` 直接 `new ColliderShapeModule` / `RigidbodyUnitBody` / `UnityPhysicsQuery` / `GroundProbeModule`

**改后：**
- Runtime 新增静态工厂 `UnitMovementRuntime.Create(rigidbody, collider, gameObject, ...)` ，内部 new 全部适配器和探测模块
- Runtime 构造器改为 `private`
- `UnitMover.CreateRuntime()` 仅调用工厂 + `ResolveMovementDataProvider()`

```csharp
// UnitMover.cs:226
private void CreateRuntime()
{
    _runtime = UnitMovementRuntime.Create(
        _rigidbody, _movementCollider, gameObject, _shapeModule,
        _profile, _jumpModule, _gravityModule,
        _edgeProtectionModule, _floatingCapsuleModule, _movementStrategy);
    ResolveMovementDataProvider();
}
```

---

## 问题二：公开纯转发方法 ✅ 已修复

**改前：** UnitMover 有 7 个纯转发方法，仅挡一层 `RequireRuntime()` 后调 `_runtime.同名()`

**改后：** 全部删除，改为暴露 Runtime 属性：

```csharp
// UnitMover.cs:71
public UnitMovementRuntime Runtime => _runtime;
```

外部直接 `unitMover.Runtime.UseMovementStrategy<T>()` / `unitMover.Runtime.RegisterCommandSource(...)` 等。

---

## 附带变更

| 改动 | 位置 |
|------|------|
| `_movementCollider` 类型收窄 `Collider` → `CapsuleCollider` | UnitMover.cs:19 |
| 新增 `[RequireComponent(typeof(CapsuleCollider))]` | UnitMover.cs:11 |
| DataProvider 在 Runtime 创建时缓存，首个 FixedUpdate 再确认启用状态 | UnitMover.cs:299 |
| 新增 `_reportedMissingDataProvider` 仅报错一次 | UnitMover.cs:62 |
| Provider 的 `OnEnable` 时序不影响绑定；未启用时只阻断固定步 | UnitMover.cs:348 |
| `DisposeRuntime()` null 安全检查 | UnitMover.cs:255 |
| 新增 `GroundProbeModule.HasStableSupport()` | GroundProbeModule.cs:214 |
| `Simulate()` 中使用 `isStableGrounded` 状态 | UnitMovementRuntime.cs:314 |
| UnitMover.cs 行数 | 文件 475 → 400 行 |
