---
tags: [Unity, Framework, ExpandComponent, UnitMover, Rigidbody]
created: 2026-07-28
updated: 2026-07-28
status: 核心结构已实施，待 Unity 场景验证
---

# UnitMover 重构方向

## 当前结论

`UnitMover` 是 Framework 的扩展组件，不是角色业务控制器。它只负责将 Unity 的生命周期、序列化引用与物理边界接入一套纯 C# 运动运行时；玩家输入、AI、Root Motion、网络同步与具体角色能力均不属于它。

当前核心已完成以下结构调整：

- 命名空间统一为 `Framework.ExpandComponent.UnitMover`。
- 删除旧的、直接依赖 Blackboard 的 `MovementStrategy`、反射参数迁移与脚本拖拽实现；改为独立的 `UnitMovementStrategy` 纯 C# 策略基类。
- `UnitMover` 只依赖 `IDataProvider` 和 `IUnitMovementInput` 两个通用契约；不引用具体 Provider、Blackboard、Input Attribute、Animator 或 NavMesh，并在固定步主动消费已绑定 Provider 的输入。
- `UnitMover` 成为唯一可挂载的 `MonoBehaviour`；运动能力、命令来源和移动策略均为纯 C# 对象。
- 移动、跳跃、浮动胶囊、接地、台阶、重力与边缘防跌落使用独立配置类，不再堆叠为 `UnitMover` 的扁平字段。
- 自定义 Inspector 按功能大类绘制独立边框面板，并通过 `SerializedProperty` 保持 Undo、Prefab Override 与多对象编辑。

## 核心边界

```text
业务层 / 扩展适配层
  Player Input / AI Navigation / Root Motion / Network
                 |
                 v
      IUnitMovementCommandSource
                 |
                 v
UnitMover (MonoBehaviour 组装器与 Unity 生命周期桥接)
                 |
                 v
        UnitMovementRuntime (纯 C#)
  Collider / Ground / Step / Jump / Hover / Gravity / Edge
                 |
                 v
      IUnitBody / IPhysicsQuery (Unity 适配边界)
                 |
                 v
         Rigidbody + Collider + Physics
```

### UnitMover 的职责

- 缓存并验证 `Rigidbody` 与 `CapsuleCollider` / `BoxCollider`。
- 保存可序列化的 `UnitMovementProfile`，并在旧 Prefab 或反序列化缺失时补齐其中的子配置。
- 在运行模式创建、推进与释放 `UnitMovementRuntime`。
- 在编辑模式同步浮动胶囊实际形状，并绘制仅供 Scene 观察的 Gizmos。
- 在“相关组件引用”中显式保存移动 `DataProvider` 引用，运行时优先缓存它实现 `IUnitMovementInput` 的黑板；引用为空或失效时才扫描同物体的兼容 Provider 作为装配便利。每个固定步由 UnitMover 直接读取该缓存黑板并提交命令，不依赖命令来源注册表。同时向业务层提供 AI、网络等独立来源的手动命令 API，以及按泛型类型选择、复用和清空移动策略状态的 API。

### 不属于 UnitMover 的职责

- Input System、具体的 DataProvider、具体 Blackboard Attribute、冲刺、技能、体力和角色状态机。
- NavMesh 寻路、巡逻、目标选择、攻击追踪。
- Animator 状态控制与 Root Motion 产生。
- 游泳体积、浮力、移动平台业务规则与网络同步协议。
- 二段跳、墙跳、击退免疫等特定游戏规则。

这些系统只能在所属模块中实现数据采集或业务适配。UnitMover 可以主动消费同物体 `IDataProvider` 暴露的通用移动输入契约，但不得反向依赖或保存任何具体业务组件。

## 纯 C# 模块规则

除 `UnitMover` 外，模块不得继承 `MonoBehaviour`，也不得自行挂载、查找场景对象、接收 `Awake` / `Update` / `FixedUpdate` 或直接依赖业务模块。它们可以使用已注入的 Unity 数据类型，例如 `Vector3`、`Collider`、`Rigidbody`，但 Unity 写入必须收口到接口边界。

`IUnitBody` 是唯一允许写入 Rigidbody 速度、位置修正与受控物理设置的边界；`IPhysicsQuery` 是物理查询边界。其余模块只能计算接触数据、候选速度或约束结果，避免跳跃、悬浮、台阶和边缘保护各自写 Rigidbody 而相互覆盖。

固定步的数据流为：

```text
Body Snapshot
  -> Effective Collider Shape
  -> Ground Probe / State Update
  -> Active Command Source or Submitted Command
  -> Locomotion Mode
  -> Step and Edge Constraints
  -> Jump / Hover / Gravity
  -> IUnitBody Single Commit
```

## 移动策略与命令来源

当前结构明确区分三类对象：

