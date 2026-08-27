using System;
using System.Collections.Generic;
using System.IO;
using Core.Gear;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace BehaviorCore
{
    internal sealed partial class BehaviorEditorWindow : UnityEditor.EditorWindow
    {

        /// <summary>
        /// 从目标 BehaviorClip 回填 Timeline：先清理无效轨道，再按快照重建各轨道片段。
        /// </summary>
        private void RebuildTimelineFromBehaviorClip()
        {
            if (sourceTimeline == null || targetBehaviorClip == null)
                return;

            UnityEditor.Undo.RegisterCompleteObjectUndo(sourceTimeline, "Rebuild Behavior Editor Timeline");
            PruneInvalidRootTrackReferences(sourceTimeline);
            ImportSession importSession = new ImportSession(this, sourceTimeline, targetBehaviorClip);
            importSession.Execute();

            UnityEditor.EditorUtility.SetDirty(sourceTimeline);
            UnityEditor.AssetDatabase.SaveAssets();

            RefreshTimelineEditor(sourceTimeline, true, previewDirector);
            Debug.Log(
                $"已按 BehaviorClip 回填 Timeline：{targetBehaviorClip.name} -> {sourceTimeline.name}",
                sourceTimeline);
        }

        /// <summary>
        /// 尝试为指定轨道构建作者期导出快照。
        /// </summary>
        /// <param name="track">需要导出的轨道。</param>
        /// <param name="director">当前预览 Director，用于解析绑定。</param>
        /// <param name="referenceRoot">角色根节点，用于骨骼路径计算。</param>
        /// <param name="sortIndex">轨道排序索引。</param>
        /// <param name="exportWarnings">导出警告列表。</param>
        /// <param name="snapshot">输出的轨道快照。</param>
        /// <returns>成功构建时返回 true。</returns>
        private static bool TryBuildAuthoringTrackSnapshot(TrackAsset track, PlayableDirector director, Transform referenceRoot, int sortIndex, List<string> exportWarnings, out BehaviorAuthoringTrackSnapshot snapshot)
        {
            snapshot = null;
            if (track == null)
                return false;

            BehaviorAuthoringTrackKind? trackKind = ResolveAuthoringTrackKind(track);
            if (trackKind == null)
                return false;

            List<BehaviorAuthoringClipSnapshot> clips = BuildAuthoringClipSnapshotsForTrack(
                track,
                director,
                referenceRoot,
                exportWarnings);

            snapshot = new BehaviorAuthoringTrackSnapshot
            {
                trackName = track.name,
                trackKind = trackKind.Value,
                sortIndex = sortIndex,
                clips = clips.ToArray()
            };
            return true;
        }

        /// <summary>
        /// 解析轨道对应的作者期轨道类型。
        /// </summary>
        /// <param name="track">需要解析的轨道。</param>
        /// <returns>对应的轨道类型；无法识别时返回 null。</returns>
        private static BehaviorAuthoringTrackKind? ResolveAuthoringTrackKind(TrackAsset track)
        {
            return track switch
            {
                BehaviorTimelineMetaTrack => BehaviorAuthoringTrackKind.Meta,
                AnimationTrack => BehaviorAuthoringTrackKind.Animation,
                AudioTrack => BehaviorAuthoringTrackKind.Audio,
                ControlTrack => BehaviorAuthoringTrackKind.VfxControl,
                ActivationTrack => BehaviorAuthoringTrackKind.VfxActivation,
                BehaviorTimelineEventTrack => BehaviorAuthoringTrackKind.Event,
                BehaviorTimelineHitboxTrack => BehaviorAuthoringTrackKind.Hitbox,
                BehaviorTimelineTransitionTrack => BehaviorAuthoringTrackKind.Transition,
                _ => null
            };
        }

        /// <summary>
        /// 为指定轨道构建全部片段的作者期快照，按轨道类型分发导出。
        /// </summary>
        /// <param name="track">需要导出的轨道。</param>
        /// <param name="director">当前预览 Director，用于解析绑定。</param>
        /// <param name="referenceRoot">角色根节点，用于骨骼路径计算。</param>
        /// <param name="exportWarnings">导出警告列表。</param>
        /// <returns>构建出的片段快照列表。</returns>
        private static List<BehaviorAuthoringClipSnapshot> BuildAuthoringClipSnapshotsForTrack(TrackAsset track, PlayableDirector director, Transform referenceRoot, List<string> exportWarnings)
        {
            List<BehaviorAuthoringClipSnapshot> results = new List<BehaviorAuthoringClipSnapshot>();
            if (track == null)
                return results;

            // Meta 轨道：导出环绕模式、速度与优先级。
            if (track is BehaviorTimelineMetaTrack)
            {
                foreach (TimelineClip clip in track.GetClips())
                {
                    if (clip?.asset is not BehaviorTimelineMetaClipAsset metaAsset)
                        continue;

                    results.Add(new BehaviorAuthoringClipSnapshot
                    {
                        displayName = clip.displayName,
                        startTime = (float)clip.start,
                        duration = (float)clip.duration,
                        meta = CloneMetaSnapshot(metaAsset)
                    });
                }

                return results;
            }

            // 动画轨道：导出动画片段与交叉淡化。
            if (track is AnimationTrack animationTrack)
            {
                int layer = ResolveAnimationLayerFromTrackName(track.name);
                foreach (TimelineClip clip in animationTrack.GetClips())
                {
                    if (clip == null)
                        continue;

                    AnimationClip animationClip = null;
                    float crossFadeDuration = 0f;
                    if (clip.asset is AnimationPlayableAsset animationPlayableAsset)
                    {
                        animationClip = animationPlayableAsset.clip;
                        crossFadeDuration = ResolveNativeAnimationCrossFade(clip);
                    }
                    else if (clip.asset is BehaviorTimelineAnimationClipAsset legacyAnimationClipAsset)
                    {
                        animationClip = legacyAnimationClipAsset.animationClip;
                        crossFadeDuration = Mathf.Max(0f, legacyAnimationClipAsset.crossFadeDuration);
                    }

                    if (animationClip == null)
                        continue;

                    results.Add(new BehaviorAuthoringClipSnapshot
                    {
                        displayName = clip.displayName,
                        startTime = (float)clip.start,
                        duration = (float)clip.duration,
                        animationSegment = new AnimationSegment
                        {
                            authoringTrackName = track.name,
                            clip = animationClip,
                            crossFadeDuration = crossFadeDuration,
                            layer = layer,
                            startTime = (float)clip.start
                        }
                    });
                }

                return results;
            }

            // 音频轨道：导出音频事件与绑定信息。
            if (track is AudioTrack audioTrack)
            {
                AudioSource boundAudioSource = ResolveBoundAudioSource(audioTrack, director);
                float trackVolume = ReadClampedFloatSerializedProperty(audioTrack, "m_TrackProperties.volume", 1f);
                foreach (TimelineClip clip in audioTrack.GetClips())
                {
                    if (clip?.asset is not AudioPlayableAsset audioPlayableAsset || audioPlayableAsset.clip == null)
                        continue;

                    BuildTransformBinding(
                        boundAudioSource != null ? boundAudioSource.transform : null,
                        referenceRoot,
                        out string referenceBone,
                        out Vector3 positionOffset,
                        out Vector3 rotationOffset,
                        out Vector3 scaleOffset);

                    results.Add(new BehaviorAuthoringClipSnapshot
                    {
                        displayName = clip.displayName,
                        startTime = (float)clip.start,
                        duration = (float)clip.duration,
                        behaviorEvent = new BehaviorEvent
                        {
                            authoringTrackName = track.name,
                            time = Mathf.Max(0f, (float)clip.start),
                            type = BehaviorEventType.PlayAudio,
                            referenceBone = referenceBone,
                            positionOffset = positionOffset,
                            rotationOffset = rotationOffset,
                            scaleOffset = scaleOffset,
                            audioRef = audioPlayableAsset.clip,
                            audioLoop = audioPlayableAsset.loop,
                            audioVolume = Mathf.Clamp01(trackVolume *
                                                       ReadClampedFloatSerializedProperty(
                                                           audioPlayableAsset,
                                                           "m_ClipProperties.volume",
                                                           1f)),
                        }
                    });
                }

                return results;
            }

            // 特效控制轨道：导出 VFX 事件。
            if (track is ControlTrack controlTrack)
            {
                AppendVfxControlSnapshots(results, controlTrack, director, referenceRoot, exportWarnings);
                return results;
            }

            // 特效激活轨道：导出激活事件。
            if (track is ActivationTrack activationTrack)
            {
                AppendVfxActivationSnapshots(results, activationTrack, director, referenceRoot, exportWarnings);
                return results;
            }

            // 事件轨道：导出行为事件。
            if (track is BehaviorTimelineEventTrack)
            {
                foreach (TimelineClip clip in track.GetClips())
                {
                    if (clip?.asset is not BehaviorTimelineEventClipAsset eventClipAsset)
                        continue;

                    results.Add(new BehaviorAuthoringClipSnapshot
                    {
                        displayName = clip.displayName,
                        startTime = (float)clip.start,
                        duration = (float)clip.duration,
                        behaviorEvent =
                            BehaviorEventResolver.CreateNormalizedClone(eventClipAsset.eventData, (float)clip.start, track.name)
                    });
                }

                return results;
            }

            // Hitbox 轨道：导出伤害判定定义。
            if (track is BehaviorTimelineHitboxTrack)
            {
                foreach (TimelineClip clip in track.GetClips())
                {
                    if (clip?.asset is not BehaviorTimelineHitboxClipAsset hitboxClipAsset)
                        continue;

                    results.Add(new BehaviorAuthoringClipSnapshot
                    {
                        displayName = clip.displayName,
                        startTime = (float)clip.start,
                        duration = (float)clip.duration,
                        hitboxDef = CloneHitboxDef(hitboxClipAsset.hitboxData, (float)clip.start, (float)clip.duration, track.name)
                    });
                }

                return results;
            }

            // 过渡轨道：导出行为过渡定义。
            if (track is BehaviorTimelineTransitionTrack)
            {
                foreach (TimelineClip clip in track.GetClips())
                {
                    if (clip?.asset is not BehaviorTimelineTransitionClipAsset transitionClipAsset)
                        continue;

                    results.Add(new BehaviorAuthoringClipSnapshot
                    {
                        displayName = clip.displayName,
                        startTime = (float)clip.start,
                        duration = (float)clip.duration,
                        transitionDefinition = CloneTransitionDefinition(
                            transitionClipAsset.transitionData,
                            (float)clip.start,
                            (float)clip.duration,
                            track.name)
                    });
                }
            }

            return results;
        }

        /// <summary>
        /// 克隆 Meta 片段资产为导出快照。
        /// </summary>
        /// <param name="source">源 Meta 片段资产。</param>
        /// <returns>克隆的 Meta 快照。</returns>
        private static BehaviorTimelineMetaSnapshot CloneMetaSnapshot(BehaviorTimelineMetaClipAsset source)
        {
            if (source == null)
                return null;

            return new BehaviorTimelineMetaSnapshot
            {
                wrapMode = source.wrapMode,
                speedMultiplier = source.speedMultiplier,
                priority = source.priority
            };
        }

        /// <summary>
        /// 追加特效控制轨道的片段快照，解析挂点绑定与预制体引用。
        /// </summary>
        /// <param name="results">输出快照列表。</param>
        /// <param name="track">特效控制轨道。</param>
        /// <param name="director">当前预览 Director。</param>
        /// <param name="referenceRoot">角色根节点。</param>
        /// <param name="exportWarnings">导出警告列表。</param>
        private static void AppendVfxControlSnapshots(List<BehaviorAuthoringClipSnapshot> results, ControlTrack track, PlayableDirector director, Transform referenceRoot, List<string> exportWarnings)
        {
            if (results == null || track == null)
                return;

            foreach (TimelineClip clip in track.GetClips())
            {
                if (clip?.asset is not ControlPlayableAsset playableAsset)
                    continue;

                // 解析源对象、预制体与变换来源。
                GameObject sourceObject = director != null ? playableAsset.sourceGameObject.Resolve(director) : null;
                GameObject prefab = playableAsset.prefabGameObject;
                GameObject transformSourceObject = sourceObject;
                if (prefab == null &&
                    TryResolveSpawnableControlTrackPrefab(sourceObject, referenceRoot, out GameObject resolvedPrefab,
                        out GameObject resolvedInstanceRoot))
                {
                    prefab = resolvedPrefab;
                    transformSourceObject = resolvedInstanceRoot;
                }

                BuildTransformBinding(
                    transformSourceObject != null ? transformSourceObject.transform : null,
                    referenceRoot,
                    out string referenceBone,
                    out Vector3 positionOffset,
                    out Vector3 rotationOffset,
                    out Vector3 scaleOffset);

                results.Add(new BehaviorAuthoringClipSnapshot
                {
                    displayName = clip.displayName,
                    startTime = (float)clip.start,
                    duration = (float)clip.duration,
                    boundObjectPath = BuildRelativeAuthoringObjectPath(referenceRoot, transformSourceObject),
                    controlPostPlayback = ReadIntSerializedProperty(playableAsset, "postPlayback", -1),
                    behaviorEvent = new BehaviorEvent
                    {
                        authoringTrackName = track.name,
                        time = Mathf.Max(0f, (float)clip.start),
                        type = BehaviorEventType.SpawnVFX,
                        referenceBone = referenceBone,
                        positionOffset = positionOffset,
                        rotationOffset = rotationOffset,
                        scaleOffset = scaleOffset,
                        prefabRef = prefab,
                        autoRecycleTime = Mathf.Max(0f, (float)clip.duration),
                    }
                });

                if (prefab == null && sourceObject == null)
                {
                    exportWarnings?.Add(
                        $"ControlTrack '{track.name}' 的片段 '{clip.displayName}' 没有 prefab 引用，也没有 sourceGameObject 绑定路径；该作者轨片段只能回填为空绑定片段。");
                }
            }
        }

        /// <summary>
        /// 追加特效激活轨道的片段快照，解析激活对象绑定。
        /// </summary>
        /// <param name="results">输出快照列表。</param>
        /// <param name="track">特效激活轨道。</param>
        /// <param name="director">当前预览 Director。</param>
        /// <param name="referenceRoot">角色根节点。</param>
        /// <param name="exportWarnings">导出警告列表。</param>
        private static void AppendVfxActivationSnapshots( List<BehaviorAuthoringClipSnapshot> results, ActivationTrack track, PlayableDirector director, Transform referenceRoot, List<string> exportWarnings)
        {
            if (results == null || track == null)
                return;

            GameObject sourceObject = ResolveActivationTrackBinding(track, director);

            BuildTransformBinding(
                sourceObject != null ? sourceObject.transform : null,
                referenceRoot,
                out string referenceBone,
                out Vector3 positionOffset,
                out Vector3 rotationOffset,
                out Vector3 scaleOffset);

            foreach (TimelineClip clip in track.GetClips())
            {
                if (clip == null)
                    continue;

                results.Add(new BehaviorAuthoringClipSnapshot
                {
                    displayName = clip.displayName,
                    startTime = (float)clip.start,
                    duration = (float)clip.duration,
                    boundObjectPath = BuildRelativeAuthoringObjectPath(referenceRoot, sourceObject),
                    behaviorEvent = new BehaviorEvent
                    {
                        authoringTrackName = track.name,
                        time = Mathf.Max(0f, (float)clip.start),
                        type = BehaviorEventType.SpawnVFX,
                        referenceBone = referenceBone,
                        positionOffset = positionOffset,
                        rotationOffset = rotationOffset,
                        scaleOffset = scaleOffset,
                        prefabRef = null,
                        autoRecycleTime = Mathf.Max(0f, (float)clip.duration),
                    }
                });
            }

            if (sourceObject == null)
            {
                exportWarnings?.Add($"ActivationTrack '{track.name}' 没有绑定 sourceGameObject；作者轨快照会保留片段，但回填时无法恢复目标对象。");
            }
        }

        /// <summary>
        /// 解析导出目标 BehaviorClip：优先缓存，否则在输出目录加载或创建。
        /// </summary>
        /// <returns>可写入的 BehaviorClip 资产。</returns>
        private BehaviorClip ResolveTargetBehaviorClip()
        {
            if (targetBehaviorClip != null)
                return targetBehaviorClip;

            string folder = EnsureFolder(outputFolder);
            string assetName = SanitizeAssetName(outputAssetName);
            string assetPath = $"{folder}/{assetName}.asset";
            BehaviorClip existing = UnityEditor.AssetDatabase.LoadAssetAtPath<BehaviorClip>(assetPath);
            if (existing != null)
            {
                targetBehaviorClip = existing;
                return existing;
            }

            // 不存在时创建新资产。
            BehaviorClip created = CreateInstance<BehaviorClip>();
            created.name = assetName;
            UnityEditor.AssetDatabase.CreateAsset(created, assetPath);
            targetBehaviorClip = created;
            return created;
        }

        /// <summary>
        /// 确保 Timeline 中存在指定类型的轨道：优先名称精确匹配，其次空轨道回退，缺失时创建。
        /// </summary>
        /// <typeparam name="T">轨道类型。</typeparam>
        /// <param name="timelineAsset">目标 Timeline 资产。</param>
        /// <param name="trackName">轨道名称。</param>
        /// <param name="timelineTracks">已收集的轨道列表；为 null 时重新枚举。</param>
        /// <param name="changed">是否发生了创建或改名。</param>
        /// <returns>解析或创建出的轨道。</returns>
        private static T EnsureTrack<T>( TimelineAsset timelineAsset, string trackName, IReadOnlyList<TrackAsset> timelineTracks, out bool changed) where T : TrackAsset, new()
        {
            changed = false;
            if (timelineAsset == null)
                return null;

            // 在已有轨道中查找名称精确匹配或空轨道回退。
            T exactNameMatch = null;
            int exactNameScore = int.MinValue;
            T fallbackMatch = null;
            if (timelineTracks != null)
            {
                for (int i = 0; i < timelineTracks.Count; i++)
                {
                    TrackAsset track = timelineTracks[i];
                    if (track is not T typedTrack)
                        continue;

                    int trackScore = GetTrackContentScore(typedTrack);
                    if (!string.IsNullOrEmpty(trackName) &&
                        string.Equals(typedTrack.name, trackName, StringComparison.Ordinal))
                    {
                        if (exactNameMatch == null || trackScore > exactNameScore)
                        {
                            exactNameMatch = typedTrack;
                            exactNameScore = trackScore;
                        }

                        continue;
                    }

                    if (trackScore == 0 && fallbackMatch == null)
                        fallbackMatch = typedTrack;
                }
            }
            else
            {
                foreach (TrackAsset track in EnumerateTimelineTracks(timelineAsset))
                {
                    if (track is not T typedTrack)
                        continue;

                    int trackScore = GetTrackContentScore(typedTrack);
                    if (!string.IsNullOrEmpty(trackName) &&
                        string.Equals(typedTrack.name, trackName, StringComparison.Ordinal))
                    {
                        if (exactNameMatch == null || trackScore > exactNameScore)
                        {
                            exactNameMatch = typedTrack;
                            exactNameScore = trackScore;
                        }

                        continue;
                    }

                    if (trackScore == 0 && fallbackMatch == null)
                        fallbackMatch = typedTrack;
                }
            }

            // 命中已有轨道时改名为目标名称并清理空重复轨道。
            T resolvedTrack = exactNameMatch ?? fallbackMatch;
            if (resolvedTrack != null)
            {
                if (!string.IsNullOrEmpty(trackName) &&
                    !string.Equals(resolvedTrack.name, trackName, StringComparison.Ordinal))
                {
                    UnityEditor.Undo.RecordObject(resolvedTrack, "Rename Behavior Track");
                    resolvedTrack.name = trackName;
                    UnityEditor.EditorUtility.SetDirty(resolvedTrack);
                    changed = true;
                }

                RemoveEmptyDuplicateTracks(timelineAsset, resolvedTrack, trackName);
                return resolvedTrack;
            }

            // 无匹配时创建新轨道。
            T created = timelineAsset.CreateTrack<T>(null, trackName);
            UnityEditor.Undo.RegisterCreatedObjectUndo(created, "Create Behavior Track");
            UnityEditor.EditorUtility.SetDirty(created);
            changed = true;
            return created;
        }

        /// <summary>
        /// 获取或创建指定类型的精确名称匹配轨道。
        /// </summary>
        /// <typeparam name="T">轨道类型。</typeparam>
        /// <param name="timelineAsset">目标 Timeline 资产。</param>
        /// <param name="trackName">轨道名称。</param>
        /// <returns>匹配或新建的轨道。</returns>
        private static T GetOrCreateExactTrack<T>(TimelineAsset timelineAsset, string trackName) where T : TrackAsset, new()
        {
            if (timelineAsset == null)
                return null;

            foreach (TrackAsset track in EnumerateTimelineTracks(timelineAsset))
            {
                if (track is T typedTrack &&
                    string.Equals(typedTrack.name, trackName, StringComparison.Ordinal))
                {
                    return typedTrack;
                }
            }

            T created = timelineAsset.CreateTrack<T>(null, trackName);
            UnityEditor.Undo.RegisterCreatedObjectUndo(created, "Create Exact Behavior Track");
            UnityEditor.EditorUtility.SetDirty(created);
            return created;
        }

        /// <summary>
        /// 清空轨道上的全部片段。
        /// </summary>
        /// <param name="track">需要清空的轨道。</param>
        private static void ClearTrackClips(TrackAsset track)
        {
            DeleteClipsByPredicate(track, "Clear Timeline Track Clips", _ => true);
        }

        /// <summary>
        /// 解析回填片段的显示名称：优先快照名，否则使用回退名。
        /// </summary>
        /// <param name="clipSnapshot">片段快照。</param>
        /// <param name="fallbackDisplayName">回退显示名。</param>
        /// <returns>显示名称。</returns>
        private static string ResolveImportedClipDisplayName(BehaviorAuthoringClipSnapshot clipSnapshot, string fallbackDisplayName)
        {
            return clipSnapshot != null && !string.IsNullOrWhiteSpace(clipSnapshot.displayName)
                ? clipSnapshot.displayName
                : fallbackDisplayName;
        }

        /// <summary>
        /// 解析轨道名称：优先作者期名称，否则使用默认名。
        /// </summary>
        /// <param name="authoringTrackName">作者期轨道名。</param>
        /// <param name="defaultTrackName">默认轨道名。</param>
        /// <returns>解析后的轨道名。</returns>
        private static string ResolveTrackNameOrDefault(string authoringTrackName, string defaultTrackName)
        {
            return !string.IsNullOrWhiteSpace(authoringTrackName) ? authoringTrackName : defaultTrackName;
        }

        /// <summary>
        /// 构建按骨骼区分的音频轨道名。
        /// </summary>
        /// <param name="referenceBone">骨骼路径。</param>
        /// <returns>音频轨道名。</returns>
        private static string BuildAudioTrackName(string referenceBone)
        {
            if (string.IsNullOrWhiteSpace(referenceBone))
                return NativeAudioTrackName;

            return $"{NativeAudioTrackName} [{referenceBone.Replace('/', '_')}]";
        }

        /// <summary>
        /// 构建行为事件的显示名称，按有效类型与负载生成可读文本。
        /// </summary>
        /// <param name="behaviorEvent">行为事件。</param>
        /// <param name="index">事件索引。</param>
        /// <returns>显示名称。</returns>
        private static string BuildEventDisplayName(BehaviorEvent behaviorEvent, int index)
        {
            if (behaviorEvent == null)
                return $"Event {index}";

            BehaviorEventType effectiveType = BehaviorEventResolver.ResolveEffectiveType(behaviorEvent);
            return effectiveType switch
            {
                BehaviorEventType.SpawnVFX when behaviorEvent.prefabRef != null => behaviorEvent.prefabRef.name,
                BehaviorEventType.SetObjectActive when !string.IsNullOrWhiteSpace(behaviorEvent.targetObjectPath) =>
                    $"{(behaviorEvent.activeState ? "Active" : "Inactive")} {behaviorEvent.targetObjectPath}",
                BehaviorEventType.SpawnProjectile when behaviorEvent.prefabRef != null => behaviorEvent.prefabRef.name,
                BehaviorEventType.ExecuteGameplayEffect when behaviorEvent.gameplayEffectRef != null =>
                    behaviorEvent.gameplayEffectRef.name,
                BehaviorEventType.ApplyBuff or BehaviorEventType.ApplySelfBuff when behaviorEvent.buffRef != null =>
                    behaviorEvent.buffRef.name,
                _ => effectiveType.ToString()
            };
        }

        /// <summary>
        /// 解析回填事件片段的时长，按事件类型取负载时长。
        /// </summary>
        /// <param name="behaviorEvent">行为事件。</param>
        /// <returns>片段时长（秒）。</returns>
        private static double ResolveImportedEventClipDuration(BehaviorEvent behaviorEvent)
        {
            if (behaviorEvent == null)
                return 0.1d;

            BehaviorEventType effectiveType = BehaviorEventResolver.ResolveEffectiveType(behaviorEvent);
            return effectiveType switch
            {
                BehaviorEventType.SpawnVFX => Math.Max(0.1d, behaviorEvent.autoRecycleTime),
                BehaviorEventType.SetObjectActive => 0.1d,
                BehaviorEventType.CameraShake => Math.Max(0.1d, behaviorEvent.cameraShakeDuration),
                _ => 0.1d
            };
        }

        /// <summary>
        /// 解析回填 Meta 片段的时长，限制在 0.1 秒以内。
        /// </summary>
        /// <param name="behaviorClip">行为数据。</param>
        /// <returns>Meta 片段时长（秒）。</returns>
        private static double ResolveImportedMetaClipDuration(BehaviorClip behaviorClip)
        {
            double totalDuration = behaviorClip != null ? Math.Max(0.01f, behaviorClip.totalDuration) : 0.1d;
            return Math.Max(0.01d, Math.Min(0.1d, totalDuration));
        }

        /// <summary>
        /// 解析回填动画片段的时长：优先下一段起点，其次总时长，最后片段长度。
        /// </summary>
        /// <param name="behaviorClip">行为数据。</param>
        /// <param name="segments">全部动画段。</param>
        /// <param name="currentIndex">当前动画段索引。</param>
        /// <param name="currentStartTime">当前动画段起点。</param>
        /// <returns>片段时长（秒）。</returns>
        private static float ResolveImportedAnimationSegmentDuration(BehaviorClip behaviorClip, AnimationSegment[] segments, int currentIndex, float currentStartTime)
        {
            if (segments == null || currentIndex < 0 || currentIndex >= segments.Length)
                return 0.1f;

            AnimationSegment currentSegment = segments[currentIndex];
            float speed = Mathf.Max(0.01f, behaviorClip != null ? behaviorClip.speedMultiplier : 1f);
            float fallbackDuration = currentSegment?.clip != null ? currentSegment.clip.length / speed : 0.1f;
            if (behaviorClip == null)
                return Mathf.Max(0.01f, fallbackDuration);

            // 查找下一段的起点作为本段结束。
            float nextStartTime = -1f;
            for (int i = currentIndex + 1; i < segments.Length; i++)
            {
                AnimationSegment nextSegment = segments[i];
                if (nextSegment == null)
                    continue;

                if (nextSegment.startTime >= 0f)
                {
                    nextStartTime = nextSegment.startTime;
                    break;
                }
            }

            if (nextStartTime >= 0f)
                return Mathf.Max(0.01f, nextStartTime - currentStartTime);

            if (behaviorClip.totalDuration > currentStartTime)
                return Mathf.Max(0.01f, behaviorClip.totalDuration - currentStartTime);

            return Mathf.Max(0.01f, fallbackDuration);
        }

        /// <summary>
        /// 解析或创建用于预览的音频源，绑定到骨骼对应对象上。
        /// </summary>
        /// <param name="referenceBone">骨骼路径。</param>
        /// <returns>解析或创建的音频源；无法解析骨骼时返回 null。</returns>
        private AudioSource ResolveOrCreatePreviewAudioSource(string referenceBone)
        {
            Transform targetTransform = ResolveReferenceTransformForImport(referenceBone);
            if (targetTransform == null)
                return null;

            if (!targetTransform.TryGetComponent(out AudioSource audioSource))
            {
                audioSource = UnityEditor.Undo.AddComponent<AudioSource>(targetTransform.gameObject);
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 1f;
                createdPreviewAudioSources.Add(audioSource);
            }

            return audioSource;
        }

        /// <summary>
        /// 解析回填时的绑定对象：优先快照路径，其次事件骨骼路径。
        /// </summary>
        /// <param name="clipSnapshot">片段快照。</param>
        /// <param name="behaviorEvent">行为事件。</param>
        /// <returns>绑定对象；无法解析时返回 null。</returns>
        private GameObject ResolveAuthoringBoundObjectForImport(BehaviorAuthoringClipSnapshot clipSnapshot, BehaviorEvent behaviorEvent)
        {
            Transform targetTransform = null;
            if (clipSnapshot != null && !string.IsNullOrWhiteSpace(clipSnapshot.boundObjectPath))
                targetTransform = ResolveTrackBindingTransformForImport(clipSnapshot.boundObjectPath);

            if (targetTransform == null && behaviorEvent != null && !string.IsNullOrWhiteSpace(behaviorEvent.referenceBone))
                targetTransform = ResolveReferenceTransformForImport(behaviorEvent.referenceBone);

            return targetTransform != null ? targetTransform.gameObject : null;
        }

        /// <summary>
        /// 解析回填时的轨道绑定 Transform：优先 Reference Root 下路径，否则 Director。
        /// </summary>
        /// <param name="boundObjectPath">绑定对象路径。</param>
        /// <returns>解析出的 Transform；无法解析时返回 null。</returns>
        private Transform ResolveTrackBindingTransformForImport(string boundObjectPath)
        {
            if (previewReferenceRoot != null)
            {
                Transform root = previewReferenceRoot.transform;
                if (string.IsNullOrWhiteSpace(boundObjectPath))
                    return root;

                Transform resolved = BehaviorReferenceBoneEditorUtility.FindChildByPath(root, boundObjectPath);
                if (resolved != null)
                    return resolved;
            }

            return previewDirector != null ? previewDirector.transform : null;
        }

        /// <summary>
        /// 绑定控制 PlayableAsset 的源对象引用。
        /// </summary>
        /// <param name="controlPlayableAsset">目标控制资产。</param>
        /// <param name="sourceObject">源对象。</param>
        private void BindControlPlayableAssetSource(ControlPlayableAsset controlPlayableAsset, GameObject sourceObject)
        {
            if (controlPlayableAsset == null)
                return;

            PropertyName exposedName = new PropertyName(Guid.NewGuid().ToString("N"));
            controlPlayableAsset.sourceGameObject = new ExposedReference<GameObject>
            {
                exposedName = exposedName,
                defaultValue = null
            };

            if (previewDirector != null)
                previewDirector.SetReferenceValue(exposedName, sourceObject);

            UnityEditor.EditorUtility.SetDirty(controlPlayableAsset);
        }

        /// <summary>
        /// 解析回填时的参考骨骼 Transform：优先 Reference Root 下路径，否则 Director。
        /// </summary>
        /// <param name="referenceBone">骨骼路径。</param>
        /// <returns>解析出的 Transform；无法解析时返回 null。</returns>
        private Transform ResolveReferenceTransformForImport(string referenceBone)
        {
            if (previewReferenceRoot != null)
            {
                Transform root = previewReferenceRoot.transform;
                if (string.IsNullOrWhiteSpace(referenceBone))
                    return root;

                Transform resolved = BehaviorReferenceBoneEditorUtility.FindChildByPath(root, referenceBone);
                if (resolved != null)
                    return resolved;
            }

            return previewDirector != null ? previewDirector.transform : null;
        }

        /// <summary>
        /// 确保 Timeline 中存在 Meta 轨道且至少含一个默认片段。
        /// </summary>
        /// <param name="timelineAsset">目标 Timeline 资产。</param>
        /// <param name="timelineTracks">已收集的轨道列表。</param>
        /// <returns>是否发生了创建或修改。</returns>
        private bool EnsureMetaTrack(TimelineAsset timelineAsset, IReadOnlyList<TrackAsset> timelineTracks)
        {
            bool changed = false;
            BehaviorTimelineMetaTrack metaTrack = EnsureTrack<BehaviorTimelineMetaTrack>(
                timelineAsset,
                MetaTrackName,
                timelineTracks,
                out bool trackChanged);
            changed |= trackChanged;
            if (TryGetTrackClipAsset<BehaviorTimelineMetaClipAsset>(metaTrack, out _))
                return changed;

            // 轨道无片段时创建默认 Meta 片段。
            TimelineClip timelineClip = metaTrack.CreateDefaultClip();
            timelineClip.displayName = MetaTrackName;
            timelineClip.start = 0d;
            timelineClip.duration = 0.1d;
            changed = true;

            if (timelineClip.asset is BehaviorTimelineMetaClipAsset metaAsset)
                ApplyMetaClipAsset(metaAsset, wrapMode, speedMultiplier, priority);

            UnityEditor.EditorUtility.SetDirty(metaTrack);
            return changed;
        }

        /// <summary>
        /// 从 Timeline 中解析唯一的 Meta 片段资产，多个时告警并取第一个。
        /// </summary>
        /// <param name="timelineAsset">目标 Timeline 资产。</param>
        /// <param name="exportWarnings">导出警告列表。</param>
        /// <returns>解析出的 Meta 资产；不存在时返回 null。</returns>
        private static BehaviorTimelineMetaClipAsset ResolveTimelineMeta(TimelineAsset timelineAsset, List<string> exportWarnings)
        {
            int metaTrackCount = 0;
            int metaClipCount = 0;
            BehaviorTimelineMetaClipAsset resolvedMeta = null;
            foreach (TrackAsset track in EnumerateTimelineTracks(timelineAsset))
            {
                if (track is not BehaviorTimelineMetaTrack)
                    continue;

                metaTrackCount++;
                foreach (TimelineClip clip in track.GetClips())
                {
                    if (clip?.asset is not BehaviorTimelineMetaClipAsset metaAsset)
                        continue;

                    metaClipCount++;
                    resolvedMeta ??= metaAsset;
                }
            }

            if (metaTrackCount > 1)
                exportWarnings?.Add($"Detected {metaTrackCount} meta tracks. Only the first meta clip will be exported.");
            if (metaClipCount > 1)
                exportWarnings?.Add($"Detected {metaClipCount} meta clips. Only the first meta clip will be exported.");
            return resolvedMeta;
        }

        /// <summary>
        /// 尝试获取轨道上的第一个指定类型片段资产。
        /// </summary>
        /// <typeparam name="TClipAsset">片段资产类型。</typeparam>
        /// <param name="track">目标轨道。</param>
        /// <param name="clipAsset">输出的片段资产。</param>
        /// <returns>找到时返回 true。</returns>
        private static bool TryGetTrackClipAsset<TClipAsset>(TrackAsset track, out TClipAsset clipAsset) where TClipAsset : class
        {
            if (track != null)
            {
                foreach (TimelineClip clip in track.GetClips())
                {
                    if (clip?.asset is TClipAsset typedClipAsset)
                    {
                        clipAsset = typedClipAsset;
                        return true;
                    }
                }
            }

            clipAsset = null;
            return false;
        }

        /// <summary>
        /// 删除同名且无内容的重复轨道，保留指定轨道。
        /// </summary>
        /// <typeparam name="T">轨道类型。</typeparam>
        /// <param name="timelineAsset">目标 Timeline 资产。</param>
        /// <param name="keepTrack">需要保留的轨道。</param>
        /// <param name="trackName">目标轨道名。</param>
        private static void RemoveEmptyDuplicateTracks<T>(TimelineAsset timelineAsset, T keepTrack, string trackName) where T : TrackAsset
        {
            if (timelineAsset == null || keepTrack == null || string.IsNullOrEmpty(trackName))
                return;

            DeleteTracksByPredicate(
                timelineAsset,
                "Remove Duplicate Behavior Tracks",
                track => !ReferenceEquals(track, keepTrack) &&
                         track is T &&
                         string.Equals(track.name, trackName, StringComparison.Ordinal) &&
                         GetTrackContentScore(track) <= 0);
        }

        /// <summary>
        /// 将行为数据条目导入为动态轨道：逐条校验并导入，最后批量标记脏。
        /// </summary>
        /// <typeparam name="TEntry">条目类型。</typeparam>
        /// <typeparam name="TTrack">轨道类型。</typeparam>
        /// <param name="entries">待导入条目数组。</param>
        /// <param name="isValidEntry">条目有效性判定。</param>
        /// <param name="importEntry">单条导入委托，返回创建的轨道。</param>
        private void ImportBehaviorClipEntriesToDynamicTracks<TEntry, TTrack>(
            TEntry[] entries,
            Func<TEntry, bool> isValidEntry,
            Func<int, TEntry, TTrack> importEntry)
            where TTrack : TrackAsset
        {
            if (entries == null || importEntry == null)
                return;

            HashSet<TrackAsset> dirtyTracks = null;
            for (int i = 0; i < entries.Length; i++)
            {
                TEntry entry = entries[i];
                if (isValidEntry != null && !isValidEntry(entry))
                    continue;

                TTrack importedTrack = importEntry(i, entry);
                AddDirtyTrack(ref dirtyTracks, importedTrack);
            }

            SetTracksDirty(dirtyTracks);
        }

        /// <summary>
        /// 将快照片段导入为单条轨道：按快照复用轨道并逐片段导入。
        /// </summary>
        /// <typeparam name="TEntry">条目类型。</typeparam>
        /// <typeparam name="TTrack">轨道类型。</typeparam>
        /// <param name="timelineAsset">目标 Timeline 资产。</param>
        /// <param name="snapshot">轨道快照。</param>
        /// <param name="trackCache">轨道缓存。</param>
        /// <param name="resolveEntry">从片段快照解析条目的委托。</param>
        /// <param name="isValidEntry">条目有效性判定。</param>
        /// <param name="importEntry">单片段导入委托。</param>
        /// <returns>导入使用的轨道。</returns>
        private TTrack ImportSnapshotEntriesToSingleTrack<TEntry, TTrack>(
            TimelineAsset timelineAsset,
            BehaviorAuthoringTrackSnapshot snapshot,
            ImportTrackCache trackCache,
            Func<BehaviorAuthoringClipSnapshot, TEntry> resolveEntry,
            Func<TEntry, bool> isValidEntry,
            Action<TTrack, BehaviorAuthoringClipSnapshot, TEntry, int> importEntry)
            where TTrack : TrackAsset, new()
        {
            if (timelineAsset == null || snapshot == null || resolveEntry == null || importEntry == null)
                return null;

            BehaviorAuthoringClipSnapshot[] clips = snapshot.clips ?? Array.Empty<BehaviorAuthoringClipSnapshot>();
            TTrack track = null;
            bool clearedTrack = false;
            for (int i = 0; i < clips.Length; i++)
            {
                BehaviorAuthoringClipSnapshot clipSnapshot = clips[i];
                TEntry entry = resolveEntry(clipSnapshot);
                if (isValidEntry != null && !isValidEntry(entry))
                    continue;

                // 确保轨道已准备，再导入当前片段。
                if (EnsurePreparedSnapshotTrack(
                        timelineAsset,
                        snapshot.trackName,
                        trackCache,
                        ref track,
                        ref clearedTrack) == null)
                {
                    continue;
                }

                importEntry(track, clipSnapshot, entry, i);
            }

            if (track != null)
                UnityEditor.EditorUtility.SetDirty(track);
            return track;
        }

        private sealed class ImportSession
        {
            private readonly BehaviorEditorWindow window;
            private readonly TimelineAsset timelineAsset;
            private readonly BehaviorClip behaviorClip;
            private readonly ImportTrackCache trackCache;

            public ImportSession(
                BehaviorEditorWindow window,
                TimelineAsset timelineAsset,
                BehaviorClip behaviorClip)
            {
                this.window = window;
                this.timelineAsset = timelineAsset;
                this.behaviorClip = behaviorClip;
                trackCache = new ImportTrackCache(timelineAsset);
            }

            public void Execute()
            {
                if (window == null || timelineAsset == null || behaviorClip == null)
                    return;

                window.ClearManagedAuthoringTracks(timelineAsset);
                if (behaviorClip.HasAuthoringTrackSnapshots)
                {
                    window.ImportAuthoringTrackSnapshots(timelineAsset, behaviorClip, trackCache);
                }
                else
                {
                    window.ImportMetaFromBehaviorClip(timelineAsset, behaviorClip, trackCache);
                    window.ImportAnimationSegmentsFromBehaviorClip(timelineAsset, behaviorClip, trackCache);
                    window.ImportEventsFromBehaviorClip(timelineAsset, behaviorClip, trackCache);
                    window.ImportHitboxesFromBehaviorClip(timelineAsset, behaviorClip, trackCache);
                    window.ImportTransitionsFromBehaviorClip(timelineAsset, behaviorClip, trackCache);
                }

                RemoveEmptyManagedAuthoringTracks(timelineAsset);
            }
        }

        private sealed class ImportTrackCache
        {
            private readonly TimelineAsset timelineAsset;
            private readonly Dictionary<Type, Dictionary<string, TrackAsset>> tracksByType =
                new Dictionary<Type, Dictionary<string, TrackAsset>>();

            public ImportTrackCache(TimelineAsset timelineAsset)
            {
                this.timelineAsset = timelineAsset;
                CacheExistingTracks();
            }

            public T GetOrCreateExactTrack<T>(TimelineAsset ownerTimelineAsset, string trackName)
                where T : TrackAsset, new()
            {
                TimelineAsset resolvedTimelineAsset = ownerTimelineAsset != null ? ownerTimelineAsset : timelineAsset;
                if (resolvedTimelineAsset == null)
                    return null;

                string resolvedTrackName = trackName ?? string.Empty;
                Dictionary<string, TrackAsset> namedTracks = GetOrCreateNamedTracks(typeof(T));
                if (namedTracks.TryGetValue(resolvedTrackName, out TrackAsset cachedTrack) && cachedTrack is T typedTrack)
                    return typedTrack;

                T createdTrack = GetOrCreateExactTrack<T>(resolvedTimelineAsset, resolvedTrackName);
                if (createdTrack != null)
                    namedTracks[resolvedTrackName] = createdTrack;
                return createdTrack;
            }

            /// <summary>
            /// 缓存 Timeline 中已有的全部轨道。
            /// </summary>
            private void CacheExistingTracks()
            {
                if (timelineAsset == null)
                    return;

                foreach (TrackAsset track in EnumerateTimelineTracks(timelineAsset))
                {
                    if (track == null)
                        continue;

                    Dictionary<string, TrackAsset> namedTracks = GetOrCreateNamedTracks(track.GetType());
                    string trackName = track.name ?? string.Empty;
                    if (!namedTracks.ContainsKey(trackName))
                        namedTracks.Add(trackName, track);
                }
            }

            /// <summary>
            /// 获取或创建指定轨道类型的名称到轨道映射。
            /// </summary>
            /// <param name="trackType">轨道类型。</param>
            /// <returns>名称到轨道的映射。</returns>
            private Dictionary<string, TrackAsset> GetOrCreateNamedTracks(Type trackType)
            {
                if (!tracksByType.TryGetValue(trackType, out Dictionary<string, TrackAsset> namedTracks))
                {
                    namedTracks = new Dictionary<string, TrackAsset>(StringComparer.Ordinal);
                    tracksByType.Add(trackType, namedTracks);
                }

                return namedTracks;
            }
        }

        /// <summary>
        /// 按作者期轨道快照回填 Timeline：排序后逐个导入并重排根轨道。
        /// </summary>
        /// <param name="timelineAsset">目标 Timeline 资产。</param>
        /// <param name="behaviorClip">数据来源行为片段。</param>
        /// <param name="trackCache">轨道缓存。</param>
        private void ImportAuthoringTrackSnapshots(
            TimelineAsset timelineAsset,
            BehaviorClip behaviorClip,
            ImportTrackCache trackCache)
        {
            if (timelineAsset == null || behaviorClip == null || !behaviorClip.HasAuthoringTrackSnapshots)
                return;

            BehaviorAuthoringTrackSnapshot[] snapshots =
                (BehaviorAuthoringTrackSnapshot[])behaviorClip.authoringTracks.Clone();
            Array.Sort(snapshots, CompareTrackSnapshotsBySortIndex);
            List<TrackAsset> importedRootTracks = new List<TrackAsset>(snapshots.Length);

            // 逐个导入快照，收集导入的根轨道。
            for (int i = 0; i < snapshots.Length; i++)
            {
                BehaviorAuthoringTrackSnapshot snapshot = snapshots[i];
                if (snapshot == null)
                    continue;

                TrackAsset importedTrack = ImportAuthoringTrackSnapshot(timelineAsset, snapshot, trackCache);
                if (importedTrack != null && !importedRootTracks.Contains(importedTrack))
                    importedRootTracks.Add(importedTrack);
            }

            // 清理空轨道并按导入顺序重排。
            RemoveEmptyManagedAuthoringTracks(timelineAsset);
            ReorderRootTracksByImportOrder(timelineAsset, importedRootTracks);
        }

        /// <summary>
        /// 按轨道类型分发导入单个作者期轨道快照。
        /// </summary>
        /// <param name="timelineAsset">目标 Timeline 资产。</param>
        /// <param name="snapshot">轨道快照。</param>
        /// <param name="trackCache">轨道缓存。</param>
        /// <returns>导入后的轨道；无法识别类型时返回 null。</returns>
        private TrackAsset ImportAuthoringTrackSnapshot(
            TimelineAsset timelineAsset,
            BehaviorAuthoringTrackSnapshot snapshot,
            ImportTrackCache trackCache)
        {
            if (timelineAsset == null || snapshot == null)
                return null;

            switch (snapshot.trackKind)
            {
                case BehaviorAuthoringTrackKind.Meta:
                    return ImportMetaTrackSnapshot(timelineAsset, snapshot, trackCache);

                case BehaviorAuthoringTrackKind.Animation:
                    return ImportAnimationTrackSnapshot(timelineAsset, snapshot, trackCache);

                case BehaviorAuthoringTrackKind.Audio:
                    return ImportAudioTrackSnapshot(timelineAsset, snapshot, trackCache);

                case BehaviorAuthoringTrackKind.VfxControl:
                case BehaviorAuthoringTrackKind.VfxActivation:
                    return ImportVfxTrackSnapshot(timelineAsset, snapshot, trackCache);

                case BehaviorAuthoringTrackKind.Event:
                    return ImportEventTrackSnapshot(timelineAsset, snapshot, trackCache);

                case BehaviorAuthoringTrackKind.Hitbox:
                    return ImportHitboxTrackSnapshot(timelineAsset, snapshot, trackCache);

                case BehaviorAuthoringTrackKind.Transition:
                    return ImportTransitionTrackSnapshot(timelineAsset, snapshot, trackCache);
            }

            return null;
        }

        /// <summary>
        /// 按特效轨道子类型分发导入：激活或控制。
        /// </summary>
        /// <param name="timelineAsset">目标 Timeline 资产。</param>
        /// <param name="snapshot">轨道快照。</param>
        /// <param name="trackCache">轨道缓存。</param>
        /// <returns>导入后的轨道。</returns>
        private TrackAsset ImportVfxTrackSnapshot(
            TimelineAsset timelineAsset,
            BehaviorAuthoringTrackSnapshot snapshot,
            ImportTrackCache trackCache)
        {
            return snapshot.trackKind switch
            {
                BehaviorAuthoringTrackKind.VfxActivation => ImportVfxActivationTrackSnapshot(
                    timelineAsset,
                    snapshot,
                    trackCache),
                _ => ImportVfxControlTrackSnapshot(timelineAsset, snapshot, trackCache)
            };
        }

        /// <summary>
        /// 导入特效控制轨道快照：逐片段创建 Control 片段。
        /// </summary>
        /// <param name="timelineAsset">目标 Timeline 资产。</param>
        /// <param name="snapshot">轨道快照。</param>
        /// <param name="trackCache">轨道缓存。</param>
        /// <returns>导入后的 Control 轨道。</returns>
        private TrackAsset ImportVfxControlTrackSnapshot(
            TimelineAsset timelineAsset,
            BehaviorAuthoringTrackSnapshot snapshot,
            ImportTrackCache trackCache)
        {
            BehaviorAuthoringClipSnapshot[] clips = snapshot.clips ?? Array.Empty<BehaviorAuthoringClipSnapshot>();
            ControlTrack controlTrack = null;
            bool clearedTrack = false;
            for (int i = 0; i < clips.Length; i++)
            {
                BehaviorAuthoringClipSnapshot clipSnapshot = clips[i];
                BehaviorEvent behaviorEvent = clipSnapshot?.behaviorEvent;
                bool hasPrefab = behaviorEvent?.prefabRef != null;
                bool hasBoundPath = clipSnapshot != null && !string.IsNullOrWhiteSpace(clipSnapshot.boundObjectPath);
                if (behaviorEvent == null || (!hasPrefab && !hasBoundPath))
                    continue;

                if (EnsurePreparedSnapshotTrack(
                        timelineAsset,
                        snapshot.trackName,
                        trackCache,
                        ref controlTrack,
                        ref clearedTrack) == null)
                {
                    continue;
                }

                TimelineClip timelineClip = controlTrack.CreateDefaultClip();
                timelineClip.displayName = ResolveImportedClipDisplayName(
                    clipSnapshot,
                    hasPrefab ? behaviorEvent.prefabRef.name : $"VFX {i}");
                timelineClip.start = clipSnapshot.startTime;
                timelineClip.duration = Math.Max(0.01d, clipSnapshot.duration);

                if (timelineClip.asset is not ControlPlayableAsset controlPlayableAsset)
                    continue;

                ConfigureControlPlayableAsset(
                    controlPlayableAsset,
                    behaviorEvent.prefabRef,
                    clipSnapshot.controlPostPlayback);

                GameObject boundObject = ResolveAuthoringBoundObjectForImport(clipSnapshot, behaviorEvent);
                BindControlPlayableAssetSource(controlPlayableAsset, boundObject);
            }

            if (controlTrack != null)
                UnityEditor.EditorUtility.SetDirty(controlTrack);
            return controlTrack;
        }

        /// <summary>
        /// 按特效激活轨道快照导入激活事件到单条轨道，并绑定目标对象。
        /// </summary>
        /// <param name="timelineAsset">目标 Timeline 资产。</param>
        /// <param name="snapshot">轨道快照。</param>
        /// <param name="trackCache">轨道缓存。</param>
        /// <returns>导入后的激活轨道。</returns>
        private TrackAsset ImportVfxActivationTrackSnapshot(
            TimelineAsset timelineAsset,
            BehaviorAuthoringTrackSnapshot snapshot,
            ImportTrackCache trackCache)
        {
            BehaviorAuthoringClipSnapshot[] clips = snapshot.clips ?? Array.Empty<BehaviorAuthoringClipSnapshot>();
            ActivationTrack activationTrack = null;
            bool clearedTrack = false;
            GameObject boundObject = null;
            bool bindingResolved = false;
            for (int i = 0; i < clips.Length; i++)
            {
                BehaviorAuthoringClipSnapshot clipSnapshot = clips[i];
                BehaviorEvent behaviorEvent = clipSnapshot?.behaviorEvent;
                bool hasBoundPath = clipSnapshot != null && !string.IsNullOrWhiteSpace(clipSnapshot.boundObjectPath);
                if (behaviorEvent == null && !hasBoundPath)
                    continue;

                if (EnsurePreparedSnapshotTrack(
                        timelineAsset,
                        snapshot.trackName,
                        trackCache,
                        ref activationTrack,
                        ref clearedTrack) == null)
                {
                    continue;
                }

                // 创建激活片段并设置显示名。
                TimelineClip timelineClip = activationTrack.CreateDefaultClip();
                timelineClip.displayName = ResolveImportedClipDisplayName(
                    clipSnapshot,
                    behaviorEvent?.prefabRef != null ? behaviorEvent.prefabRef.name : $"Active VFX {i}");
                timelineClip.start = clipSnapshot.startTime;
                timelineClip.duration = Math.Max(0.01d, clipSnapshot.duration);

                // 首次解析绑定目标对象。
                if (!bindingResolved)
                {
                    boundObject = ResolveAuthoringBoundObjectForImport(clipSnapshot, behaviorEvent);
                    bindingResolved = true;
                }
            }

            // 将目标对象绑定到激活轨道。
            if (previewDirector != null && activationTrack != null && boundObject != null)
                previewDirector.SetGenericBinding(activationTrack, boundObject);

            if (activationTrack != null)
                UnityEditor.EditorUtility.SetDirty(activationTrack);
            return activationTrack;
        }

        /// <summary>
        /// 清空全部管理轨道：删除原生动画/音频轨道，清空其余管理轨道的片段。
        /// </summary>
        /// <param name="timelineAsset">目标 Timeline 资产。</param>
        private void ClearManagedAuthoringTracks(TimelineAsset timelineAsset)
        {
            if (timelineAsset == null)
                return;

            DeleteTracksByPredicate(
                timelineAsset,
                "Clear Managed Timeline Tracks",
                track => track is AnimationTrack || track is AudioTrack);

            foreach (TrackAsset track in EnumerateTimelineTracks(timelineAsset))
            {
                if (track == null)
                    continue;

                if (track is ControlTrack ||
                    track is ActivationTrack ||
                    track is BehaviorTimelineMetaTrack ||
                    track is BehaviorTimelineEventTrack ||
                    track is BehaviorTimelineHitboxTrack ||
                    track is BehaviorTimelineTransitionTrack)
                {
                    ClearTrackClips(track);
                }
            }
        }

        /// <summary>
        /// 从行为数据导入 Meta 片段到 Meta 轨道。
        /// </summary>
        /// <param name="timelineAsset">目标 Timeline 资产。</param>
        /// <param name="behaviorClip">数据来源行为片段。</param>
        /// <param name="trackCache">轨道缓存。</param>
        private void ImportMetaFromBehaviorClip(
            TimelineAsset timelineAsset,
            BehaviorClip behaviorClip,
            ImportTrackCache trackCache)
        {
            if (timelineAsset == null || behaviorClip == null)
                return;

            BehaviorTimelineMetaTrack metaTrack =
                EnsureTrack<BehaviorTimelineMetaTrack>(timelineAsset, MetaTrackName, null, out _);
            ClearTrackClips(metaTrack);
            ImportMetaClipToTrack(
                timelineAsset,
                metaTrack,
                trackCache,
                MetaTrackName,
                MetaTrackName,
                0d,
                ResolveImportedMetaClipDuration(behaviorClip),
                behaviorClip.wrapMode,
                behaviorClip.speedMultiplier,
                behaviorClip.priority);
            UnityEditor.EditorUtility.SetDirty(metaTrack);
        }

        /// <summary>
        /// 按 Meta 轨道快照导入 Meta 片段。
        /// </summary>
        /// <param name="timelineAsset">目标 Timeline 资产。</param>
        /// <param name="snapshot">轨道快照。</param>
        /// <param name="trackCache">轨道缓存。</param>
        /// <returns>导入后的 Meta 轨道。</returns>
        private BehaviorTimelineMetaTrack ImportMetaTrackSnapshot(
            TimelineAsset timelineAsset,
            BehaviorAuthoringTrackSnapshot snapshot,
            ImportTrackCache trackCache)
        {
            return ImportSnapshotEntriesToSingleTrack<BehaviorTimelineMetaSnapshot, BehaviorTimelineMetaTrack>(
                timelineAsset,
                snapshot,
                trackCache,
                clipSnapshot => clipSnapshot?.meta,
                meta => meta != null,
                (metaTrack, clipSnapshot, meta, _) =>
                {
                    ImportMetaClipToTrack(
                        timelineAsset,
                        metaTrack,
                        trackCache,
                        snapshot.trackName,
                        ResolveImportedClipDisplayName(clipSnapshot, MetaTrackName),
                        clipSnapshot.startTime,
                        clipSnapshot.duration,
                        meta.wrapMode,
                        meta.speedMultiplier,
                        meta.priority);
                });
        }

        /// <summary>
        /// 配置 Meta 时间轴片段的位置与时长。
        /// </summary>
        /// <param name="timelineClip">目标片段。</param>
        /// <param name="displayName">显示名。</param>
        /// <param name="startTime">起点。</param>
        /// <param name="duration">时长。</param>
        private static void ConfigureMetaTimelineClip(
            TimelineClip timelineClip,
            string displayName,
            double startTime,
            double duration)
        {
            if (timelineClip == null)
                return;

            timelineClip.displayName = displayName;
            timelineClip.start = startTime;
            timelineClip.duration = Math.Max(0.01d, duration);
        }

        /// <summary>
        /// 将环绕模式、速度与优先级写入 Meta 片段资产。
        /// </summary>
        /// <param name="metaAsset">目标 Meta 资产。</param>
        /// <param name="resolvedWrapMode">环绕模式。</param>
        /// <param name="resolvedSpeedMultiplier">速度倍率。</param>
        /// <param name="resolvedPriority">打断优先级。</param>
        private static void ApplyMetaClipAsset(
            BehaviorTimelineMetaClipAsset metaAsset,
            WrapMode resolvedWrapMode,
            float resolvedSpeedMultiplier,
            InterruptPriority resolvedPriority)
        {
            if (metaAsset == null)
                return;

            metaAsset.wrapMode = resolvedWrapMode;
            metaAsset.speedMultiplier = Mathf.Max(0.01f, resolvedSpeedMultiplier);
            metaAsset.priority = resolvedPriority;
            UnityEditor.EditorUtility.SetDirty(metaAsset);
        }

        /// <summary>
        /// 在 Meta 轨道上创建并配置 Meta 片段。
        /// </summary>
        /// <param name="timelineAsset">目标 Timeline 资产。</param>
        /// <param name="metaTrack">目标 Meta 轨道。</param>
        /// <param name="trackCache">轨道缓存。</param>
        /// <param name="trackName">轨道名。</param>
        /// <param name="displayName">显示名。</param>
        /// <param name="startTime">起点。</param>
        /// <param name="duration">时长。</param>
        /// <param name="wrapMode">环绕模式。</param>
        /// <param name="speedMultiplier">速度倍率。</param>
        /// <param name="priority">打断优先级。</param>
        /// <returns>目标 Meta 轨道。</returns>
        private BehaviorTimelineMetaTrack ImportMetaClipToTrack(
            TimelineAsset timelineAsset,
            BehaviorTimelineMetaTrack metaTrack,
            ImportTrackCache trackCache,
            string trackName,
            string displayName,
            double startTime,
            double duration,
            WrapMode wrapMode,
            float speedMultiplier,
            InterruptPriority priority)
        {
            metaTrack ??= trackCache.GetOrCreateExactTrack<BehaviorTimelineMetaTrack>(timelineAsset, trackName);
            if (metaTrack == null)
                return null;

            TimelineClip timelineClip = metaTrack.CreateDefaultClip();
            ConfigureMetaTimelineClip(timelineClip, displayName, startTime, duration);
            ApplyMetaClipAsset(
                timelineClip.asset as BehaviorTimelineMetaClipAsset,
                wrapMode,
                speedMultiplier,
                priority);
            return metaTrack;
        }

        /// <summary>
        /// 从行为数据导入动画段到对应动画轨道（按层动态命名）。
        /// </summary>
        /// <param name="timelineAsset">目标 Timeline 资产。</param>
        /// <param name="behaviorClip">数据来源行为片段。</param>
        /// <param name="trackCache">轨道缓存。</param>
        private void ImportAnimationSegmentsFromBehaviorClip(
            TimelineAsset timelineAsset,
            BehaviorClip behaviorClip,
            ImportTrackCache trackCache)
        {
            if (timelineAsset == null || behaviorClip == null)
                return;

            AnimationSegment[] segments = behaviorClip.animationSegments ?? Array.Empty<AnimationSegment>();
            float fallbackStartTime = 0f;
            ImportBehaviorClipEntriesToDynamicTracks(
                segments,
                segment => segment != null && segment.clip != null,
                (i, segment) =>
                {
                    // 解析起点与时长，缺失时按顺序累积。
                    float clipStart = segment.startTime >= 0f ? segment.startTime : fallbackStartTime;
                    float clipDuration = ResolveImportedAnimationSegmentDuration(behaviorClip, segments, i, clipStart);
                    fallbackStartTime = Mathf.Max(fallbackStartTime, clipStart + clipDuration);

                    return ImportAnimationSegmentToTrack(
                        timelineAsset,
                        null,
                        trackCache,
                        ResolveTrackNameOrDefault(
                            segment.authoringTrackName,
                            $"Behavior Animation L{Mathf.Max(0, segment.layer)}"),
                        segment.clip.name,
                        clipStart,
                        clipDuration,
                        segment);
                });
        }

        /// <summary>
        /// 按动画轨道快照导入动画段到单条动画轨道。
        /// </summary>
        /// <param name="timelineAsset">目标 Timeline 资产。</param>
        /// <param name="snapshot">轨道快照。</param>
        /// <param name="trackCache">轨道缓存。</param>
        /// <returns>导入后的动画轨道。</returns>
        private AnimationTrack ImportAnimationTrackSnapshot(
            TimelineAsset timelineAsset,
            BehaviorAuthoringTrackSnapshot snapshot,
            ImportTrackCache trackCache)
        {
            return ImportSnapshotEntriesToSingleTrack<AnimationSegment, AnimationTrack>(
                timelineAsset,
                snapshot,
                trackCache,
                clipSnapshot => clipSnapshot?.animationSegment,
                segment => segment != null && segment.clip != null,
                (animationTrack, clipSnapshot, segment, _) =>
                {
                    ImportAnimationSegmentToTrack(
                        timelineAsset,
                        animationTrack,
                        trackCache,
                        snapshot.trackName,
                        ResolveImportedClipDisplayName(clipSnapshot, segment.clip.name),
                        clipSnapshot.startTime,
                        clipSnapshot.duration,
                        segment);
                });
        }

        /// <summary>
        /// 在动画轨道上创建动画时间轴片段，写入动画资源与交叉淡化。
        /// </summary>
        /// <param name="animationTrack">目标动画轨道。</param>
        /// <param name="displayName">显示名。</param>
        /// <param name="startTime">起点。</param>
        /// <param name="duration">时长。</param>
        /// <param name="segment">动画段数据。</param>
        private static void CreateAnimationTimelineClip(
            AnimationTrack animationTrack,
            string displayName,
            double startTime,
            double duration,
            AnimationSegment segment)
        {
            TimelineClip timelineClip = animationTrack.CreateClip<AnimationPlayableAsset>();
            timelineClip.displayName = displayName;
            timelineClip.start = startTime;
            timelineClip.duration = Math.Max(0.01d, duration);
            timelineClip.easeInDuration = Mathf.Clamp01(segment.crossFadeDuration) * timelineClip.duration;

            // 写入动画资源。
            if (timelineClip.asset is AnimationPlayableAsset animationPlayableAsset)
                animationPlayableAsset.clip = segment.clip;
        }

        /// <summary>
        /// 将动画段导入到指定动画轨道，轨道缺失时创建。
        /// </summary>
        /// <param name="timelineAsset">目标 Timeline 资产。</param>
        /// <param name="animationTrack">目标动画轨道。</param>
        /// <param name="trackCache">轨道缓存。</param>
        /// <param name="trackName">轨道名。</param>
        /// <param name="displayName">显示名。</param>
        /// <param name="startTime">起点。</param>
        /// <param name="duration">时长。</param>
        /// <param name="segment">动画段数据。</param>
        /// <returns>目标动画轨道。</returns>
        private AnimationTrack ImportAnimationSegmentToTrack(
            TimelineAsset timelineAsset,
            AnimationTrack animationTrack,
            ImportTrackCache trackCache,
            string trackName,
            string displayName,
            double startTime,
            double duration,
            AnimationSegment segment)
        {
            animationTrack ??= trackCache.GetOrCreateExactTrack<AnimationTrack>(timelineAsset, trackName);
            if (animationTrack == null)
                return null;

            CreateAnimationTimelineClip(animationTrack, displayName, startTime, duration, segment);
            return animationTrack;
        }

        /// <summary>
        /// 从行为数据导入事件：音频事件进音频轨道，其余进事件轨道。
        /// </summary>
        /// <param name="timelineAsset">目标 Timeline 资产。</param>
        /// <param name="behaviorClip">数据来源行为片段。</param>
        /// <param name="trackCache">轨道缓存。</param>
        private void ImportEventsFromBehaviorClip(
            TimelineAsset timelineAsset,
            BehaviorClip behaviorClip,
            ImportTrackCache trackCache)
        {
            if (timelineAsset == null || behaviorClip == null)
                return;

            BehaviorEvent[] events = behaviorClip.events ?? Array.Empty<BehaviorEvent>();
            ImportBehaviorClipEntriesToDynamicTracks<BehaviorEvent, TrackAsset>(
                events,
                behaviorEvent => behaviorEvent != null,
                (i, behaviorEvent) =>
                {
                    // 音频事件分发到音频轨道。
                    if (BehaviorEventResolver.ResolveEffectiveType(behaviorEvent) == BehaviorEventType.PlayAudio &&
                        behaviorEvent.audioRef != null)
                    {
                        return ImportAudioEventToTrack(
                            timelineAsset,
                            null,
                            trackCache,
                            ResolveTrackNameOrDefault(
                                behaviorEvent.authoringTrackName,
                                BuildAudioTrackName(behaviorEvent.referenceBone)),
                            behaviorEvent.audioRef.name,
                            Mathf.Max(0f, behaviorEvent.time),
                            behaviorEvent.audioRef.length,
                            behaviorEvent);
                    }

                    // 其余事件进事件轨道。
                    return ImportBehaviorEventToTrack(
                        timelineAsset,
                        null,
                        trackCache,
                        ResolveTrackNameOrDefault(behaviorEvent.authoringTrackName, EventTrackName),
                        BuildEventDisplayName(behaviorEvent, i),
                        Mathf.Max(0f, behaviorEvent.time),
                        ResolveImportedEventClipDuration(behaviorEvent),
                        behaviorEvent);
                });
        }

        /// <summary>
        /// 按事件轨道快照导入事件：音频事件单独进音频轨道，其余进事件轨道。
        /// </summary>
        /// <param name="timelineAsset">目标 Timeline 资产。</param>
        /// <param name="snapshot">轨道快照。</param>
        /// <param name="trackCache">轨道缓存。</param>
        /// <returns>导入后的事件轨道。</returns>
        private BehaviorTimelineEventTrack ImportEventTrackSnapshot(
            TimelineAsset timelineAsset,
            BehaviorAuthoringTrackSnapshot snapshot,
            ImportTrackCache trackCache)
        {
            BehaviorAuthoringClipSnapshot[] clips = snapshot.clips ?? Array.Empty<BehaviorAuthoringClipSnapshot>();
            BehaviorTimelineEventTrack eventTrack = null;
            HashSet<TrackAsset> dirtyTracks = null;
            bool clearedTrack = false;
            for (int i = 0; i < clips.Length; i++)
            {
                BehaviorAuthoringClipSnapshot clipSnapshot = clips[i];
                BehaviorEvent behaviorEvent = clipSnapshot?.behaviorEvent;
                if (behaviorEvent == null)
                    continue;

                // 音频事件分发到音频轨道。
                if (BehaviorEventResolver.ResolveEffectiveType(behaviorEvent) == BehaviorEventType.PlayAudio &&
                    behaviorEvent.audioRef != null)
                {
                    AudioTrack audioTrack = ImportAudioEventToTrack(
                        timelineAsset,
                        null,
                        trackCache,
                        ResolveTrackNameOrDefault(
                            behaviorEvent?.authoringTrackName,
                            BuildAudioTrackName(behaviorEvent?.referenceBone)),
                        ResolveImportedClipDisplayName(clipSnapshot, behaviorEvent.audioRef.name),
                        clipSnapshot.startTime,
                        clipSnapshot.duration,
                        behaviorEvent);
                    AddDirtyTrack(ref dirtyTracks, audioTrack);
                    continue;
                }

                // 确保事件轨道已准备，再导入当前事件。
                if (EnsurePreparedSnapshotTrack(
                        timelineAsset,
                        snapshot.trackName,
                        trackCache,
                        ref eventTrack,
                        ref clearedTrack) == null)
                {
                    continue;
                }

                ImportBehaviorEventToTrack(
                    timelineAsset,
                    eventTrack,
                    trackCache,
                    snapshot.trackName,
                    ResolveImportedClipDisplayName(clipSnapshot, BuildEventDisplayName(behaviorEvent, i)),
                    clipSnapshot.startTime,
                    clipSnapshot.duration,
                    behaviorEvent);
            }

            SetTracksDirty(dirtyTracks);
            if (eventTrack != null)
                UnityEditor.EditorUtility.SetDirty(eventTrack);
            return eventTrack;
        }

        /// <summary>
        /// 在事件轨道上创建行为事件时间轴片段。
        /// </summary>
        /// <param name="eventTrack">目标事件轨道。</param>
        /// <param name="displayName">显示名。</param>
        /// <param name="startTime">起点。</param>
        /// <param name="duration">时长。</param>
        /// <param name="behaviorEvent">行为事件数据。</param>
        private static void CreateBehaviorEventTimelineClip(
            BehaviorTimelineEventTrack eventTrack,
            string displayName,
            double startTime,
            double duration,
            BehaviorEvent behaviorEvent)
        {
            TimelineClip timelineClip = eventTrack.CreateDefaultClip();
            timelineClip.displayName = displayName;
            timelineClip.start = startTime;
            timelineClip.duration = Math.Max(0.01d, duration);

            if (timelineClip.asset is BehaviorTimelineEventClipAsset clipAsset)
                clipAsset.eventData =
                    BehaviorEventResolver.CreateNormalizedClone(behaviorEvent, behaviorEvent.time);
        }

        /// <summary>
        /// 将行为事件导入到指定事件轨道，轨道缺失时创建。
        /// </summary>
        /// <param name="timelineAsset">目标 Timeline 资产。</param>
        /// <param name="eventTrack">目标事件轨道。</param>
        /// <param name="trackCache">轨道缓存。</param>
        /// <param name="trackName">轨道名。</param>
        /// <param name="displayName">显示名。</param>
        /// <param name="startTime">起点。</param>
        /// <param name="duration">时长。</param>
        /// <param name="behaviorEvent">行为事件数据。</param>
        /// <returns>目标事件轨道。</returns>
        private BehaviorTimelineEventTrack ImportBehaviorEventToTrack(
            TimelineAsset timelineAsset,
            BehaviorTimelineEventTrack eventTrack,
            ImportTrackCache trackCache,
            string trackName,
            string displayName,
            double startTime,
            double duration,
            BehaviorEvent behaviorEvent)
        {
            eventTrack ??= trackCache.GetOrCreateExactTrack<BehaviorTimelineEventTrack>(timelineAsset, trackName);
            if (eventTrack == null)
                return null;

            CreateBehaviorEventTimelineClip(eventTrack, displayName, startTime, duration, behaviorEvent);
            return eventTrack;
        }

        /// <summary>
        /// 按音频轨道快照导入音频事件，并绑定预览音频源。
        /// </summary>
        /// <param name="timelineAsset">目标 Timeline 资产。</param>
        /// <param name="snapshot">轨道快照。</param>
        /// <param name="trackCache">轨道缓存。</param>
        /// <returns>导入后的音频轨道。</returns>
        private AudioTrack ImportAudioTrackSnapshot(
            TimelineAsset timelineAsset,
            BehaviorAuthoringTrackSnapshot snapshot,
            ImportTrackCache trackCache)
        {
            AudioSource previewAudioSource = null;
            bool previewAudioSourceResolved = false;
            AudioTrack audioTrack = ImportSnapshotEntriesToSingleTrack<BehaviorEvent, AudioTrack>(
                timelineAsset,
                snapshot,
                trackCache,
                clipSnapshot => clipSnapshot?.behaviorEvent,
                behaviorEvent => behaviorEvent != null &&
                                 BehaviorEventResolver.ResolveEffectiveType(behaviorEvent) == BehaviorEventType.PlayAudio &&
                                 behaviorEvent.audioRef != null,
                (resolvedAudioTrack, clipSnapshot, behaviorEvent, _) =>
                {
                    ImportAudioEventToTrack(
                        timelineAsset,
                        resolvedAudioTrack,
                        trackCache,
                        snapshot.trackName,
                        ResolveImportedClipDisplayName(clipSnapshot, behaviorEvent.audioRef.name),
                        clipSnapshot.startTime,
                        clipSnapshot.duration,
                        behaviorEvent,
                        false);

                    // 首次导入时解析预览音频源供轨道绑定。
                    if (!previewAudioSourceResolved && previewDirector != null)
                    {
                        previewAudioSource = ResolveOrCreatePreviewAudioSource(behaviorEvent.referenceBone);
                        previewAudioSourceResolved = true;
                    }
                });

            // 将预览音频源绑定到导入的音频轨道。
            if (previewDirector != null && audioTrack != null && previewAudioSource != null)
                previewDirector.SetGenericBinding(audioTrack, previewAudioSource);

            return audioTrack;
        }

        /// <summary>
        /// 在音频轨道上创建音频时间轴片段。
        /// </summary>
        /// <param name="audioTrack">目标音频轨道。</param>
        /// <param name="displayName">显示名。</param>
        /// <param name="startTime">起点。</param>
        /// <param name="duration">时长。</param>
        /// <param name="behaviorEvent">音频行为事件。</param>
        private void CreateAudioTimelineClip(
            AudioTrack audioTrack,
            string displayName,
            double startTime,
            double duration,
            BehaviorEvent behaviorEvent)
        {
            TimelineClip timelineClip = audioTrack.CreateClip<AudioPlayableAsset>();
            timelineClip.displayName = displayName;
            timelineClip.start = startTime;
            timelineClip.duration = Math.Max(0.01d, duration);

            // 写入音频资源、循环与音量。
            if (timelineClip.asset is AudioPlayableAsset audioPlayableAsset)
            {
                audioPlayableAsset.clip = behaviorEvent.audioRef;
                audioPlayableAsset.loop = behaviorEvent.audioLoop;
                TrySetAudioPlayableAssetVolume(audioPlayableAsset, Mathf.Clamp01(behaviorEvent.audioVolume));
            }
        }

        /// <summary>
        /// 将音频事件导入到指定音频轨道，轨道缺失时创建。
        /// </summary>
        /// <param name="timelineAsset">目标 Timeline 资产。</param>
        /// <param name="audioTrack">目标音频轨道。</param>
        /// <param name="trackCache">轨道缓存。</param>
        /// <param name="trackName">轨道名。</param>
        /// <param name="displayName">显示名。</param>
        /// <param name="startTime">起点。</param>
        /// <param name="duration">时长。</param>
        /// <param name="behaviorEvent">音频行为事件。</param>
        /// <param name="bindPreviewTrack">是否绑定预览音频源。</param>
        /// <returns>目标音频轨道。</returns>
        private AudioTrack ImportAudioEventToTrack(
            TimelineAsset timelineAsset,
            AudioTrack audioTrack,
            ImportTrackCache trackCache,
            string trackName,
            string displayName,
            double startTime,
            double duration,
            BehaviorEvent behaviorEvent,
            bool bindPreviewTrack = true)
        {
            if (behaviorEvent?.audioRef == null)
                return null;

            audioTrack ??= trackCache.GetOrCreateExactTrack<AudioTrack>(timelineAsset, trackName);
            if (audioTrack == null)
                return null;

            CreateAudioTimelineClip(audioTrack, displayName, startTime, duration, behaviorEvent);
            if (bindPreviewTrack)
                BindPreviewAudioTrack(audioTrack, behaviorEvent.referenceBone);
            return audioTrack;
        }

        /// <summary>
        /// 将音频轨道绑定到骨骼对应的预览音频源。
        /// </summary>
        /// <param name="audioTrack">目标音频轨道。</param>
        /// <param name="referenceBone">骨骼路径。</param>
        private void BindPreviewAudioTrack(AudioTrack audioTrack, string referenceBone)
        {
            if (previewDirector == null || audioTrack == null)
                return;

            AudioSource previewAudioSource = ResolveOrCreatePreviewAudioSource(referenceBone);
            if (previewAudioSource != null)
                previewDirector.SetGenericBinding(audioTrack, previewAudioSource);
        }

        /// <summary>
        /// 从行为数据导入 Hitbox 到 Hitbox 轨道。
        /// </summary>
        /// <param name="timelineAsset">目标 Timeline 资产。</param>
        /// <param name="behaviorClip">数据来源行为片段。</param>
        /// <param name="trackCache">轨道缓存。</param>
        private void ImportHitboxesFromBehaviorClip(
            TimelineAsset timelineAsset,
            BehaviorClip behaviorClip,
            ImportTrackCache trackCache)
        {
            if (timelineAsset == null || behaviorClip == null)
                return;

            HitboxDef[] hitboxes = behaviorClip.hitboxes ?? Array.Empty<HitboxDef>();
            ImportBehaviorClipEntriesToDynamicTracks(
                hitboxes,
                hitbox => hitbox != null,
                (i, hitbox) => ImportHitboxToTrack(
                    timelineAsset,
                    null,
                    trackCache,
                    ResolveTrackNameOrDefault(hitbox.authoringTrackName, HitboxTrackName),
                    string.IsNullOrWhiteSpace(hitbox.name) ? $"Hitbox {i}" : hitbox.name,
                    Mathf.Max(0f, hitbox.startTime),
                    hitbox.duration,
                    hitbox));
        }

        /// <summary>
        /// 按 Hitbox 轨道快照导入 Hitbox 到单条轨道。
        /// </summary>
        /// <param name="timelineAsset">目标 Timeline 资产。</param>
        /// <param name="snapshot">轨道快照。</param>
        /// <param name="trackCache">轨道缓存。</param>
        /// <returns>导入后的 Hitbox 轨道。</returns>
        private BehaviorTimelineHitboxTrack ImportHitboxTrackSnapshot(
            TimelineAsset timelineAsset,
            BehaviorAuthoringTrackSnapshot snapshot,
            ImportTrackCache trackCache)
        {
            return ImportSnapshotEntriesToSingleTrack<HitboxDef, BehaviorTimelineHitboxTrack>(
                timelineAsset,
                snapshot,
                trackCache,
                clipSnapshot => clipSnapshot?.hitboxDef,
                hitbox => hitbox != null,
                (hitboxTrack, clipSnapshot, hitbox, i) =>
                {
                    ImportHitboxToTrack(
                        timelineAsset,
                        hitboxTrack,
                        trackCache,
                        snapshot.trackName,
                        ResolveImportedClipDisplayName(
                            clipSnapshot,
                            string.IsNullOrWhiteSpace(hitbox.name) ? $"Hitbox {i}" : hitbox.name),
                        clipSnapshot.startTime,
                        clipSnapshot.duration,
                        hitbox);
                });
        }

        /// <summary>
        /// 在 Hitbox 轨道上创建 Hitbox 时间轴片段。
        /// </summary>
        /// <param name="hitboxTrack">目标 Hitbox 轨道。</param>
        /// <param name="displayName">显示名。</param>
        /// <param name="startTime">起点。</param>
        /// <param name="duration">时长。</param>
        /// <param name="hitbox">Hitbox 数据。</param>
        private static void CreateHitboxTimelineClip(
            BehaviorTimelineHitboxTrack hitboxTrack,
            string displayName,
            double startTime,
            double duration,
            HitboxDef hitbox)
        {
            TimelineClip timelineClip = hitboxTrack.CreateDefaultClip();
            timelineClip.displayName = displayName;
            timelineClip.start = startTime;
            timelineClip.duration = Math.Max(0.01d, duration);

            // 写入克隆的 Hitbox 数据。
            if (timelineClip.asset is BehaviorTimelineHitboxClipAsset clipAsset)
                clipAsset.hitboxData = CloneHitboxDef(hitbox, hitbox.startTime, hitbox.duration);
        }

        /// <summary>
        /// 将 Hitbox 导入到指定轨道，轨道缺失时创建。
        /// </summary>
        /// <param name="timelineAsset">目标 Timeline 资产。</param>
        /// <param name="hitboxTrack">目标 Hitbox 轨道。</param>
        /// <param name="trackCache">轨道缓存。</param>
        /// <param name="trackName">轨道名。</param>
        /// <param name="displayName">显示名。</param>
        /// <param name="startTime">起点。</param>
        /// <param name="duration">时长。</param>
        /// <param name="hitbox">Hitbox 数据。</param>
        /// <returns>目标 Hitbox 轨道。</returns>
        private BehaviorTimelineHitboxTrack ImportHitboxToTrack(
            TimelineAsset timelineAsset,
            BehaviorTimelineHitboxTrack hitboxTrack,
            ImportTrackCache trackCache,
            string trackName,
            string displayName,
            double startTime,
            double duration,
            HitboxDef hitbox)
        {
            hitboxTrack ??= trackCache.GetOrCreateExactTrack<BehaviorTimelineHitboxTrack>(timelineAsset, trackName);
            if (hitboxTrack == null)
                return null;

            CreateHitboxTimelineClip(hitboxTrack, displayName, startTime, duration, hitbox);
            return hitboxTrack;
        }

        /// <summary>
        /// 从行为数据导入过渡到过渡轨道。
        /// </summary>
        /// <param name="timelineAsset">目标 Timeline 资产。</param>
        /// <param name="behaviorClip">数据来源行为片段。</param>
        /// <param name="trackCache">轨道缓存。</param>
        private void ImportTransitionsFromBehaviorClip(
            TimelineAsset timelineAsset,
            BehaviorClip behaviorClip,
            ImportTrackCache trackCache)
        {
            if (timelineAsset == null || behaviorClip == null)
                return;

            BehaviorTransitionDefinition[] transitions =
                behaviorClip.transitions ?? Array.Empty<BehaviorTransitionDefinition>();
            ImportBehaviorClipEntriesToDynamicTracks(
                transitions,
                transition => transition != null,
                (i, transition) =>
                {
                    float startTime = Mathf.Max(0f, transition.startTime);
                    float duration = Mathf.Max(0.01f, transition.endTime - transition.startTime);
                    return ImportTransitionToTrack(
                        timelineAsset,
                        null,
                        trackCache,
                        ResolveTrackNameOrDefault(transition.authoringTrackName, TransitionTrackName),
                        string.IsNullOrWhiteSpace(transition.targetBehaviorKey) ? $"Transition {i}" : transition.targetBehaviorKey,
                        startTime,
                        duration,
                        transition);
                });
        }

        /// <summary>
        /// 按过渡轨道快照导入过渡到单条轨道。
        /// </summary>
        /// <param name="timelineAsset">目标 Timeline 资产。</param>
        /// <param name="snapshot">轨道快照。</param>
        /// <param name="trackCache">轨道缓存。</param>
        /// <returns>导入后的过渡轨道。</returns>
        private BehaviorTimelineTransitionTrack ImportTransitionTrackSnapshot(
            TimelineAsset timelineAsset,
            BehaviorAuthoringTrackSnapshot snapshot,
            ImportTrackCache trackCache)
        {
            return ImportSnapshotEntriesToSingleTrack<BehaviorTransitionDefinition, BehaviorTimelineTransitionTrack>(
                timelineAsset,
                snapshot,
                trackCache,
                clipSnapshot => clipSnapshot?.transitionDefinition,
                transition => transition != null,
                (transitionTrack, clipSnapshot, transition, i) =>
                {
                    ImportTransitionToTrack(
                        timelineAsset,
                        transitionTrack,
                        trackCache,
                        snapshot.trackName,
                        ResolveImportedClipDisplayName(
                            clipSnapshot,
                            string.IsNullOrWhiteSpace(transition.targetBehaviorKey) ? $"Transition {i}" : transition.targetBehaviorKey),
                        clipSnapshot.startTime,
                        Mathf.Max(0.01f, clipSnapshot.duration),
                        transition);
                });
        }

        /// <summary>
        /// 在过渡轨道上创建过渡时间轴片段。
        /// </summary>
        /// <param name="transitionTrack">目标过渡轨道。</param>
        /// <param name="displayName">显示名。</param>
        /// <param name="startTime">起点。</param>
        /// <param name="duration">时长。</param>
        /// <param name="transition">过渡数据。</param>
        private static void CreateTransitionTimelineClip(
            BehaviorTimelineTransitionTrack transitionTrack,
            string displayName,
            float startTime,
            float duration,
            BehaviorTransitionDefinition transition)
        {
            TimelineClip timelineClip = transitionTrack.CreateDefaultClip();
            timelineClip.displayName = displayName;
            timelineClip.start = startTime;
            timelineClip.duration = Math.Max(0.01d, duration);

            // 写入克隆的过渡数据。
            if (timelineClip.asset is BehaviorTimelineTransitionClipAsset clipAsset)
                clipAsset.transitionData = CloneTransitionDefinition(transition, startTime, duration);
        }

        /// <summary>
        /// 将过渡导入到指定轨道，轨道缺失时创建。
        /// </summary>
        /// <param name="timelineAsset">目标 Timeline 资产。</param>
        /// <param name="transitionTrack">目标过渡轨道。</param>
        /// <param name="trackCache">轨道缓存。</param>
        /// <param name="trackName">轨道名。</param>
        /// <param name="displayName">显示名。</param>
        /// <param name="startTime">起点。</param>
        /// <param name="duration">时长。</param>
        /// <param name="transition">过渡数据。</param>
        /// <returns>目标过渡轨道。</returns>
        private BehaviorTimelineTransitionTrack ImportTransitionToTrack(
            TimelineAsset timelineAsset,
            BehaviorTimelineTransitionTrack transitionTrack,
            ImportTrackCache trackCache,
            string trackName,
            string displayName,
            float startTime,
            float duration,
            BehaviorTransitionDefinition transition)
        {
            transitionTrack ??= trackCache.GetOrCreateExactTrack<BehaviorTimelineTransitionTrack>(timelineAsset, trackName);
            if (transitionTrack == null)
                return null;

            CreateTransitionTimelineClip(transitionTrack, displayName, startTime, duration, transition);
            return transitionTrack;
        }

        /// <summary>
        /// 确保快照导入的轨道已准备：获取或创建轨道，首次使用时清空旧片段。
        /// </summary>
        /// <typeparam name="T">轨道类型。</typeparam>
        /// <param name="timelineAsset">目标 Timeline 资产。</param>
        /// <param name="trackName">轨道名。</param>
        /// <param name="trackCache">轨道缓存。</param>
        /// <param name="track">轨道引用（引用修改）。</param>
        /// <param name="clearedTrack">是否已清空过旧片段（引用修改）。</param>
        /// <returns>准备就绪的轨道。</returns>
        private static T EnsurePreparedSnapshotTrack<T>(
            TimelineAsset timelineAsset,
            string trackName,
            ImportTrackCache trackCache,
            ref T track,
            ref bool clearedTrack)
            where T : TrackAsset, new()
        {
            track ??= trackCache != null
                ? trackCache.GetOrCreateExactTrack<T>(timelineAsset, trackName)
                : GetOrCreateExactTrack<T>(timelineAsset, trackName);
            if (track == null)
                return null;

            // 首次使用前清空轨道旧片段，避免重复回填累积。
            if (!clearedTrack)
            {
                ClearTrackClips(track);
                clearedTrack = true;
            }

            return track;
        }

        /// <summary>
        /// 动画段导出条目：起点时间 + 动画段数据，用于导出排序。
        /// </summary>
        private sealed class AnimationSegmentEntry
        {
            // 动画段起点时间。
            public float startTime;
            // 动画段数据。
            public AnimationSegment segment;
        }
    }
}
