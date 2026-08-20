# DataProvider

以类型为身份、属性为粒度的实体数据驱动框架。将"这个实体有什么数据"、"数据从哪来"、"数据怎么用"三者解耦。

## 分层架构

```
┌──────────────────────────────────────────────┐
│  Providers/   MonoBehaviour 胶水层            │
│  把 Blackboard 和 Handler 装配到一个 GameObject │
│  Update 中调 Tick()，驱动 Handler 跑           │
└──────────────────┬───────────────────────────┘
                   │ 持有 & 调用
┌──────────────────▼───────────────────────────┐
│  Handlers/     纯 C# 数据源处理器              │
│  从设备/AI/回放读取数据，写入 Blackboard 属性    │
│  键位绑定、灵敏度、输入设备等配置全在这           │
└──────────────────┬───────────────────────────┘
                   │ 读写
┌──────────────────▼───────────────────────────┐
│  Blackboards/  预配置属性面板                   │
│  声明"这个实体有哪些属性"，构造时一次性注册       │
│  暴露类型化属性引用，消费方直接 .Value 或 .Consume│
└──────────────────┬───────────────────────────┘
                   │ 组装自
┌──────────────────▼───────────────────────────┐
│  Attributes/   属性定义                        │
│  一个 class = 一个属性 = Blackboard 一个槽位     │
│  类型即身份，board.Get<JumpAttribute>() 零字符串  │
└──────────────────────────────────────────────┘
```

## 数据流向

```
Update() → Provider.Tick() → Handler.Process(Blackboard)
                                    │
                          ReadMove() → blackboard.Move.Value = ...
                          ReadHeld() → blackboard.Jump.SetHeld(...)
                          ReadScroll() → blackboard.Scroll.Add(...)

下游消费:
  var move = board.Get<MoveAttribute>();
  move.Value   → 状态直接读，不消费
  jump.ConsumePressed(ref cursor, out pressed) → 按钮边沿消费，多消费者独立
  look.Consume(ref cursor, out delta) → 增量累计消费，多消费者独立
```

## 属性类型的时间语义

| 语义            | 基类                                            | 写入                            | 读取                                   | 示例                     |
| ------------- | --------------------------------------------- | ----------------------------- | ------------------------------------ | ---------------------- |
| **State 状态**  | `BlackboardAttribute<T>`                      | `Value = x`，后写覆盖前写            | 直接读 `Value`                          | Move, Sprint, Aim      |
| **Delta 增量**  | `Vector2DeltaAttribute` / `IntDeltaAttribute` | `Add(delta)`，累计               | `Consume(ref cursor, out delta)`     | Look, Scroll           |
| **Button 按钮** | `ButtonAttribute`                             | `SetHeld(bool)` 或 `Trigger()` | `ConsumePressed/Released` + `IsHeld` | Jump, Attack, Interact |

## 新增一个实体属性（示例：添加"闪避"）

**1. 定义属性** — `Example/Attributes/CombatAttributes.cs`:

```csharp
public sealed class DodgeAttribute : ButtonAttribute { }
```

**2. 装配到黑板** — `Example/Blackboards/CharacterBlackboard.cs`:

```csharp
public DodgeAttribute Dodge { get; }
// 构造函数里
Dodge = Register(new DodgeAttribute());
```

**3. 写入数据** — `Example/Handlers/PlayerDataSourceHandler.cs`:

```csharp
blackboard.Dodge.SetHeld(ReadHeld(_dodgeAction, _dodgeKey));
```

**4. 消费数据** — 任何下游系统:

```csharp
var dodge = board.Get<DodgeAttribute>();
if (dodge.ConsumePressed(ref _cursor, out _))
    PerformDodge();
```

零改动 Blackboard、零改动 Provider 基类、零改动 Editor。四步全在 Example 目录完成。

## Core/ vs Example/

| | Core/ | Example/ |
|---|---|---|
| 包含 | 框架基础设施 | 项目具体使用示例 |
| 可独立打包 | 是 | 否（依赖 Core） |
| 命名空间 | `Framework.ExpandComponent.DataProvider` | `Framework.ExpandComponent.DataProvider.Example` |
| 典型内容 | Blackboard、BlackboardAttribute、InputButton、DataSourceHandler 基类 | MoveAttribute、PlayerBlackboard、PlayerDataSourceHandler |

## 文件清单

```
DataProvider/
├─ Core/
│  ├─ Attributes/     # BlackboardAttribute, ButtonAttribute, DeltaAttributes, InputButton
│  ├─ Blackboards/    # Blackboard (基础容器)
│  ├─ Handlers/       # DataSourceHandler (抽象基类 + 泛型版本)
│  └─ Providers/      # IDataProvider, DataProviderBase
│
└─ Example/
   ├─ Attributes/     # Move, Look, Sprint, Jump, Crouch, Interact, Scroll
   ├─ Blackboards/    # Character, Player, Enemy, Npc
   ├─ Handlers/       # PlayerDataSourceHandler (设备输入采集)
   └─ Providers/      # PlayerDataProvider (MonoBehaviour 装配点)
```