| 类型 | 核心接口 | 负责内容 | 不负责内容 |
|---|---|---|---|
| 命令来源 | `IUnitMovementCommandSource` | 产生本物理步的移动、速度倍率与跳跃意图 | 直接操作 Rigidbody、计算重力或加速度 |
| 移动策略 | `UnitMovementStrategy` | 根据状态与命令选择运动状态并计算候选平面速度 | 输入读取、Blackboard、业务状态机 |
| 功能模块 | `GroundProbeModule` 等 | 一项独立物理能力或约束 | 业务输入与场景生命周期 |

Inspector 通过 `[SerializeReference]` 保存初始 `UnitMovementStrategy`，编辑器仅列出可实例化的派生类供选择，不保存项目脚本类型字符串。运行时通过 `UseMovementStrategy<TStrategy>()` 指定当前策略；运行时内部按 `Type -> UnitMovementStrategy` 缓存策略实例，首次选择时初始化，后续切回时保留该实例全部状态。所有策略必须实现 `ClearState()`；业务层可调用 `ClearMovementStrategyState<TStrategy>()` 显式清空状态，运行时销毁时也会统一清空全部缓存策略。

业务层可以通过 `RegisterCommandSource`、`ReplaceCommandSource`、`ActivateCommandSource`、`UnregisterCommandSource` 注册纯 C# 命令来源；简单桥接与测试可调用 `SubmitCommand`。命令仅携带移动意图、速度倍率和跳跃状态；角色朝向、镜头朝向与动画旋转留在所属业务模块，不由 UnitMover 暗中处理。

`PlayerDataProvider` 只负责将设备输入写入 `PlayerBlackboard`，不引用或驱动 `UnitMover`。在 UnitMover 的“相关组件引用”面板指定同对象的 `PlayerDataProvider` 后，`CharacterBlackboard` 实现的 `IUnitMovementInput` 会被 UnitMover 缓存，并在每个固定步直接生成移动、冲刺与跳跃命令；该引用为空时才自动发现同对象兼容 Provider。故同一对象同时存在 `PlayerDataProvider`、`UnitMover` 与默认刚体策略时，WASD/配置的 Input Action 会形成完整移动链路。

命令来源实例的运行时状态属于其自身。只要实例仍处于注册表中，切走后再激活时应保留其必要状态；替换与注销时由运行时执行完整的清理回调。

## 模块化配置

`UnitMovementProfile` 仅是可序列化的配置聚合器，不执行物理逻辑。每个大类采用一个独立 `[Serializable]` 纯 C# 配置类，避免 `UnitMover` 成为巨型字段表：

| 配置类 | 负责字段 |
|---|---|
| `LocomotionSettings` | 地面与空中速度、加速度、减速度、空中控制 |
| `JumpSettings` | 跳跃开关、初速度、土狼时间、跳跃缓冲、截断倍率 |
| `GravitySettings` | 重力倍率、下落倍率、最大下落速度 |
| `GroundSettings` | 地面层、坡度限制、悬浮高度、探测距离、弹簧与阻尼 |
| `FloatingCapsuleSettings` | 浮动胶囊开关与底部无碰撞留空高度 |
| `StepSettings` | 自动台阶高度、前探余量、最大上抬速度 |
| `EdgeProtectionSettings` | 支撑预测、短缝确认、异常跌落回退 |

配置类只保存默认参数和开关；运行时缓存、查询结果与安全位置快照不参与序列化，分别保存在相应模块中。新能力需要先判断它是否是通用物理能力：是则增加独立配置模块与运行时模块；否则放在业务层，不在 `UnitMover` 增加字段。

## 浮动胶囊与编辑模式预览

浮动胶囊用于形成底部无碰撞空腔，帮助单位通过低台阶或减少底部卡顿。它不是单独挂载的脚本，而是：

- `FloatingCapsuleSettings` 保存可复用配置。
- `FloatingCapsuleAuthoringState` 保存当前组件自身的基础 `CapsuleCollider` 形状快照。
- `ColliderShapeModule` 根据配置同步实际 Collider 形状。

对默认 Y 轴 CapsuleCollider：

```text
基础形状：radius = 0.5, height = 2.0, center.y = 1.0
bottomClearance = 0.4：radius = 0.5, height = 1.6, center.y = 1.2
```

公式为：

```text
effectiveHeight = baseHeight - clearance
effectiveCenter = baseCenter + capsuleAxis * (clearance * 0.5)
```

顶部保持与基础胶囊对齐，仅底部向上抬升。浮动功能关闭时恢复基础形状；持续关闭时允许作者直接修改 CapsuleCollider 并重新记录为新的基础形状，避免旧快照覆盖作者的编辑。

