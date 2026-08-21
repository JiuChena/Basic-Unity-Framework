using System;
using System.Collections.Generic;
using Core.Gear;
using UnityEngine;
using UnityEngine.Profiling;

namespace BehaviorCore
{
    /// <summary>
    /// 行为解释器，负责按时间轴推进行为、调度事件与命中判定。
    /// </summary>
    public class BehaviorInterpreter : MonoBehaviour
    {
        [Header("Hitbox")]
        [SerializeField, Tooltip("行为命中检测使用的目标层过滤")]
        private LayerMask targetLayerMask = ~0;

        [SerializeField, Tooltip("单次物理查询最多写入多少个碰撞体结果")]
        [Min(1)]
        private int maxOverlapResults = 16;

        [SerializeField, Tooltip("开启后会在 Scene 视图中绘制当前行为的 Hitbox")]
        private bool drawDebugGizmos = true;

        [Space(8)]
        [Header("Debug")]
        [SerializeField, Tooltip("开启后输出行为开始、切段、完成等流程日志")]
        private bool logBehaviorFlow;

        [SerializeField, Tooltip("开启后输出行为事件触发日志，如特效、音频、投射物等")]
        private bool logBehaviorEvents;

        [SerializeField, Tooltip("开启后输出命中成功日志，包括命中目标、伤害值和剩余生命")]
        private bool logHitResults;

        /// <summary>绑定的 Animator 组件</summary>
        public Animator Animator { get; private set; }
        /// <summary>动画片段播放策略接口</summary>
        public IBehaviorAnimationPlayer AnimationPlayer { get; private set; }
        /// <summary>AnimationPlayer 在 AnimatorSegmentPlayer 时的强类型转换</summary>
        public AnimatorSegmentPlayer AnimatorSegmentPlayer => AnimationPlayer as AnimatorSegmentPlayer;
        /// <summary>绑定的 CharacterController 组件</summary>
        public CharacterController Controller { get; private set; }
        /// <summary>行为宿主自身的单位数据（攻击方）</summary>
        public IBehaviorUnit OwnerData { get; private set; }
        /// <summary>行为事件接收器，负责 VFX/音频/投射物/伤害计算的具体实现</summary>
        public IBehaviorEventReceiver Receiver { get; private set; }

        /// <summary>当前正在播放的行为数据</summary>
        public BehaviorClip CurrentClip { get; private set; }
        /// <summary>当前行为已播放的经过时间（秒，含 speedMultiplier 缩放）</summary>
        public float ElapsedTime { get; private set; }
        /// <summary>当前行为的归一化时间（0~1）</summary>
        public float NormalizedTime { get; private set; }
        /// <summary>是否正在播放行为</summary>
        public bool IsPlaying { get; private set; }

        /// <summary>行为播放完毕时触发（仅 WrapMode.Once 的 Clip）</summary>
        public event Action<BehaviorClip> OnCompleted;

        /// <summary>当前行为的所有活跃 Hitbox（已解析骨骼引用）</summary>
        private readonly List<ActiveHitbox> _activeHitboxes = new List<ActiveHitbox>();
        /// <summary>骨骼路径 → Transform 缓存，避免重复 FindChildByPath</summary>
        private readonly Dictionary<string, Transform> _boneCache = new Dictionary<string, Transform>(StringComparer.Ordinal);
        /// <summary>SetObjectActive 目标物体路径 → Transform 缓存</summary>
        private readonly Dictionary<string, Transform> _targetObjectCache = new Dictionary<string, Transform>(StringComparer.Ordinal);
        /// <summary>已确认为无效的骨骼路径集合，避免重复 Find + 重复 Warning</summary>
        private readonly HashSet<string> _missingBonePaths = new HashSet<string>(StringComparer.Ordinal);
        /// <summary>碰撞体实例 ID → IBehaviorUnit 缓存，避免每帧接口 GetComponent</summary>
        private readonly Dictionary<int, IBehaviorUnit> _statusDataByColliderId = new Dictionary<int, IBehaviorUnit>(32);
        /// <summary>hitGroupId → 已命中目标实例 ID 集合，防止同一 Hitbox 组内重复命中</summary>
        private readonly Dictionary<int, HashSet<int>> _hitGroupTargets = new Dictionary<int, HashSet<int>>();
        /// <summary>当前行为通过 PlayAudio(loop=true) 启动的音频句柄列表，Stop 时统一回收</summary>
        private readonly List<int> _loopingAudioHandles = new List<int>();

