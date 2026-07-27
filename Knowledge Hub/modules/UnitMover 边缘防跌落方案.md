---
tags: [UnitMover, 边缘检测, 悬崖, 跌落保护, 重构]
created: 2026-07-27
status: 已确认
---

# UnitMover 边缘防跌落方案

## 目标与边界

本方案用于阻止单位在可站立状态下因主动移动、惯性或常规物理推力走出超过允许落差的边缘，并在保护失效时回退到最近的可靠落脚点。

它不使用空气墙，也不将 `UnitMover` 改造成完整的运动学角色控制器。边缘保护只负责限制水平移动与回退异常跌落；跳跃、空中控制、击退和坠落伤害等业务规则仍由策略或上层系统决定。

完整策略分为三层：

```text
预测支撑检测
  -> 边缘速度约束与沿边候选
  -> 无有效落脚点时的安全位置回退
```

## 当前实现问题

当前 `ApplyLedgeCheck` 仅沿输入方向进行单点向下射线检测。检测到悬崖后直接返回零方向：

```text
玩家 -> 单点前探 -> 探到悬崖 -> return Vector3.zero -> 硬停
```

该行为存在以下问题：

- 单点无法覆盖胶囊或 BoxCollider 的真实脚底占地，可能漏过窄裂缝和斜向边缘。
- 仅清除目标输入，不能清除 Rigidbody 已有的向悬崖速度；冲刺、斜坡惯性或外力仍可能让单位跌落。
- 输入方向被清零后不能可靠地沿边移动。
- 检测异常或高速跨出边缘时没有兜底恢复。

## 核心术语

### 预测支撑

根据目标水平速度、当前水平速度和本物理步时长，计算角色下一物理步可能达到的前缘位置，并在该位置向下检查是否存在可站立地面。

```text
预测距离 = max(目标水平速度, 当前水平速度) * fixedDeltaTime
         + 碰撞体前缘半径
         + skinWidth
```

其中碰撞体前缘半径必须由当前 `movementCollider` 的实际世界空间尺寸计算，自动兼容浮动胶囊体改变后的中心和高度；不得使用固定的 `ledgeProbeDistance`。

### 边缘外法线

`edgeOutNormal` 是水平面内指向无支撑区域的单位向量，不是 `Vector3.down`。它表示禁止继续运动的方向。

```text
安全平台                    无支撑区域
----------- 边缘 ----------------------
                 -> edgeOutNormal

edgeTangent = Cross(Vector3.up, edgeOutNormal)
```

`edgeTangent` 是沿边的候选方向。最终是否允许沿边移动，仍由预测支撑检测确认。

### 完全安全位置

可用于回退的位置必须同时满足：

- 当前处于可行走地面，且不处于跳跃豁免期。
- 当前位置脚底支撑满足稳定规则，不是仅依赖 `IsGrounded`。
- 未处于边缘保护的危险状态。

不能在角色半悬空、只有胶囊边缘仍触地时覆盖安全位置，否则回退会重复落在危险边缘。

## 配置参数

```csharp
[Header("边缘保护")]
public bool ledgeCheckEnabled = true;

[Tooltip("允许自然走下或落下的最大高度；超过该范围仍无有效地面时视为危险。")]
[Min(0f)] public float maxFallHeight = 2f;

[Header("跌落保护")]
[Tooltip("启用后，异常跌出 maxFallHeight 范围且没有有效落脚点时回退到最近完全安全位置。")]
public bool fallRecoveryEnabled = true;

[Tooltip("启用时，Jump 及其空中阶段不触发回退；关闭后，所有无有效落脚点的跌落都可回退。")]
public bool recoverUnexpectedFallsOnly = true;

[Tooltip("允许中间暂时无支撑、但后方重新出现稳定地面的最大水平缝隙宽度。0 表示不允许跨越无支撑缝隙。")]
[Min(0f)] public float maxBridgeableGapWidth = 0.15f;
```

框架默认不公开射线数量、固定前探距离和最小安全弧角度等实现细节：

- 常态探测点由碰撞体形状推导。
- 仅在预测支撑失败时执行局部边缘定位。
- 内部采样数量作为私有常量维护；未来确有性能档位需求时再由具体 Provider 或策略暴露。
- `maxBridgeableGapWidth` 是水平跨越语义，不与垂直落差 `maxFallHeight` 混用。默认值应小于角色实际脚底直径，并可按玩法调低或关闭。

