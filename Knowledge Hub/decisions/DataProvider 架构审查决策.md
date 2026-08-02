---
tags: [Unity, Framework, ExpandComponent, DataProvider, 决策, 审查]
created: 2026-08-02
updated: 2026-08-02
status: 审查定稿，P1/P3 待实施；E2/E3/U2 不立项
---

# DataProvider 架构审查决策（2026-08-02）

## 背景

对 `DataProvider` 模块（`Framework.ExpandComponent.DataProvider`）做全量架构审查，评估分层合理性、扩展性与易用性。本文件记录讨论定稿：仅保留有当前收益和明确实现边界的事项；不为假设性扩展改变类型化 Blackboard 的既有约束。

## 架构结论

**分层合理性：高。** Core（可独立打包）/ Example（项目示例）分层干净；类型即身份（Type→Attribute 字典）零字符串零装箱；三种时间语义（State/Delta/Button）各自独立基类；`InputButton` 多消费者版本号游标与 `Delta` 双精度累计是扎实设计；Provider 薄胶水层仅 49 行。

**设计意图确认**：一个单位只挂一个 DataProvider，作为该单位所有数据需求唯一来源 → 不存在多 Provider 帧序调度问题。

**跨层边界确认**：`IUnitMovementInput` 是 UnitMover 消费 DataProvider 的应用契约，归属 UnitMover 层合理（DataProvider 只做框架级规范，UnitMover 是扩展组件）。

**输入时序确认**：输入在 `Update` 采集、物理刚体在 `FixedUpdate` 消费是 Unity 标准做法，不构成问题。

---

## 待实施项

### P1: OnInitialize 缓存 InputAction — ⚠️ 未实施

**问题**：`PlayerDataSourceHandler` 每帧按名称执行 7 次 `FindAction` 查找（`ReadMove` / `ReadLook` / `ReadScroll` / `ReadHeld` × 4），属于可避免的输入热路径开销。

**决策**：在 `OnInitialize` 一次性解析并缓存各个 `InputAction` 引用。序列化字段继续保留 Action 名称；运行时读取缓存，不再查找字符串。缓存为空时保持既有 Legacy Input 回退语义。

**实施边界**：
- 仅修改 `PlayerDataSourceHandler` 示例层，不扩大 `Blackboard` 基础 API。
- Action 名称在 Inspector 修改后，重新进入 Play 模式时重新解析。
- 不在 `ProcessData`、`ReadMove`、`ReadLook`、`ReadScroll` 或按钮读取热路径中调用 `FindAction`。

### P3: Blackboard.Get 的失败语义 — ⚠️ 未实施

**问题**：`Get<T>()` 未注册时返回 `null`；调用方的裸 `Get().Value` 最终表现为 NRE，无法直接定位到“属性没有注册”的根因。

**决策**：
- `Get<T>()` 未注册时抛出包含属性类型的 `InvalidOperationException`，用于要求属性存在的调用路径。
- `TryGet<T>()` 保持纯查询：未注册时返回 `false` 和 `null`，不输出“只警告一次”的日志。
- 可选属性由调用方显式使用 `TryGet<T>()` 或 `Has<T>()` 处理；日志不进入正常控制流或热路径。

## 不立项与规则

### E3: OnChange 变更通知 — ❌ 暂不立项

**结论**：当前 Blackboard 的主要数据是逐帧输入状态、Delta 和按钮版本，消费方按自己的 Update / FixedUpdate 时序轮询是正确模型。泛型 `ValueChanged` 会额外引入相等性判定、Delta 语义、批处理、调度归属、订阅释放等基础框架责任；当前没有具体消费者，不创建通用通知机制。

若未来出现明确的低频状态变更消费者，再按该消费者的实际时序在业务层或既有 `EventCenter` 设计事件，不预设 Blackboard 全局事件系统。

### E2: 运行时动态注册 — ❌ 暂不立项

**结论**：构造期固定注册和类型化属性访问是当前 Blackboard 的核心不变量。技能临时状态应由技能自身持有，或作为业务 Blackboard 中已声明的属性表达；“Mod / 网络未来字段”不是当前项目的具体需求，不为其公开注册、注销和清理 API。

### U2: Handler 序列化迁移保护 — ✅ 编码规则，不单独实施

**结论**：`[FormerlySerializedAs]` 只能在字段实际改名时填写真实旧字段名，不能预先保护未知的未来改名。当前没有已发生的 `PlayerDataSourceHandler` 字段迁移，不添加无效标注；今后任一序列化字段改名必须在同次修改中添加对应迁移属性。

---

## 未采纳项（撤销/不做）

| 编号 | 内容 | 决策 |
|------|------|------|
| E4 | 多 Provider 统一帧序调度 | ❌ 撤销（一单位一 Provider 设计意图） |
| U4 | 单元测试 | ❌ 不做（后续集成测试案例覆盖） |
| P2 | IUnitMovementInput 接口归属迁移 | ❌ 撤销（归属 UnitMover 层合理） |
| P4 | Update 采集 / FixedUpdate 消费时序 | ❌ 撤销（Unity 标准做法） |

## 待办

- [ ] P1 OnInitialize 缓存 InputAction 实施
- [ ] P3 Blackboard.Get 抛异常 / TryGet 保持无日志查询实施
- [ ] 发生任何序列化字段改名时，在同次修改中添加准确的 `[FormerlySerializedAs]`

## 相关文档

- [[modules/DataProvider技能]]
- [[decisions/UnitMover 审查决策 2026-08-01]]
- [[_conventions/框架设计原则]]