        /// <summary>当前行为按时间升序排列的事件数组，Tick 时只读遍历</summary>
        private BehaviorEvent[] _sortedEvents = Array.Empty<BehaviorEvent>();
        /// <summary>当前行为各动画段的起始时间数组</summary>
        private float[] _segmentStartTimes = Array.Empty<float>();
        /// <summary>预分配的 PhysX Overlap 结果缓冲区，避免每帧分配</summary>
        private Collider[] _overlapResults = Array.Empty<Collider>();
        /// <summary>已执行事件在 _sortedEvents 中的索引，保证每个事件只执行一次</summary>
        private int _nextEventIndex;
        /// <summary>当前播放到的动画段索引</summary>
        private int _currentSegmentIndex;
        /// <summary>当前行为的索敌范围 ID，用于投射物区分不同次行为释放</summary>
        private int _targetingScopeId;

        /// <summary>
        /// 注入运行时依赖。由项目侧运行时中控在初始化时调用，将 Animator、动画播放器、CharacterController、
        /// 单位数据、事件接收器与命中层绑定到解释器。
        /// </summary>
        public void Configure(Animator animator, IBehaviorAnimationPlayer animationPlayer, CharacterController controller,
            IBehaviorUnit ownerData, IBehaviorEventReceiver receiver, LayerMask hitboxLayerMask)
        {
            Animator = animator;
            AnimationPlayer = animationPlayer;
            Controller = controller;
            OwnerData = ownerData;
            Receiver = receiver;
            targetLayerMask = hitboxLayerMask;

            if (_overlapResults.Length != maxOverlapResults)
                _overlapResults = new Collider[maxOverlapResults];
        }

        /// <summary>
        /// 播放指定的 BehaviorClip。若当前正在播放同一 Loop 行为则忽略重复请求；
        /// 否则先停止当前行为，构建排序事件表、动画段表和 Hitbox 列表，然后播放第一段动画。
        /// </summary>
        /// <param name="clip">待播放的行为数据</param>
        /// <param name="firstSegmentCrossFadeOverride">首段动画的过渡时间覆盖值，-1 表示使用片段自身配置</param>
        public void Play(BehaviorClip clip, float firstSegmentCrossFadeOverride = -1f)
        {
            Profiler.BeginSample("BehaviorInterpreter.Play");
            
            if (clip == null)
            {
                Stop();
                return;
            }

            if (CurrentClip == clip && IsPlaying && clip.wrapMode == WrapMode.Loop)
                return;

            Stop();

            CurrentClip = clip;
            ElapsedTime = 0f;
            NormalizedTime = 0f;
            IsPlaying = true;
            _nextEventIndex = 0;
            _currentSegmentIndex = 0;
            _targetingScopeId = GetNextTargetingScopeId(_targetingScopeId);

            BuildSortedEvents(clip);
            BuildSegments(clip);
            BuildHitboxes(clip);

            if (Animator != null)
            {
                Animator.speed = clip.speedMultiplier;
                PlaySegment(0, firstSegmentCrossFadeOverride);
            }

            if (logBehaviorFlow)
            {
                Debug.Log(
                    $"[{name}] 开始行为：{clip.name} | Duration={clip.totalDuration:F2}s | Wrap={clip.wrapMode} | Segments={(clip.animationSegments != null ? clip.animationSegments.Length : 0)}",
                    this);
            }

            TypedEventBus.Publish(new BehaviorPlaybackStartedEvent(this, clip));
            
            Profiler.EndSample();
        }

        /// <summary>
        /// 每帧驱动行为时间轴推进。按顺序执行：动画段切换 → 归一化时间更新 → 到期事件触发 → Hitbox 命中检测。
        /// 根据 WrapMode 处理 Loop（循环重置）、ClampForever（停在末尾）和 Once（播放完毕后自动 Stop）。
        /// </summary>
        /// <param name="deltaTime">本帧时间增量（未经 speedMultiplier 缩放）</param>
        public void Tick(float deltaTime)
        {
            if (!IsPlaying || CurrentClip == null) return;

            float scaledDeltaTime = deltaTime * Mathf.Max(0.01f, CurrentClip.speedMultiplier);
            ElapsedTime += scaledDeltaTime;

            UpdateAnimationSegments();
            UpdateNormalizedTime();
            ExecuteDueEvents();
            UpdateHitboxes();

            if (CurrentClip.wrapMode == WrapMode.Loop)
            {
                float totalDuration = GetClipDuration(CurrentClip);
                if (totalDuration > 0f && ElapsedTime >= totalDuration)
                    RestartLoopingClip(totalDuration);
                return;
            }

            if (CurrentClip.wrapMode == WrapMode.ClampForever)
            {
                if (NormalizedTime >= 1f)
                {
                    ElapsedTime = GetClipDuration(CurrentClip);
                    NormalizedTime = 1f;
                }

                return;
            }

            if (NormalizedTime >= 1f)
            {
                BehaviorClip completed = CurrentClip;
                if (logBehaviorFlow)
                    Debug.Log($"[{name}] 行为完成：{completed.name}", this);
                Stop();
                TypedEventBus.Publish(new BehaviorPlaybackCompletedEvent(this, completed));
                OnCompleted?.Invoke(completed);
            }
        }

