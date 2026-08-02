---
tags: [Unity, Framework, ExpandComponent, UnitMover, 决策, 审查]
created: 2026-08-01
updated: 2026-08-02
status: A/B/C/D 已实施，E 策略架构讨论定稿、待实施，待 Unity 场景验证
---

# UnitMover 审查决策（2026-08-01）

## 背景

对 `UnitMover` 及关联模块做全量审查，产出漏洞、架构与算法问题清单，逐条与用户讨论定稿。A/B/C 确认项已于 2026-08-02 在代码中实施；后续核对确认的三项坡面整改（D1-D3）也已实施，仍需在 Unity 场景完成行为验证。E 组策略架构已完成讨论定稿但尚未实施。本文保留决策、撤销理由和实施状态，避免把目标方案误写为现状。

---

## 一、漏洞类

### A1/B2: 无 DataProvider 时物理管线停摆 + 命令输入双入口矛盾 — ✅ 已实施

**决策**：开放两种使用方式，无 DataProvider 时不得阻断物理步。
1. **DataProvider 模式**（默认）：UnitMover 主动从 `IUnitMovementInput` 抓取命令。
2. **手动注入模式**：`SubmitCommand` / `RegisterCommandSource`，UnitMover 不阻断 Simulate。

**实施**：`UnitMover.FixedUpdate` 改为 `SubmitDataProviderCommand(); _runtime.Simulate(...)`；`SubmitDataProviderCommand` 变 void，Provider 不可用时由 Runtime 消费空命令或外部命令。

### A2: Time.time 固定步语义澄清 — ❌ 撤销

**决策**：Unity 在 `FixedUpdate` 内返回的 `Time.time` 与固定步时间一致，不存在帧域波动问题。不改动计时域，不自行累计时间。`Time.fixedTime` 仅作可读性替换，不构成修复需求。

### A3: 惯性滑落边缘无法拦截 — ✅ 已实施

**决策**：预测方向取候选速度与当前实际速度中会导致越界的一方；候选速度为零但当前速度非零时仍执行检测并剔除当前速度的外向分量。

**实施**：`EdgeProtectionModule` 新增 `SelectPredictionVelocity`（候选/当前取较大者），`ConstrainVelocity` 与 `HasStableSupportForVelocity` 改用预测速度判定，输入为零时仍约束惯性。

### A4: 安全点回退 → 存档点模式 — ✅ 已实施

**决策**：不主动记录安全点、不自动回退位置。提供显式 `SetCheckpoint` / `RestoreCheckpoint` API，解除对玩家其他位置变化的所有主动限制。

**实施**：`SetCheckpoint`/`RestoreCheckpoint` 贯穿 UnitMover → Runtime → EdgeProtectionModule（`CheckpointSnapshot`）；恢复位置后清空命令与诊断速度；旧 `_safePosition` / `TryGetFallRecovery` / 自动回退全部移除。

### A5: 陡坡下滑配置不变量 — ❌ 不改动

**决策**：`SteepSlopeSlideFactor=0` 是显式关闭下滑的特殊配置，正常配置保证大于零。不将其误记为重力算法缺陷。

### A6: 重力最大下落速度比较 — ❌ 撤销

**决策**：`resultDownwardSpeed` 是速度在归一化重力方向上的投影，沿重力方向为正，比较正确。用 `Mathf.Abs` 会把高速上升误判为下落超速。仅可修正注释措辞，不修改算法。

---

## 二、架构类

### B1: 编辑模式强制改写 CapsuleCollider — ❌ 不修改

**决策**：维持现状。

### B3: 稳定支撑检查无条件执行 — ✅ 已实施

**决策**：复用既有边缘保护开关，不新增第二个设置。边缘保护关闭时跳过五点稳定支撑检查。

**实施**：`GroundProbeModule.CreateMovementState` 增加 `evaluateStableSupport` 参数：`isGrounded && (!evaluateStableSupport || HasStableSupport(contact))`；Runtime 传 `_edgeProtection.IsEnabled`。

### B4: 单帧物理查询总量偏高 — ✅ 已实施

**决策**：边缘保护开关控制全部边缘检测（稳定支撑、前缘预测、短缝、危险方向），关闭时压力显著下降，无 LOD 需求。

**实施**：随 B3 一并落地（开关关闭即跳过 5 点稳定支撑；`ConstrainVelocity` 已按 `_enabled` 短路）。

### B5: Rigidbody 接管策略过强 — ✅ 已实施

