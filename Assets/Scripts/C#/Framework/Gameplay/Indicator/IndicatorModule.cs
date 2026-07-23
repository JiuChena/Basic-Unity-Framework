using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class IndicatorModule : MonoBehaviour
{
    private const string RangeShaderName = "MiniatureGarden/Indicator/Range";
    private const float GeometryPadding = 0.05f;
    private static readonly Color DefaultPrimaryFillCenterColor = new Color(1f, 1f, 1f, 0.28f);
    private static readonly Color DefaultPrimaryFillOuterColor = new Color(1f, 1f, 1f, 0.08f);
    private static readonly Color DefaultPrimaryEdgeColor = new Color(1f, 1f, 1f, 0.95f);
    private static readonly Color DefaultSecondaryFillCenterColor = new Color(1f, 1f, 1f, 0.14f);
    private static readonly Color DefaultSecondaryFillOuterColor = new Color(1f, 1f, 1f, 0.04f);
    private static readonly Color DefaultSecondaryEdgeColor = new Color(1f, 1f, 1f, 0.72f);

    [Tooltip("技能指示器类型。")] public IndicatorType type = IndicatorType.Sector;
    [SerializeField, Tooltip("启用组件时是否立即显示指示器。")] private bool showOnEnable;
    [SerializeField, Tooltip("显示期间是否自动检测变化并刷新绘制。")] private bool autoRefresh = true;
    [SerializeField, Min(0f), Tooltip("指示器离地偏移。")] private float surfaceOffset = 0.02f;
    [SerializeField, Min(0f), Tooltip("副指示器相对主指示器的额外离地偏移。")] private float secondarySurfaceOffset = 0.005f;
    [SerializeField, Range(6, 128), Tooltip("圆弧采样段数。")] private int arcSegments = 36;
    [SerializeField, Min(0f), Tooltip("扇形半径。")] private float sectorRadius = 5f;
    [SerializeField, Range(1f, 360f), Tooltip("扇形角度。")] private float sectorAngle = 90f;
    [SerializeField, Min(0f), Tooltip("指向性长度。")] private float directionLength = 6f;
    [SerializeField, Min(0f), Tooltip("指向性宽度。")] private float directionWidth = 2f;
    [SerializeField, Min(0f), Tooltip("投掷最大距离。")] private float throwableMaxDistance = 8f;
    [SerializeField, Min(0f), Tooltip("投掷落点范围半径。")] private float throwableAreaRadius = 2f;
    [SerializeField, Tooltip("当前瞄准的世界坐标。")] private Vector3 aimWorldPosition;
    [SerializeField, Tooltip("主指示器 MeshFilter。")] private MeshFilter primaryMeshFilter;
    [SerializeField, Tooltip("主指示器 MeshRenderer。")] private MeshRenderer primaryMeshRenderer;
    [SerializeField, Tooltip("主指示器白边 MeshFilter。")] private MeshFilter primaryEdgeMeshFilter;
    [SerializeField, Tooltip("主指示器白边 MeshRenderer。")] private MeshRenderer primaryEdgeMeshRenderer;
    [SerializeField, Tooltip("副指示器 MeshFilter，仅投掷型使用。")] private MeshFilter secondaryMeshFilter;
    [SerializeField, Tooltip("副指示器 MeshRenderer，仅投掷型使用。")] private MeshRenderer secondaryMeshRenderer;
    [SerializeField, Tooltip("副指示器白边 MeshFilter，仅投掷型使用。")] private MeshFilter secondaryEdgeMeshFilter;
    [SerializeField, Tooltip("副指示器白边 MeshRenderer，仅投掷型使用。")] private MeshRenderer secondaryEdgeMeshRenderer;

    private Transform _cachedTransform;
    private IndicatorCore _core;
    private IndicatorMeshBuffer _buffer;
    private Mesh _primaryMesh;
    private Mesh _primaryEdgeMesh;
    private Mesh _secondaryMesh;
    private Mesh _secondaryEdgeMesh;
    private Material _runtimeIndicatorMaterial;
    private bool _visible;
    private bool _dirty = true;
    private IndicatorSnapshot _lastSnapshot;

    internal float SurfaceOffset => Mathf.Max(0f, surfaceOffset);
    internal float SecondarySurfaceOffset => Mathf.Max(0f, secondarySurfaceOffset);
    internal int ArcSegments => Mathf.Clamp(arcSegments, 6, 128);
    internal float SectorRadius => Mathf.Max(0f, sectorRadius);
    internal float SectorAngle => Mathf.Clamp(sectorAngle, 1f, 360f);
    internal float DirectionLength => Mathf.Max(0f, directionLength);
    internal float DirectionWidth => Mathf.Max(0f, directionWidth);
    internal float ThrowableMaxDistance => Mathf.Max(0f, throwableMaxDistance);
    internal float ThrowableAreaRadius => Mathf.Max(0f, throwableAreaRadius);
    internal Color PrimaryFillCenterColor => DefaultPrimaryFillCenterColor;
    internal Color PrimaryFillOuterColor => DefaultPrimaryFillOuterColor;
    internal Color PrimaryEdgeColor => DefaultPrimaryEdgeColor;
    internal Color SecondaryFillCenterColor => DefaultSecondaryFillCenterColor;
    internal Color SecondaryFillOuterColor => DefaultSecondaryFillOuterColor;
    internal Color SecondaryEdgeColor => DefaultSecondaryEdgeColor;
    internal float PrimaryEdgeWidth => ResolveEdgeWidth(type == IndicatorType.Directionality
        ? Mathf.Min(DirectionLength, DirectionWidth)
        : type == IndicatorType.Throwable ? ThrowableAreaRadius : SectorRadius);
    internal float SecondaryEdgeWidth => ResolveEdgeWidth(ThrowableMaxDistance);

    public bool IsVisible => _visible;

    private void Reset()
    {
        _cachedTransform = transform;
        CachePrimaryTargets();
        CachePrimaryEdgeTargets();
        aimWorldPosition = transform.position + transform.forward * Mathf.Max(1f, sectorRadius);
    }

    private void Awake()
    {
        _cachedTransform = transform;
        _buffer = new IndicatorMeshBuffer();
        CachePrimaryTargets();
        CachePrimaryEdgeTargets();
        EnsureSecondaryTargets();
        EnsureSecondaryEdgeTargets();
        CreatePrimaryMeshIfNeeded();
        CreatePrimaryEdgeMeshIfNeeded();
        CreateSecondaryMeshIfNeeded();
        CreateSecondaryEdgeMeshIfNeeded();
        CacheCore();
        EnsureRuntimeMaterial();
        ApplyRendererState(_visible);
    }

    private void OnEnable()
    {
        if (showOnEnable)
            _visible = true;

        _dirty = true;
        if (_visible)
        {
            RefreshIndicator();
            return;
        }

        ApplyRendererState(false);
    }

    private void LateUpdate()
    {
        if (!_visible)
            return;
        if (!_dirty && (!autoRefresh || !HasRuntimeStateChanged()))
            return;

        RefreshIndicator();
    }

    private void OnDisable()
    {
        ApplyRendererState(false);
    }

    private void OnDestroy()
    {
        DestroyRuntimeMesh(ref _primaryMesh);
        DestroyRuntimeMesh(ref _primaryEdgeMesh);
        DestroyRuntimeMesh(ref _secondaryMesh);
        DestroyRuntimeMesh(ref _secondaryEdgeMesh);
        DestroyRuntimeMaterial(ref _runtimeIndicatorMaterial);
    }

    private void OnValidate()
    {
        arcSegments = Mathf.Clamp(arcSegments, 6, 128);
        sectorAngle = Mathf.Clamp(sectorAngle, 1f, 360f);
        surfaceOffset = Mathf.Max(0f, surfaceOffset);
        secondarySurfaceOffset = Mathf.Max(0f, secondarySurfaceOffset);
        sectorRadius = Mathf.Max(0f, sectorRadius);
        directionLength = Mathf.Max(0f, directionLength);
        directionWidth = Mathf.Max(0f, directionWidth);
        throwableMaxDistance = Mathf.Max(0f, throwableMaxDistance);
        throwableAreaRadius = Mathf.Max(0f, throwableAreaRadius);
        CachePrimaryTargets();
        CachePrimaryEdgeTargets();
        _dirty = true;
        if (Application.isPlaying)
            EnsureRuntimeMaterial();
        if (Application.isPlaying && _visible)
            RefreshIndicator();
    }

    public void SetIndicatorType(IndicatorType indicatorType)
    {
        if (type == indicatorType)
            return;

        type = indicatorType;
        MarkDirty();
    }

    public void ApplyDisplayConfig(SkillIndicatorDisplayConfig config)
    {
        if (config == null)
            return;

        bool changed = false;
        if (type != config.type)
        {
            type = config.type;
            changed = true;
        }

        changed |= AssignIfDifferent(ref surfaceOffset, Mathf.Max(0f, config.surfaceOffset));
        changed |= AssignIfDifferent(ref secondarySurfaceOffset, Mathf.Max(0f, config.secondarySurfaceOffset));
        changed |= AssignIfDifferent(ref arcSegments, Mathf.Clamp(config.arcSegments, 6, 128));
        changed |= AssignIfDifferent(ref sectorRadius, Mathf.Max(0f, config.sectorRadius));
        changed |= AssignIfDifferent(ref sectorAngle, Mathf.Clamp(config.sectorAngle, 1f, 360f));
        changed |= AssignIfDifferent(ref directionLength, Mathf.Max(0f, config.directionLength));
        changed |= AssignIfDifferent(ref directionWidth, Mathf.Max(0f, config.directionWidth));
        changed |= AssignIfDifferent(ref throwableMaxDistance, Mathf.Max(0f, config.throwableMaxDistance));
        changed |= AssignIfDifferent(ref throwableAreaRadius, Mathf.Max(0f, config.throwableAreaRadius));
        if (changed)
            MarkDirty();
    }

    public void SetAimWorldPosition(Vector3 worldPosition)
    {
        if (aimWorldPosition == worldPosition)
            return;

        aimWorldPosition = worldPosition;
        MarkDirty();
    }

    public void SetAimDirection(Vector3 worldDirection, float distance = -1f)
    {
        Vector3 planarDirection = worldDirection;
        planarDirection.y = 0f;
        if (planarDirection.sqrMagnitude <= 0.0001f)
            planarDirection = _cachedTransform != null ? _cachedTransform.forward : transform.forward;

        planarDirection.y = 0f;
        if (planarDirection.sqrMagnitude <= 0.0001f)
            planarDirection = Vector3.forward;

        float resolvedDistance = distance > 0f ? distance : ResolveDefaultAimDistance();
        Vector3 origin = _cachedTransform != null ? _cachedTransform.position : transform.position;
        SetAimWorldPosition(origin + planarDirection.normalized * resolvedDistance);
    }

    public void ShowIndicator()
    {
        if (!_visible)
            _visible = true;

        RefreshIndicator();
    }

    public void HideIndicator()
    {
        if (!_visible)
            return;

        _visible = false;
        ApplyRendererState(false);
    }

    public void RefreshIndicator()
    {
        EnsureInitialized();
        if (!_visible)
        {
            ApplyRendererState(false);
            return;
        }

        CacheCore();
        CreatePrimaryMeshIfNeeded();
        CreatePrimaryEdgeMeshIfNeeded();
        if (_core.UsesSecondaryMesh)
        {
            CreateSecondaryMeshIfNeeded();
            CreateSecondaryEdgeMeshIfNeeded();
        }
        else
        {
            ClearMeshIfExists(_secondaryMesh);
            ClearMeshIfExists(_secondaryEdgeMesh);
        }

        EnsureRuntimeMaterial();
        _core.Execute(this, _buffer, _primaryMesh, _secondaryMesh, _primaryEdgeMesh, _secondaryEdgeMesh);
        ApplyRendererState(true);
        _dirty = false;
        _lastSnapshot = CaptureSnapshot();
    }

    public Vector3 GetResolvedTargetWorldPosition()
    {
        EnsureInitialized();
        if (type == IndicatorType.Throwable)
            return _cachedTransform.TransformPoint(ResolveThrowableLocalCenter());

        Vector3 localDirection = ResolveLocalAimDirection();
        float distance = type == IndicatorType.Directionality ? DirectionLength : SectorRadius;
        return _cachedTransform.TransformPoint(localDirection * distance);
    }

    public void GetPrimaryQuerySphere(out Vector3 worldCenter, out float radius)
    {
        EnsureInitialized();
        switch (type)
        {
            case IndicatorType.Directionality:
                Vector3 localDirection = ResolveLocalAimDirection();
                worldCenter = _cachedTransform.TransformPoint(localDirection * (DirectionLength * 0.5f));
                float halfWidth = DirectionWidth * 0.5f;
                radius = Mathf.Sqrt(DirectionLength * DirectionLength * 0.25f + halfWidth * halfWidth) + GeometryPadding;
                return;
            case IndicatorType.Throwable:
                worldCenter = _cachedTransform.TransformPoint(ResolveThrowableLocalCenter());
                radius = ThrowableAreaRadius + GeometryPadding;
                return;
            default:
                worldCenter = _cachedTransform.position;
                radius = SectorRadius + GeometryPadding;
                return;
        }
    }

    public bool ContainsWorldPosition(Vector3 worldPosition)
    {
        EnsureInitialized();
        Vector3 localPosition = _cachedTransform.InverseTransformPoint(worldPosition);
        localPosition.y = 0f;
        switch (type)
        {
            case IndicatorType.Directionality:
                if (DirectionLength <= 0f || DirectionWidth <= 0f)
                    return false;

                Quaternion rotation = Quaternion.LookRotation(ResolveLocalAimDirection(), Vector3.up);
                Vector3 alignedLocal = Quaternion.Inverse(rotation) * localPosition;
                return alignedLocal.z >= -GeometryPadding &&
                       alignedLocal.z <= DirectionLength + GeometryPadding &&
                       Mathf.Abs(alignedLocal.x) <= DirectionWidth * 0.5f + GeometryPadding;
            case IndicatorType.Throwable:
                if (ThrowableAreaRadius <= 0f)
                    return false;

                Vector3 throwableDelta = localPosition - ResolveThrowableLocalCenter();
                float throwableRadius = ThrowableAreaRadius + GeometryPadding;
                return throwableDelta.sqrMagnitude <= throwableRadius * throwableRadius;
            default:
                if (SectorRadius <= 0f)
                    return false;

                float sectorRadiusWithPadding = SectorRadius + GeometryPadding;
                float planarSqrMagnitude = localPosition.x * localPosition.x + localPosition.z * localPosition.z;
                if (planarSqrMagnitude > sectorRadiusWithPadding * sectorRadiusWithPadding)
                    return false;
                if (SectorAngle >= 359.9f || planarSqrMagnitude <= 0.0001f)
                    return true;

                return Vector3.Angle(ResolveLocalAimDirection(), localPosition.normalized) <= SectorAngle * 0.5f;
        }
    }

    internal Vector3 ResolveLocalAimDirection()
    {
        EnsureInitialized();
        Vector3 localAim = _cachedTransform.InverseTransformPoint(aimWorldPosition);
        localAim.y = 0f;
        if (localAim.sqrMagnitude <= 0.0001f)
            return Vector3.forward;

        return localAim.normalized;
    }

    internal Vector3 ResolveThrowableLocalCenter()
    {
        EnsureInitialized();
        Vector3 localAim = _cachedTransform.InverseTransformPoint(aimWorldPosition);
        localAim.y = 0f;
        float maxDistance = ThrowableMaxDistance;
        if (maxDistance > 0f && localAim.sqrMagnitude > maxDistance * maxDistance)
            localAim = localAim.normalized * maxDistance;

        return localAim;
    }

    private void EnsureInitialized()
    {
        if (_cachedTransform == null)
            _cachedTransform = transform;
        if (_buffer == null)
            _buffer = new IndicatorMeshBuffer();
        CachePrimaryTargets();
        CachePrimaryEdgeTargets();
        CacheCore();
    }

    private void CachePrimaryTargets()
    {
        if (primaryMeshFilter == null)
            primaryMeshFilter = GetComponent<MeshFilter>();
        if (primaryMeshRenderer == null)
            primaryMeshRenderer = GetComponent<MeshRenderer>();
        ConfigureIndicatorRenderer(primaryMeshRenderer);
    }

    private void CachePrimaryEdgeTargets()
    {
        EnsureMeshTarget("IndicatorPrimaryEdge", ref primaryEdgeMeshFilter, ref primaryEdgeMeshRenderer);
    }

    private void CacheCore()
    {
        _core = IndicatorCore.Resolve(type);
    }

    private void CreatePrimaryMeshIfNeeded()
    {
        if (_primaryMesh != null)
            return;

        _primaryMesh = CreateRuntimeMesh($"{name}_IndicatorPrimary");
        if (primaryMeshFilter != null)
            primaryMeshFilter.sharedMesh = _primaryMesh;
    }

    private void CreatePrimaryEdgeMeshIfNeeded()
    {
        CachePrimaryEdgeTargets();
        if (_primaryEdgeMesh != null)
            return;

        _primaryEdgeMesh = CreateRuntimeMesh($"{name}_IndicatorPrimaryEdge");
        if (primaryEdgeMeshFilter != null)
            primaryEdgeMeshFilter.sharedMesh = _primaryEdgeMesh;
    }

    private void CreateSecondaryMeshIfNeeded()
    {
        EnsureSecondaryTargets();
        if (_secondaryMesh != null)
            return;

        _secondaryMesh = CreateRuntimeMesh($"{name}_IndicatorSecondary");
        if (secondaryMeshFilter != null)
            secondaryMeshFilter.sharedMesh = _secondaryMesh;
    }

    private void CreateSecondaryEdgeMeshIfNeeded()
    {
        EnsureSecondaryEdgeTargets();
        if (_secondaryEdgeMesh != null)
            return;

        _secondaryEdgeMesh = CreateRuntimeMesh($"{name}_IndicatorSecondaryEdge");
        if (secondaryEdgeMeshFilter != null)
            secondaryEdgeMeshFilter.sharedMesh = _secondaryEdgeMesh;
    }

    private void EnsureSecondaryTargets()
    {
        EnsureMeshTarget("ThrowableIndicatorSecondary", ref secondaryMeshFilter, ref secondaryMeshRenderer);
    }

    private void EnsureSecondaryEdgeTargets()
    {
        EnsureMeshTarget("ThrowableIndicatorSecondaryEdge", ref secondaryEdgeMeshFilter, ref secondaryEdgeMeshRenderer);
    }

    private void EnsureMeshTarget(string childName, ref MeshFilter meshFilter, ref MeshRenderer meshRenderer)
    {
        if (meshFilter == null && meshRenderer != null)
            meshFilter = meshRenderer.GetComponent<MeshFilter>();
        if (meshRenderer == null && meshFilter != null)
            meshRenderer = meshFilter.GetComponent<MeshRenderer>();
        if (meshFilter != null && meshRenderer != null)
        {
            ConfigureIndicatorRenderer(meshRenderer);
            return;
        }

        Transform parentTransform = _cachedTransform != null ? _cachedTransform : transform;
        Transform childTransform = parentTransform.Find(childName);
        if (childTransform == null)
        {
            GameObject childObject = new GameObject(childName);
            childObject.layer = gameObject.layer;
            childTransform = childObject.transform;
            childTransform.SetParent(parentTransform, false);
        }

        childTransform.localPosition = Vector3.zero;
        childTransform.localRotation = Quaternion.identity;
        childTransform.localScale = Vector3.one;
        if (!childTransform.TryGetComponent(out meshFilter))
            meshFilter = childTransform.gameObject.AddComponent<MeshFilter>();
        if (!childTransform.TryGetComponent(out meshRenderer))
            meshRenderer = childTransform.gameObject.AddComponent<MeshRenderer>();
        ConfigureIndicatorRenderer(meshRenderer);
    }

    private void EnsureRuntimeMaterial()
    {
        if (_runtimeIndicatorMaterial == null)
        {
            Shader indicatorShader = Shader.Find(RangeShaderName);
            if (indicatorShader == null)
                indicatorShader = Shader.Find("Sprites/Default");
            if (indicatorShader == null)
                return;

            _runtimeIndicatorMaterial = new Material(indicatorShader)
            {
                name = $"{name}_IndicatorRange(Runtime)",
                enableInstancing = true,
            };
        }

        ApplyIndicatorMaterial(primaryMeshRenderer);
        ApplyIndicatorMaterial(primaryEdgeMeshRenderer);
        ApplyIndicatorMaterial(secondaryMeshRenderer);
        ApplyIndicatorMaterial(secondaryEdgeMeshRenderer);
    }

    private void ApplyIndicatorMaterial(MeshRenderer renderer)
    {
        if (renderer == null || _runtimeIndicatorMaterial == null)
            return;

        if (renderer.sharedMaterial != _runtimeIndicatorMaterial)
            renderer.sharedMaterial = _runtimeIndicatorMaterial;
        ConfigureIndicatorRenderer(renderer);
    }

    private void ConfigureIndicatorRenderer(MeshRenderer renderer)
    {
        if (renderer == null)
            return;

        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        renderer.allowOcclusionWhenDynamic = false;
    }

    private void ApplyRendererState(bool visible)
    {
        bool usesSecondaryMesh = visible && _core != null && _core.UsesSecondaryMesh;
        if (primaryMeshRenderer != null)
            primaryMeshRenderer.enabled = visible;
        if (primaryEdgeMeshRenderer != null)
            primaryEdgeMeshRenderer.enabled = visible;
        if (secondaryMeshRenderer != null)
            secondaryMeshRenderer.enabled = usesSecondaryMesh;
        if (secondaryEdgeMeshRenderer != null)
            secondaryEdgeMeshRenderer.enabled = usesSecondaryMesh;
    }

    private void MarkDirty()
    {
        _dirty = true;
    }

    private static bool AssignIfDifferent(ref float field, float value)
    {
        if (Mathf.Approximately(field, value))
            return false;

        field = value;
        return true;
    }

    private static bool AssignIfDifferent(ref int field, int value)
    {
        if (field == value)
            return false;

        field = value;
        return true;
    }

    private float ResolveDefaultAimDistance()
    {
        return type switch
        {
            IndicatorType.Directionality => Mathf.Max(1f, DirectionLength),
            IndicatorType.Throwable => Mathf.Max(1f, ThrowableMaxDistance),
            _ => Mathf.Max(1f, SectorRadius),
        };
    }

    private bool HasRuntimeStateChanged()
    {
        return !CaptureSnapshot().Equals(_lastSnapshot);
    }

    private IndicatorSnapshot CaptureSnapshot()
    {
        EnsureInitialized();
        return new IndicatorSnapshot
        {
            type = type,
            visible = _visible,
            worldPosition = _cachedTransform.position,
            worldRotation = _cachedTransform.rotation,
            worldScale = _cachedTransform.lossyScale,
            aimWorldPosition = aimWorldPosition,
            surfaceOffset = surfaceOffset,
            secondarySurfaceOffset = secondarySurfaceOffset,
            arcSegments = arcSegments,
            sectorRadius = sectorRadius,
            sectorAngle = sectorAngle,
            directionLength = directionLength,
            directionWidth = directionWidth,
            throwableMaxDistance = throwableMaxDistance,
            throwableAreaRadius = throwableAreaRadius,
        };
    }

    private static Mesh CreateRuntimeMesh(string meshName)
    {
        Mesh mesh = new Mesh
        {
            name = meshName,
        };
        mesh.MarkDynamic();
        return mesh;
    }

    private static void DestroyRuntimeMesh(ref Mesh mesh)
    {
        if (mesh == null)
            return;

        if (Application.isPlaying)
            Destroy(mesh);
        else
            DestroyImmediate(mesh);

        mesh = null;
    }

    private static void DestroyRuntimeMaterial(ref Material material)
    {
        if (material == null)
            return;

        if (Application.isPlaying)
            Destroy(material);
        else
            DestroyImmediate(material);

        material = null;
    }

    private static void ClearMeshIfExists(Mesh mesh)
    {
        if (mesh != null)
            mesh.Clear(false);
    }

    private static float ResolveEdgeWidth(float referenceSize)
    {
        return Mathf.Clamp(referenceSize * 0.06f, 0.05f, 0.18f);
    }

    private struct IndicatorSnapshot
    {
        public IndicatorType type;
        public bool visible;
        public Vector3 worldPosition;
        public Quaternion worldRotation;
        public Vector3 worldScale;
        public Vector3 aimWorldPosition;
        public float surfaceOffset;
        public float secondarySurfaceOffset;
        public int arcSegments;
        public float sectorRadius;
        public float sectorAngle;
        public float directionLength;
        public float directionWidth;
        public float throwableMaxDistance;
        public float throwableAreaRadius;

        public bool Equals(IndicatorSnapshot other)
        {
            return type == other.type &&
                   visible == other.visible &&
                   worldPosition == other.worldPosition &&
                   worldRotation == other.worldRotation &&
                   worldScale == other.worldScale &&
                   aimWorldPosition == other.aimWorldPosition &&
                   Mathf.Approximately(surfaceOffset, other.surfaceOffset) &&
                   Mathf.Approximately(secondarySurfaceOffset, other.secondarySurfaceOffset) &&
                   arcSegments == other.arcSegments &&
                   Mathf.Approximately(sectorRadius, other.sectorRadius) &&
                   Mathf.Approximately(sectorAngle, other.sectorAngle) &&
                   Mathf.Approximately(directionLength, other.directionLength) &&
                   Mathf.Approximately(directionWidth, other.directionWidth) &&
                   Mathf.Approximately(throwableMaxDistance, other.throwableMaxDistance) &&
                   Mathf.Approximately(throwableAreaRadius, other.throwableAreaRadius);
        }
    }
}

