---
tags: [HSM, StateMachine, Architecture, Decision, Gameplay]
created: 2026-09-07
updated: 2026-09-07
status: implemented
---

# HSM 中断容器解耦重构方案

## 1. 决策摘要

HSM 是绑定单个实体上下文的纯 C# 状态机。它只负责持有状态实例、执行状态生命周期和仲裁当前状态的转换边，不负责输入、动画、死亡、攻击或其他业务判断。

状态实例在实体创建时注册并持久化，状态切换只切换已有实例。每个状态持有一个局部中断容器；状态图组装器在初始化阶段把目标状态的静态 `CanEnter` 方法、优先级和目标实例注入来源状态的容器。每次 `Update()` 完整执行当前状态的全部条件，选择优先级最大的边，最多切换一次。

```text
状态类：实现自身进入、更新、退出和清理行为，并提供静态 CanEnter
状态图组装器：集中声明实体状态节点和有向转换边
中断容器：完整评估当前状态的所有 CanEnter 并选出胜者
HSM：统一执行 OnExit -> 切换已有实例 -> OnEnter
实体拥有者：在 Update() 中驱动 HSM.Update()
```

这不是全局事件系统的替代品。中断容器只属于一个实体的一个状态，不广播，也不允许条件方法直接切换状态。

## 2. 核心原则

### 2.1 运行时对象按实体独立

以下对象不得跨实体共享：

```text
HSM<TContext>
StateBase<TContext> 实例
StateInterruptContainer<TContext>
状态内部计时器、缓存、订阅和临时数据
```

状态类本身、不可变配置和静态 `CanEnter` 方法可以复用；每个实体必须创建自己的状态实例。

### 2.2 状态实例持久化，进入时重置本轮数据

状态不因切换销毁，也不代表所有运行数据永久保留：

```text
OnInitialize：缓存本状态长期需要的依赖，只执行一次
OnEnter：重置计时器、连段索引、临时目标等本轮数据
OnUpdate：执行当前状态自己的正常逻辑
OnExit：清理本轮创建的表现、锁定和临时订阅
OnDispose：释放长期订阅和资源，只执行一次
```

状态不能持有其他状态引用，也不能从状态内部调用其他状态的生命周期方法。

### 2.3 进入条件必须静态且无副作用

每条边持有目标状态的静态 `CanEnter(TContext)`。该方法只读取实体上下文，不得消费输入、清除请求、播放表现、查找组件、加载资源或直接切换状态。因为同一帧必须完整判断所有边，任何副作用都会导致低优先级条件影响高优先级结果。

目标状态在 `OnEnter()` 中消费属于自己的输入或一次性请求。这样不需要额外创建 `IStateInterrupt`、`OnAccepted` 或一状态一中断类。

### 2.4 优先级属于转换边

优先级使用 `int`，数字越大越优先。优先级不属于目标状态，也不由 HSM 解释业务含义；死亡、受击、攻击的数值由具体实体状态图决定。

同优先级时使用更早注入的边。注入顺序由状态图代码明确决定，不依赖字典枚举顺序。

### 2.5 每次更新最多切换一次

`HSM.Update()` 先完整仲裁当前状态。没有命中时调用当前状态 `OnUpdate()`；命中时调用当前状态 `OnExit()`，切换到已经注册的目标实例，再调用目标 `OnEnter()`，本次更新结束，不继续评估新状态，也不调用新状态的 `OnUpdate()`。

## 3. 文件职责

```text
Assets/_Project/Scripts/CSharp/Core/Gameplay/HSM/
├── HSM.cs
├── StateBase.cs
├── StateInterruptContainer.cs
├── StateTransitionBinding.cs
├── StateTransitionRequest.cs
└── StateGraphBuilder.cs
```