        /// <summary>
        /// 停止当前行为的播放。清理所有循环音频、Hitbox 命中记录、事件表和动画段表，
        /// 将 Animator.speed 恢复为 1，状态重置为空闲。
        /// </summary>
        public void Stop()
        {
            Profiler.BeginSample("BehaviorInterpreter.Stop");
            if (logBehaviorFlow && IsPlaying && CurrentClip != null)
                Debug.Log($"[{name}] 停止行为：{CurrentClip.name}", this);

            StopLoopingAudios();
            CurrentClip = null;
            ElapsedTime = 0f;
            NormalizedTime = 0f;
            IsPlaying = false;
            _nextEventIndex = 0;
            _currentSegmentIndex = 0;
            _activeHitboxes.Clear();
            _hitGroupTargets.Clear();
            _segmentStartTimes = Array.Empty<float>();
            _sortedEvents = Array.Empty<BehaviorEvent>();

            if (Animator != null)
                Animator.speed = 1f;
            
            Profiler.EndSample();
        }

        /// <summary>
        /// 强制切换到指定 BehaviorClip，等价于直接调用 <see cref="Play"/>。
        /// </summary>
        public int PeekNextTargetingScopeId()
        {
            return GetNextTargetingScopeId(_targetingScopeId);
        }

        /// <summary>
        /// 判断当前行为是否可被指定优先级打断。当前无行为播放时始终返回 true。
        /// </summary>
        /// <param name="incoming">请求打断的优先级</param>
        public bool CanBeInterruptedBy(InterruptPriority incoming)
        {
            if (CurrentClip == null) return true;
            return incoming >= CurrentClip.priority;
        }

        /// <summary>
        /// 从 BehaviorClip 提取编译后的事件列表，按时间升序排列，存入 <see cref="_sortedEvents"/>。
        /// Play 时调用一次，Tick 期间只读遍历。
        /// </summary>
        private void BuildSortedEvents(BehaviorClip clip)
        {
            _sortedEvents = clip != null ? clip.GetCompiledRuntimeEvents() : Array.Empty<BehaviorEvent>();
        }

        /// <summary>
        /// 从 BehaviorClip 提取各动画段的起始时间数组，存入 <see cref="_segmentStartTimes"/>。
        /// 用于 Tick 期间判断当前时间是否跨入下一段。
        /// </summary>
        private void BuildSegments(BehaviorClip clip)
        {
            _segmentStartTimes = clip != null ? clip.GetCompiledRuntimeSegmentStartTimes() : Array.Empty<float>();
        }

        /// <summary>
        /// 从 BehaviorClip 构建活跃 Hitbox 列表和命中分组字典。清空上一行为的骨骼缓存与缺失路径记录，
        /// 每个 HitboxDef 预先解析其参考骨骼 Transform 并缓存为 <see cref="ActiveHitbox"/>。
        /// </summary>
        private void BuildHitboxes(BehaviorClip clip)
        {
            _activeHitboxes.Clear();
            _boneCache.Clear();
            _missingBonePaths.Clear();

            HitboxDef[] hitboxes = clip.hitboxes ?? Array.Empty<HitboxDef>();
            for (int i = 0; i < hitboxes.Length; i++)
            {
                HitboxDef definition = hitboxes[i];
                Transform reference = ResolveReferenceTransform(definition.referenceBone);
                _activeHitboxes.Add(new ActiveHitbox(definition, reference));

                if (!_hitGroupTargets.ContainsKey(definition.hitGroupId))
                    _hitGroupTargets.Add(definition.hitGroupId, new HashSet<int>());
            }
        }