[DisallowMultipleComponent]
public sealed class IndicatorTargetHighlightView : MonoBehaviour
{
    private const string HighlightShaderName = "MiniatureGarden/Indicator/TargetHighlight";
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static Material s_sharedHighlightMaterial;

    private readonly List<OverlayEntry> _entries = new List<OverlayEntry>(8);
    private MaterialPropertyBlock _propertyBlock;
    private bool _isBuilt;

    public void Show(Color color)
    {
        EnsureBuilt();
        if (_entries.Count == 0)
            return;

        if (_propertyBlock == null)
            _propertyBlock = new MaterialPropertyBlock();

        _propertyBlock.Clear();
        _propertyBlock.SetColor(BaseColorId, color);
        for (int i = 0; i < _entries.Count; i++)
        {
            OverlayEntry entry = _entries[i];
            if (entry.sourceRenderer == null || entry.overlayRenderer == null)
                continue;

            entry.overlayRenderer.SetPropertyBlock(_propertyBlock);
            entry.overlayRenderer.enabled = entry.sourceRenderer.enabled;
        }
    }

    public void Hide()
    {
        for (int i = 0; i < _entries.Count; i++)
        {
            if (_entries[i].overlayRenderer != null)
                _entries[i].overlayRenderer.enabled = false;
        }
    }