**决策**：
- `collisionDetectionMode` 不再强改（保留刚体原值）。
- `FreezeRotation` 改为面板开关，默认开启；关闭时不追加约束且不清零 `angularVelocity`。

**实施**：`RigidbodyUnitBody` 移除 `collisionDetectionMode` 赋值；构造接收 `freezeRotation` 参数，仅冻结时追加约束并清零角速度；`UnitMover` 新增 `_freezeRigidbodyRotation` 字段（默认 true）。

### B6: 输入方向依赖 Camera.main — ✅ 已实施

**决策**：UnitMover 添加摄像机引用（SerializeField + 公开可写属性），经可选移动参考系契约注入输入黑板；为空时黑板回退 `Camera.main` 并缓存。

**实施**：`UnitMover._movementReferenceCamera` + `MovementReferenceCamera` 属性 + `SynchronizeMovementReference()`；新契约 `IUnitMovementReferenceFrame`（`MovementReference` 可写）注入黑板。

### B7: UnityPhysicsQuery 注入点 — ❌ 保持原样（用户定稿）

**决策**：不提供注入点，保持 `UnitMovementRuntime.Create` 内硬编码 `new UnityPhysicsQuery()`。该接口职责仍是 Unity Physics 静态 API 的统一封装（Raycast/SphereCast/BoxCast NonAlloc + 忽略 Trigger），不做改动。

---

## 三、算法类

### C1: GetHorizontalExtent 前缘距离高估 — ✅ 已实施

**决策**：仅在直立、水平等比缩放的胶囊上返回实际世界半径（圆形截面）；X/Z 轴胶囊、水平非等比缩放保留保守 AABB 算法。不得用 `Mathf.Min` 低估前缘。

**实施**：`ColliderShapeModule.GetHorizontalExtent` 新增 `TryGetCircularHorizontalRadius`（校验胶囊轴竖直 + 两水平半径世界缩放一致），通过则直接返回 `radius * worldScale`，否则回退 AABB。

### C2: 窄柱/细走廊恒判 Unstable — ❌ 维持现状

**决策**：边缘保护定位是箱庭防跌落，箱庭不会设计窄处；非箱庭不开边缘保护。个别窄处用空气墙。开关关闭时不做检测（与 B4 一致）。

### C3: 两条接地路径 Distance 语义 — ❌ 撤销

**决策**：`SphereCast` 从胶囊底部球心开始，`hit.distance` 已表示球底到地面间隙，再减半径会造成悬浮错误。保持原样。

### C4: 跳跃后接地忽略窗口硬编码 — ✅ 已实施

**决策**：做成面板参数放入 `JumpModule`，与土狼时间、跳跃缓冲保持独立语义。

**实施**：`JumpModule` 新增 `_groundIgnoreAfterStartDuration`（可序列化面板参数，属性 `GroundIgnoreAfterStartDuration`），Runtime 使用 `_jumpModule.GroundIgnoreAfterStartDuration` 取代硬编码 0.1f。

### C5: CaptureBaseShape 捕获时序 — ✅ 已实施

**决策**：提供名称明确的显式"将当前 CapsuleCollider 捕获为基础形状"入口，由调用方修改 Collider 后立即调用；不增加猜测外部意图的自动捕获逻辑。（回调竞争结论：`OnValidate` 与固定步同主线程，无并发污染，问题是缺少显式协议。）

**实施**：`UnitMover.RecaptureFloatingCapsuleBaseShape()` → `FloatingCapsuleModule.RecaptureBaseShape(capsule)`，重新捕获基础形状并立即同步有效形状。

---

## 四、坡面与浮动胶囊整改

### D1: 区域检测后再以射线兜底 — ✅ 已实施

**原问题**：脚底 `BoxCast` 已作为区域检测主路径；中心射线仅在区域检测命中后、且命中同一 Collider 时用于细化三角面法线。区域检测无有效命中时会直接返回无接地，因此它不是实际的射线兜底。

**决策**：
1. 先使用按 `FootBoxSupportWidthScale` 收缩后的区域检测判断有效支撑。
2. 仅当区域检测没有任何过滤后的有效命中时，再用受同一 `GroundCheckDistance` 限制的中心射线兜底。
3. 区域检测已命中时不让射线覆盖其支撑判定，避免台阶前缘或相邻表面被中心射线错误认作脚下支撑。
4. 脚底物理 Box 保持完整宽度；只缩小探测区域，不以缩小物理碰撞体解决“前缘被托上高台阶”。