`UnitMover` 使用 `[ExecuteAlways]`，但编辑模式只执行形状同步和 Gizmos 绘制；`FixedUpdate` 以 `Application.isPlaying` 为边界，编辑模式不会创建运行时模块、写 Rigidbody 或执行运动逻辑。Scene 预览读取实际有效 Collider，因此在 Inspector 修改 clearance 后能立即看到尺寸与底部位置变化。

## 边缘防跌落

边缘防跌落是可关闭的纯 C# `EdgeProtectionModule`，详细算法以《[UnitMover 边缘防跌落方案](UnitMover%20边缘防跌落方案.md)》为准。当前模块负责：

- 预测左、中、右三个支撑点，并区分稳定、临界与无支撑状态。
- 仅在支撑失败后检查短缝桥接，降低正常移动时的额外物理查询。
- 同时移除候选速度和已有惯性速度的外向分量，避免高速或外力把单位推出边缘。
- 尝试沿边切向速度而不是立即硬停。
- 保存最近安全位置，可选回退异常跌落。
- 提供支撑点、危险点与受约束速度的运行时 Gizmos。

窄桥、短缝退出、复杂斜边与移动平台仍需通过真实场景调参，不应仅以静态代码检查作为验收。

## Inspector 组织

自定义 Inspector 采用接近材质 Inspector 的独立边框功能面板，而不是一次性展开 `UnitMovementProfile`。固定顺序为：

1. `Script`：只读，始终显示。
2. `相关组件引用`：Rigidbody 与移动 Collider。
3. `移动策略与命令来源`：选择 Inspector 初始策略，显示当前缓存策略、命令来源和自动运动状态；运行时策略通过泛型 API 切换，不提供脚本拖拽或类型字符串选择。
4. `基础移动`：地面速度、加速度与减速度。
5. `跳跃功能`：先显示开关，关闭时隐藏其余跳跃参数。
6. `浮动胶囊、接地与台阶`：浮动开关、clearance、地面、坡面、悬浮与台阶参数。
7. `空中行为与重力`：空中控制、重力倍率、下落倍率与最大下落速度。
8. `边缘防跌落`：先显示开关，关闭时隐藏相关参数。
9. `编辑器预览`：Scene Gizmos 开关。

各面板用 `EditorStyles.helpBox` 形成明确边界；模块标题以整行横向填满的深色 Header 绘制，保留左侧折叠箭头与粗体名称，避免 Unity 内置 `foldoutHeader` 只在标签宽度范围内绘制背景。运行期诊断会将缓存移动策略、命令输入和自动运动模式分开显示：前者由 Inspector 初始策略或运行时泛型选择决定；检测到兼容 Provider 时，命令输入显示为 `DataProvider`，否则显示默认的 `SubmitCommand` 通用接口；自动运动状态由策略和接地结果共同确定。运行时还显示命令世界方向、策略候选速度、最终刚体提交速度和 Rigidbody 约束，以便定位数据、策略或物理边界的断点。所有属性继续通过 `SerializedProperty` 绘制，禁止混入类型字符串保存或每帧脚本扫描。

## 目录与命名空间

```text
Assets/Scripts/C#/Framework/Expand Component/UnitMover/
  UnitMover.cs                 Unity 生命周期与组装入口
  Core/                        命令、状态、接口与运行时管线
  Profiles/                    可序列化的模块化配置
  Physics/                     Collider、接地、台阶、边缘保护
  Motor/                       地面/空中模式、跳跃、悬浮、重力
  UnityAdapters/               Rigidbody 与 Physics 的 Unity 适配

Assets/Editor/Framework/Expand Component/
  UnitMoverEditor.cs           模块化 Inspector
```

运行时代码统一使用：

```csharp
namespace Framework.ExpandComponent.UnitMover
```

## 后续接入顺序

1. 为需要自动驱动 UnitMover 的黑板实现 `IUnitMovementInput`；Provider 保持只采集和更新自己的黑板数据。
2. 在 AI 模块实现导航命令来源，仅提交导航方向、速度倍率与跳跃请求。
3. 在动画模块实现正式 Root Motion Adapter，经验证后转换为命令或专用移动策略。
4. 需要游泳、低重力或击退时，在所属环境/业务模块实现 `UnitMovementStrategy` 派生类，并用 `UseMovementStrategy<TStrategy>()` 切换；不要在核心追加玩家专属字段。
5. 在实际场景验证台阶、坡面、窄桥、短缝、外力推挤与移动平台，再调整专用参数和边缘算法细节。

## 验收清单

