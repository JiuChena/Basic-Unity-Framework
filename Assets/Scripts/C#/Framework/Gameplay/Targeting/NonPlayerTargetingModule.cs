using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 非玩家单位索敌模块。
/// 用于敌人和中立单位的目标发现、目标保持和攻击前朝向修正。
/// </summary>
[DisallowMultipleComponent]
public sealed class NonPlayerTargetingModule : MonoBehaviour, IUnitTargetingProvider
{
    [SerializeField, Tooltip("非玩家单位可搜索到的目标层级。通过切换层级即可复用到敌人或中立单位。")] private LayerMask targetLayers = ~0;
    [SerializeField, Min(0f), Tooltip("发现新目标时使用的搜索半径。")] private float searchRadius = 18f;
    [SerializeField, Min(0f), Tooltip("已锁定目标后允许保持追踪的最大距离。")] private float loseTargetDistance = 24f;
    [SerializeField, Range(1f, 180f), Tooltip("发现新目标时使用的前方扇形半角。")] private float searchHalfAngle = 85f;
    [SerializeField, Tooltip("是否将 Trigger 碰撞体也纳入候选目标。")] private bool includeTriggerColliders = true;
    [SerializeField, Tooltip("是否仅允许敌对阵营目标进入候选列表。")] private bool requireEnemyAlignment = true;
    [SerializeField, Tooltip("在敌对阵营过滤开启时，是否允许将中立单位作为合法候选。")] private bool allowNeutralTargets;
    [SerializeField, Tooltip("是否优先用碰撞体表面离搜索点最近的位置作为瞄准点。")] private bool aimAtClosestSurfacePoint = true;
    [SerializeField, Tooltip("关闭最近表面瞄准后，是否改用碰撞体包围盒中心作为瞄准点。")] private bool aimAtColliderBoundsCenter = true;
    [SerializeField, Tooltip("瞄准点的额外世界偏移。")] private Vector3 aimPointOffset;
    [SerializeField, Min(0f), Tooltip("敌人在攻击状态中每隔多少秒刷新一次攻击转向与投射目标快照。设为 0 表示每帧刷新。")] private float attackRetargetInterval = 0.2f;
    [SerializeField, Tooltip("是否输出非玩家索敌运行时日志。")] private bool logTargetingRuntime;
    [SerializeField, Tooltip("选中该单位时是否绘制非玩家索敌半径、丢失距离和前方扇形调试 Gizmos。")] private bool drawDebugGizmos = true;

    private readonly Collider[] _overlapResults = new Collider[32];
    private readonly HashSet<int> _visitedTargetIds = new HashSet<int>();
    private readonly Dictionary<int, StatusData> _statusDataByColliderId = new Dictionary<int, StatusData>(32);

    private StatusData _trackedTargetData;
    private Transform _trackedTargetTransform;
    private Collider _trackedTargetCollider;
    private bool _hasLockedProjectileSnapshot;
    private ProjectileTargetingResult _lockedProjectileSnapshot;

    public float SearchRadius => Mathf.Max(0f, searchRadius);
    public float LoseTargetDistance => loseTargetDistance > 0f ? loseTargetDistance : SearchRadius;
    public float AttackRetargetInterval => Mathf.Max(0f, attackRetargetInterval);

    public bool TryResolveCombatTarget(StatusData ownerData, Vector3 searchOrigin, Vector3 facingDirection,
        out NonPlayerTargetingResult result)
    {
        return TryResolveBestTarget(ownerData, searchOrigin, facingDirection, true, out result);
    }

    public bool TryResolveProjectileTargeting(StatusData ownerData, Vector3 spawnPosition, Vector3 fallbackDirection,
        out ProjectileTargetingResult result, int targetingScopeId = 0)
    {
        result = default;
        if (ownerData == null)
            return false;

        if (_hasLockedProjectileSnapshot)
        {
            result = _lockedProjectileSnapshot;
            result.usesLockedSnapshot = true;
            return true;
        }

        if (!TryResolveTrackedTarget(ownerData, spawnPosition, fallbackDirection, out NonPlayerTargetingResult trackedResult) &&
            !TryResolveBestTarget(ownerData, spawnPosition, fallbackDirection, false, out trackedResult))
        {
            return false;
        }

        SetTrackedTarget(trackedResult);
        result = new ProjectileTargetingResult
        {
            targetData = trackedResult.targetData,
            targetTransform = trackedResult.targetTransform,
            targetCollider = trackedResult.targetCollider,
            aimPoint = trackedResult.aimPoint,
            launchDirection = trackedResult.launchDirection,
            usesLockedSnapshot = false,
        };
        return true;
    }

    public void LockProjectileTargetingSnapshot(ProjectileTargetingResult snapshot)
    {
        snapshot.usesLockedSnapshot = true;
        _lockedProjectileSnapshot = snapshot;
        _hasLockedProjectileSnapshot = snapshot.launchDirection.sqrMagnitude > 0.0001f;
    }

    public void ClearLockedProjectileTargetingSnapshot()
    {
        _hasLockedProjectileSnapshot = false;
        _lockedProjectileSnapshot = default;
    }

