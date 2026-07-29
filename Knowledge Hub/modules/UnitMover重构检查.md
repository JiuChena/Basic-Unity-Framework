---
tags: [Unity, Framework, ExpandComponent, UnitMover, 重构]
created: 2026-07-29
updated: 2026-07-29
status: 讨论结论已记录，未实施
---

# UnitMover 重构检查

## 问题发现

UnitMover 的模块组装职责存在设计不一致：

### 当前状态

- `UnitMover.cs` 负责 Unity 生命周期、序列化入口、数据提供、Tick 轮询
- `UnitMovementRuntime.cs` 负责定义运动执行顺序
- **但组装逻辑仍然写在 `UnitMover.cs` 内部**

```csharp
// UnitMover.cs:306 — 需要知道 ColliderShapeModule、RigidbodyUnitBody、
// GroundProbeModule、UnityPhysicsQuery 这些具体类型的存在
private void CreateRuntime()
{
    _shapeModule = new ColliderShapeModule(_movementCollider, gameObject, _floatingCapsuleModule);
    IUnitBody body = new RigidbodyUnitBody(_rigidbody);
    IPhysicsQuery physicsQuery = new UnityPhysicsQuery();
    GroundProbeModule groundProbe = new GroundProbeModule(
        _shapeModule, transform, physicsQuery, _profile.Ground);
    _runtime = new UnitMovementRuntime(
        body, _shapeModule, groundProbe, _profile,
        _jumpModule, _gravityModule, _edgeProtectionModule,
        _movementStrategy);
}
```

### 问题分析

- `UnitMover.cs` 当前 import 了 `RigidbodyUnitBody`、`UnityPhysicsQuery`、`ColliderShapeModule`、`GroundProbeModule` 这些**不属于数据配置**的具体类型
- 如果组装职责已经分给 Runtime，UnitMover 不应该知道适配器、形状模块、接地模块的存在
- 当前架构：组装写在自己内部 + 执行交给 Runtime → 两头都在管组装

### 建议方案

Runtime 提供静态工厂方法，UnitMover 只给数据和 Tick：

```csharp
// UnitMover.cs —— 不再知道适配器/形状模块/接地模块的存在
private void CreateRuntime()
{
    _runtime = UnitMovementRuntime.Create(
        _rigidbody,
        _movementCollider,
        gameObject,
        _profile,
        _jumpModule,
        _gravityModule,
        _edgeProtectionModule,
        _floatingCapsuleModule,
        _movementStrategy);
    ResolveMovementDataProvider();
}
```

### 收益

- UnitMover 不再直接依赖 `RigidbodyUnitBody`、`UnityPhysicsQuery`、`ColliderShapeModule`、`GroundProbeModule` 的具体 import
- UnitMover 只需知道自己引用了哪些**可序列化的数据模块**
- 所有中间适配器和探测模块的创建逻辑收口到 Runtime 内部

---

## 讨论结论

三层分工应为：

| 层级 | 类 | 职责 |
|------|-----|------|
| 数据声明 | `UnitMover.cs` | 有什么模块（序列化字段） |
| 组装 | `UnitMovementRuntime.Create()` | 怎么连起来（静态工厂收原始引用，new 所有适配器） |
| 执行 | `UnitMovementRuntime.Simulate()` | 先做什么后做什么（执行顺序） |

**注意：本文档仅记录检查结论，不实施代码修改。**

---

## 模块整合标准

### 原则

每个大功能单开文件夹层级。层级内部实现可以复杂（多脚本），但必须有唯一的整合脚本负责按运行流程组装内部模块，对外只暴露一个实例。Runtime 只需 `new` 整合脚本即可使用该大模块的全部功能。UnitMover 只需 `new` 字段 + Tick Runtime 传参。

### 按代码量逐模块评估

判断是否需要拆分的唯一标准是**实际实现复杂度和代码量**，不为了"看起来整齐"强行分割。