        /// <summary>
        /// 每帧检查当前时间是否越过了下一个动画段的起始时间，若越过则切到对应段。
        /// </summary>
        private void UpdateAnimationSegments()
        {
            if (Animator == null || CurrentClip == null || _segmentStartTimes.Length == 0) return;

            while (_currentSegmentIndex + 1 < _segmentStartTimes.Length && ElapsedTime >= _segmentStartTimes[_currentSegmentIndex + 1])
            {
                _currentSegmentIndex++;
                PlaySegment(_currentSegmentIndex);
            }
        }

        /// <summary>
        /// 根据当前经过时间和行为总时长，更新归一化时间（0~1）。
        /// </summary>
        private void UpdateNormalizedTime()
        {
            float totalDuration = GetClipDuration(CurrentClip);
            if (totalDuration <= 0f)
            {
                NormalizedTime = 1f;
                return;
            }

            NormalizedTime = Mathf.Clamp01(ElapsedTime / totalDuration);
        }

        /// <summary>
        /// 遍历排序事件表，触发所有时间 ≤ 当前经过时间但尚未执行的事件。
        /// 采用递增索引 <see cref="_nextEventIndex"/> 保证每个事件只执行一次。
        /// </summary>
        private void ExecuteDueEvents()
        {
            while (_nextEventIndex < _sortedEvents.Length && _sortedEvents[_nextEventIndex].time <= ElapsedTime)
            {
                ExecuteEvent(_sortedEvents[_nextEventIndex]);
                _nextEventIndex++;
            }
        }

        /// <summary>
        /// 执行单个行为事件。根据事件类型分发到 Receiver 的对应方法：
        /// VFX 生成、物体激活、音频播放、投射物生成、Buff 施加、GameplayEffect 执行、镜头震动。
        /// 事件位置和旋转以 referenceBone 为锚点计算；留空时使用世界空间。
        /// </summary>
        private void ExecuteEvent(BehaviorEvent behaviorEvent)
        {
            if (Receiver == null)
                return;

            bool useWorldSpace = string.IsNullOrWhiteSpace(behaviorEvent.referenceBone);
            Transform reference = useWorldSpace ? null : ResolveReferenceTransform(behaviorEvent.referenceBone);
            Transform anchor = reference != null ? reference : transform;

            Vector3 position = useWorldSpace
                ? behaviorEvent.positionOffset
                : anchor.TransformPoint(behaviorEvent.positionOffset);
            Quaternion rotation = useWorldSpace
                ? Quaternion.Euler(behaviorEvent.rotationOffset)
                : anchor.rotation * Quaternion.Euler(behaviorEvent.rotationOffset);

            BehaviorEventType effectiveType = BehaviorEventResolver.ResolveEffectiveType(behaviorEvent);
            switch (effectiveType)
            {
                case BehaviorEventType.SpawnVFX:
                    if (behaviorEvent.prefabRef != null && OwnerData != null)
                    {
                        float recycleTime = behaviorEvent.autoRecycleTime > 0f ? behaviorEvent.autoRecycleTime : 1f;
                        Vector3 resolvedScaleOffset = ResolveSpawnVfxScaleOffset(behaviorEvent);
                        Receiver.SpawnVFX(OwnerData.UnitId, behaviorEvent.prefabRef, position, rotation,
                            resolvedScaleOffset, recycleTime);
                    }
                    break;

                case BehaviorEventType.SetObjectActive:
                    if (!string.IsNullOrWhiteSpace(behaviorEvent.targetObjectPath))
                    {
                        Transform targetTransform = ResolveTargetObjectTransformStrict(behaviorEvent.targetObjectPath);
                        if (targetTransform != null)
                            targetTransform.gameObject.SetActive(behaviorEvent.activeState);
                    }
                    break;

                case BehaviorEventType.PlayAudio:
                    if (behaviorEvent.audioRef != null)
                    {
                        int handle = Receiver.PlayAudio(behaviorEvent.audioRef, position,
                            behaviorEvent.audioLoop, behaviorEvent.audioVolume);
                        if (behaviorEvent.audioLoop && handle > 0)
                            _loopingAudioHandles.Add(handle);
                    }
                    break;

                case BehaviorEventType.SpawnProjectile:
                    if (ShouldSuppressGameplayExecution())
                        break;
                    if (behaviorEvent.prefabRef != null && OwnerData != null)
                        Receiver.SpawnProjectile(behaviorEvent.prefabRef, position, rotation, OwnerData,
                            behaviorEvent.damageMultiplier, behaviorEvent.numericKey, _targetingScopeId);
                    break;

                case BehaviorEventType.ApplyBuff:
                case BehaviorEventType.ApplySelfBuff:
                    if (ShouldSuppressGameplayExecution())
                        break;
                    if (behaviorEvent.buffRef != null)
                        Receiver.ApplyEffect(gameObject, behaviorEvent.buffRef, gameObject);
                    break;

                case BehaviorEventType.ExecuteGameplayEffect:
                    if (ShouldSuppressGameplayExecution())
                        break;
                    if (behaviorEvent.gameplayEffectRef != null && OwnerData != null)
                        Receiver.ExecuteEffect(behaviorEvent.gameplayEffectRef, OwnerData, position, gameObject);
                    break;

                case BehaviorEventType.CameraShake:
                    Receiver.ShakeCamera(behaviorEvent.cameraShakeAmplitude,
                        behaviorEvent.cameraShakeFrequency, behaviorEvent.cameraShakeDuration);
                    break;
            }

            if (logBehaviorEvents)
            {
                Debug.Log(
                    $"[{name}] 触发事件：{effectiveType} | Time={behaviorEvent.time:F2}s | Bone={(string.IsNullOrWhiteSpace(behaviorEvent.referenceBone) ? "<World>" : behaviorEvent.referenceBone)}",
                    this);
            }
        }

