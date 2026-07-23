using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public enum UnitTargetingPerspective
{
    AutoByAlignment = 0,
    PlayerLike = 1,
    EnemyLike = 2,
}

/// <summary>
/// UnitTargetingModule 的共享实现基类。
/// 运行时主入口应优先使用 UnitTargetingModule；旧 Character 命名组件仅作为兼容壳保留。
/// </summary>
[DisallowMultipleComponent]
public abstract class UnitTargetingModuleCore : MonoBehaviour, IUnitTargetingProvider
{
    [Header("Targeting")]
    [SerializeField, Tooltip("启用后，该单位发射投射物时会尝试执行一次性索敌方向修正。")]
    private bool enableProjectileTargeting = true;

    [SerializeField, Tooltip("索敌时允许搜索到的目标层级。开发者可按具体单位自行调整。")]
    private LayerMask targetLayers = ~0;

    [SerializeField, Tooltip("从投射物出生点开始的索敌半径，单位米。")]
    [Min(0f)]
    private float searchRadius = 30f;

    [SerializeField, Tooltip("启用后，索敌查询会把 Trigger 碰撞体也视为可选候选。单位的锁定体、受击体常常就是 Trigger，通常建议开启。")]
    private bool includeTriggerColliders = true;

    [SerializeField, Tooltip("启用后，仅会把敌对阵营单位视为有效索敌目标。")]
    private bool requireEnemyAlignment = true;

    [SerializeField, Tooltip("启用后会额外套用全局的“摄像机正对优先”索敌规则。玩家单位通常建议开启；敌人 AI 通常建议关闭，避免被玩家镜头方向影响索敌。")]
    private bool useCameraFacingPreference = true;

    [SerializeField, Tooltip("索敌视角策略。AutoByAlignment 时，友方/中立按玩家侧处理，敌方按敌人侧处理。")]
    private UnitTargetingPerspective targetingPerspective = UnitTargetingPerspective.AutoByAlignment;

    [SerializeField, Tooltip("敌人侧索敌是否优先保留自身正前方扇形内的目标；若扇形内没有候选则回退到全部候选。")]
    private bool preferOwnerFacingTargetsForEnemy = true;

    [SerializeField, Range(1f, 180f), Tooltip("敌人侧前向优先的半角范围。")]
    private float ownerFacingHalfAngle = 85f;

    [SerializeField, Tooltip("当启用敌对阵营筛选时，是否允许把中立单位视为有效目标。")]
    private bool allowNeutralTargets;

    [SerializeField, Tooltip("启用后，没有驱动脚本或未提供完整状态快照的 StatusData 仍可被索敌，但会作为最低优先级候选，只在没有更合适的正式战斗单位时才会被选中。")]
    private bool allowFallbackStatusTargets = true;

    [SerializeField, Tooltip("启用后优先瞄准目标碰撞体包围盒中心；关闭则瞄准目标根节点位置。")]
    private bool aimAtColliderBoundsCenter = true;

    [SerializeField, Tooltip("启用后优先取目标碰撞体相对于发射点最近的表面点作为瞄准点，可减少子弹明显向上飘的感觉。")]
    private bool aimAtClosestSurfacePoint = true;

    [SerializeField, Tooltip("最终瞄准点在基础目标点上的额外偏移。可用于微调瞄准胸口、头部等位置。")]
    private Vector3 aimPointOffset;

    [Header("Debug")]
    [SerializeField, Tooltip("启用后在选中该单位时绘制索敌范围、摄像机优先范围和最终目标。")]
    private bool drawDebugGizmos = true;

    [SerializeField, Tooltip("启用后会输出索敌成功/失败日志，便于快速定位为什么没有转向。")]
    private bool logTargetingRuntime;

    [SerializeField, Tooltip("最近一次索敌是否成功，仅用于 Inspector 调试观察。")]
    private bool runtimeLastResolveSucceeded;

