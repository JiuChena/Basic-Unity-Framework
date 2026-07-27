---
tags: [modules, dataprovider, framework]
updated: 2026-07-27
---

# DataProvider 模块

命名空间：核心 API 使用 `Framework.ExpandComponent.DataProvider`；示例使用 `Framework.ExpandComponent.DataProvider.Example`。

## 职责边界

DataProvider 将外部数据源转换为实体运行时可消费的数据。模块只提供容器、生命周期和通用属性机制；角色职业、战斗技能、AI 决策、任务交互等业务数据必须由业务层实现。

```mermaid
classDiagram
    class IDataProvider {
        +Blackboard Blackboard
        +Tick()
    }
    class "DataProviderBase~TBlackboard~" as DataProviderBase {
        <<abstract MonoBehaviour>>
        +Blackboard Blackboard
        #DataSourceHandler DataSource
        +Tick()
    }
    class DataSourceHandler {
        <<abstract pure C#>>
        +Initialize(GameObject)
        +Process(Blackboard)
    }
    class Blackboard {
        +Get~T~()
        +TryGet~T~()
        #Register~T~()
    }
    IDataProvider <|.. DataProviderBase
    DataProviderBase --> DataSourceHandler
    DataProviderBase --> Blackboard
    Blackboard <|-- CharacterBlackboard
    CharacterBlackboard <|-- PlayerBlackboard
    CharacterBlackboard <|-- EnemyBlackboard
    CharacterBlackboard <|-- NpcBlackboard
```

## Blackboard

- `Blackboard` 是可继承的容器；`Register`、`Remove` 和 `Clear` 仅对派生黑板开放，Provider 不能动态注册属性。
- 每种具体黑板在构造阶段声明并注册它拥有的属性，并通过类型化只读属性公开。
- `CharacterBlackboard` 声明共有的 `Move`、`Look`、`Sprint`、`Crouch` 和 `Jump`。
- `PlayerBlackboard` 在角色基础上增加 `Interact` 和 `Scroll`；`EnemyBlackboard`、`NpcBlackboard` 是业务层继续扩展的独立入口。
- `DataProviderBase<TBlackboard>` 以泛型约束专用黑板类型；具体 Provider 直接暴露类型化 `Blackboard`，而 `IDataProvider` 显式暴露基类 `Blackboard` 给通用消费者。

## DataSourceHandler

- `DataSourceHandler` 是非 `MonoBehaviour` 的纯 C# 类；它通过 `Initialize(GameObject)` 自行获取所需组件并保存运行时引用。
- `DataSourceHandler<TBlackboard>` 在 `Process` 时校验黑板类型，并将处理入口收敛为 `ProcessData(TBlackboard)`。
- Provider 仅序列化并声明处理器、创建专用黑板；基类在 `Awake` 初始化处理器，并在 `OnDestroy` 释放它。基类不规定更新时机，具体 Provider 或外部系统决定何时调用 `Tick`。
- `DataSourceHandler` 只允许初始化一次；`Process` 会拒绝未初始化或已释放的处理器。需要订阅事件的处理器应在 `OnDispose` 中解除订阅。
- `PlayerDataSourceHandler` 是当前的设备输入示例。业务层可按同样方式实现 `EnemyDataSourceHandler`、`NpcDataSourceHandler`，但不得把业务决策放回基础类。

## 通用属性

- `BlackboardAttribute<T>` 用于持续状态值；需要约束时由通用派生类实现，例如 `MoveAttribute` 将二维输入限制在单位圆内。
- `Vector2DeltaAttribute` 以 `double` 累计和存储游标，再将本次差值转换为 `Vector2` 输出，避免长时间运行时累计浮点精度侵蚀；`IntDeltaAttribute` 使用 `long` 累计。
- `InputButton` 是按钮边沿版本的通用值对象；`ButtonAttribute` 将它适配为黑板属性。持续输入使用 `SetHeld`，一次性命令使用 `Trigger`；消费者只能通过版本游标消费事件，不读取帧态标志。
- 旧的攻击、天赋、爆发、装填、角色切换等业务属性，以及 AI 巡逻/战斗示例，已从基础模块删除。业务层应创建自己的黑板子类和薄属性类型。

## 新增方式

1. 在业务层继承合适的黑板，构造函数中注册业务属性并提供类型化访问器。
2. 实现 `DataSourceHandler<TBlackboard>`，在 `OnInitialize` 内获取组件，在 `ProcessData` 内写入该黑板。
3. 实现 `DataProviderBase<TBlackboard>`，声明类型化 `Blackboard` 与处理器实例；不实现注册逻辑，也不在 Provider 内读取数据源，并由具体 Provider 选择 `Tick` 的调度位置。
4. 消费者优先使用黑板的类型化属性；仅在泛型扩展点使用 `Get<T>` 或 `TryGet<T>`。