| 模块文件 | 行数 | 单脚本自包含？ | 需要整合入口？ |
|---------|------|:---:|:---:|
| `HoverModule.cs` | 57 | ✅ | 不，就这么点代码 |
| `GravityModule.cs` | 64 | ✅ | 不 |
| `JumpModule.cs` | 113 | ✅ | 不 |
| `UnitMovementStrategy.cs` | 160 | ✅ | 不，基类+默认策略放一起足够 |
| `GroundProbeModule.cs` | 265 | ✅ | 不，一个类搞定接地 |
| `EdgeProtectionModule.cs` | 513 | ✅ | 不，虽然长但自包含，且同样只需外部注入依赖 |
| `ColliderShapeModule.cs` | 412 | ⚠️ | 两块职责（胶囊同步 + 脚底Box管理），但还在可接受范围内 |

**结论：所有模块代码量都在合理范围，不需要拆分文件。真正的问题不在于文件组织，而在于职责放错了地方。**

---

## Runtime 当前组装方式

Runtime 构造函数接收的是**半成品**——3 个已由 UnitMover 预建的适配器 + 5 个纯数据模块，构造函数内部继续完成连线：

```csharp
// UnitMovementRuntime.cs:64 — 8个入参
public UnitMovementRuntime(
    IUnitBody body,                              // ← UnitMover 已 new
    ColliderShapeModule shapeModule,             // ← UnitMover 已 new
    GroundProbeModule groundProbe,               // ← UnitMover 已 new
    UnitMovementProfile profile,                 // 纯数据
    JumpModule jumpModule,                       // 纯数据
    GravityModule gravityModule,                 // 纯数据
    EdgeProtectionModule edgeProtectionModule,   // 纯数据
    UnitMovementStrategy initialMovementStrategy)// 纯数据
{
    // 构造函数内部做的事：
    // ① 拆 Profile → LocomotionSettings + GroundSettings
    // ② 内部创建 HoverModule（UnitMover 完全不知道它的存在）
    // ③ 跨模块注入 edgeProtection.Initialize(shapeModule, groundProbe)
    // ④ 运行时初始化 ResetRuntimeState / Initialize
    // ⑤ 策略注入 strategy.Initialize(locomotionSettings)
}
```

**组装实际切成了两半：**

| 阶段 | 谁干的 | 内容 |
|------|--------|------|
| 适配器创建 | `UnitMover.CreateRuntime()` | `new RigidbodyUnitBody` `new ColliderShapeModule` `new GroundProbeModule` |
| 内部连线 | `UnitMovementRuntime()` 构造函数 | 拆 Profile、创建 HoverModule、EdgeProtection 注入、策略注入、状态初始化 |

---

## 最终改造目标（修正版）

**只改一处：把适配器创建从 UnitMover 移到 Runtime，让 UnitMover 只传原始 Unity 引用 + 纯数据模块。不拆分任何文件。**

```
改造前：
  UnitMover.CreateRuntime()
    ├── new ColliderShapeModule          ← UnitMover 不该知道
    ├── new RigidbodyUnitBody            ← UnitMover 不该知道
    ├── new UnityPhysicsQuery            ← UnitMover 不该知道
    ├── new GroundProbeModule            ← 依赖 shape 和 physicsQuery
    └── new UnitMovementRuntime(8参数)   ← 3个半成品 + 5个数据

改造后：
  UnitMover.CreateRuntime()
    └── UnitMovementRuntime.Create(rigidbody, collider, gameObject, 6个纯数据)
          ├── 内部 new RigidbodyUnitBody(rigidbody)
          ├── 内部 new UnityPhysicsQuery()
          ├── 内部 new ColliderShapeModule(collider, gameObject, floatingCapsuleModule)
          ├── 内部 new GroundProbeModule(shape, transform, physicsQuery, groundSettings)
          ├── 内部 new HoverModule(groundSettings, groundProbe)
          ├── 内部 new EdgeProtectionModule(shapeModule, groundProbe)  ← 构造函数内部自包含
          └── 内部完成所有 Initialize + ResetRuntimeState
```

### 收益

- `UnitMover.cs` 删掉 4 个不再需要的 import：`RigidbodyUnitBody` `UnityPhysicsQuery` `ColliderShapeModule` `GroundProbeModule`
- `CreateRuntime()` 从 ~30 行缩减到 ~10 行
- UnitMover 只需知道"我有哪些可序列化数据字段" + "我把原始引用丢给 Runtime"
- `UnitMovementRuntime` 真正成为"唯一知道所有模块如何连接"的类**