    [SerializeField, Tooltip("最近一次索敌失败原因，仅用于 Inspector 调试观察。")]
    private string runtimeLastFailureReason = string.Empty;

    [SerializeField, Tooltip("最近一次查询命中的碰撞体数量，仅用于 Inspector 调试观察。")]
    private int runtimeLastOverlapHitCount;

    [SerializeField, Tooltip("最近一次通过过滤后的候选目标数量，仅用于 Inspector 调试观察。")]
    private int runtimeLastCandidateCount;

    [SerializeField, Tooltip("最近一次因未找到 StatusData 而被过滤的碰撞体数量，仅用于 Inspector 调试观察。")]
    private int runtimeRejectedMissingDataPanelCount;

    [SerializeField, Tooltip("最近一次因命中自身而被过滤的碰撞体数量，仅用于 Inspector 调试观察。")]
    private int runtimeRejectedSelfCount;

    [SerializeField, Tooltip("最近一次因目标已死亡而被过滤的碰撞体数量，仅用于 Inspector 调试观察。")]
    private int runtimeRejectedDeadCount;

    [SerializeField, Tooltip("最近一次因阵营不符合要求而被过滤的碰撞体数量，仅用于 Inspector 调试观察。")]
    private int runtimeRejectedAlignmentCount;

    [SerializeField, Tooltip("最近一次因属于兜底 StatusData 候选而被降级处理的数量，仅用于 Inspector 调试观察。")]
    private int runtimeDeprioritizedFallbackCount;

    [SerializeField, Tooltip("最近一次索敌时拥有者阵营，仅用于 Inspector 调试观察。")]
    [FormerlySerializedAs("runtimeLastOwnerAlignment")]
    private UnitAlignment runtimeLastOwnerUnitAlignment;

    [SerializeField, Tooltip("最近一次选中的目标名，仅用于 Inspector 调试观察。")]
    private string runtimeLastTargetName = string.Empty;

    [SerializeField, Tooltip("最近一次选中的目标阵营，仅用于 Inspector 调试观察。")]
    [FormerlySerializedAs("runtimeLastTargetAlignment")]
    private UnitAlignment runtimeLastTargetUnitAlignment;

    [SerializeField, Tooltip("最近一次索敌算出的发射方向，仅用于 Inspector 调试观察。")]
    private Vector3 runtimeLastLaunchDirection;

    [SerializeField, Tooltip("最近一次是否启用了摄像机正对候选优先，仅用于 Inspector 调试观察。")]
    private bool runtimeLastUsedCameraFocusedOnly;

    [SerializeField, Tooltip("最近一次是否仅在自身正前方候选中选目标，仅用于 Inspector 调试观察。")]
    private bool runtimeLastUsedOwnerFacingOnly;

    private readonly Collider[] _overlapResults = new Collider[32];
    private readonly HashSet<int> _visitedTargetIds = new HashSet<int>();
    private readonly List<TargetCandidate> _candidates = new List<TargetCandidate>(16);
    private readonly Dictionary<int, StatusData> _statusDataByColliderId = new Dictionary<int, StatusData>(32);
    private Transform _lastResolvedTarget;
    private Vector3 _lastResolvedAimPoint;
    private Vector3 _lastSpawnPosition;
    private bool _hadValidResultLastResolve;
    private readonly Dictionary<int, ScopedTargetCacheEntry> _scopedTargetCacheByOwnerId =
        new Dictionary<int, ScopedTargetCacheEntry>(4);
    private Camera _cachedMainCamera;

    internal bool RuntimeLastResolveSucceeded => runtimeLastResolveSucceeded;

