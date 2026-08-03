---
tags: [Unity, Framework, ExpandComponent, UnitMover, 决策, 重构]
created: 2026-08-03
updated: 2026-08-03
status: 讨论定稿，代码未改
---

# UnitMover 层级职责重构决策

## 背景

对 UnitMover 进行架构重构，目标是：UnitMover 只承担基本属性与生命周期承载；策略决定用什么模块、怎么用；模块自己管理自己的功能与计算。本文档记录已定稿的层级职责划分，作为后续实施的依据。

## 一、总体架构：三层层级，不可跨级交流

```text
UnitMover（密封承载器，实现 IUnitMovementContext）
   │  只与策略交流，OnInitialize 时把自己作为 context 传入
   ▼
UnitMovementStrategy（策略抽象基类，唯一多态点）
   │  持有模块（new 在策略内）、调用模块、传入模块实例
   ▼
功能模块（具体类，无接口，不持有其他模块引用）
```

交流规则：UnitMover 与策略交流；策略与功能模块交流。UnitMover 不直接接触功能模块，功能模块也不直接接触 UnitMover，不可跨级交流。

## 二、UnitMover（密封承载器，实现 IUnitMovementContext）

### 职责

- 持有 Unity 基本组件引用（Rigidbody、CapsuleCollider、Camera、DataProvider）
- 持有策略实例（`[SerializeReference]` 多态序列化）
- 生命周期承载：Awake / OnEnable / OnDisable / OnDestroy / OnValidate / Reset / FixedUpdate / OnDrawGizmosSelected
- 生命周期事件注册与调度（RegisterHandler / Clear / Commit / Invoke）
- 实现 `IUnitMovementContext`（Body / Physics / Profile / Input），OnInitialize 时把自己传给策略
- 暴露访问入口（诊断属性、检查点、形状重捕获）
- 把 DataProvider 引用传递给策略（不解释黑板内容）

### 字段

- Unity 引用：`_rigidbody`、`_movementCollider`、`_dataProvider`、`_movementReferenceCamera`
- 配置：`_profile`、`_freezeRigidbodyRotation`、`_showScenePreview`
- 策略：`_movementStrategy`（`[SerializeReference]`）
- 运行时：`_shapeModule`、`_movementDataProvider`、`_movementInput`、`_jumpPressedVersion`、`_reportedMissingDependencies`

### 不承担

- 不持有功能模块字段（Jump/Gravity/Hover/EdgeProtection/SteepSlope 等全部由策略持有）
- 不读取 DataProvider 黑板内容、不生成移动命令
- 不定义模块调用顺序
- 不实现任何运动逻辑
- 不再有 Runtime 类（模块容器职责删除，模块由策略管理）

### IUnitMovementContext 接口

```csharp
public interface IUnitMovementContext
{
    IUnitBody Body { get; }
    IPhysicsQuery Physics { get; }
    UnitMovementProfile Profile { get; }
    Blackboard Input { get; }   // DataProvider 黑板
    // 不持有模块（模块在策略里）
}
```

UnitMover 实现该接口，策略 `OnInitialize(context)` 时接收的就是 UnitMover 自身。策略从 context 取启动数据（刚体边界、物理查询、配置、输入），内部自建模块、自管理。

## 三、UnitMovementStrategy（策略抽象基类，唯一多态点）

### 职责

- 决定用什么模块（模块实例 `new` 在策略内，策略持有并序列化）
- 决定怎么用模块（调用顺序、参数组装、模块实例传入）
- 生命周期注册（抽象方法，内部注册自己持有的全部处理器）
- 解释输入（通用或特殊，从 DataProvider 黑板读取）
- 累加运动贡献、约束方向、限速
- **Commit 提交速度（抽象方法，强制每个策略显式提交）**

### 持有内容

- 自己的模块字段（`[SerializeField]` 具体类，如 `JumpModule`、`GravityModule`、`HoverModule`、`EdgeProtectionModule`、`SteepSlopeSlideModule`）
- 缓存的上下文（`IUnitMovementContext` 引用）
- 缓存的输入契约（`IUnitMovementInput` 或特殊输入接口）

### 抽象方法