**验收**：按住前进撞到超过 `BottomClearance` 的台阶时，物理 Box 阻止上行且悬浮不会把玩家托到台阶顶；松开输入也不会因前缘探测而上浮。玩家正下方只有窄支撑时，区域未命中可由中心射线提供有界接地兜底。

**实施**：`GroundProbeModule.ProbeFootBoxGround` 在 `BoxCast` 找到有效命中时直接使用该命中；只有区域没有有效命中时才调用 `TryProbeFootCenterGround`。原有预分配命中缓冲区继续复用，区域命中路径还减少了一次射线查询。

### D2: 初上陡坡仅清除上坡分量 — ✅ 已实施

**原问题**：现有 `_isSteepSlopeConstraintActive` 一旦在进入阈值和确认时长后置为 true，同一物理步既移除上坡输入，又由 `Apply(...)` 叠加下坡速度。这不符合“刚进入陡坡时先只阻止上坡”的手感要求。

**决策**：为陡坡约束增加明确的首次进入阶段（可用独立 bool 或小型状态枚举）：
1. 首次确认进入不可行走陡坡的物理步，只清除沿斜面向上的命令/速度分量，不叠加沿斜面向下的速度。
2. 下一次仍连续接触该陡坡时，才进入常规下滑状态，并沿既有锁定下坡方向应用当前的下滑速度规则。
3. 退出滞回、短暂丢失接触宽限和接触确认时长继续复用现有配置；回到可行走面或超过丢失宽限后重置首次进入状态。

**验收**：玩家首次顶到陡坡时不会获得突兀的下坡冲量，只能停止继续上行；持续停留或继续接触后才按配置自然下滑；坡面边缘或三角面切换不应反复触发“首次进入”。

**实施**：`SteepSlopeSlideModule` 新增内部 `_isFirstConstraintStep`。首次锁定陡坡时置位；`Apply(...)` 每步先清除实际速度中的沿坡上行分量，首次步清标志后直接返回，不叠加下坡速度；下一连续物理步才进入原有下滑计算。退出、丢失接触超时和重置都会清除该标志。

### D3: 可行走坡面保持切向移动速度 — ✅ 已实施

**原问题**：地面策略已经将输入投影到可行走坡面的切平面并归一化，但 Runtime 为边缘保护提取水平速度后，又把水平速度投影回支撑面。第二次投影没有恢复长度，导致上坡速度随坡度增大而额外衰减。

**决策**：可行走接地时使用策略生成的坡面切向速度，配置移动速度表示沿坡面的实际速度，而不是水平投影速度。边缘保护未修改候选水平速度时直接保留原切向速度；边缘保护移除危险分量后，按水平约束比例缩减速度，再将剩余方向映射回坡面。空中移动和不可行走陡坡保持原有处理。

**验收**：平地与不同可行走坡度上的配置速度保持一致；上坡、下坡和横切坡面都沿地面切平面运动；坡度增大不会额外降低沿坡速度；靠近边缘时危险方向仍会被削减或清零。

**实施**：`UnitMovementRuntime.Simulate(...)` 在可行走接地分支复用策略的 `candidateVelocity`；边缘保护介入时，以约束前后水平速度平方长度之比计算缩放，并恢复坡面切向速度长度。固定步没有新增分配、组件查询或物理查询。

---

## 五、空中行为与移动策略架构重构（讨论定稿，待实施）

### E1: AirControl 归属为基本移动参数 — ⚠️ 讨论定稿

**用户决策**：`AirControl`（空中强弱控制度）应属于**基本移动参数**（`LocomotionSettings`），不属于独立"空中模块"，也不属于跳跃模块。它是移动策略"空中行为"维度的控制度，跳跃离地时由策略履行该职责。

**结论**：
- `AirControl` 保留在 `LocomotionSettings`（基本移动参数），支持三通道调控：代码控制、面板调控、策略类访问调控。
- 空中行为是策略的一个**维度**（每个策略可定义自己的空中/水下/地下行为），不再是 Runtime 内建的世界二分法。

### E2: 移除空中模块冗余 — ⚠️ 讨论定稿

**用户决策**：空中模块中所有有必要保留的**非重复属性**分发到其他模块；重复/冗余部分删除。