    public void Release()
    {
        Hide();
        DisposeOverlayEntries();
        _propertyBlock = null;
        _isBuilt = false;
    }

    private void OnDisable()
    {
        Hide();
    }

    private void OnDestroy()
    {
        DisposeOverlayEntries();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSharedMaterial()
    {
        if (s_sharedHighlightMaterial == null)
            return;

        if (Application.isPlaying)
            Destroy(s_sharedHighlightMaterial);
        else
            DestroyImmediate(s_sharedHighlightMaterial);

        s_sharedHighlightMaterial = null;
    }

    private void DisposeOverlayEntries()
    {
        for (int i = 0; i < _entries.Count; i++)
        {
            if (_entries[i].overlayObject == null)
                continue;

            if (Application.isPlaying)
                Destroy(_entries[i].overlayObject);
            else
                DestroyImmediate(_entries[i].overlayObject);
        }

        _entries.Clear();
    }

    private void EnsureBuilt()
    {
        if (_isBuilt)
            return;

        _isBuilt = true;
        Material sharedMaterial = ResolveSharedMaterial();
        if (sharedMaterial == null)
            return;

        Renderer[] sourceRenderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < sourceRenderers.Length; i++)
        {
            Renderer sourceRenderer = sourceRenderers[i];
            if (sourceRenderer == null || sourceRenderer is ParticleSystemRenderer)
                continue;
            if (sourceRenderer.GetComponent<IndicatorTargetHighlightMarker>() != null)
                continue;

            if (sourceRenderer is SkinnedMeshRenderer skinnedMeshRenderer)
            {
                CreateSkinnedOverlay(skinnedMeshRenderer, sharedMaterial);
                continue;
            }

            if (sourceRenderer is MeshRenderer meshRenderer)
                CreateMeshOverlay(meshRenderer, sharedMaterial);
        }
    }