        /// <summary>
        /// 每帧遍历所有活跃 Hitbox，对每个处于激活时间窗的 Hitbox 进行物理查询（NonAlloc），
        /// 去重后计算伤害、施加击退与命中 Buff，并将命中目标记录到对应 hitGroup 防止重复命中。
        /// </summary>
        private void UpdateHitboxes()
        {
            if (Receiver == null || OwnerData == null || _overlapResults.Length == 0 || ShouldSuppressGameplayExecution())
                return;

            for (int i = 0; i < _activeHitboxes.Count; i++)
            {
                ActiveHitbox activeHitbox = _activeHitboxes[i];
                if (!activeHitbox.IsActive(ElapsedTime))
                    continue;

                activeHitbox.GetWorldPose(transform, out Vector3 center, out Quaternion rotation, out Vector3 size);

                int hitCount = QueryOverlap(activeHitbox.Definition, center, rotation, size);
                if (hitCount <= 0)
                    continue;

                HashSet<int> hitTargets = _hitGroupTargets[activeHitbox.Definition.hitGroupId];
                for (int resultIndex = 0; resultIndex < hitCount; resultIndex++)
                {
                    Collider collider = _overlapResults[resultIndex];
                    if (collider == null)
                        continue;

                    IBehaviorUnit targetData = ResolveStatusData(collider);
                    if (targetData == null || targetData == OwnerData || targetData.IsDead || !targetData.IsTargetable)
                        continue;

                    int targetInstanceId = targetData.RuntimeGameObject.GetInstanceID();
                    if (hitTargets.Contains(targetInstanceId))
                        continue;

                    IDamageable damageable = targetData.RuntimeGameObject.GetComponentInParent<IDamageable>();
                    if (damageable == null || !damageable.IsAlive)
                        continue;

                    float damage = Receiver.CalculateDamage(OwnerData, targetData,
                        activeHitbox.Definition.damageMultiplier, activeHitbox.Definition.numericKey);
                    Vector3 worldKnockback = transform.rotation * activeHitbox.Definition.knockbackForce;
                    float beforeHealth = targetData.CurrentHealth;

                    damageable.ReceiveDamage(damage, worldKnockback, activeHitbox.Definition.hitStunDuration, gameObject);
                    hitTargets.Add(targetInstanceId);

                    if (logHitResults)
                    {
                        Debug.Log(
                            $"[{name}] 命中目标：{targetData.DebugName} | Hitbox={GetHitboxDisplayName(activeHitbox.Definition)} | Damage={damage:F1} | HP {beforeHealth:F1} -> {targetData.CurrentHealth:F1} | Dead={targetData.IsDead}",
                            this);
                    }

                    if (activeHitbox.Definition.onHitBuff != null)
                        Receiver.ApplyEffect(targetData.RuntimeGameObject, activeHitbox.Definition.onHitBuff, gameObject);
                }
            }
        }