**分发方案（待实施）**：
| 原空中属性 | 去处 | 理由 |
|-----------|------|------|
| `AirControl` | 保留在 `LocomotionSettings`（基本移动参数） | 用户明确归属 |
| `AirMaxSpeed` / `AirAcceleration` | 由各移动策略自行定义（策略内部字段） | 空中速度规则是策略多样性的一部分（飞天/遁地各有不同） |
| 空中重力/下落手感 | 已由 `GravityModule`（`_multiplier` / `_fallMultiplier`）承担 | 无需重复 |
| 跳跃期水平弱控制 | 由策略的"空中行为"在离地时履行（读取 `AirControl`） | 不属于跳跃模块字段 |

**移除项**：`DefaultRigidbodyMovementStrategy` 的硬编码 Ground/Air 分支；`Runtime.Simulate` 的 `mode = _jumpModule.IsJumping ? Air : detectedMode` 强制切换。`MovementMode` 枚举保留作为诊断信息（`State.Mode` 仍显示 Ground/Air/Swimming/Flying），不驱动逻辑分支。

### E3: 策略使用受信任的完整上下文 — ⚠️ 讨论定稿

**用户决策**：移动策略是受信任的框架扩展代码，应获得实现完整移动行为所需的全部能力。框架不通过大量包装、白名单或防御性检查限制策略，也不为每一种 Rigidbody 操作重复设计代理 API。

**设计（待实施）**：
- 保留 `IUnitMovementContext` 作为统一注入入口和依赖目录，但它不是权限沙箱，也不以“窄化访问面”为设计目标。
- 上下文应提供 `UnitMover`、`UnitMovementRuntime`、`Rigidbody`、主 Collider、`IPhysicsQuery`、DataProvider/Blackboard、移动设置、当前状态，以及 GroundProbe、Jump、Gravity、Hover、SteepSlope、EdgeProtection 等全部移动功能模块。
- 策略可以直接使用 Rigidbody 的完整能力，包括设置速度、施加不同 `ForceMode` 的力或实现飞行、游泳、遁地、冲刺、击飞等特殊行为。
- 功能模块和上下文对象按实际类型提供，不为阻止开发者误用而重复增加只读镜像、能力 flags 或逐方法包装。

**规范边界**：策略作者应在规范文档中了解固定步无分配、避免重复组件查找、异步/协程生命周期、共享状态恢复等要求。此类要求以文档和代码审查保障，不在 Runtime 中堆叠防御逻辑。完整权限意味着策略作者对直接修改共享状态和 Rigidbody 所产生的结果负责。

### E4: 策略定义行为，Runtime 只负责执行与提交 — ⚠️ 讨论定稿

**用户决策**：策略的身份是“决定如何移动”，Runtime 的身份是“执行当前策略”。Runtime 不应先内建一套地面/空中/跳跃流程，再让策略通过豁免 flags 申请跳过。

**职责边界（待实施）**：
- Runtime 负责：创建并缓存策略、驱动生命周期、构造固定步上下文、调用当前策略、维护诊断状态，并机械地提交本物理步最终结果。
- 策略负责：决定本物理步调用哪些功能模块、调用顺序、输入解释、地面/空中/水下/地下行为、约束顺序和最终限速规则。
- Runtime 不再自动执行 GroundProbe、Jump、Gravity、Hover、SteepSlope、EdgeProtection 等行为模块，也不再根据 `IgnoreGravity` / `IgnoreHover` 一类 flags 改写流程。
- Jump 改为策略可选调用的功能模块。默认地面策略可以使用它完成平地/坡面跳跃；飞行、游泳或遁地策略可以不调用，也可以将同一个输入解释成上升、冲刺或其他动作。
- `MovementMode` 只保留为状态表达和诊断信息，不作为 Runtime 强制选择行为分支的依据。

**默认策略建议流程**：读取输入与当前刚体速度 → 按需探测地面 → 调用地面/空中移动、Jump、Gravity、Hover 等模块累加贡献 → 执行陡坡和边缘方向约束 → 按该策略的空间规则 Clamp → 由 Runtime Commit。该流程只是默认策略的实现，不是 Runtime 对所有策略强加的模板。

### E5: 策略生命周期、切换与实例访问 — ⚠️ 讨论定稿

**生命周期契约（待实施）**：
- `OnInitialize(IUnitMovementContext context)`：策略实例首次进入 Runtime 缓存时执行一次，用于缓存上下文、模块和数据源。
- `OnActivated()`：每次真正切换到该策略时执行，用于建立必须保证的初始状态，例如关闭边缘保护、切换碰撞层或重置策略内状态。
- `Simulate(...)`：策略处于激活状态时，每个固定物理步执行一次。
- `OnDeactivated()`：每次离开该策略时执行，由该策略恢复自己修改过的共享状态。
- `OnDispose()`：Runtime 销毁时执行一次，用于最终解绑和释放策略持有的资源。