- CapsuleCollider 与 BoxCollider 均可创建并运行，不发生空引用。
- 基础胶囊 `r=0.5, h=2, center.y=1` 在 clearance `0.4` 时变为 `r=0.5, h=1.6, center.y=1.2`，顶部保持不动。
- 浮动胶囊的底部留空不是仅用于缩短 `CapsuleCollider` 的显示数据：接地探测距离、悬浮弹簧目标和 Scene 预览均使用 `底部留空 + 常规悬浮高度`。因此在平地静止时，有效胶囊底部会保持该距离，不会因刚体落下而抵消底部留空；以上例且 `hoverHeight=0.05` 为例，有效胶囊底部与地面保持 `0.45m`。
- `GroundSettings.HoverHeight` 是用于弹簧手感的额外微小悬浮距离，Inspector 和运行时均限制为 `0m` 到 `0.5m`；需要更大的台阶通过空间应调整浮动胶囊的 `Bottom Clearance`，而不是无限增大该参数。
- 编辑模式修改浮动开关、clearance 和基础 CapsuleCollider 后，Scene Gizmo 立即反映有效形状，且不写 Rigidbody。
- 跳跃关闭时不显示、也不消费跳跃细节参数。
- 命令来源切换、替换和注销不残留订阅，运动速度不由外部来源直接写入；策略首次选择后会缓存并保留实例状态，调用 `ClearMovementStrategyState<TStrategy>()` 或运行时销毁后会执行其 `ClearState()`。
- 地面、上坡、下坡、连续台阶、直边、斜边、凸角、窄桥与短缝均经场景测试。
- UnitMover 只通过 `IDataProvider` 与 `IUnitMovementInput` 直接消费数据，不出现具体 Provider、Blackboard 或 Input Attribute 引用；不存在旧策略类、类型字符串反射或 StrategyParam 引用；所有 `UnitMovementStrategy` 派生类均实现 `ClearState()`。

## 验证记录

- 2026-07-28：Unity 已生成 UnitMover 新目录及全部 21 个 C# 脚本的 `.meta`，`Assembly-CSharp.csproj` 已包含新的运行时源文件且不再列出旧策略文件。
- 2026-07-28：`dotnet build Assembly-CSharp-Editor.csproj --no-restore` 完成，`Assembly-CSharp` 与 `Assembly-CSharp-Editor` 均为 0 errors。构建仍输出项目既有的 `System.Net.Http` 与 `System.IO.Compression` 程序集版本冲突警告，不由 UnitMover 引入。
- 2026-07-28：`UnitMover` 在运行时创建时一次性发现同物体的 `IDataProvider`，并在每个固定步直接消费其 `IUnitMovementInput` 黑板；`PlayerDataProvider` 不再持有、查找或驱动 `UnitMover`。删除旧的 `PlayerCommandSource` 和中间命令来源桥接，完整编译验证为 0 errors。
- 2026-07-28：删除零使用、且依赖已移除 `Framework.Core.RequireInterfaceAttribute` 的 `RequireInterfaceDrawer`，避免旧 Core 遗留编辑器代码阻断全项目编译。
- 仍需在真实场景人工验证台阶、坡面、窄桥、短缝、异常回退及浮动胶囊的物理手感；这些是参数和场景行为验收，不影响当前基础框架的编译与结构完成度。

## 2026-07-28 移动链路修正

- `UnitMover` 在每个固定步提交黑板命令前，若未缓存可用的 `IUnitMovementInput`，会重新解析同一 `GameObject` 上的 `IDataProvider`。这消除了 Unity 组件启用顺序导致 Provider 晚于 UnitMover 创建时输入永久丢失的问题。
- `IUnitMovementInput` 同时提供移动方向、速度倍率、跳跃持续按住状态和跳跃按下事件；UnitMover 每个固定步完整提交这四类通用输入。
- `GroundProbeModule` 对 CapsuleCollider 从实际底部半球球心向下执行 `SphereCast`，命中距离即为有效脚底间隙，浮动胶囊缩短后不再以胶囊中心错误计算接地距离。
- `RigidbodyUnitBody` 在 UnitMover 接管期间冻结全部物理旋转并清除角速度，避免胶囊碰撞扭矩驱动角色自行转向；组件停用时恢复接管前的刚体约束。角色朝向属于独立业务或旋转模块，不由平移管线推导。
- 边缘防跌落面板提供独立的“显示边缘检测 Gizmo”开关。运行时在 Scene 窗口显示前缘三点支撑与环形危险扫描的向下射线：绿色表示命中可行走支撑，红色表示无支撑危险区；未执行检测的本帧不会保留旧射线。

## 非目标

- 不把 UnitMover 发展为包含 AI、输入、动画、技能或角色状态机的业务总控。
- 不为未确认需求增加每帧自动脚本扫描、全局命令来源、复杂事件总线或大量预置策略；显式 DataProvider 引用为空或失效时才进行同对象兼容项查找，以建立缓存命令来源。
- 不用空气墙替代基于支撑预测的边缘防跌落。
- 不将配置类拆为新的挂载组件；只有 `UnitMover` 需要挂载职责。