    public bool TryResolveProjectileTargeting(StatusData ownerData, Vector3 spawnPosition, Vector3 fallbackDirection,
        out ProjectileTargetingResult result, int targetingScopeId = 0)
    {
        result = default;
        ResetRuntimeDebug(ownerData, spawnPosition);

        if (!enableProjectileTargeting)
            return Fail("索敌模块已关闭。");

        if (ownerData == null)
            return Fail("拥有者数据为空，无法执行索敌。");

        if (searchRadius <= 0f)
            return Fail("索敌半径小于等于 0。");

        if (targetLayers.value == 0)
            return Fail("TargetLayers 为空，没有任何可搜索层级。");

        BattleGlobalSettingsSO settings = GlobalConfigManager.Instance.BattleSettings;
        _lastSpawnPosition = spawnPosition;
        _lastResolvedTarget = null;
        _lastResolvedAimPoint = spawnPosition;
        _hadValidResultLastResolve = false;

        if (targetingScopeId > 0 &&
            TryResolveScopedCachedTarget(ownerData, spawnPosition, fallbackDirection, targetingScopeId, out result))
        {
            return CompleteResolveSuccess(result);
        }

        QueryTriggerInteraction triggerInteraction = includeTriggerColliders
            ? QueryTriggerInteraction.Collide
            : QueryTriggerInteraction.Ignore;
        int hitCount = Physics.OverlapSphereNonAlloc(spawnPosition, searchRadius, _overlapResults, targetLayers,
            triggerInteraction);
        runtimeLastOverlapHitCount = hitCount;
        if (hitCount <= 0)
            return Fail("索敌范围内没有命中任何碰撞体。请检查 TargetLayers、SearchRadius 和目标碰撞体层级。");

        _visitedTargetIds.Clear();
        _candidates.Clear();
        Camera mainCamera = GetMainCameraCached();
        UnitTargetingPerspective runtimePerspective = ResolveTargetingPerspective(ownerData);
        bool useCameraFacingPreferenceRuntime = runtimePerspective != UnitTargetingPerspective.EnemyLike &&
                                               settings.prioritizeCameraFacingTargets &&
                                               useCameraFacingPreference;
        float cameraDotThreshold = useCameraFacingPreferenceRuntime
            ? Mathf.Cos(settings.cameraFacingHalfAngle * Mathf.Deg2Rad)
            : -1f;
        bool useOwnerFacingPreferenceRuntime = runtimePerspective == UnitTargetingPerspective.EnemyLike &&
                                               preferOwnerFacingTargetsForEnemy;
        float ownerFacingDotThreshold = useOwnerFacingPreferenceRuntime
            ? Mathf.Cos(Mathf.Clamp(ownerFacingHalfAngle, 1f, 180f) * Mathf.Deg2Rad)
            : -1f;
        Vector3 ownerForward = transform.forward;
        if (ownerForward.sqrMagnitude <= 0.0001f)
            ownerForward = fallbackDirection.sqrMagnitude > 0.0001f ? fallbackDirection : Vector3.forward;

        for (int i = 0; i < hitCount; i++)
        {
            Collider collider = _overlapResults[i];
            if (collider == null)
                continue;

            StatusData candidateData = ResolveStatusData(collider);
            if (candidateData == null)
            {
                runtimeRejectedMissingDataPanelCount++;
                continue;
            }

            if (candidateData == ownerData)
            {
                runtimeRejectedSelfCount++;
                continue;
            }

            if (candidateData.IsDead)
            {
                runtimeRejectedDeadCount++;
                continue;
            }

            if (!candidateData.IsTargetable)
            {
                runtimeRejectedAlignmentCount++;
                continue;
            }

            int candidateId = candidateData.gameObject.GetInstanceID();
            if (!_visitedTargetIds.Add(candidateId))
                continue;

            if (!TryResolvePriorityTier(ownerData, candidateData, out TargetPriorityTier priorityTier))
            {
                runtimeRejectedAlignmentCount++;
                continue;
            }

            if (priorityTier == TargetPriorityTier.Fallback)
                runtimeDeprioritizedFallbackCount++;

            Vector3 candidateAimPoint = ResolveAimPoint(collider, candidateData, spawnPosition);
            float sqrDistance = (candidateAimPoint - spawnPosition).sqrMagnitude;
            float currentHealth = candidateData.CurrentHealth;
            bool isCameraFocused = IsCameraFocusedTarget(mainCamera, candidateAimPoint, cameraDotThreshold,
                out float viewportCenterScore);
            bool isOwnerFacing = IsOwnerFacingTarget(ownerForward, spawnPosition, candidateAimPoint,
                ownerFacingDotThreshold);

            _candidates.Add(new TargetCandidate
            {
                targetData = candidateData,
                targetTransform = candidateData.transform,
                targetCollider = collider,
                aimPoint = candidateAimPoint,
                sqrDistance = sqrDistance,
                currentHealth = currentHealth,
                isCameraFocused = isCameraFocused,
                isOwnerFacing = isOwnerFacing,
                viewportCenterScore = viewportCenterScore,
                priorityTier = priorityTier,
            });
        }

        runtimeLastCandidateCount = _candidates.Count;
        if (_candidates.Count == 0)
            return Fail("命中了碰撞体，但全部在自身/死亡/阵营过滤后被排除了。");

        int bestIndex = ResolveBestCandidateIndex(settings, useCameraFacingPreferenceRuntime, useOwnerFacingPreferenceRuntime);
        if (bestIndex < 0 || bestIndex >= _candidates.Count)
            return Fail("候选目标存在，但没有选出最终目标。");

        TargetCandidate bestCandidate = _candidates[bestIndex];
        runtimeLastUsedCameraFocusedOnly = useCameraFacingPreferenceRuntime &&
                                           HasCameraFocusedCandidates(bestCandidate.priorityTier);
        runtimeLastUsedOwnerFacingOnly = !runtimeLastUsedCameraFocusedOnly &&
                                         useOwnerFacingPreferenceRuntime &&
                                         HasOwnerFacingCandidates(bestCandidate.priorityTier);
        Vector3 direction = bestCandidate.aimPoint - spawnPosition;
        if (direction.sqrMagnitude <= 0.0001f)
            direction = fallbackDirection;

        if (direction.sqrMagnitude <= 0.0001f)
            return Fail("最终发射方向长度为 0。");

        result = new ProjectileTargetingResult
        {
            targetTransform = bestCandidate.targetTransform,
            targetData = bestCandidate.targetData,
            targetCollider = bestCandidate.targetCollider,
            aimPoint = bestCandidate.aimPoint,
            launchDirection = direction.normalized,
            usesLockedSnapshot = targetingScopeId > 0,
        };

        CacheScopedTargetIfNeeded(ownerData, targetingScopeId, result);
        return CompleteResolveSuccess(result);
    }