    private void CreateMeshOverlay(MeshRenderer sourceRenderer, Material sharedMaterial)
    {
        if (sourceRenderer == null)
            return;

        MeshFilter sourceMeshFilter = sourceRenderer.GetComponent<MeshFilter>();
        if (sourceMeshFilter == null || sourceMeshFilter.sharedMesh == null)
            return;

        GameObject overlayObject = new GameObject("IndicatorHighlightMesh");
        overlayObject.layer = sourceRenderer.gameObject.layer;
        overlayObject.transform.SetParent(sourceRenderer.transform, false);
        overlayObject.AddComponent<IndicatorTargetHighlightMarker>();

        MeshFilter overlayMeshFilter = overlayObject.AddComponent<MeshFilter>();
        overlayMeshFilter.sharedMesh = sourceMeshFilter.sharedMesh;

        MeshRenderer overlayRenderer = overlayObject.AddComponent<MeshRenderer>();
        overlayRenderer.sharedMaterials = BuildSharedMaterialArray(sourceRenderer, sharedMaterial);
        ConfigureOverlayRenderer(overlayRenderer, sourceRenderer);
        _entries.Add(new OverlayEntry(sourceRenderer, overlayRenderer, overlayObject));
    }

    private void CreateSkinnedOverlay(SkinnedMeshRenderer sourceRenderer, Material sharedMaterial)
    {
        if (sourceRenderer == null || sourceRenderer.sharedMesh == null)
            return;

        GameObject overlayObject = new GameObject("IndicatorHighlightSkinned");
        overlayObject.layer = sourceRenderer.gameObject.layer;
        overlayObject.transform.SetParent(sourceRenderer.transform, false);
        overlayObject.AddComponent<IndicatorTargetHighlightMarker>();

        SkinnedMeshRenderer overlayRenderer = overlayObject.AddComponent<SkinnedMeshRenderer>();
        overlayRenderer.sharedMesh = sourceRenderer.sharedMesh;
        overlayRenderer.rootBone = sourceRenderer.rootBone;
        overlayRenderer.bones = sourceRenderer.bones;
        overlayRenderer.localBounds = sourceRenderer.localBounds;
        overlayRenderer.updateWhenOffscreen = sourceRenderer.updateWhenOffscreen;
        overlayRenderer.quality = sourceRenderer.quality;
        overlayRenderer.sharedMaterials = BuildSharedMaterialArray(sourceRenderer, sharedMaterial);
        ConfigureOverlayRenderer(overlayRenderer, sourceRenderer);
        _entries.Add(new OverlayEntry(sourceRenderer, overlayRenderer, overlayObject));
    }