**切换规则（待实施）**：
- `UseMovementStrategy<TStrategy>()` 在调用时完成缓存获取或首次创建、必要的初始化、旧策略停用和新策略激活，并立即返回 Runtime 实际缓存且当前已激活的实例。
- 再次选择已经激活的同一策略不算切换：只返回同一实例，不重复调用 `OnActivated()`。
- 暂不增加“只获取但不切换”的额外 API；出现明确业务需求后再设计，避免预先扩张接口。
- Runtime 不为策略激活/停用增加共享状态快照、自动回滚或行为合法性检查。策略对其生命周期内的修改与恢复负责。

**实例访问规则（待实施）**：
- 外部业务和其他组件通过 `unitMover.Runtime.UseMovementStrategy<TStrategy>()` 取得并调控 Runtime 使用的缓存实例。
- 外部不得自行 `new` 出游离策略、持有静态策略实例或建立第二套实例缓存；修改游离实例不会影响实际移动。
- 策略的公开字段、属性和方法就是其业务调控面，应按真实需求提供。

### E6: 策略可主动取得特殊移动输入 — ⚠️ 讨论定稿

**用户决策**：通用 `IUnitMovementInput` 只提供默认移动命令，不得成为策略能读取的数据上限。特殊策略必须可以主动取得与自身有关的输入或状态契约。

**规则（待实施）**：
- 上下文向策略提供 DataProvider/Blackboard 访问能力；策略可读取飞行升降、游泳、冲刺、攀爬、遁地等自定义数据契约。
- 通用移动命令仍作为默认策略的便利入口，Runtime 不尝试预先枚举或翻译所有特殊策略输入。
- 策略应在 `OnInitialize` 或 `OnActivated` 阶段取得并缓存稳定的数据访问句柄；`Simulate` 固定步内不得反复 `GetComponent`、`Find` 或重复进行类型发现。
- 输入缺失时采用何种中性行为由具体策略定义，不由 Runtime 擅自补出移动语义。

### E7: 速度、加速度与力默认累加，特殊覆盖必须显式 — ⚠️ 讨论定稿

**用户决策**：策略和功能模块提交的普通运动贡献是叠加关系，不应互相覆盖。多个方向叠加后导致合速度方向偏移是正常结果；所有贡献完成后，再由当前策略按自身规则限制最大速度。只有确有特殊行为需求时才允许显式覆盖。

**提交语义（待实施）**：
- 每个固定步创建一个以 Rigidbody 当前速度为起点的运动累加器，策略及其调用的模块共同操作该累加器。
- 普通接口使用明确的增量语义，例如 `AddVelocityDelta`、`AddAcceleration`、`AddForce`、`AddImpulse`；连续调用按向量相加，不采用“最后一次普通提交覆盖前面结果”的规则。
- `AddAcceleration`、`AddForce`、`AddImpulse` 应按固定步时长、刚体质量和对应 `ForceMode` 换算或记录为可合并贡献，使同一累加器中的结果能够参与本步约束和 Clamp。
- 地面移动不能每步把完整目标速度当增量重复相加；应先用加速度规则求得下一速度，再提交 `nextVelocity - currentVelocity`。
- Gravity 提交 `acceleration * fixedDeltaTime`；Jump 只补足沿跳跃方向所缺少的速度；Hover 提交沿支撑法线的修正增量。
- EdgeProtection、SteepSlope 等约束可以剔除累计结果中的危险方向分量；它们属于显式约束累计结果，不属于普通贡献覆盖。

**限速与覆盖（待实施）**：
- Clamp 由当前策略在完成贡献累加和方向约束后执行，Runtime 不固定执行全局 `Vector3.ClampMagnitude`。
- 地面策略通常限制支撑面切向速度，飞行策略可以限制完整三维速度；跳跃、下落和外部击飞不应被普通水平移动限速误裁剪。
- 特殊行为需要直接改写结果时，必须调用语义醒目的接口，例如 `OverrideVelocity` 或按指定方向覆盖分量。覆盖发生在调用位置，之后的普通贡献仍继续叠加。
- 策略仍可直接使用原生 Rigidbody API，但这是有意绕过累加器：直接写速度可能被 Runtime 随后的最终 Commit 覆盖，直接 `Rigidbody.AddForce` 则会在后续物理求解中积分，不会自动进入当前累加器，也不一定受本步 Clamp 影响。需要同一步统一计算、覆盖和限速时，应分别使用累加器的 `OverrideVelocity` 或贡献提交接口；选择直接操作 Rigidbody 时，执行顺序和最终结果由策略作者负责。