    private bool TryResolvePriorityTier(StatusData ownerData, StatusData candidateData, out TargetPriorityTier priorityTier)
    {
        priorityTier = TargetPriorityTier.Invalid;
        if (ownerData == null || candidateData == null)
            return false;

        bool isFallbackTarget = candidateData.UsesFallbackStatus;

        if (!requireEnemyAlignment)
        {
            if (isFallbackTarget)
            {
                if (!allowFallbackStatusTargets)
                    return false;

                priorityTier = TargetPriorityTier.Fallback;
                return true;
            }

            priorityTier = TargetPriorityTier.Normal;
            return true;
        }

        if (candidateData.UnitAlignment == ownerData.UnitAlignment &&
            candidateData.UnitAlignment != UnitAlignment.Neutral)
            return false;

        if (candidateData.UnitAlignment == UnitAlignment.Neutral)
        {
            if (!allowNeutralTargets && !isFallbackTarget)
                return false;

            if (isFallbackTarget)
            {
                if (!allowFallbackStatusTargets)
                    return false;

                priorityTier = TargetPriorityTier.Fallback;
                return true;
            }

            priorityTier = TargetPriorityTier.Normal;
            return true;
        }

        if (ownerData.UnitAlignment == candidateData.UnitAlignment)
            return false;

        if (isFallbackTarget)
        {
            if (!allowFallbackStatusTargets)
                return false;

            priorityTier = TargetPriorityTier.Fallback;
            return true;
        }

        priorityTier = TargetPriorityTier.Normal;
        return true;
    }