    public void SetTrackedTarget(NonPlayerTargetingResult result)
    {
        _trackedTargetData = result.targetData;
        _trackedTargetTransform = result.targetTransform;
        _trackedTargetCollider = result.targetCollider;
    }

    public void ClearTrackedTarget()
    {
        _trackedTargetData = null;
        _trackedTargetTransform = null;
        _trackedTargetCollider = null;
    }

    public bool IsTargetWithinLoseDistance(Transform targetTransform)
    {
        if (targetTransform == null)
            return false;

        float maxDistance = LoseTargetDistance;
        if (maxDistance <= 0f)
            return true;

        Vector3 offset = targetTransform.position - transform.position;
        offset.y = 0f;
        return offset.sqrMagnitude <= maxDistance * maxDistance;
    }

    public bool IsTargetWithinSearchRadius(Transform targetTransform)
    {
        if (targetTransform == null)
            return false;

        float maxDistance = SearchRadius;
        if (maxDistance <= 0f)
            return false;

        Vector3 offset = targetTransform.position - transform.position;
        offset.y = 0f;
        return offset.sqrMagnitude <= maxDistance * maxDistance;
    }

    private bool TryResolveTrackedTarget(StatusData ownerData, Vector3 searchOrigin, Vector3 fallbackDirection,
        out NonPlayerTargetingResult result)
    {
        result = default;
        if (_trackedTargetData == null || _trackedTargetTransform == null)
            return false;

        if (!IsCandidateAliveAndTargetable(_trackedTargetData))
        {
            ClearTrackedTarget();
            return false;
        }

        if (!PassesAlignmentFilter(ownerData, _trackedTargetData))
        {
            ClearTrackedTarget();
            return false;
        }

        if (!IsTargetWithinLoseDistance(_trackedTargetTransform))
        {
            return false;
        }

        Vector3 aimPoint = ResolveAimPoint(_trackedTargetCollider, _trackedTargetData, searchOrigin);
        Vector3 launchDirection = aimPoint - searchOrigin;
        if (launchDirection.sqrMagnitude <= 0.0001f)
            launchDirection = fallbackDirection;

        if (launchDirection.sqrMagnitude <= 0.0001f)
            return false;

        result = new NonPlayerTargetingResult
        {
            targetData = _trackedTargetData,
            targetTransform = _trackedTargetTransform,
            targetCollider = _trackedTargetCollider,
            aimPoint = aimPoint,
            launchDirection = launchDirection.normalized,
            sqrDistance = (_trackedTargetTransform.position - searchOrigin).sqrMagnitude,
        };
        return true;
    }

    private bool TryResolveBestTarget(StatusData ownerData, Vector3 searchOrigin, Vector3 facingDirection,
        bool requireFacingSector, out NonPlayerTargetingResult result)
    {
        result = default;
        if (ownerData == null || targetLayers.value == 0 || SearchRadius <= 0f)
            return false;

        QueryTriggerInteraction triggerInteraction = includeTriggerColliders
            ? QueryTriggerInteraction.Collide
            : QueryTriggerInteraction.Ignore;
        int hitCount = Physics.OverlapSphereNonAlloc(searchOrigin, SearchRadius, _overlapResults, targetLayers,
            triggerInteraction);
        if (hitCount <= 0)
            return false;

        Vector3 resolvedFacing = facingDirection;
        resolvedFacing.y = 0f;
        if (resolvedFacing.sqrMagnitude <= 0.0001f)
            resolvedFacing = transform.forward;
        if (resolvedFacing.sqrMagnitude <= 0.0001f)
            resolvedFacing = Vector3.forward;
        resolvedFacing.Normalize();

        float minDot = Mathf.Cos(Mathf.Clamp(searchHalfAngle, 1f, 180f) * Mathf.Deg2Rad);
        int bestTargetId = 0;
        float bestSqrDistance = float.MaxValue;
        StatusData bestTargetData = null;
        Transform bestTargetTransform = null;
        Collider bestTargetCollider = null;
        Vector3 bestAimPoint = Vector3.zero;

        _visitedTargetIds.Clear();
        for (int i = 0; i < hitCount; i++)
        {
            Collider candidateCollider = _overlapResults[i];
            if (candidateCollider == null)
                continue;

            StatusData candidateData = ResolveStatusData(candidateCollider);
            if (!IsCandidateValid(ownerData, candidateData))
                continue;

            int candidateId = candidateData.GetInstanceID();
            if (!_visitedTargetIds.Add(candidateId))
                continue;

            Vector3 aimPoint = ResolveAimPoint(candidateCollider, candidateData, searchOrigin);
            Vector3 flatDirection = aimPoint - searchOrigin;
            flatDirection.y = 0f;
            if (flatDirection.sqrMagnitude <= 0.0001f)
                continue;

            if (requireFacingSector && Vector3.Dot(resolvedFacing, flatDirection.normalized) < minDot)
                continue;

            float sqrDistance = flatDirection.sqrMagnitude;
            if (bestTargetData != null && sqrDistance >= bestSqrDistance)
                continue;

            bestTargetId = candidateId;
            bestSqrDistance = sqrDistance;
            bestTargetData = candidateData;
            bestTargetTransform = candidateData.transform;
            bestTargetCollider = candidateCollider;
            bestAimPoint = aimPoint;
        }

        if (bestTargetId == 0 || bestTargetData == null || bestTargetTransform == null)
            return false;

        Vector3 launchDirection = bestAimPoint - searchOrigin;
        if (launchDirection.sqrMagnitude <= 0.0001f)
            return false;

        result = new NonPlayerTargetingResult
        {
            targetData = bestTargetData,
            targetTransform = bestTargetTransform,
            targetCollider = bestTargetCollider,
            aimPoint = bestAimPoint,
            launchDirection = launchDirection.normalized,
            sqrDistance = bestSqrDistance,
        };

        if (logTargetingRuntime)
        {
            Debug.Log(
                $"[{name}] 非玩家索敌成功 | Target={bestTargetTransform.name} | Distance={Mathf.Sqrt(bestSqrDistance):F2} | RequireFacing={requireFacingSector}",
                this);
        }

        return true;
    }

