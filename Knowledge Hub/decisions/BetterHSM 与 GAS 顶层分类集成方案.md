---
tags: [GAS, BetterHSM, Architecture, Decision, Gameplay]
created: 2026-09-08
updated: 2026-09-08
status: implemented
---

# BetterHSM 与 GAS 顶层分类集成方案

## 决策

`GameplayAbilitySystem` 通过 `BetterHSMAbilitySO` 挂载 BetterHSM 能力。SO 目前选择两级枚举：第一层是顶层实体分类 `Character`、`Enemy`、`NPC`，第二层是该分类下的状态图注册方案 ID。具体角色、敌人和 NPC 的第二层方案暂不提前定义；确定后在相应分类的内层字典中显式增加枚举和工厂映射。

```text
GameplayAbilitySystem
    -> BetterHSMAbilitySO（选择顶层分类 + 注册方案 ID）
    -> BetterHSMAbilityRuntime（创建、唯一驱动、销毁）
    -> BetterHSMAbilitySchemeCatalog（分类枚举 -> 工厂方法）
    -> 具体状态图工厂（创建 Context + BetterHSM + State + Graph）
    -> BetterHSMAbilityContext（交回 Runtime 持有）
```

## 目录与职责

- `BetterHSMAbilitySO`：静态资产，只保存顶层实体分类和注册方案 ID，不保存场景对象、状态实例、状态机或委托。
- `BetterHSMAbilitySchemeCatalog`：静态固定的“顶层分类枚举 -> 注册方案枚举 -> 工厂方法”嵌套字典，不提供 `Register`，不使用反射扫描，也不允许运行时增删映射。
- 具体分类工厂：收到当前实体的 `AbilityOwnerContext`，创建该实体独有的状态上下文、`BetterHSM<TContext>`、状态实例和转换边；`Build(initialState)` 后返回运行时包装。
- `BetterHSMAbilityContext`：保存构建完成后的状态上下文和状态机；不保存已封口且运行期不应再使用的 `StateGraphBuilder`。它对外只公开状态读取，驱动和销毁入口仅对所属 Ability Runtime 开放。
- `BetterHSMAbilityRuntime`：唯一调用 `Update()` 与 `Dispose()` 的拥有者；它将包装对象登记到 `BetterHSMAbilityRuntimeData`，供其它能力或实体只读查询。

## 具体方案接入模板

具体角色方案确定后，新增静态工厂：

```csharp
public static BetterHSMAbilityContext Create(AbilityOwnerContext ownerContext)
{
    CharacterStateContext context = new CharacterStateContext(ownerContext);
    BetterHSM<CharacterStateContext> hsm = new BetterHSM<CharacterStateContext>(context);
    CharacterIdleState idle = new CharacterIdleState();

    StateGraphBuilder<CharacterStateContext> graph = hsm.BeginBuild();
    graph.AddState(idle);
    graph.Build(idle);

    return new BetterHSMAbilityContext<CharacterStateContext>(context, hsm);
}
```

然后在 `BetterHSMRegistrationId` 增加对应二级方案枚举值，并仅在 `BetterHSMAbilitySchemeCatalog` 的对应顶层分类内层字典中增加一条工厂映射。未配置工厂的“分类 + 方案 ID”组合属于启动配置错误，能力初始化必须直接抛出异常，不能创建无状态逻辑的空 HSM。

当前已提供最小测试方案 `Character + CH0221Test`，代码位于 `Assets/_Project/Scripts/CSharp/Core/Gameplay/GAS/BetterHSM/Test/CH0221/`。该方案只通过 `InputRuntimeData.Move` 在 `Idle` 和 `Move` 间切换，用于验证 SO 选择、静态目录、工厂返回包装和 GAS 帧驱动的整条链路；它不驱动 CH0221 的实际移动或动画。

## 跨实体读取边界

其它能力或实体可从 `AbilityRuntimeDataType.BetterHSM` 取得 `BetterHSMAbilityRuntimeData.AbilityContext`，只读访问 `CurrentState`、`CurrentStateType` 与 `StateContext`。状态机的 `Update()` 和 `Dispose()` 只能由其所属 `BetterHSMAbilityRuntime` 调用；外部重复驱动会导致同帧重复仲裁。