| 类型 | 责任 | 不承担 |
|---|---|---|
| `HSM<TContext>` | 状态注册、初始化封口、当前状态、更新仲裁、生命周期切换、销毁 | 具体业务条件和组件查找 |
| `StateBase<TContext>` | 保存实体上下文、持有自身容器、提供生命周期 | 保存其他状态引用、直接转换 |
| `StateInterruptContainer<TContext>` | 保存转换边、完整评估、优先级仲裁 | 生命周期调用和业务副作用 |
| `StateTransitionBinding<TContext>` | 保存目标实例、静态条件、优先级和注册顺序 | 执行业务逻辑 |
| `StateTransitionRequest<TContext>` | 保存一帧仲裁结果 | 判断条件或切换状态 |
| `StateGraphBuilder<TContext>` | 初始化阶段注册节点、注入边、校验并封口 | 每帧调度和业务逻辑 |

角色状态、上下文和状态图放在角色或业务模块，不放进 HSM 核心目录：

```text
Assets/_Project/Scripts/CSharp/Framework/Gameplay/<Domain>/States/
├── CharacterStateContext.cs
├── CharacterStateMachine.cs
├── IdleState.cs
├── MoveState.cs
└── AttackState.cs
```

## 4. 当前公共 API

状态使用基类统一生命周期，不使用 `OnTick(float deltaTime)`。HSM 由实体的 `Update()` 驱动；具体状态需要时间时，从实体上下文提供的时间源读取。

```csharp
public abstract class StateBase<TContext>
{
    protected TContext Context { get; }
    public virtual void OnInitialize() { }
    public virtual void OnEnter() { }
    public virtual void OnUpdate() { }
    public virtual void OnExit() { }
    public virtual void OnDispose() { }
}
```

图组装的最小调用方式：

```csharp
HSM<CharacterStateContext> hsm = new HSM<CharacterStateContext>(context);
IdleState idle = new IdleState();
MoveState move = new MoveState();
AttackState attack = new AttackState();

StateGraphBuilder<CharacterStateContext> graph = hsm.BeginBuild();
graph.AddState(idle).AddState(move).AddState(attack);
graph.From(idle)
    .To(move, MoveState.CanEnter, priority: 10)
    .To(attack, AttackState.CanEnter, priority: 20);
graph.From(move)
    .To(idle, IdleState.CanEnter, priority: 10)
    .To(attack, AttackState.CanEnter, priority: 20);
graph.Build(idle);
```

实体拥有者每帧驱动：

```csharp
private void Update()
{
    _hsm.Update();
}
```

## 5. 状态类示例

下面的代码只用于说明状态写法，不属于 HSM 核心实现：

```csharp
public sealed class AttackState : StateBase<CharacterStateContext>
{
    public static bool CanEnter(CharacterStateContext context)
    {
        return context.Input.IsPressed(CharacterInputButton.Attack);
    }

    public override void OnEnter()
    {
        // 进入状态后消费已确认属于本状态的输入。
        Context.Input.Consume(CharacterInputButton.Attack);
        Context.Animation.PlayAttack();
    }

    public override void OnUpdate()
    {
        // 攻击状态只推进自己的行为，不判断其他目标状态。
        Context.Animation.UpdateAttack();
    }

    public override void OnExit()
    {
        // 离开攻击时清理本轮攻击行为。
        Context.Animation.StopAttack();
    }
}
```

`CanEnter` 的责任是表达“什么情况下可以进入我”，而不是表达“我从哪个状态来”。同一个目标状态的静态条件可以被多条来源边复用，是否允许这条边存在由状态图决定。

## 6. 初始化和更新流程

### 6.1 初始化

```text
1. 实体拥有者准备自己的 TContext
2. 创建实体专属 HSM<TContext>
3. 创建该实体的全部 StateBase<TContext> 实例
4. StateGraphBuilder 注册所有状态节点
5. From/To 注入目标状态、CanEnter 和优先级
6. Build 校验初始状态、初始化全部状态并封口容器
7. 进入初始状态 OnEnter
```