    private Vector3 ResolveAimPoint(Collider collider, StatusData candidateData, Vector3 spawnPosition)
    {
        Vector3 basePoint;
        if (aimAtClosestSurfacePoint && collider != null)
        {
            basePoint = collider.ClosestPoint(spawnPosition);
        }
        else if (aimAtColliderBoundsCenter && collider != null)
        {
            basePoint = collider.bounds.center;
        }
        else if (candidateData != null)
        {
            basePoint = candidateData.transform.position;
        }
        else
        {
            basePoint = transform.position;
        }

        return basePoint + aimPointOffset;
    }

    private bool TryResolveScopedCachedTarget(StatusData ownerData, Vector3 spawnPosition, Vector3 fallbackDirection,
        int targetingScopeId, out ProjectileTargetingResult result)
    {
        result = default;
        if (ownerData == null || targetingScopeId <= 0)
            return false;

        int ownerId = ownerData.gameObject.GetInstanceID();
        if (!_scopedTargetCacheByOwnerId.TryGetValue(ownerId, out ScopedTargetCacheEntry entry) ||
            entry.targetingScopeId != targetingScopeId)
        {
            return false;
        }

        runtimeLastCandidateCount = 1;
        result = entry.result;
        result.usesLockedSnapshot = true;
        return true;
    }

    private static bool IsCameraFocusedTarget(Camera camera, Vector3 aimPoint, float cameraDotThreshold,
        out float viewportCenterScore)
    {
        viewportCenterScore = 0f;
        if (camera == null || cameraDotThreshold < -0.5f)
        {
            viewportCenterScore = 1f;
            return true;
        }

        Vector3 viewportPoint = camera.WorldToViewportPoint(aimPoint);
        if (viewportPoint.z <= 0f)
            return false;

        if (viewportPoint.x < 0f || viewportPoint.x > 1f || viewportPoint.y < 0f || viewportPoint.y > 1f)
            return false;

        Vector3 cameraToTarget = aimPoint - camera.transform.position;
        if (cameraToTarget.sqrMagnitude <= 0.0001f)
            return false;

        Vector2 viewportOffset = new Vector2(viewportPoint.x - 0.5f, viewportPoint.y - 0.5f);
        float normalizedCenterDistance = viewportOffset.magnitude / 0.70710678f;
        viewportCenterScore = 1f - Mathf.Clamp01(normalizedCenterDistance);

        return Vector3.Dot(camera.transform.forward, cameraToTarget.normalized) >= cameraDotThreshold;
    }