    private static void ConfigureOverlayRenderer(Renderer overlayRenderer, Renderer sourceRenderer)
    {
        if (overlayRenderer == null || sourceRenderer == null)
            return;

        overlayRenderer.shadowCastingMode = ShadowCastingMode.Off;
        overlayRenderer.receiveShadows = false;
        overlayRenderer.lightProbeUsage = LightProbeUsage.Off;
        overlayRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        overlayRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        overlayRenderer.allowOcclusionWhenDynamic = false;
        overlayRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
        overlayRenderer.sortingOrder = sourceRenderer.sortingOrder;
        overlayRenderer.renderingLayerMask = sourceRenderer.renderingLayerMask;
        overlayRenderer.enabled = false;
    }

    private static Material[] BuildSharedMaterialArray(Renderer sourceRenderer, Material sharedMaterial)
    {
        int materialCount = 1;
        if (sourceRenderer != null && sourceRenderer.sharedMaterials != null && sourceRenderer.sharedMaterials.Length > 0)
            materialCount = sourceRenderer.sharedMaterials.Length;

        Material[] materials = new Material[materialCount];
        for (int i = 0; i < materials.Length; i++)
            materials[i] = sharedMaterial;

        return materials;
    }

    private static Material ResolveSharedMaterial()
    {
        if (s_sharedHighlightMaterial != null)
            return s_sharedHighlightMaterial;

        Shader shader = Shader.Find(HighlightShaderName);
        if (shader == null)
            return null;

        s_sharedHighlightMaterial = new Material(shader)
        {
            name = "IndicatorTargetHighlight(Runtime)",
            enableInstancing = true,
        };
        return s_sharedHighlightMaterial;
    }

    private readonly struct OverlayEntry
    {
        public readonly Renderer sourceRenderer;
        public readonly Renderer overlayRenderer;
        public readonly GameObject overlayObject;

        public OverlayEntry(Renderer sourceRenderer, Renderer overlayRenderer, GameObject overlayObject)
        {
            this.sourceRenderer = sourceRenderer;
            this.overlayRenderer = overlayRenderer;
            this.overlayObject = overlayObject;
        }
    }
}

[DisallowMultipleComponent]
internal sealed class IndicatorTargetHighlightMarker : MonoBehaviour
{
}
