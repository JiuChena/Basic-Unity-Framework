using System;
using System.Collections.Generic;
using Core.Gear;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace BehaviorCore
{
    /// <summary>
    /// 单位单个行为的纯数据配置。
    /// </summary>
    [CreateAssetMenu(fileName = "BehaviorClip", menuName = "Framework/BehaviorCore/Authoring/Behavior Clip")]
    public class BehaviorClip : ScriptableObject
    {
        [NonSerialized]
        private BehaviorEvent[] runtimeCompiledEvents = Array.Empty<BehaviorEvent>();

        [NonSerialized]
        private float[] runtimeCompiledSegmentStartTimes = Array.Empty<float>();

        [NonSerialized]
        private bool runtimeCacheDirty = true;

        [Header("Animation")]
        [Tooltip("按时间顺序播放的动画片段列表，支持一个行为由多个动画组成")]
        public AnimationSegment[] animationSegments = Array.Empty<AnimationSegment>();

        [Tooltip("该行为的总时长，单位为秒。第一版建议手工维护，后续可由 Timeline 编译器回写")]
        [Min(0.01f)]
        public float totalDuration = 1f;

        [Tooltip("行为播放完成后的包裹模式，Loop 会从头重新进入当前行为")]
        public WrapMode wrapMode = WrapMode.Once;

        [Tooltip("行为全局播放速度倍率，会同时影响动画和时间轴推进")]
        [Min(0.01f)]
        public float speedMultiplier = 1f;

        [Space(8)]
        [Header("Events")]
        [Tooltip("行为过程中触发的时间轴事件列表，不负责 Hitbox 启停")]
        public BehaviorEvent[] events = Array.Empty<BehaviorEvent>();

        [Tooltip("行为过程中使用的命中判定配置列表")]
        public HitboxDef[] hitboxes = Array.Empty<HitboxDef>();

        [Space(8)]
        [Header("State")]
        [Tooltip("当前行为的打断优先级")]
        public InterruptPriority priority = InterruptPriority.Normal;


        [Space(8)]
        [Header("Transitions")]
        [Tooltip("当前行为允许切换到其他行为的时间窗与过渡参数配置")]
        public BehaviorTransitionDefinition[] transitions = Array.Empty<BehaviorTransitionDefinition>();

        [Space(8)]
        [Header("Authoring Snapshot")]
        [Tooltip("作者期 Timeline 轨道结构快照。用于把 BehaviorClip 回填为多轨 Timeline，而不是仅靠运行时数据猜测。")]
        public BehaviorAuthoringTrackSnapshot[] authoringTracks = Array.Empty<BehaviorAuthoringTrackSnapshot>();

        public bool HasTransitionDefinitions => transitions != null && transitions.Length > 0;
        public bool HasAuthoringTrackSnapshots => authoringTracks != null && authoringTracks.Length > 0;

        public BehaviorEvent[] GetCompiledRuntimeEvents()
        {
            EnsureRuntimeCache();
            return runtimeCompiledEvents;
        }

        public float[] GetCompiledRuntimeSegmentStartTimes()
        {
            EnsureRuntimeCache();
            return runtimeCompiledSegmentStartTimes;
        }

        public void InvalidateRuntimeCache()
        {
            runtimeCacheDirty = true;
            runtimeCompiledEvents = Array.Empty<BehaviorEvent>();
            runtimeCompiledSegmentStartTimes = Array.Empty<float>();
        }

        public bool TryGetTransitionDefinition(string targetBehaviorKey, float currentTime,
            out BehaviorTransitionDefinition definition)
        {
            definition = null;
            if (transitions == null || transitions.Length == 0 || string.IsNullOrWhiteSpace(targetBehaviorKey))
                return false;

            for (int i = 0; i < transitions.Length; i++)
            {
                BehaviorTransitionDefinition candidate = transitions[i];
                if (candidate == null ||
                    !string.Equals(candidate.targetBehaviorKey, targetBehaviorKey, StringComparison.Ordinal))
                {
                    continue;
                }

                if (currentTime < candidate.startTime || currentTime > candidate.endTime)
                    continue;

                definition = candidate;
                return true;
            }

            return false;
        }

        public bool ValidateData(bool logWarnings = true)
        {
            List<string> issues = new List<string>();
            CollectValidationIssues(issues);

            if (logWarnings)
            {
                for (int i = 0; i < issues.Count; i++)
                    Debug.LogWarning($"[{name}] {issues[i]}", this);
            }

            return issues.Count == 0;
        }

        public int CollectValidationIssues(List<string> issues)
        {
            if (issues == null)
                return 0;

            int initialCount = issues.Count;

            if (animationSegments == null || animationSegments.Length == 0)
            {
                issues.Add("animationSegments 为空，运行时将无法播放任何动画片段。");
            }
            else
            {
                for (int i = 0; i < animationSegments.Length; i++)
                {
                    AnimationSegment segment = animationSegments[i];
                    if (segment == null)
                    {
                        issues.Add($"AnimationSegment[{i}] 为空引用。");
                        continue;
                    }

                    if (segment.clip == null)
                        issues.Add($"AnimationSegment[{i}] 没有绑定 AnimationClip。");

                    if (segment.crossFadeDuration < 0f || segment.crossFadeDuration > 1f)
                        issues.Add($"AnimationSegment[{i}] 的 crossFadeDuration 必须在 0 到 1 之间。");

                    if (segment.layer < 0)
                        issues.Add($"AnimationSegment[{i}] 的 layer 小于 0。");

                    if (segment.startTime > totalDuration && totalDuration > 0f)
                    {
                        issues.Add(
                            $"AnimationSegment[{i}] 的 startTime={segment.startTime:F2}s 超出 totalDuration={totalDuration:F2}s。");
                    }
                }
            }

            if (totalDuration <= 0f)
                issues.Add("totalDuration 必须大于 0。");

            if (speedMultiplier <= 0f)
                issues.Add("speedMultiplier 必须大于 0。");


            if (transitions != null)
            {
                for (int i = 0; i < transitions.Length; i++)
                {
                    BehaviorTransitionDefinition transition = transitions[i];
                    if (transition == null)
                    {
                        issues.Add($"BehaviorTransitionDefinition[{i}] 为空引用。");
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(transition.targetBehaviorKey))
                        issues.Add($"BehaviorTransitionDefinition[{i}] 的 targetBehaviorKey 为空。");

                    if (transition.crossFadeDuration < 0f || transition.crossFadeDuration > 1f)
                        issues.Add($"BehaviorTransitionDefinition[{i}] 的 crossFadeDuration 必须在 0 到 1 之间。");

                    if (transition.startTime > transition.endTime)
                        issues.Add($"BehaviorTransitionDefinition[{i}] 的 startTime 不能大于 endTime。");

                    if (transition.endTime > totalDuration && totalDuration > 0f)
                    {
                        issues.Add(
                            $"BehaviorTransitionDefinition[{i}] 的 endTime={transition.endTime:F2}s 超出 totalDuration={totalDuration:F2}s。");
                    }
                }
            }

            if (events != null)
            {
                for (int i = 0; i < events.Length; i++)
                {
                    BehaviorEvent behaviorEvent = events[i];
                    if (behaviorEvent == null)
                    {
                        issues.Add($"BehaviorEvent[{i}] 为空引用。");
                        continue;
                    }

                    if (behaviorEvent.time < 0f)
                        issues.Add($"BehaviorEvent[{i}] 的 time 小于 0。");

                    if (behaviorEvent.time > totalDuration && totalDuration > 0f)
                        issues.Add($"BehaviorEvent[{i}] 的 time={behaviorEvent.time:F2}s 超出 totalDuration={totalDuration:F2}s。");

                    if (!string.IsNullOrWhiteSpace(behaviorEvent.referenceBone) &&
                        behaviorEvent.referenceBone.Contains("\\"))
                    {
                        issues.Add($"BehaviorEvent[{i}] 的 referenceBone 使用了反斜杠，层级路径应统一使用 '/'.");
                    }

                    BehaviorEventType effectiveType = BehaviorEventResolver.ResolveEffectiveType(behaviorEvent);

                    if (effectiveType == BehaviorEventType.PlayAudio && behaviorEvent.audioRef == null)
                        issues.Add($"BehaviorEvent[{i}] 被配置为 PlayAudio，但 audioRef 为空。");

                    if (effectiveType == BehaviorEventType.SpawnVFX && behaviorEvent.prefabRef == null)
                        issues.Add($"BehaviorEvent[{i}] 被配置为 SpawnVFX，但 prefabRef 为空。");

                    if (effectiveType == BehaviorEventType.SetObjectActive &&
                        string.IsNullOrWhiteSpace(behaviorEvent.targetObjectPath))
                    {
                        issues.Add($"BehaviorEvent[{i}] 被配置为 SetObjectActive，但 targetObjectPath 为空。");
                    }

                    if (effectiveType == BehaviorEventType.SpawnProjectile && behaviorEvent.prefabRef == null)
                        issues.Add($"BehaviorEvent[{i}] 被配置为 SpawnProjectile，但 prefabRef 为空。");

                    if (effectiveType == BehaviorEventType.SpawnProjectile &&
                        behaviorEvent.prefabRef != null &&
                        !BehaviorEventResolver.PrefabSupportsProjectileContract(behaviorEvent.prefabRef))
                    {
                        issues.Add($"BehaviorEvent[{i}] 被配置为 SpawnProjectile，但 prefabRef 没有挂载实现 IBehaviorProjectileContract 的组件。");
                    }

                    if ((effectiveType == BehaviorEventType.ApplyBuff ||
                         effectiveType == BehaviorEventType.ApplySelfBuff) &&
                        behaviorEvent.buffRef == null)
                    {
                        issues.Add($"BehaviorEvent[{i}] 被配置为 ApplyBuff/ApplySelfBuff，但 buffRef 为空。");
                    }

                    if (effectiveType == BehaviorEventType.ExecuteGameplayEffect &&
                        behaviorEvent.gameplayEffectRef == null)
                    {
                        issues.Add($"BehaviorEvent[{i}] 被配置为 ExecuteGameplayEffect，但 gameplayEffectRef 为空。");
                    }
                }
            }

            if (hitboxes != null)
            {
                for (int i = 0; i < hitboxes.Length; i++)
                {
                    HitboxDef hitbox = hitboxes[i];
                    if (hitbox == null)
                    {
                        issues.Add($"HitboxDef[{i}] 为空引用。");
                        continue;
                    }

                    if (hitbox.startTime < 0f)
                        issues.Add($"HitboxDef[{i}] 的 startTime 小于 0。");

                    if (hitbox.duration < 0f)
                        issues.Add($"HitboxDef[{i}] 的 duration 小于 0。");

                    if (hitbox.startTime + hitbox.duration > totalDuration && totalDuration > 0f)
                    {
                        issues.Add(
                            $"HitboxDef[{i}] 的结束时间 {hitbox.startTime + hitbox.duration:F2}s 超出 totalDuration={totalDuration:F2}s。");
                    }

                    if (!string.IsNullOrWhiteSpace(hitbox.referenceBone) && hitbox.referenceBone.Contains("\\"))
                        issues.Add($"HitboxDef[{i}] 的 referenceBone 使用了反斜杠，层级路径应统一使用 '/'.");
                }
            }

            return issues.Count - initialCount;
        }

        private void OnValidate()
        {
            InvalidateRuntimeCache();
            NormalizeSerializedBehaviorEvents();
            ValidateData();
        }

        private void OnEnable()
        {
            InvalidateRuntimeCache();
        }

        private void NormalizeSerializedBehaviorEvents()
        {
            NormalizeEventArray(events);

            if (authoringTracks == null)
                return;

            for (int i = 0; i < authoringTracks.Length; i++)
            {
                BehaviorAuthoringTrackSnapshot trackSnapshot = authoringTracks[i];
                if (trackSnapshot?.clips == null)
                    continue;

                for (int clipIndex = 0; clipIndex < trackSnapshot.clips.Length; clipIndex++)
                {
                    BehaviorAuthoringClipSnapshot clipSnapshot = trackSnapshot.clips[clipIndex];
                    if (clipSnapshot?.behaviorEvent == null)
                        continue;

                    BehaviorEventResolver.NormalizeInPlace(clipSnapshot.behaviorEvent);
                }
            }
        }

        private static void NormalizeEventArray(BehaviorEvent[] behaviorEvents)
        {
            if (behaviorEvents == null)
                return;

            for (int i = 0; i < behaviorEvents.Length; i++)
                BehaviorEventResolver.NormalizeInPlace(behaviorEvents[i]);
        }

        private void EnsureRuntimeCache()
        {
            if (!runtimeCacheDirty)
                return;

            RebuildRuntimeEvents();
            RebuildRuntimeSegmentStartTimes();
            runtimeCacheDirty = false;
        }

        private void RebuildRuntimeEvents()
        {
            if (events == null || events.Length == 0)
            {
                runtimeCompiledEvents = Array.Empty<BehaviorEvent>();
                return;
            }

            if (runtimeCompiledEvents.Length != events.Length)
                runtimeCompiledEvents = new BehaviorEvent[events.Length];

            for (int i = 0; i < events.Length; i++)
            {
                BehaviorEvent sourceEvent = events[i];
                runtimeCompiledEvents[i] = BehaviorEventResolver.CreateNormalizedClone(
                    sourceEvent,
                    sourceEvent != null ? sourceEvent.time : 0f,
                    sourceEvent != null ? sourceEvent.authoringTrackName : null);
            }

            Array.Sort(runtimeCompiledEvents, (left, right) => left.time.CompareTo(right.time));
        }

        private void RebuildRuntimeSegmentStartTimes()
        {
            AnimationSegment[] segments = animationSegments ?? Array.Empty<AnimationSegment>();
            if (runtimeCompiledSegmentStartTimes.Length != segments.Length)
                runtimeCompiledSegmentStartTimes = new float[segments.Length];

            float cursor = 0f;
            for (int i = 0; i < segments.Length; i++)
            {
                AnimationSegment segment = segments[i];
                float explicitStartTime = segment != null ? segment.startTime : -1f;
                float resolvedStartTime = explicitStartTime >= 0f ? explicitStartTime : cursor;
                runtimeCompiledSegmentStartTimes[i] = Mathf.Max(0f, resolvedStartTime);

                AnimationClip animationClip = segment != null ? segment.clip : null;
                if (animationClip == null)
                    continue;

                float clipDuration = animationClip.length / Mathf.Max(0.01f, speedMultiplier);
                cursor = Mathf.Max(cursor, runtimeCompiledSegmentStartTimes[i] + clipDuration);
            }
        }
    }
}