    private int ResolveBestCandidateIndex(BattleGlobalSettingsSO settings, bool useCameraFacingPreferenceRuntime,
        bool useOwnerFacingPreferenceRuntime)
    {
        TargetPriorityTier activeTier = ResolveActivePriorityTier();
        bool useCameraFocusedOnly = useCameraFacingPreferenceRuntime && HasCameraFocusedCandidates(activeTier);
        bool useOwnerFacingOnly = !useCameraFocusedOnly &&
                                  useOwnerFacingPreferenceRuntime &&
                                  HasOwnerFacingCandidates(activeTier);

        float distanceWeight = 1f - Mathf.Clamp01(settings.projectileTargetPriorityWeight);
        float lowHealthWeight = Mathf.Clamp01(settings.projectileTargetPriorityWeight);
        float cameraCenterWeight = Mathf.Clamp01(settings.cameraCenterPreferenceWeight);

        float minSqrDistance = float.MaxValue;
        float maxSqrDistance = float.MinValue;
        float minHealth = float.MaxValue;
        float maxHealth = float.MinValue;

        for (int i = 0; i < _candidates.Count; i++)
        {
            TargetCandidate candidate = _candidates[i];
            if (candidate.priorityTier != activeTier)
                continue;

            if (useCameraFocusedOnly && !candidate.isCameraFocused)
                continue;

            if (useOwnerFacingOnly && !candidate.isOwnerFacing)
                continue;

            if (candidate.sqrDistance < minSqrDistance)
                minSqrDistance = candidate.sqrDistance;
            if (candidate.sqrDistance > maxSqrDistance)
                maxSqrDistance = candidate.sqrDistance;
            if (candidate.currentHealth < minHealth)
                minHealth = candidate.currentHealth;
            if (candidate.currentHealth > maxHealth)
                maxHealth = candidate.currentHealth;
        }

        int bestIndex = -1;
        float bestScore = float.MinValue;
        for (int i = 0; i < _candidates.Count; i++)
        {
            TargetCandidate candidate = _candidates[i];
            if (candidate.priorityTier != activeTier)
                continue;

            if (useCameraFocusedOnly && !candidate.isCameraFocused)
                continue;

            if (useOwnerFacingOnly && !candidate.isOwnerFacing)
                continue;

            float distanceScore = ResolveDescendingScore(candidate.sqrDistance, minSqrDistance, maxSqrDistance);
            float lowHealthScore = ResolveDescendingScore(candidate.currentHealth, minHealth, maxHealth);
            float blendedScore = distanceScore * distanceWeight + lowHealthScore * lowHealthWeight;

            if (useCameraFocusedOnly)
            {
                float viewportRadius = Mathf.Clamp(settings.cameraCenterViewportRadius, 0.05f, 0.5f) / 0.5f;
                float centerScore = Mathf.Clamp01(candidate.viewportCenterScore / Mathf.Max(0.0001f, viewportRadius));
                blendedScore += centerScore * cameraCenterWeight;
            }
            else if (candidate.isCameraFocused)
            {
                blendedScore += 0.001f;
            }

            if (blendedScore <= bestScore)
                continue;

            bestScore = blendedScore;
            bestIndex = i;
        }

        return bestIndex;
    }

    private TargetPriorityTier ResolveActivePriorityTier()
    {
        for (int i = 0; i < _candidates.Count; i++)
        {
            if (_candidates[i].priorityTier == TargetPriorityTier.Normal)
                return TargetPriorityTier.Normal;
        }

        return TargetPriorityTier.Fallback;
    }

    private bool HasOwnerFacingCandidates(TargetPriorityTier tier)
    {
        for (int i = 0; i < _candidates.Count; i++)
        {
            TargetCandidate candidate = _candidates[i];
            if (candidate.priorityTier == tier && candidate.isOwnerFacing)
                return true;
        }

        return false;
    }