## 支撑判定

### 支撑结果

预测支撑不是二元结果。每一个横向支撑截面都返回以下三种状态之一：

```text
Stable         : 满足最低支撑规则，可安全移动或记录安全位置
Unstable       : 仍有少量命中，但整体支撑不足
Unsupported    : 没有可用支撑，或命中均不满足有效地面规则
```

后续悬崖判定关注的是“是否重新出现 `Stable` 截面”，而不是要求每一根射线都没有命中。单个边缘点命中并不代表角色仍有足够支撑。

### 有效地面规则

一次向下检测命中后，只有同时满足以下条件才可视为支撑：

- 命中 Collider 位于 `groundLayer`。
- 忽略 Trigger，并过滤自身及其子物体 Collider。
- 命中表面坡度不超过 `slopeLimit`。
- 落点相对当前脚底的下落高度不超过 `maxFallHeight`。

现有 `TryGetGroundRay`、`IsGroundCollider` 和 `IsWalkable` 的层过滤、Trigger 过滤与坡度规则继续复用。

### 常态：预测前缘三点支撑

当角色已接地且存在水平移动时，以原始候选方向建立水平前向 `forward` 与右向 `right`，在预测前缘检测三个点：

```text
左前 = 前缘中心 - right * 横向偏移
中前 = 前缘中心
右前 = 前缘中心 + right * 横向偏移
```

三点都从预测位置上方向下检测有效地面。默认稳定规则为：

```text
中前有支撑
且左前 / 右前至少一个有支撑
```

该规则允许角色自然沿直边移动，同时避免胶囊前方完全越出平台。对于宽体 `BoxCollider`，扩展为五点：左外、左内、中、右内、右外，并由具体碰撞体形状定义最小支撑数量。

任何候选方向都必须执行该检测，包括后续投影出的沿边方向。

### 深窄缝隙：可跨越无支撑区

前缘首次得到 `Unstable` 或 `Unsupported` 时，不能立即判为悬崖。深但窄的地砖缝、桥面接缝或断裂装饰会让单点向下射线完全穿过缝底，但角色实际脚底仍可跨越。

此时沿原始候选移动方向，在 `maxBridgeableGapWidth` 的有限窗口内继续检查横向支撑截面：

```text
Stable -> Unstable / Unsupported -> Stable
  平台 A          窄缝           平台 B
```

只有同时满足以下条件，才返回 `BridgeableGap`：

- 首次失去稳定支撑后，在 `maxBridgeableGapWidth` 范围内重新找到 `Stable` 截面。
- 缝隙出口截面的地面满足有效地面规则和最低支撑规则，不能只是命中一小块边角。
- 使用角色实际脚底体积进行宽体向下确认，证明碰撞体仍能覆盖并接触可行走地面。

若到达最大可跨越宽度仍没有重新找到 `Stable` 截面，则返回 `Unsupported`，并按悬崖处理。缝隙深度本身不参与“可跨越缝隙”判断；它只影响跌落保护的深度回退逻辑。

### 缝隙宽体确认

胶囊角色在预测位置使用底部半球进行向下 `SphereCast`；其起点和半径均从当前实际 `CapsuleCollider` 世界空间尺寸计算：

```text
sphereOrigin = 预测胶囊的底部半球中心 + 向上起始偏移
sphereRadius = 实际胶囊世界空间半径 - skinWidth - 安全边距
```

`SphereCast` 只负责确认角色真实脚底体积仍可接触支撑，不能替代多点支撑截面：它可能擦到远端平台边角，且其命中法线不适合直接用作边缘法线。对于 `BoxCollider`，使用缩小后的 `BoxCast` 或对应的宽体支撑检测。

## 边缘方向定位与速度约束

### 失败时局部定位

仅当缝隙扫描确认结果为 `Unsupported` 时，才在角色脚底边界附近进行 6 或 8 个方向的局部支撑采样。每个方向记录是否有有效支撑，并将无支撑方向进行加权求和：

```csharp
Vector3 hazardSum = Vector3.zero;

foreach (ProbeSample sample in samples)
{
    if (!sample.HasWalkableSupport)
        hazardSum += sample.Direction * sample.Weight;
}

Vector3 edgeOutNormal = Vector3.ProjectOnPlane(hazardSum, Vector3.up).normalized;
```