    private bool IsCandidateValid(StatusData ownerData, StatusData candidateData)
    {
        if (candidateData == null || candidateData == ownerData)
            return false;

        if (!IsCandidateAliveAndTargetable(candidateData))
            return false;

        return PassesAlignmentFilter(ownerData, candidateData);
    }

    private static bool IsCandidateAliveAndTargetable(StatusData candidateData)
    {
        return candidateData != null && !candidateData.IsDead && candidateData.IsTargetable;
    }

    private bool PassesAlignmentFilter(StatusData ownerData, StatusData candidateData)
    {
        if (ownerData == null || candidateData == null)
            return false;

        if (!requireEnemyAlignment)
            return true;

        UnitAlignment ownerAlignment = ownerData.UnitAlignment;
        UnitAlignment candidateAlignment = candidateData.UnitAlignment;
        if (candidateAlignment == UnitAlignment.Neutral)
            return allowNeutralTargets;

        return ownerAlignment != candidateAlignment;
    }

    private Vector3 ResolveAimPoint(Collider collider, StatusData candidateData, Vector3 searchOrigin)
    {
        Vector3 aimPoint;
        if (aimAtClosestSurfacePoint && collider != null)
        {
            aimPoint = collider.ClosestPoint(searchOrigin);
        }
        else if (aimAtColliderBoundsCenter && collider != null)
        {
            aimPoint = collider.bounds.center;
        }
        else if (candidateData != null)
        {
            aimPoint = candidateData.transform.position;
        }
        else
        {
            aimPoint = searchOrigin;
        }

        return aimPoint + aimPointOffset;
    }

    private StatusData ResolveStatusData(Collider collider)
    {
        if (collider == null)
            return null;

        int colliderId = collider.GetInstanceID();
        if (_statusDataByColliderId.TryGetValue(colliderId, out StatusData cachedStatusData))
        {
            if (cachedStatusData != null)
                return cachedStatusData;

            _statusDataByColliderId.Remove(colliderId);
        }

        if (!UnitCombatResolver.TryResolveStatusData(collider, out StatusData resolvedStatusData, out bool canCache))
            return null;

        if (canCache)
            _statusDataByColliderId[colliderId] = resolvedStatusData;

        return resolvedStatusData;
    }

    private void OnDisable()
    {
        ClearLockedProjectileTargetingSnapshot();
        ClearTrackedTarget();
        _statusDataByColliderId.Clear();
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawDebugGizmos)
            return;

        Vector3 origin = transform.position;
        float resolvedSearchRadius = SearchRadius;
        if (resolvedSearchRadius > 0f)
        {
            Gizmos.color = new Color(1f, 0.85f, 0.15f, 0.95f);
            Gizmos.DrawWireSphere(origin, resolvedSearchRadius);
            DrawFacingSector(origin, resolvedSearchRadius);
        }

        float resolvedLoseDistance = LoseTargetDistance;
        if (resolvedLoseDistance > resolvedSearchRadius)
        {
            Gizmos.color = new Color(1f, 0.35f, 0.2f, 0.95f);
            Gizmos.DrawWireSphere(origin, resolvedLoseDistance);
        }

        if (_trackedTargetTransform == null)
            return;

        Vector3 trackedTargetPosition = _trackedTargetTransform.position;
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.95f);
        Gizmos.DrawLine(origin, trackedTargetPosition);
        Gizmos.DrawWireSphere(trackedTargetPosition, 0.18f);
    }

    private void DrawFacingSector(Vector3 origin, float radius)
    {
        Vector3 forward = transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude <= 0.0001f)
            forward = Vector3.forward;
        forward.Normalize();

        Vector3 left = Quaternion.AngleAxis(-searchHalfAngle, Vector3.up) * forward;
        Vector3 right = Quaternion.AngleAxis(searchHalfAngle, Vector3.up) * forward;

        Gizmos.color = new Color(1f, 0.55f, 0.1f, 0.95f);
        Gizmos.DrawLine(origin, origin + forward * radius);
        Gizmos.DrawLine(origin, origin + left * radius);
        Gizmos.DrawLine(origin, origin + right * radius);
    }
}