`Build` 之后不能再次注册状态、注入边或修改图。中断容器的边集合在运行期保持不变，状态实例一直复用到 `Dispose`。

### 6.2 每帧更新

```text
实体 MonoBehaviour.Update()
    ↓
HSM.Update()
    ↓
当前状态容器按注入顺序完整调用所有 CanEnter(Context)
    ↓
选出 Priority 最大的命中边
    ↓
没有命中：CurrentState.OnUpdate()
命中：CurrentState.OnExit()
    ↓
CurrentState = TargetState
    ↓
TargetState.OnEnter()
    ↓
本次更新结束
```

仲裁使用普通 `for` 循环扫描，时间复杂度是当前状态出边数量 `O(E_current)`；不排序、不使用 LINQ、不创建临时集合。

### 6.3 销毁

`Dispose()` 先对当前状态调用 `OnExit()`，再按注册顺序对所有已初始化状态调用 `OnDispose()`，最后清空状态引用。普通状态切换不调用 `OnDispose()`。

## 7. 校验和错误边界

初始化阶段遇到以下问题应直接抛出异常，不能静默修复：

- 初始状态为空或未注册到当前 HSM。
- 同一状态类型在一个 HSM 中重复注册。
- 状态实例已经绑定其他 HSM。
- 来源或目标状态不属于当前 HSM。
- `CanEnter` 为空或不是静态方法。
- 来源状态等于目标状态。
- 同一来源重复注入完全相同的目标、条件和优先级边。
- `Build` 或 `Seal` 后继续改变状态图。

重复状态、跨 HSM 引用和运行期改图都属于组装错误，应在实体开始运行前暴露。暂不提供自我重入边；确实需要重启同一状态时，后续再设计显式重入 API。

## 8. 使用边界

适合放入实体上下文的是当前实体的运行数据、输入访问接口、动画适配器、状态机所需时间源和已缓存的可选组件适配器。上下文不是全局服务定位器，也不应持有所有可能组件的万能引用集合。

HSM 不负责：

- 输入采集、输入黑板和输入消费实现。
- Animator、CharacterController、刚体或其他 Unity 组件查找。
- BehaviorEditor、Timeline、Hitbox 和动画播放规则。
- 网络同步、AI 决策树、任务流程或业务优先级语义。
- 策划可编辑的可视化状态图。

状态独占的组件由状态在 `OnInitialize()` 中从受控上下文获取；多个状态共同依赖的适配器才由实体上下文统一准备。HSM 核心不主动获取 Unity 组件。

## 9. 性能要求

- 状态实例和转换边只在初始化时创建，`Update()` 不改变结构。
- `CanEnter` 只读取已经缓存的数据，不执行 `GetComponent`、场景查找、资源加载或日志输出。
- 容器使用顺序 `for` 扫描，禁止热路径排序、LINQ、闭包和临时列表。
- 单个状态出边数量保持可审查；边数量明显膨胀时重新拆分状态或使用分层状态机。
- 调试信息只记录最终胜出的转换，不逐帧输出全部失败条件。

## 10. 当前落地状态

本方案已经落地到：

```text
Assets/_Project/Scripts/CSharp/Core/Gameplay/HSM/HSM.cs
Assets/_Project/Scripts/CSharp/Core/Gameplay/HSM/StateBase.cs
Assets/_Project/Scripts/CSharp/Core/Gameplay/HSM/StateInterruptContainer.cs
Assets/_Project/Scripts/CSharp/Core/Gameplay/HSM/StateTransitionBinding.cs
Assets/_Project/Scripts/CSharp/Core/Gameplay/HSM/StateTransitionRequest.cs
Assets/_Project/Scripts/CSharp/Core/Gameplay/HSM/StateGraphBuilder.cs
```

旧的非泛型 `HSM`、旧的 `StateBase` 构造注入方式、`Tick()` 和 `OnTick(float deltaTime)` 链路已移除。当前搜索范围内没有项目代码调用旧 HSM，因此未增加兼容层。