```csharp
public abstract class UnitMovementStrategy
{
    public abstract void OnInitialize(IUnitMovementContext context);
    public abstract void RegisterLifecycle(IUnitMoverLifecycleRegistrar registrar);
    public abstract void Commit(IUnitBody body);   // 强制策略显式提交速度
    // 生命周期处理器由策略在 RegisterLifecycle 内注册，策略内部私有方法承载
}
```

### 模块间访问方式：方法参数传实例

模块方法需要其他模块的能力时，直接声明在方法参数里；由策略在调用点组装并传入实例。模块自身不持有其他模块引用。

```csharp
// 示例：策略的处理器内部
_edge.ConstrainVelocity(state, candidate, current, dt,
    _groundProbe, _shapeModule, out constrainedCandidate, out constrainedCurrent);
```

## 四、功能模块（具体类，无接口）

### 职责

- 自包含参数（`[SerializeField]` 序列化）与运行时状态
- 自包含功能计算（能力方法，如 `Apply()`、`Update()`、`ProbeGround()`）
- 生命周期处理器由策略统一注册，模块自身不注册
- 需要其他模块能力时，在方法参数中声明，由策略传入实例

### 持有内容

- 自身参数与状态
- **不持有其他模块引用**（依赖全部通过方法参数传入）

### 不需要

- 不实现任何接口（`IUnitMoverLifecycleModule` 等已否决）
- 不定义公开能力接口（保持具体类，YAGNI）
- 不接触 UnitMover
- 不持有策略引用（数据由策略作为参数传入，模块无反向引用）

## 五、交流规则矩阵

| 从\到 | UnitMover | 策略 | 模块 |
|-------|-----------|------|------|
| UnitMover | — | ✅ 调用抽象方法 / 作为 context 传入 | ❌ 禁止 |
| 策略 | ✅ 实现 Commit / 回写结果 | — | ✅ 持有 / 调用 / 传实例 |
| 模块 | ❌ 禁止 | ✅ 接收参数（数据/实例） | ❌ 禁止互相持有 |

## 六、生命周期注册机制

```text
注册期（OnInitialize / 策略切换时）：
  1. Clear()：清空所有生命周期事件
  2. Register(event, [index,] handler)：策略把自己的处理器注册进 List
     - 无 index → 追加到末尾
     - 有 index → 插入到该位置（List.Insert）
  3. Commit()：遍历 List，排序后写入事件字段（数组快照）

执行期（FixedUpdate / OnValidate / OnDrawGizmos）：
  Invoke(event)：遍历事件字段的数组快照，逐个调用
```

- 切换策略时：Clear() → 新策略 OnInitialize → 重新注册 → Commit()，天然无重复
- 固定步执行零分配：Commit 后遍历数组快照

### 顺序契约

框架提供默认顺序常量，开发者可覆盖（改的人需自行保证顺序正确）：

```csharp
public static class UnitMoverLifecycleOrder
{
    public const int ShapeSync     = 10;   // 形状同步
    public const int GroundProbe   = 20;   // 接地探测
    public const int MotionModules = 50;   // 重力/跳跃/悬浮
    public const int Constraints   = 80;   // 边缘/陡坡
    public const int StrategyFlow  = 100;  // 策略主流程
    public const int FinalCommit   = 999;  // 最终提交
}
```

## 七、DataProvider 归属

- DataProvider 读取完全归策略
- UnitMover 只持有 `_dataProvider` 引用，并在初始化时把黑板传递给策略（经 context.Input）
- 策略在 OnInitialize 时把黑板 `as` 转换成自己需要的输入契约并缓存引用
- 通用输入 `IUnitMovementInput`：默认策略使用
- 特殊输入（飞行升降、游泳下潜等）：策略在初始化时取自己的契约，输入缺失时由策略定义中性行为

## 八、已定事项汇总（原待定项）

| 待定项 | 已定结论 |
|--------|---------|
| Commit 提交速度 | 归策略：`UnitMovementStrategy.Commit(IUnitBody)` 抽象方法，强制每个策略显式提交 |
| 模块间依赖 | 模块不持有其他模块引用；需要时在方法参数中声明，由策略在调用点传入实例 |
| 模块实例归属 | 模块 `new` 在策略内，策略持有、序列化、管理、传参 |
| 顺序契约 | 框架提供默认常量，开发者可覆盖 |
| Runtime 去留 | 删除 Runtime 类：模块由策略管理，UnitMover 直接实现 `IUnitMovementContext` 提供启动数据 |
