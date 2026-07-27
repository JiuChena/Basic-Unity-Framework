---
tags: [modules, dataprovider, skill]
created: 2026-07-26
updated: 2026-07-26
---

# DataProvider 开发技能

## 概述

`dataprovider` 是 DataProvider 数据驱动框架的专属开发规范 skill。编写或修改任何 DataProvider 相关代码时自动触发。

## 技能覆盖范围

1. **架构层次**：IDataProvider → DataProviderBase → 具体 Provider → Blackboard → BlackboardAttribute<T>
2. **零装箱红线**：禁止 object 类型数据中转，全泛型
3. **创建新属性**：模板、命名规则、已有属性清单
4. **创建新 Provider**：步骤、两种写入方式（缓存 / SetValue）、数据源采集规则
5. **消费方读取**：通过 KEY 常量查找，禁止字符串字面量
6. **旧 Input 移除**：删除清单、保留清单、删除流程
7. **UnitMover 适配**：最小改动策略

## 属性清单

| 属性 | KEY | 值类型 |
|------|-----|--------|
| MoveAttribute | `"Move"` | `Vector2` |
| LookAttribute | `"Look"` | `Vector2` |
| JumpAttribute | `"Jump"` | `InputButton` |
| SprintAttribute | `"Sprint"` | `bool` |
| CrouchAttribute | `"Crouch"` | `InputButton` |
| AttackAttribute | `"Attack"` | `InputButton` |
| AimAttribute | `"Aim"` | `bool` |
| ReloadAttribute | `"Reload"` | `InputButton` |
| InteractAttribute | `"Interact"` | `InputButton` |
| ScrollAttribute | `"Scroll"` | `int` |

## 相关文档

- [[modules/输入系统整改建议]] — 完整架构设计
- [[decisions/输入系统重构决策]] — 决策记录
- Skill 文件：`dataprovider.md`（已同步到全部 Agent 目录）