无支撑样本集中在右前方时，`edgeOutNormal` 指向右前方；无支撑样本分布在多个方向时，它指向凸角外侧。

若危险样本过少、`hazardSum` 长度不足或没有足够置信度，不猜测边缘切线：直接拒绝当前危险候选方向，仅允许经过完整支撑检测的候选方向。

### 清除向悬崖外的速度

仅修改输入方向不足以阻止跌落。边缘保护必须同时约束目标水平速度与 Rigidbody 当前水平速度：

```csharp
private static Vector3 RemoveOutwardComponent(Vector3 velocity, Vector3 edgeOutNormal)
{
    float outwardSpeed = Vector3.Dot(velocity, edgeOutNormal);
    return outwardSpeed > 0f
        ? velocity - edgeOutNormal * outwardSpeed
        : velocity;
}
```

处理顺序：

1. 从目标移动得到 `targetVelocity`，从 Rigidbody 得到 `currentHorizontalVelocity`。
2. 前缘预测支撑与缝隙扫描确认结果为 `Unsupported` 后，定位 `edgeOutNormal`。
3. 对 `targetVelocity` 与 `currentHorizontalVelocity` 都执行 `RemoveOutwardComponent`。
4. 立即通过 `VelocityChange` 或等价方式移除刚体已有的外向速度，不能只等待减速度慢慢消耗。
5. 用投影后的 `targetVelocity` 再次执行预测支撑检测。

### 沿边候选

若投影后的目标速度仍不安全，计算：

```csharp
Vector3 edgeTangent = Vector3.Cross(Vector3.up, edgeOutNormal).normalized;
```

依次检查 `edgeTangent` 与 `-edgeTangent` 两个候选速度。只在预测支撑检测通过时允许移动；两个候选都不安全时，目标水平速度归零，同时保留已被剔除外向分量后的当前速度。

若两个候选都可行，选择与原始输入方向点积更大的方向，保证结果尽可能符合玩家意图。

## 跌落保护与安全位置回退

### 安全快照

`UnitMover` 私有维护最近完全安全位置，不向业务层暴露可随意写入的字段：

```csharp
private struct SafePositionSnapshot
{
    public bool IsValid;
    public Vector3 Position;
    public Quaternion Rotation;
    public Vector3 GroundNormal;
}
```

当角色稳定接地且脚底支撑通过时更新快照。运行时不产生 GC。

对于移动平台，后续扩展应额外保存支撑 Collider 或 Rigidbody 的相对位置与旋转；回退时基于平台当前 Transform 还原世界位置，避免平台移动后回到历史世界坐标。

### 武装与跳跃豁免

跌落保护只在存在有效安全快照且已武装时工作：

```text
稳定接地并更新安全快照 -> Armed
调用 Jump -> Disarmed
重新稳定接地 -> Armed
```

`recoverUnexpectedFallsOnly = true` 时，跳跃及其空中阶段不触发回退。这样不会破坏跳跃、二段跳和空中移动。

已验证的 `BridgeableGap` 也是临时豁免状态。穿越期间脚下可能短暂没有普通接地命中，因此需保存缝隙出口或最大穿越范围：

```text
已验证 BridgeableGap -> 暂不触发深度回退
重新获得 Stable 接地 -> 清除缝隙豁免
偏离出口方向、超出最大宽度或超时仍未接地 -> 清除豁免，按普通跌落处理
```

豁免只用于已验证存在稳定出口的短缝，不能因单次无支撑检测无限延长。

是否将击退、爆炸或其他外部推力也纳入回退，由上层规则决定：

- `recoverUnexpectedFallsOnly = true`：默认框架行为，仅保护主动行走导致的异常跌落。
- `recoverUnexpectedFallsOnly = false`：所有无有效落脚点的跌落都允许回退。

### 深度检测与回退

普通接地检测只覆盖 `hoverHeight + groundProbeDistance`，不能作为跌落回退判断。角色离开支撑面后，应从当前实际碰撞体向下进行深度检测：

```text
检测距离 = hoverHeight + maxFallHeight + skinWidth
```

处理规则：

