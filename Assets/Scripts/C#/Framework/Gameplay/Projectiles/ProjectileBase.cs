using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 投射物统一基类。
/// 负责生命周期、碰撞处理、命中特效和伤害结算；具体运动方式由子类实现。
/// </summary>
[DisallowMultipleComponent]
public abstract class ProjectileBase : MonoBehaviour, IProjectileLaunchHandler, BehaviorCore.IBehaviorProjectileContract
{
    [Header("Lifecycle")]
    [SerializeField, Tooltip("投射物最大存活时间，超时自动回收。")]
    [Min(0.01f)]
    private float maxLifetime = 5f;

    [SerializeField, Tooltip("命中后是否立即回收。关闭后可用于穿透弹或多段命中弹。")]
    private bool recycleOnHit = true;

    [Header("Collision")]
    [SerializeField, Tooltip("允许命中的目标层级。")]
    private LayerMask targetLayers = ~0;

    [SerializeField, Tooltip("需要忽略的目标层级。优先于目标层级过滤。")]
    private LayerMask ignoredLayers;

    [Header("Impact VFX")]
    [SerializeField, Tooltip("按目标层级匹配的命中特效表。按列表顺序优先匹配。")]
    private List<ProjectileImpactVfxEntry> impactVfxEntries = new List<ProjectileImpactVfxEntry>();

    [SerializeField, Tooltip("命中特效沿碰撞法线向外偏移的距离，避免嵌入目标表面。")]
    [Min(0f)]
    private float impactSurfaceOffset = 0.02f;

    public ProjectileLaunchContext LaunchContext { get; private set; }
    public float CurrentSpeed { get; private set; }
    public bool IsLaunched { get; private set; }
    protected Vector3 CurrentDirection { get; private set; }

    protected Rigidbody ProjectileRigidbody { get; private set; }
    private Transform _ownerRoot;
    private float _remainingLifetime;
    private bool _hasRecycled;
    private readonly HashSet<int> _hitTargetIds = new HashSet<int>();
    private Vector3 _initialLocalScale = Vector3.one;

    protected virtual void Awake()
    {
        _initialLocalScale = transform.localScale;
        ProjectileRigidbody = GetComponent<Rigidbody>();

        if (ProjectileRigidbody != null)
        {
            ProjectileRigidbody.useGravity = false;
            ProjectileRigidbody.isKinematic = true;
            ProjectileRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            ProjectileRigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        }
    }

    protected virtual void OnDisable()
    {
        ResetRuntimeState();
    }

    protected virtual void Update()
    {
        if (!IsLaunched || _hasRecycled)
            return;

        _remainingLifetime -= Time.deltaTime;
        if (_remainingLifetime <= 0f)
        {
            RecycleSelf();
            return;
        }
    }

    protected virtual void FixedUpdate()
    {
        if (!IsLaunched || _hasRecycled)
            return;

        Vector3 startPosition = GetCurrentPosition();
        MoveProjectile(Time.fixedDeltaTime, CurrentSpeed);
        Vector3 endPosition = GetCurrentPosition();
        SweepForHits(startPosition, endPosition);
    }

    public virtual void Launch(ProjectileLaunchContext context)
    {
        LaunchContext = context;
        CurrentSpeed = Mathf.Max(0f, context.defaultSpeed);
        _remainingLifetime = maxLifetime;
        _hasRecycled = false;
        IsLaunched = true;
        _hitTargetIds.Clear();
        _ownerRoot = context.ownerData != null ? context.ownerData.transform : null;
        SetTravelDirection(ResolveInitialDirection(context), false);

        // ObjectsPool keeps world transform state from the previous use.
        // Restore the prefab-authored local scale before computing the launch pose.
        transform.localScale = _initialLocalScale;

        Quaternion launchRotation = CurrentDirection.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(CurrentDirection, Vector3.up)
            : context.rotation;
        transform.SetPositionAndRotation(context.position, launchRotation);

        if (ProjectileRigidbody != null)
        {
            ProjectileRigidbody.velocity = Vector3.zero;
            ProjectileRigidbody.angularVelocity = Vector3.zero;
        }
    }

    protected abstract void MoveProjectile(float deltaTime, float speed);

    protected void SetTravelDirection(Vector3 direction, bool updateRotation = true)
    {
        if (direction.sqrMagnitude <= 0.0001f)
            return;

        CurrentDirection = direction.normalized;
        if (updateRotation)
            transform.rotation = Quaternion.LookRotation(CurrentDirection, Vector3.up);
    }

    protected virtual void OnProjectileHit(Collider hitCollider, Vector3 hitPoint, Vector3 hitNormal, StatusData targetData, float damage)
    {
    }

    protected virtual bool CanHitCollider(Collider hitCollider)
    {
        if (hitCollider == null || _hasRecycled)
            return false;

        int layer = hitCollider.gameObject.layer;
        if (IsLayerInMask(layer, ignoredLayers))
            return false;

        if (!IsLayerInMask(layer, targetLayers))
            return false;

        if (_ownerRoot != null && hitCollider.transform.IsChildOf(_ownerRoot))
            return false;

        return true;
    }