        /// <summary>
        /// 根据 Hitbox 形状（球/胶囊/盒）执行对应的 PhysX NonAlloc 查询，结果写入预分配的 <see cref="_overlapResults"/> 数组。
        /// </summary>
        /// <returns>命中的碰撞体数量</returns>
        private int QueryOverlap(HitboxDef definition, Vector3 center, Quaternion rotation, Vector3 size)
        {
            switch (definition.shape)
            {
                case HitboxShape.Sphere:
                    return Physics.OverlapSphereNonAlloc(center, Mathf.Abs(size.x), _overlapResults, targetLayerMask);

                case HitboxShape.Capsule:
                    float radius = Mathf.Abs(size.x);
                    float cylinderHeight = Mathf.Max(0f, Mathf.Abs(size.y) - radius * 2f);
                    Vector3 halfOffset = rotation * Vector3.up * (cylinderHeight * 0.5f);
                    return Physics.OverlapCapsuleNonAlloc(center + halfOffset, center - halfOffset,
                        radius, _overlapResults, targetLayerMask);

                case HitboxShape.Box:
                default:
                    return Physics.OverlapBoxNonAlloc(center, size * 0.5f, _overlapResults, rotation, targetLayerMask);
            }
        }

        /// <summary>
        /// 按层级路径解析宿主根节点下的参考 Transform。命中时写入 <see cref="_boneCache"/> 缓存；
        /// 未命中时记录到 <see cref="_missingBonePaths"/>（同路径只警告一次），并退回宿主根节点。
        /// </summary>
        /// <param name="path">以宿主根节点为起点的层级路径，如 "Root/Hips/Spine"</param>
        private Transform ResolveReferenceTransform(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;

            if (_boneCache.TryGetValue(path, out Transform cachedTransform))
                return cachedTransform;

            Transform found = FindChildByPath(transform, path);
            if (found == null && TryNormalizeHostRelativePath(path, out string normalizedPath))
            {
                found = string.IsNullOrWhiteSpace(normalizedPath)
                    ? transform
                    : FindChildByPath(transform, normalizedPath);

                if (found != null) _boneCache[normalizedPath] = found;
            }

            if (found == null)
            {
                if (_missingBonePaths.Add(path))
                {
                    string clipName = CurrentClip != null ? CurrentClip.name : "<无行为>";
                    Debug.LogWarning(
                        $"未找到骨骼路径：{path}，行为解释器将退回宿主根节点。\n" +
                        $"  当前行为：{clipName}",
                        this);
                }

                _boneCache[path] = transform;
                return transform;
            }

            _boneCache[path] = found;
            return found;
        }

        private static Vector3 ResolveSpawnVfxScaleOffset(BehaviorEvent behaviorEvent)
        {
            if (behaviorEvent == null)
                return Vector3.one;

            if (!IsLegacyControlTrackScaleSerializedAsAbsolute(behaviorEvent))
                return behaviorEvent.scaleOffset;

            return Vector3.one;
        }