```text
未正常接地
  -> 在 maxFallHeight 范围内找到有效可站立地面：允许自然落到较低台阶或坡面
  -> 范围内没有有效落脚点，且保护已 Armed：回退最近完全安全位置
```

执行回退时必须：

```text
Rigidbody.position = SafePositionSnapshot.Position
Rigidbody.rotation = SafePositionSnapshot.Rotation
Rigidbody.velocity = Vector3.zero
Rigidbody.angularVelocity = Vector3.zero
清空当前物理步移动命令
```

回退成功后应提前结束当前物理步，避免同一帧继续施加重力、台阶辅助或旧的移动命令。

## UnitMover 执行顺序

建议的固定物理步流程：

```text
UpdateGroundState
  -> 更新完全安全位置，或检查深度跌落并执行回退
  -> ExecuteStrategy
  -> ApplyJumpRequest
  -> ApplyStepAssist
  -> ApplyVerticalForces
  -> ApplyHorizontalForces
       -> 预测前缘支撑
       -> 失败时扫描 maxBridgeableGapWidth 内的稳定出口，并做宽体确认
       -> BridgeableGap：允许原候选移动并设置短暂回退豁免
       -> Unsupported：定位边缘外法线，投影目标速度与当前水平速度
       -> Unsupported：验证沿边候选
  -> ClearStepCommands
```

`ApplyLedgeCheck` 可以继续保留为受保护的方向过滤入口；应新增内部速度约束与跌落回退方法，而不是让单个方法承担所有物理状态修改。

建议的内部职责划分：

```text
TryEvaluatePredictedSupport  : 评估指定候选速度的前缘支撑
TryEvaluateBridgeableGap     : 扫描短无支撑区，确认稳定出口与宽体可跨越性
TryResolveEdgeOutNormal      : 在失败时定位无支撑区域方向
ApplyLedgeVelocityConstraint : 删除目标与当前速度的外向分量，并选择沿边候选
UpdateSafePosition            : 更新最近完全安全快照
TryRecoverUnexpectedFall      : 深度无落脚点时回退安全快照
```

## 性能策略

| 状态 | 物理查询 | 说明 |
|---|---:|---|
| 已接地且无水平移动 | 复用现有接地检测 | 不做边缘扫描 |
| 已接地且正常移动 | 3 次前缘向下检测 | 主要路径 |
| 前缘检测失败 | 少量前向截面检测 + 1 次宽体向下 Cast | 先区分深窄缝与悬崖 |
| 确认不可跨越 | 额外 6 或 8 次局部下探 | 仅在真实边缘附近定位方向 |
| 已离地且保护已武装 | 1 次深度碰撞体向下 Cast | 仅作跌落兜底 |

所有查询复用既有预分配命中缓冲区，并使用 `QueryTriggerInteraction.Ignore`。不使用每帧固定全周 16/32/64 条射线，也不公开固定前探距离等与速度脱节的配置。

## 编辑器预览

在 `OnDrawGizmosSelected` 中绘制：

- 预测前缘的左、中、右支撑点及向下检测线。
- 支撑有效点为绿色，无支撑点为红色。
- `BridgeableGap` 激活时绘制失去支撑区、稳定出口截面、最大可跨越宽度和宽体 Cast。
- 局部边缘定位激活时的危险样本与 `edgeOutNormal`。
- 投影后的最终移动方向，以及两个沿边候选方向。
- 最近完全安全位置；在回退发生后短暂标记回退起点与落点。

浮动胶囊体预览继续使用现有 Gizmos 绘制逻辑；边缘检测预览必须基于实际参与物理的当前 Collider 尺寸。

## 实施顺序

1. 以当前 `movementCollider` 的真实世界尺寸实现可复用的有效地面检测与预测前缘三点检测。
2. 将 `ApplyLedgeCheck` 从单点硬停改为候选方向支撑判定。
3. 新增局部边缘定位、目标速度投影和 Rigidbody 外向速度剔除。
4. 新增完全安全位置快照、跳跃豁免武装状态与深度回退逻辑。
5. 新增可跨越深窄缝扫描、底部宽体确认及缝隙穿越期间的跌落回退豁免。
6. 接入 Gizmos，并验证直边、斜边、凸角、深窄缝、窄桥、台阶、浮动胶囊、跳跃、移动平台与外力推挤场景。