    protected void RecycleSelf()
    {
        if (_hasRecycled)
            return;

        _hasRecycled = true;
        IsLaunched = false;

        if (ProjectileRigidbody != null)
        {
            ProjectileRigidbody.velocity = Vector3.zero;
            ProjectileRigidbody.angularVelocity = Vector3.zero;
        }

        GameplayPresentationBridge.ReturnPooledObject(gameObject);
    }

    private void SweepForHits(Vector3 startPosition, Vector3 endPosition)
    {
        Vector3 delta = endPosition - startPosition;
        float distance = delta.magnitude;
        if (distance <= 0.0001f)
            return;

        int effectiveLayerMask = targetLayers.value & ~ignoredLayers.value;
        if (effectiveLayerMask == 0)
            return;

        Vector3 direction = delta / distance;
        if (!Physics.Raycast(startPosition, direction, out RaycastHit hitInfo, distance, effectiveLayerMask, QueryTriggerInteraction.Ignore))
        {
            return;
        }

        Collider hitCollider = hitInfo.collider;
        if (!CanHitCollider(hitCollider))
            return;

        Vector3 hitPoint = hitInfo.point;
        Vector3 hitNormal = hitInfo.normal;
        StatusData targetData = UnitCombatResolver.ResolveStatusData(hitCollider);
        float damage = 0f;

        if (targetData != null && targetData != LaunchContext.ownerData && !targetData.IsDead && targetData.IsTargetable)
        {
            int targetInstanceId = targetData.gameObject.GetInstanceID();
            if (_hitTargetIds.Add(targetInstanceId))
            {
                damage = ProjectileDamageResolver.CalculateDamage(
                    LaunchContext.ownerData,
                    targetData,
                    LaunchContext.damageMultiplier,
                    LaunchContext.numericKey);

                targetData.ReceiveDamage(damage, Vector3.zero, 0f,
                    LaunchContext.ownerData != null ? LaunchContext.ownerData.gameObject : gameObject);
            }
        }

        SpawnImpactVfx(hitCollider.gameObject.layer, hitPoint, hitNormal);
        OnProjectileHit(hitCollider, hitPoint, hitNormal, targetData, damage);

        if (recycleOnHit)
            RecycleSelf();
    }

    private void SpawnImpactVfx(int hitLayer, Vector3 hitPoint, Vector3 hitNormal)
    {
        ProjectileImpactVfxEntry matchedEntry = ResolveImpactVfxEntry(hitLayer);
        if (matchedEntry == null || matchedEntry.impactVfxPrefab == null)
            return;

        Vector3 safeNormal = hitNormal.sqrMagnitude > 0.0001f ? hitNormal.normalized : Vector3.up;
        Quaternion rotation = Quaternion.FromToRotation(Vector3.up, safeNormal);
        Vector3 surfacePoint = hitPoint + safeNormal * impactSurfaceOffset;
        Vector3 prefabScale = matchedEntry.impactVfxPrefab.transform != null
            ? matchedEntry.impactVfxPrefab.transform.localScale
            : Vector3.one;

        int unitId = LaunchContext.ownerData != null ? LaunchContext.ownerData.UnitId : 0;
        GameplayPresentationBridge.SpawnOwnerVfx(unitId, matchedEntry.impactVfxPrefab, surfacePoint, rotation, prefabScale,
            Mathf.Max(0.01f, matchedEntry.autoRecycleTime));
    }

    private ProjectileImpactVfxEntry ResolveImpactVfxEntry(int hitLayer)
    {
        for (int i = 0; i < impactVfxEntries.Count; i++)
        {
            ProjectileImpactVfxEntry entry = impactVfxEntries[i];
            if (entry == null)
                continue;

            if (IsLayerInMask(hitLayer, entry.targetLayers))
                return entry;
        }

        return null;
    }

    private static bool IsLayerInMask(int layer, LayerMask mask)
    {
        return (mask.value & (1 << layer)) != 0;
    }

    private void ResetRuntimeState()
    {
        LaunchContext = default;
        CurrentSpeed = 0f;
        CurrentDirection = Vector3.zero;
        IsLaunched = false;
        _remainingLifetime = 0f;
        _hasRecycled = false;
        _ownerRoot = null;
        _hitTargetIds.Clear();
    }

    private Vector3 GetCurrentPosition()
    {
        if (ProjectileRigidbody != null)
            return ProjectileRigidbody.position;

        return transform.position;
    }

    private static Vector3 ResolveInitialDirection(ProjectileLaunchContext context)
    {
        Vector3 directionToAimPoint = context.aimPoint - context.position;
        if (directionToAimPoint.sqrMagnitude > 0.0001f)
            return directionToAimPoint.normalized;

        if (context.launchDirection.sqrMagnitude > 0.0001f)
            return context.launchDirection.normalized;

        return context.rotation * Vector3.forward;
    }
}