        private static bool IsLegacyControlTrackScaleSerializedAsAbsolute(BehaviorEvent behaviorEvent)
        {
            if (behaviorEvent == null || behaviorEvent.prefabRef == null)
                return false;

            if (string.IsNullOrWhiteSpace(behaviorEvent.authoringTrackName) ||
                behaviorEvent.authoringTrackName.IndexOf("Control Track", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return false;
            }

            return ApproximatelyEqualVector3(behaviorEvent.scaleOffset, behaviorEvent.prefabRef.transform.localScale);
        }

        private static bool ApproximatelyEqualVector3(Vector3 a, Vector3 b, float epsilon = 0.0001f)
        {
            return Mathf.Abs(a.x - b.x) <= epsilon &&
                   Mathf.Abs(a.y - b.y) <= epsilon &&
                   Mathf.Abs(a.z - b.z) <= epsilon;
        }

        /// <summary>
        /// 按层级路径解析 SetObjectActive 事件的目标物体 Transform。与 <see cref="ResolveReferenceTransform"/>
        /// 不同的是未命中时返回 null 而非退回根节点，确保激活控制只作用于明确存在的物体。
        /// </summary>
        private Transform ResolveTargetObjectTransformStrict(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;

            if (_targetObjectCache.TryGetValue(path, out Transform cachedTransform))
                return cachedTransform;

            Transform found = FindChildByPath(transform, path);
            if (found == null && TryNormalizeHostRelativePath(path, out string normalizedPath))
            {
                found = string.IsNullOrWhiteSpace(normalizedPath)
                    ? transform
                    : FindChildByPath(transform, normalizedPath);

                if (found != null)
                    _targetObjectCache[normalizedPath] = found;
            }

            _targetObjectCache[path] = found;
            if (found == null && _missingBonePaths.Add($"[Target]{path}"))
            {
                Debug.LogWarning($"未找到目标物体路径：{path}，SetObjectActive 事件已跳过。", this);
            }

            return found;
        }

        private bool TryNormalizeHostRelativePath(string path, out string normalizedPath)
        {
            normalizedPath = null;
            if (string.IsNullOrWhiteSpace(path))
                return false;

            string trimmedPath = path.Trim();
            int slashIndex = trimmedPath.IndexOf('/');
            if (slashIndex < 0)
            {
                if (LooksLikeLegacyRootMarker(trimmedPath))
                {
                    normalizedPath = string.Empty;
                    return true;
                }

                return false;
            }

            string firstSegment = trimmedPath.Substring(0, slashIndex);
            if (HasDirectChildNamed(firstSegment))
                return false;

            normalizedPath = trimmedPath.Substring(slashIndex + 1).TrimStart('/');
            return !string.IsNullOrWhiteSpace(normalizedPath) || LooksLikeLegacyRootMarker(firstSegment);
        }

        private bool HasDirectChildNamed(string childName)
        {
            if (string.IsNullOrWhiteSpace(childName))
                return false;

            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (child != null && string.Equals(child.name, childName, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static bool LooksLikeLegacyRootMarker(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            bool hasLetter = false;
            bool hasDigit = false;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (char.IsLetter(c))
                    hasLetter = true;
                else if (char.IsDigit(c))
                    hasDigit = true;
                else if (c != '_' && c != '-')
                    return false;
            }

            return hasLetter && hasDigit;
        }

        /// <summary>
        /// 解析碰撞体所属单位的 IBehaviorUnit。通过 collider 实例 ID 缓存结果，
        /// 同一碰撞体只查询一次组件，后续命中直接返回缓存引用。
        /// </summary>
        /// <param name="collider">Overlap 查询返回的碰撞体</param>
        private IBehaviorUnit ResolveStatusData(Collider collider)
        {
            if (collider == null) return null;

            int colliderId = collider.GetInstanceID();
            if (_statusDataByColliderId.TryGetValue(colliderId, out IBehaviorUnit cachedStatusData))
            {
                if (cachedStatusData != null) return cachedStatusData;

                _statusDataByColliderId.Remove(colliderId);
            }

            if (!UnitCombatResolver.TryResolvebehaviorUnit(collider, out IBehaviorUnit resolvedStatusData,
                    out bool canCache))
            {
                return null;
            }

            if (resolvedStatusData != null && canCache)
                _statusDataByColliderId[colliderId] = resolvedStatusData;

            return resolvedStatusData;
        }

        /// <summary>
        /// 按 "/" 分隔的层级路径在 root 下查找子 Transform。若路径首段与 root 同名则自动跳过。
        /// 纯静态工具方法，不产生 GC（注意：Split 仅在缓存 miss 时调用）。
        /// </summary>
        private static Transform FindChildByPath(Transform root, string path)
        {
            string[] parts = path.Split('/');
            int startIndex = 0;
            if (parts.Length > 0 && string.Equals(parts[0], root.name, StringComparison.Ordinal))
                startIndex = 1;

            Transform current = root;
            for (int i = startIndex; i < parts.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(parts[i]))
                    continue;

                current = current.Find(parts[i]);
                if (current == null)
                    return null;
            }

            return current;
        }

        /// <summary>
        /// Loop 行为播放到末尾时重置所有状态：停止循环音频、将经过时间取模回卷、
        /// 重置事件索引和动画段索引、清空命中记录，重新播放第一段。
        /// </summary>
        private void RestartLoopingClip(float totalDuration)
        {
            StopLoopingAudios();

            ElapsedTime = totalDuration > 0f ? ElapsedTime % totalDuration : 0f;
            NormalizedTime = totalDuration > 0f ? Mathf.Clamp01(ElapsedTime / totalDuration) : 0f;
            _nextEventIndex = 0;
            _currentSegmentIndex = 0;

            foreach (KeyValuePair<int, HashSet<int>> pair in _hitGroupTargets)
                pair.Value.Clear();

            PlaySegment(0);
        }

        /// <summary>
        /// 停止所有正在循环播放的音频。遍历 <see cref="_loopingAudioHandles"/> 逐一通知 Receiver 停止，然后清空列表。
        /// </summary>
        private void StopLoopingAudios()
        {
            if (Receiver == null)
            {
                _loopingAudioHandles.Clear();
                return;
            }

            for (int i = 0; i < _loopingAudioHandles.Count; i++) Receiver.StopAudio(_loopingAudioHandles[i]);

            _loopingAudioHandles.Clear();
        }

        /// <summary>
        /// 播放指定索引的动画段。通过 <see cref="IBehaviorAnimationPlayer"/> 尝试播放，
        /// 成功时输出状态名和过渡时长供日志使用；失败时输出警告。
        /// </summary>
        /// <param name="index">动画段在 segments 数组中的索引</param>
        /// <param name="crossFadeDurationOverride">过渡时长覆盖，-1 使用段自身配置</param>
        private void PlaySegment(int index, float crossFadeDurationOverride = -1f)
        {
            if (Animator == null || CurrentClip == null) return;

            AnimationSegment[] segments = CurrentClip.animationSegments ?? Array.Empty<AnimationSegment>();
            if (index < 0 || index >= segments.Length) return;

            AnimationSegment segment = segments[index];
            if (segment.clip == null) return;

            if (AnimationPlayer != null && AnimationPlayer.TryPlaySegment(segment, index, crossFadeDurationOverride, out string stateName))
            {
                if (logBehaviorFlow)
                {
                    Debug.Log(
                        $"[{name}] 切换动画片段：Clip={segment.clip.name} | Layer={segment.layer} | Slot={index} | State={stateName} | CrossFade={(crossFadeDurationOverride >= 0f ? crossFadeDurationOverride : segment.crossFadeDuration):P0}",
                        this);
                }

                return;
            }

            if (Animator != null)
            {
                Debug.LogWarning(
                    $"BehaviorInterpreter 无法播放动画片段 {segment.clip.name}。请确认当前动画播放器已初始化，" +
                    $"并且存在 Layer {segment.layer} 的可用槽位 {index}。",
                    this);
            }
        }

        /// <summary>
        /// 计算 BehaviorClip 的运行时总时长 = totalDuration / speedMultiplier（最低 0.01s 兜底）。
        /// </summary>
        private static float GetClipDuration(BehaviorClip clip)
        {
            if (clip == null) return 0f;
            return Mathf.Max(0.01f, clip.totalDuration / Mathf.Max(0.01f, clip.speedMultiplier));
        }

        /// <summary>
        /// 生成下一个索敌范围 ID。每次 Play 时递增，用于投射物区分不同次行为释放的索敌上下文。
        /// 到达 int.MaxValue 时回卷到 1。
        /// </summary>
        private static int GetNextTargetingScopeId(int currentScopeId)
        {
            if (currentScopeId == int.MaxValue)
                return 1;

            return currentScopeId + 1;
        }

        private bool ShouldSuppressGameplayExecution()
        {
            return OwnerData != null &&
                   OwnerData.IsDead &&
                   (CurrentClip == null || CurrentClip.priority < InterruptPriority.Death);
        }

        /// <summary>
        /// 获取 Hitbox 的显示名称，用于日志输出。未命名时返回 "&lt;UnnamedHitbox&gt;"。
        /// </summary>
        private static string GetHitboxDisplayName(HitboxDef definition)
        {
            if (definition == null)
                return "<Null>";

            return string.IsNullOrWhiteSpace(definition.name) ? "<UnnamedHitbox>" : definition.name;
        }

        /// <summary>
        /// [Editor] 在 Scene 视图中绘制当前行为所有 Hitbox 的线框。红色=已激活，黄色=未激活。
        /// 仅在选中该对象且 drawDebugGizmos 开启时生效。
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            if (!drawDebugGizmos || CurrentClip == null) return;

            for (int i = 0; i < _activeHitboxes.Count; i++)
            {
                ActiveHitbox hitbox = _activeHitboxes[i];
                hitbox.GetWorldPose(transform, out Vector3 center, out Quaternion rotation, out Vector3 size);

                bool active = hitbox.IsActive(ElapsedTime);
                Gizmos.color = active ? Color.red : Color.yellow;
                Matrix4x4 previous = Gizmos.matrix;
                Gizmos.matrix = Matrix4x4.TRS(center, rotation, Vector3.one);

                switch (hitbox.Definition.shape)
                {
                    case HitboxShape.Sphere:
                        Gizmos.DrawWireSphere(Vector3.zero, size.x);
                        break;
                    case HitboxShape.Capsule:
                        Gizmos.DrawWireCube(Vector3.zero, new Vector3(size.x * 2f, size.y, size.x * 2f));
                        break;
                    case HitboxShape.Box:
                    default:
                        Gizmos.DrawWireCube(Vector3.zero, size);
                        break;
                }

                Gizmos.matrix = previous;
            }
        }
    }
}