**最终边界**：策略拥有完整运动计算权，Runtime 只拥有最终机械提交职责。Runtime 不解释贡献含义、不替策略选择 Clamp 空间，也不自动补调任何运动模块。

---

## 六、确认项汇总

| 编号 | 决策 | 状态 |
|------|------|------|
| A1/B2 | 无 DataProvider 仍执行 Simulate；双模式 | ✅ 已实施 |
| A2 | 撤销：Time.time 在 FixedUpdate 语义正确 | ❌ 撤销 |
| A3 | 预测位置取候选/当前实际速度，输入为零仍约束惯性 | ✅ 已实施 |
| A4 | 安全点 → 存档点（SetCheckpoint/RestoreCheckpoint） | ✅ 已实施 |
| A5 | 不改动：SteepSlopeSlideFactor=0 是显式关闭配置 | ❌ 不改动 |
| A6 | 撤销：沿重力方向投影的下落速度比较正确 | ❌ 撤销 |
| B1 | 不修改 | ❌ 不修改 |
| B3 | 边缘保护关闭时跳过稳定支撑检查 | ✅ 已实施 |
| B4 | 边缘保护开关控制全部边缘检测 | ✅ 已实施 |
| B5 | 不强改 collisionDetectionMode；FreezeRotation 面板开关 | ✅ 已实施 |
| B6 | 摄像机引用经契约注入黑板，空值回退 Camera.main | ✅ 已实施 |
| B7 | 不提供注入点（用户定稿） | ❌ 保持原样 |
| C1 | 直立等比胶囊圆形半径；其余保守 AABB | ✅ 已实施 |
| C2 | 维持现状 | ❌ 维持现状 |
| C3 | 撤销：SphereCast 距离已表示球底间隙 | ❌ 撤销 |
| C4 | 接地忽略窗口做成 JumpModule 面板参数 | ✅ 已实施 |
| C5 | 显式重新捕获基础形状入口 | ✅ 已实施 |
| D1 | 区域检测无有效命中时才中心射线兜底 | ✅ 已实施 |
| D2 | 初上陡坡只阻止上坡，后续再下滑 | ✅ 已实施 |
| D3 | 可行走坡面保持沿坡切向移动速度 | ✅ 已实施 |
| E1 | AirControl 归属基本移动参数，三通道调控 | ⚠️ 讨论定稿，待实施 |
| E2 | 空中模块冗余属性分发/删除，策略自定义空中规则 | ⚠️ 讨论定稿，待实施 |
| E3 | 受信任完整上下文，不做权限沙箱和逐方法包装 | ⚠️ 讨论定稿，待实施 |
| E4 | 策略定义行为并选择功能模块；Runtime 只执行与提交 | ⚠️ 讨论定稿，待实施 |
| E5 | 初始化/激活/停用/固定步/释放生命周期；切换即返回缓存实例 | ⚠️ 讨论定稿，待实施 |
| E6 | 策略可从 DataProvider/Blackboard 主动取得特殊输入 | ⚠️ 讨论定稿，待实施 |
| E7 | 普通运动贡献累加；策略最终 Clamp；特殊覆盖显式调用 | ⚠️ 讨论定稿，待实施 |

## 待办

- [ ] Unity 场景行为验证（跳跃、台阶、D1 窄支撑兜底、D1 高台阶前缘、D2 首次陡坡、D2 持续下滑、D3 不同坡度上坡/下坡/横切速度、窄桥、短缝、存档点恢复、FreezeRotation 开关、摄像机参考）
- [ ] E1-E7 空中行为与移动策略架构重构（AirControl 归属、空中属性分发、完整策略上下文、Runtime 职责收缩、策略生命周期、特殊输入、运动贡献累加/显式覆盖）

## 相关文档

- [[modules/UnitMover 重构方向]]
- [[modules/UnitMover重构检查]]
- [[modules/UnitMover 边缘防跌落方案]]
- [[decisions/DataProvider 架构审查决策]]
- [[_conventions/框架设计原则]]
