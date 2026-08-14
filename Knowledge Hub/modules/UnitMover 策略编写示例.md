---
tags: [Unity, Framework, ExpandComponent, UnitMover, 策略, 示例]
created: 2026-08-04
updated: 2026-08-04
status: 待 Unity 验证
---

# UnitMover 策略编写示例

> [!important] 当前生命周期写法
> 本文此前使用的 `OnActivated`、`Simulate`、`OnDeactivated`、`OnDispose`、`OnRuntimeDependenciesChanged` 与 `RestoreAuthoring` 已全部移除。策略不能再覆盖这些旧入口；必须在 `OnRegisterLifecycle()` 中向 `UnitMoverLifecycleContainer` 按顺序注册无参包装方法。

## 生命周期容器最小示例

```csharp
[Serializable]
public sealed class ExampleMovementStrategy : UnitMovementStrategy
{
    [SerializeField] private ExampleSettings _settings = new ExampleSettings();

    protected override void OnInitialized()
    {
        // 仅创建本策略需要的纯 C# 模块，并缓存策略私有的数据契约。
    }

    protected override void OnRegisterLifecycle()
    {
        Lifecycle.RegisterInitialize(InitializeRuntime);
        Lifecycle.RegisterDependenciesChanged(RefreshDependencies);
        Lifecycle.RegisterActivated(ActivateRuntime);
        Lifecycle.RegisterFixedUpdate(SimulateFixedStep);
        Lifecycle.RegisterDeactivated(DeactivateRuntime);
        Lifecycle.RegisterDisposed(DisposeRuntime);
        Lifecycle.RegisterAuthoringValidate(SynchronizeAuthoring);
        Lifecycle.RegisterAuthoringRestore(RestoreAuthoring);
        Lifecycle.RegisterDrawGizmosSelected(DrawGizmos);
    }

    public override void ClearState()
    {
        // 清理本策略实例的运行时状态，保留序列化配置。
    }

    private void SimulateFixedStep()
    {
        float fixedDeltaTime = Lifecycle.FixedDeltaTime;
        // 由策略组织模块顺序，并在本方法中只调用一次 Body.Commit(...）。
    }
}
```

`OnInitialized()` 只在策略实例首次加入当前 `UnitMover` 缓存时运行；策略重新激活时，`UnitMover` 会清空同一容器、让目标策略重新执行 `OnRegisterLifecycle()`，再依次调用 `DependenciesChanged` 与 `Activated`。不要把可切换策略模块的注册放入 Unity 的 `Awake` 或 `Start`。

`BasicMovementTestStrategy` 位于 `Assets/Scripts/C#/Framework/Expand Component/UnitMover/Strategies/Test/BasicMovementTestStrategy.cs`。它只持有一个 `LocomotionSettings` 序列化字段，只读取 `IUnitMovementInput`，只提交 XZ 平面速度。

它刻意不包含接地、重力、跳跃、浮动胶囊、斜坡和边缘保护。因此它只用于两件事：确认策略类型会出现在 UnitMover Inspector 的策略下拉框中；确认 `_locomotionSettings` 与其内部字段会被策略 Inspector 递归显示。

## 新策略的最小结构

```csharp
[Serializable]
public sealed class ExampleMovementStrategy : UnitMovementStrategy
{
    [SerializeField] private ExampleSettings _settings = new ExampleSettings();

    protected override void OnInitialized()
    {
        // 创建该策略独有的纯 C# 运行时模块，缓存所需的 Blackboard 契约。
    }

    protected override void OnRuntimeDependenciesChanged()
    {
        // Provider 或移动参考改变时，只刷新自己的契约缓存。
    }

    public override void OnActivated() { }

    public override void Simulate(float fixedDeltaTime, float currentTime)
    {
        // 组合模块结果，并且只调用一次 Body.Commit(finalVelocity)。
    }

    public override void OnDeactivated() { }
    public override void ClearState() { }
    public override void OnDispose() { }
}
```

## 约束

- 新策略必须是非抽象、可序列化且具有公开无参构造函数的纯 C# 类型，才能被 `UnitMoverEditor` 的 `TypeCache` 下拉框创建。
- 业务代码通过 `UseMovementStrategy<TStrategy>()` 切换；播放模式下 Inspector 的“运行时移动策略”通过 `UseMovementStrategy(Type)` 执行相同生命周期，不会只替换 Authoring 的初始策略配置。
- 将策略配置和模块声明为带 `Tooltip` 的 `[SerializeField]` 字段。需要中文模块标题时，同时声明 `[UnitMovementModuleName("中文模块名称")]`；UnitMover Inspector 会从当前 `SerializedProperty` 的字段名和完整路径反查该特性，再将标题同时用于外层模块栏与内层折叠栏。新增策略不需要修改 `UnitMoverEditor`。
- `[NonSerialized]` 字段只保存依赖引用、物理命中缓冲、计时器、状态和诊断数据；它们不会显示在 Inspector，也不能依赖序列化恢复。
- `UnitMover` 已传入 `Rigidbody`、`CapsuleCollider`、实体 `Transform`、`IUnitBody`、`IPhysicsQuery`、`IDataProvider` 与移动参考 Transform。策略不得保存 `UnitMover`、根 GameObject 或自建万能 Context。
- Blackboard 由策略自行转换为所需接口，例如 `IUnitMovementInput`；UnitMover 不读取或验证具体 Blackboard 类型。
- `Simulate` 是唯一的固定步入口。避免在其中执行 `GetComponent`、`Find`、反射、LINQ 或动态分配；策略完成组合后只调用一次 `Body.Commit(...)`。
- `ClearState` 清理该策略拥有的全部运行时状态但保留 Inspector 配置。`OnDispose` 在此基础上清理缓存的依赖引用。
- 策略若写入共享 Unity 组件或创建附加组件，必须在 `OnDeactivated` 中归还，并在 `OnDispose` 再次幂等清理；若该 Authoring 状态会被 Inspector 直接替换，还应重写 `RestoreAuthoring(...)`。不得让后续策略读取自己留下的临时组件状态。

## 使用测试策略

1. 将 `UnitMover` 初始移动策略切换为 `Basic Movement Test Strategy`。该下拉框按策略类名显示，运行时诊断会显示“基础移动测试策略”。
2. 展开其 `Locomotion Settings` 模块，确认最大速度、加速、减速、空中速度与空中控制字段完整出现。
3. 在同对象保留可提供 `IUnitMovementInput` 的 `PlayerDataProvider`，再进入播放模式确认基础 XZ 移动。
4. 测试结束后切回 `Normal Ground Movement Strategy`。测试策略没有重力和接地能力，不能作为正式角色控制策略。
