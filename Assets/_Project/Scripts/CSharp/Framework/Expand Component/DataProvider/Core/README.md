# Blackboard Core

这里仅保留能力系统使用的类型化运行时数据容器，不再提供 DataProvider、Handler 或 Provider 调度外壳。

## 当前职责

- Blackboard：按属性类型注册和读取单位级数据。
- BlackboardAttribute<T>：提供类型化状态值。
- ButtonAttribute：提供按住状态、按下/抬起边沿和多消费者游标。
- DeltaAttributes：提供可消费的增量数据。
- InputButton：封装按钮边沿版本号，不直接暴露给业务层。

输入采集由 Framework.Gameplay.Abilities.Input.InputListenerAbilitySO 直接实现。该能力创建单位独占 InputBlackboard，读取 PlayerInput 后写入属性；移动、跳跃等后续能力直接从上下文服务读取，不经过 Provider 转发。

## 数据语义

| 语义 | 类型 | 写入 | 读取 |
| --- | --- | --- | --- |
| 状态 | BlackboardAttribute<T> | Value = x | 直接读取 Value |
| 增量 | Vector2DeltaAttribute / IntDeltaAttribute | Add(delta) | Consume(ref cursor, out value) |
| 按钮 | ButtonAttribute | SetHeld / Trigger | IsHeld 或 ConsumePressed/Released |

## 使用约定

能力运行时在 Initialize 或 Start 阶段创建并注册黑板服务；固定帧逻辑只读取已缓存属性，不创建 Provider、Handler 或额外 MonoBehaviour。需要新增输入或行为数据时，新增对应属性并由具体能力配置负责装配。

## 文件清单

```
DataProvider/Core/
├─ Attributes/
│  ├─ BlackboardAttribute.cs
│  ├─ ButtonAttribute.cs
│  ├─ DeltaAttributes.cs
│  └─ InputButton.cs
└─ Blackboards/
   └─ Blackboard.cs
```