    private bool HasCameraFocusedCandidates(TargetPriorityTier tier)
    {
        for (int i = 0; i < _candidates.Count; i++)
        {
            TargetCandidate candidate = _candidates[i];
            if (candidate.priorityTier == tier && candidate.isCameraFocused)
                return true;
        }

        return false;
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

    private Camera GetMainCameraCached()
    {
        if (_cachedMainCamera == null)
            _cachedMainCamera = Camera.main;

        return _cachedMainCamera;
    }

    private void CacheScopedTargetIfNeeded(StatusData ownerData, int targetingScopeId, ProjectileTargetingResult result)
    {
        if (ownerData == null || targetingScopeId <= 0)
            return;

        int ownerId = ownerData.gameObject.GetInstanceID();
        result.usesLockedSnapshot = true;
        _scopedTargetCacheByOwnerId[ownerId] = new ScopedTargetCacheEntry
        {
            targetingScopeId = targetingScopeId,
            result = result,
        };
    }

    private void ClearScopedTargetCache()
    {
        _scopedTargetCacheByOwnerId.Clear();
    }

    private void OnDisable()
    {
        ClearScopedTargetCache();
        _statusDataByColliderId.Clear();
        _cachedMainCamera = null;
    }

    private bool CompleteResolveSuccess(ProjectileTargetingResult result)
    {
        _lastResolvedTarget = result.targetTransform;
        _lastResolvedAimPoint = result.aimPoint;
        _hadValidResultLastResolve = true;
        runtimeLastResolveSucceeded = true;
        runtimeLastFailureReason = string.Empty;
        runtimeLastTargetName = result.targetTransform != null ? result.targetTransform.name : string.Empty;
        runtimeLastTargetUnitAlignment = result.targetData != null
            ? result.targetData.UnitAlignment
            : UnitAlignment.Neutral;
        runtimeLastLaunchDirection = result.launchDirection;

        if (logTargetingRuntime)
        {
            Debug.Log(
                $"[{name}] 索敌成功 | Owner={runtimeLastOwnerUnitAlignment} | Target={runtimeLastTargetName}({runtimeLastTargetUnitAlignment}) | Overlap={runtimeLastOverlapHitCount} | Candidates={runtimeLastCandidateCount} | CameraFocusedOnly={runtimeLastUsedCameraFocusedOnly} | Direction={runtimeLastLaunchDirection}",
                this);
        }

        return true;
    }

    private void ResetRuntimeDebug(StatusData ownerData, Vector3 spawnPosition)
    {
        runtimeLastResolveSucceeded = false;
        runtimeLastFailureReason = string.Empty;
        runtimeLastOverlapHitCount = 0;
        runtimeLastCandidateCount = 0;
        runtimeRejectedMissingDataPanelCount = 0;
        runtimeRejectedSelfCount = 0;
        runtimeRejectedDeadCount = 0;
        runtimeRejectedAlignmentCount = 0;
        runtimeDeprioritizedFallbackCount = 0;
        runtimeLastOwnerUnitAlignment = ownerData != null ? ownerData.UnitAlignment : UnitAlignment.Neutral;
        runtimeLastTargetName = string.Empty;
        runtimeLastTargetUnitAlignment = UnitAlignment.Neutral;
        runtimeLastLaunchDirection = Vector3.zero;
        runtimeLastUsedCameraFocusedOnly = false;
        runtimeLastUsedOwnerFacingOnly = false;
        _lastSpawnPosition = spawnPosition;
    }

    private bool Fail(string reason)
    {
        runtimeLastResolveSucceeded = false;
        runtimeLastFailureReason = reason;
        runtimeLastTargetName = string.Empty;
        runtimeLastTargetUnitAlignment = UnitAlignment.Neutral;
        runtimeLastLaunchDirection = Vector3.zero;
        runtimeLastUsedOwnerFacingOnly = false;

        if (logTargetingRuntime)
            Debug.LogWarning($"[{name}] 索敌失败 | {reason}", this);

        return false;
    }

    private static float ResolveDescendingScore(float value, float minValue, float maxValue)
    {
        if (Mathf.Approximately(minValue, maxValue))
            return 1f;

        return 1f - Mathf.InverseLerp(minValue, maxValue, value);
    }

    private UnitTargetingPerspective ResolveTargetingPerspective(StatusData ownerData)
    {
        if (targetingPerspective != UnitTargetingPerspective.AutoByAlignment)
            return targetingPerspective;

        if (ownerData == null)
            return UnitTargetingPerspective.PlayerLike;

        return ownerData.UnitAlignment == UnitAlignment.Enemy
            ? UnitTargetingPerspective.EnemyLike
            : UnitTargetingPerspective.PlayerLike;
    }

    private static bool IsOwnerFacingTarget(Vector3 ownerForward, Vector3 spawnPosition, Vector3 aimPoint,
        float ownerFacingDotThreshold)
    {
        if (ownerFacingDotThreshold < -0.5f)
            return true;

        Vector3 flatOwnerForward = ownerForward;
        flatOwnerForward.y = 0f;
        if (flatOwnerForward.sqrMagnitude <= 0.0001f)
            return true;

        Vector3 flatToTarget = aimPoint - spawnPosition;
        flatToTarget.y = 0f;
        if (flatToTarget.sqrMagnitude <= 0.0001f)
            return true;

        return Vector3.Dot(flatOwnerForward.normalized, flatToTarget.normalized) >= ownerFacingDotThreshold;
    }

    private struct TargetCandidate
    {
        public StatusData targetData;
        public Transform targetTransform;
        public Collider targetCollider;
        public Vector3 aimPoint;
        public float sqrDistance;
        public float currentHealth;
        public bool isCameraFocused;
        public bool isOwnerFacing;
        public float viewportCenterScore;
        public TargetPriorityTier priorityTier;
    }

    private enum TargetPriorityTier
    {
        Invalid = 0,
        Normal = 1,
        Fallback = 2,
    }

    private struct ScopedTargetCacheEntry
    {
        public int targetingScopeId;
        public ProjectileTargetingResult result;
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawDebugGizmos)
            return;

        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, Mathf.Max(0f, searchRadius));

        if (_hadValidResultLastResolve)
        {
            Gizmos.color = new Color(1f, 0.35f, 0.15f, 0.95f);
            Gizmos.DrawLine(_lastSpawnPosition, _lastResolvedAimPoint);
            Gizmos.DrawSphere(_lastResolvedAimPoint, 0.08f);

            if (_lastResolvedTarget != null)
            {
                Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.85f);
                Gizmos.DrawLine(transform.position, _lastResolvedTarget.position);
                Gizmos.DrawWireSphere(_lastResolvedTarget.position, 0.16f);
            }
        }

        Camera mainCamera = Camera.main;
        GlobalConfigManager configManager = Application.isPlaying
            ? GlobalConfigManager.Instance
            : FindFirstObjectByType<GlobalConfigManager>(FindObjectsInactive.Include);
        if (configManager == null)
            return;

        BattleGlobalSettingsSO settings = Application.isPlaying
            ? configManager.BattleSettings
            : configManager.ConfiguredBattleSettings;
        if (settings == null)
            return;

        if (mainCamera == null || !settings.prioritizeCameraFacingTargets || !useCameraFacingPreference)
            return;

        DrawCameraFacingCone(mainCamera, settings.cameraFacingHalfAngle,
            Mathf.Min(searchRadius, 4f));
    }

    private static void DrawCameraFacingCone(Camera camera, float halfAngle, float rayLength)
    {
        if (camera == null)
            return;

        Transform cameraTransform = camera.transform;
        Vector3 origin = cameraTransform.position;
        Vector3 forward = cameraTransform.forward;
        Vector3 up = cameraTransform.up;
        Vector3 right = cameraTransform.right;

        Quaternion yawLeft = Quaternion.AngleAxis(-halfAngle, up);
        Quaternion yawRight = Quaternion.AngleAxis(halfAngle, up);
        Quaternion pitchUp = Quaternion.AngleAxis(-halfAngle, right);
        Quaternion pitchDown = Quaternion.AngleAxis(halfAngle, right);

        Gizmos.color = new Color(0.4f, 1f, 0.4f, 0.85f);
        Gizmos.DrawLine(origin, origin + forward * rayLength);
        Gizmos.DrawLine(origin, origin + yawLeft * forward * rayLength);
        Gizmos.DrawLine(origin, origin + yawRight * forward * rayLength);
        Gizmos.DrawLine(origin, origin + pitchUp * forward * rayLength);
        Gizmos.DrawLine(origin, origin + pitchDown * forward * rayLength);
    }
}
