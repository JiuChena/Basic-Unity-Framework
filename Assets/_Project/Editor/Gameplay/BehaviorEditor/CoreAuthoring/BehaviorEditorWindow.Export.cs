using System;
using System.Collections.Generic;
using System.IO;
using Core.Gear;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace BehaviorEditor
{
    internal sealed partial class BehaviorEditorWindow : UnityEditor.EditorWindow
    {

        /// <summary>
        /// 将 Timeline 全部轨道导出编译为 BehaviorClip 资产。
        /// </summary>
        private void ExportToBehaviorClip()
        {
            if (sourceTimeline == null)
                return;

            BehaviorClip target = ResolveTargetBehaviorClip();
            if (target == null)
                return;

            // 创建导出环境，由自动发现的各轨道编译器独立写入自己的运行时数据。
            PlayableDirector exportDirector = ResolvePreviewDirectorForOpen(sourceTimeline, previewDirector);
            Transform exportReferenceRoot = ResolveExportReferenceRoot();
            BehaviorMetaData fallbackMeta = new BehaviorMetaData
            {
                wrapMode = wrapMode,
                speedMultiplier = Mathf.Max(0.01f, speedMultiplier),
                priority = priority
            };
            BehaviorExportContext exportContext = new BehaviorExportContext(sourceTimeline, exportDirector,
                exportReferenceRoot, fallbackMeta);

            // 遍历全部轨道，目录按轨道实际类型自动分发对应编译器。
            foreach (TrackAsset track in EnumerateTimelineTracks(sourceTimeline))
            {
                if (track == null || track.mutedInHierarchy)
                    continue;

                if (!BehaviorTrackCompilerCatalog.TryExport(track, exportContext))
                    exportContext.AddWarning($"轨道 '{track.name}' ({track.GetType().Name}) 没有注册 Behavior 导出编译器，已跳过。");
            }

            // 提交多态轨道数据，运行时直接由轨道数据创建对应执行器。
            UnityEditor.Undo.RegisterCompleteObjectUndo(target, "Export BehaviorClip");
            exportContext.CommitTo(target);

            UnityEditor.EditorUtility.SetDirty(target);
            UnityEditor.AssetDatabase.SaveAssets();
            UnityEditor.AssetDatabase.Refresh();
            UnityEditor.Selection.activeObject = target;

            for (int i = 0; i < exportContext.Warnings.Count; i++)
                Debug.LogWarning($"[Timeline Export] {exportContext.Warnings[i]}", sourceTimeline);

            AnimationTrackData animationData = target.GetTrackData<AnimationTrackData>();
            EventTrackData eventData = target.GetTrackData<EventTrackData>();
            HitboxTrackData hitboxData = target.GetTrackData<HitboxTrackData>();
            BehaviorMetaData metaData = target.GetTrackData<BehaviorMetaData>();
            Debug.Log(
                $"Timeline 已导出到 BehaviorClip：{target.name}\n" +
                $"Segments={animationData?.segments?.Length ?? 0}, Events={eventData?.events?.Length ?? 0}, Hitboxes={hitboxData?.hitboxes?.Length ?? 0}, Duration={metaData?.duration ?? 0f:F2}s",
                target);
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
        /// 保存动画段与其时间轴起点，供导出阶段进行稳定排序。
        /// </summary>
        internal sealed class AnimationSegmentEntry
        {
            // 动画段在行为时间轴中的起点。
            public float startTime;
            // 当前导出的动画段数据。
            public AnimationSegment segment;
        }

        /// <summary>
        /// 解析导出时的 Reference Root：优先 Reference Root，其次 Animator，最后 Director。
        /// </summary>
        /// <returns>导出参考根节点；未找到时返回 null。</returns>
        private Transform ResolveExportReferenceRoot()
        {
            if (previewReferenceRoot != null)
                return previewReferenceRoot.transform;

            if (previewAnimator != null)
                return previewAnimator.transform;

            PlayableDirector director = ResolvePreviewDirectorForOpen(sourceTimeline, previewDirector);
            if (director != null)
                return director.transform;

            return null;
        }

        /// <summary>
        /// 导出原生动画轨道的片段为动画段，并收集导出警告。
        /// </summary>
        /// <param name="track">原生动画轨道。</param>
        /// <param name="segmentEntries">动画段收集列表。</param>
        /// <param name="exportWarnings">导出警告列表。</param>
        /// <param name="maxEndTime">输出的最大结束时间。</param>
        internal static void ExportNativeAnimationTrack(AnimationTrack track, List<AnimationSegmentEntry> segmentEntries, List<string> exportWarnings, ref double maxEndTime)
        {
            if (track == null || segmentEntries == null)
                return;

            int layer = ResolveAnimationLayerFromTrackName(track.name);
            foreach (TimelineClip clip in track.GetClips())
            {
                if (clip == null)
                    continue;

                // 仅接受当前原生 AnimationTrack 片段资产。
                if (clip.asset is not AnimationPlayableAsset playableAsset)
                    continue;

                AnimationClip animationClip = playableAsset.clip;
                float crossFadeDuration = ResolveNativeAnimationCrossFade(clip);

                if (animationClip == null)
                    continue;

                // 记录不支持精确复现的 Clip In 裁切。
                if (Math.Abs(clip.clipIn) > 0.0001d)
                {
                    exportWarnings?.Add(
                        $"AnimationTrack '{track.name}' 的片段 '{clip.displayName}' 使用了 Clip In={clip.clipIn:F2}s，当前运行时不会精确复现该裁切。");
                }

                // 记录不支持精确复现的 Time Scale 变速。
                if (Math.Abs(clip.timeScale - 1d) > 0.0001d)
                {
                    exportWarnings?.Add(
                        $"AnimationTrack '{track.name}' 的片段 '{clip.displayName}' 使用了 Time Scale={clip.timeScale:F2}，当前运行时不会精确复现该变速。");
                }

                // 记录不会导出的位置/旋转偏移。
                if (playableAsset.position != Vector3.zero || playableAsset.eulerAngles != Vector3.zero)
                {
                    exportWarnings?.Add(
                        $"AnimationTrack '{track.name}' 的片段 '{clip.displayName}' 配置了位置或旋转偏移，当前运行时不会导出这部分偏移。");
                }

                double resolvedEndTime = clip.end;
                if (resolvedEndTime > maxEndTime)
                    maxEndTime = resolvedEndTime;

                segmentEntries.Add(new AnimationSegmentEntry
                {
                    startTime = (float)clip.start,
                    segment = new AnimationSegment
                    {
                        authoringTrackName = track.name,
                        clip = animationClip,
                        crossFadeDuration = crossFadeDuration,
                        layer = layer,
                        startTime = (float)clip.start
                    }
                });
            }
        }

        /// <summary>
        /// 导出原生音频轨道的片段为音频事件。
        /// </summary>
        /// <param name="track">原生音频轨道。</param>
        /// <param name="director">当前预览 Director。</param>
        /// <param name="referenceRoot">角色根节点。</param>
        /// <param name="behaviorEvents">行为事件收集列表。</param>
        /// <param name="exportWarnings">导出警告列表。</param>
        /// <param name="maxEndTime">输出的最大结束时间。</param>
        internal static void ExportNativeAudioTrack(AudioTrack track, PlayableDirector director, Transform referenceRoot, List<BehaviorEvent> behaviorEvents, List<string> exportWarnings, ref double maxEndTime)
        {
            if (track == null || behaviorEvents == null)
                return;

            AudioSource boundAudioSource = ResolveBoundAudioSource(track, director);
            float trackVolume = ReadClampedFloatSerializedProperty(track, "m_TrackProperties.volume", 1f);

            foreach (TimelineClip clip in track.GetClips())
            {
                if (clip == null)
                    continue;

                AudioPlayableAsset playableAsset = clip.asset as AudioPlayableAsset;
                if (playableAsset == null || playableAsset.clip == null)
                    continue;

                if (clip.end > maxEndTime)
                    maxEndTime = clip.end;

                BuildTransformBinding(boundAudioSource != null ? boundAudioSource.transform : null, referenceRoot,
                    out string referenceBone, out Vector3 positionOffset, out Vector3 rotationOffset,
                    out Vector3 scaleOffset);

                behaviorEvents.Add(new BehaviorEvent
                {
                    authoringTrackName = track.name,
                    time = Mathf.Max(0f, (float)clip.start),
                    type = BehaviorEventType.PlayAudio,
                    referenceBone = referenceBone,
                    positionOffset = positionOffset,
                    rotationOffset = rotationOffset,
                    scaleOffset = scaleOffset,
                    audioRef = playableAsset.clip,
                    audioLoop = playableAsset.loop,
                    audioVolume = Mathf.Clamp01(trackVolume *
                                               ReadClampedFloatSerializedProperty(
                                                   playableAsset,
                                                   "m_ClipProperties.volume",
                                                   1f)),
                });

                if (boundAudioSource == null && referenceRoot == null)
                {
                    exportWarnings?.Add(
                        $"AudioTrack '{track.name}' 未找到绑定的 AudioSource 或 Reference Root，导出的音频事件将回退到世界空间原点。");
                }
            }
        }

        /// <summary>
        /// 导出原生特效控制轨道的片段：有预制体时导出 SpawnVFX，否则导出对象激活事件。
        /// </summary>
        /// <param name="track">原生特效控制轨道。</param>
        /// <param name="director">当前预览 Director。</param>
        /// <param name="referenceRoot">角色根节点。</param>
        /// <param name="behaviorEvents">行为事件收集列表。</param>
        /// <param name="exportWarnings">导出警告列表。</param>
        /// <param name="maxEndTime">输出的最大结束时间。</param>
        internal static void ExportNativeVfxTrack(ControlTrack track, PlayableDirector director, Transform referenceRoot, List<BehaviorEvent> behaviorEvents, List<string> exportWarnings, ref double maxEndTime)
        {
            if (track == null || behaviorEvents == null)
                return;

            foreach (TimelineClip clip in track.GetClips())
            {
                if (clip == null)
                    continue;

                ControlPlayableAsset playableAsset = clip.asset as ControlPlayableAsset;
                if (playableAsset == null)
                    continue;

                if (clip.end > maxEndTime)
                    maxEndTime = clip.end;

                // 解析源对象与预制体。
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

                // 无预制体时回退为对象激活事件（进入时激活，退出时禁用）。
                if (prefab == null)
                {
                    string targetObjectPath = BuildRelativeAuthoringObjectPath(referenceRoot, sourceObject);
                    if (string.IsNullOrWhiteSpace(targetObjectPath))
                    {
                        exportWarnings?.Add(
                            $"ControlTrack '{track.name}' 的片段 '{clip.displayName}' 没有设置 prefabGameObject，且 sourceGameObject 也无法解析为 Reference Root 下的有效层级路径，已跳过运行时导出。");
                        continue;
                    }

                    behaviorEvents.Add(new BehaviorEvent
                    {
                        authoringTrackName = track.name,
                        time = Mathf.Max(0f, (float)clip.start),
                        type = BehaviorEventType.SetObjectActive,
                        targetObjectPath = targetObjectPath,
                        activeState = true,
                    });

                    behaviorEvents.Add(new BehaviorEvent
                    {
                        authoringTrackName = track.name,
                        time = Mathf.Max(0f, (float)clip.end),
                        type = BehaviorEventType.SetObjectActive,
                        targetObjectPath = targetObjectPath,
                        activeState = false,
                    });
                    continue;
                }

                // 有预制体时导出 VFX 生成事件。
                BuildTransformBinding(transformSourceObject != null ? transformSourceObject.transform : null, referenceRoot,
                    out string referenceBone, out Vector3 positionOffset, out Vector3 rotationOffset,
                    out Vector3 scaleOffset);
                NormalizeSpawnablePrefabTransformOffsets(prefab, transformSourceObject != null ? transformSourceObject.transform : null,
                    ref positionOffset, ref rotationOffset, ref scaleOffset);

                if (transformSourceObject == null)
                {
                    exportWarnings?.Add(
                        $"ControlTrack '{track.name}' 的片段 '{clip.displayName}' 未解析到场景预览对象，导出的特效事件将回退到 Reference Root；如果未设置 Reference Root，则使用世界空间原点。");
                }

                behaviorEvents.Add(new BehaviorEvent
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
                });
            }
        }

        /// <summary>
        /// 导出原生激活轨道的片段为对象激活事件（进入时激活，退出时禁用）。
        /// </summary>
        /// <param name="track">原生激活轨道。</param>
        /// <param name="director">当前预览 Director。</param>
        /// <param name="referenceRoot">角色根节点。</param>
        /// <param name="behaviorEvents">行为事件收集列表。</param>
        /// <param name="exportWarnings">导出警告列表。</param>
        /// <param name="maxEndTime">输出的最大结束时间。</param>
        internal static void ExportNativeActivationTrack(ActivationTrack track, PlayableDirector director, Transform referenceRoot, List<BehaviorEvent> behaviorEvents, List<string> exportWarnings, ref double maxEndTime)
        {
            if (track == null || behaviorEvents == null)
                return;

            GameObject sourceObject = ResolveActivationTrackBinding(track, director);
            string targetObjectPath = BuildRelativeAuthoringObjectPath(referenceRoot, sourceObject);
            foreach (TimelineClip clip in track.GetClips())
            {
                if (clip == null)
                    continue;

                if (clip.end > maxEndTime)
                    maxEndTime = clip.end;

                if (string.IsNullOrWhiteSpace(targetObjectPath))
                {
                    exportWarnings?.Add(
                        $"ActivationTrack '{track.name}' 的片段 '{clip.displayName}' 无法解析到 Reference Root 下的目标路径，已跳过运行时导出。");
                    continue;
                }

                behaviorEvents.Add(new BehaviorEvent
                {
                    authoringTrackName = track.name,
                    time = Mathf.Max(0f, (float)clip.start),
                    type = BehaviorEventType.SetObjectActive,
                    targetObjectPath = targetObjectPath,
                    activeState = true,
                });

                behaviorEvents.Add(new BehaviorEvent
                {
                    authoringTrackName = track.name,
                    time = Mathf.Max(0f, (float)clip.end),
                    type = BehaviorEventType.SetObjectActive,
                    targetObjectPath = targetObjectPath,
                    activeState = false,
                });
            }
        }

        /// <summary>
        /// 解析原生动画片段的交叉淡化时长（归一化到 0-1）。
        /// </summary>
        /// <param name="clip">Timeline 片段。</param>
        /// <returns>归一化交叉淡化时长。</returns>
        private static float ResolveNativeAnimationCrossFade(TimelineClip clip)
        {
            if (clip == null)
                return 0f;

            double blendDuration = Math.Max(clip.blendInDuration, clip.easeInDuration);
            double clipDuration = Math.Max(0.0001d, clip.duration);
            double normalizedDuration = blendDuration > 0.0001d ? blendDuration / clipDuration : 0d;
            return Mathf.Clamp01((float)normalizedDuration);
        }

        /// <summary>
        /// 从轨道名解析动画层索引，支持 "L0" 与 "Layer 0" 两种写法。
        /// </summary>
        /// <param name="trackName">轨道名称。</param>
        /// <returns>解析出的层索引；无法解析时返回 0。</returns>
        private static int ResolveAnimationLayerFromTrackName(string trackName)
        {
            if (string.IsNullOrWhiteSpace(trackName))
                return 0;

            string[] tokens = trackName.Split(new[] { ' ', '_', '-', '/' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < tokens.Length; i++)
            {
                string token = tokens[i];
                // 匹配 "L0" 形式。
                if (token.Length >= 2 &&
                    (token[0] == 'L' || token[0] == 'l') &&
                    int.TryParse(token.Substring(1), out int tokenLayer))
                {
                    return Mathf.Max(0, tokenLayer);
                }

                // 匹配 "Layer 0" 形式。
                if ((string.Equals(token, "Layer", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(token, "L", StringComparison.OrdinalIgnoreCase)) &&
                    i + 1 < tokens.Length &&
                    int.TryParse(tokens[i + 1], out int nextLayer))
                {
                    return Mathf.Max(0, nextLayer);
                }
            }

            return 0;
        }

        /// <summary>
        /// 解析音频轨道绑定的 AudioSource。
        /// </summary>
        /// <param name="track">音频轨道。</param>
        /// <param name="director">当前预览 Director。</param>
        /// <returns>绑定的音频源；未找到时返回 null。</returns>
        private static AudioSource ResolveBoundAudioSource(AudioTrack track, PlayableDirector director)
        {
            if (track == null || director == null)
                return null;

            return director.GetGenericBinding(track) as AudioSource;
        }

        /// <summary>
        /// 解析激活轨道绑定的目标 GameObject。
        /// </summary>
        /// <param name="track">激活轨道。</param>
        /// <param name="director">当前预览 Director。</param>
        /// <returns>绑定的目标对象；未找到时返回 null。</returns>
        private static GameObject ResolveActivationTrackBinding(ActivationTrack track, PlayableDirector director)
        {
            if (track == null || director == null)
                return null;

            UnityEngine.Object binding = director.GetGenericBinding(track);
            if (binding is GameObject gameObject)
                return gameObject;

            if (binding is Component component)
                return component.gameObject;

            return null;
        }

        /// <summary>
        /// 配置特效控制 PlayableAsset 的预制体、激活与更新选项。
        /// </summary>
        /// <param name="playableAsset">目标控制资产。</param>
        /// <param name="prefab">需要绑定的预制体。</param>
        /// <param name="postPlayback">播放后的行为；-1 表示不修改。</param>
        private static void ConfigureControlPlayableAsset( ControlPlayableAsset playableAsset, GameObject prefab, int postPlayback)
        {
            if (playableAsset == null)
                return;

            using (UnityEditor.SerializedObject serializedObject = new UnityEditor.SerializedObject(playableAsset))
            {
                SetSerializedPropertyValue(serializedObject, "prefabGameObject", prefab);
                SetSerializedPropertyValue(serializedObject, "active", true);
                SetSerializedPropertyValue(serializedObject, "updateParticle", true);
                SetSerializedPropertyValue(serializedObject, "updateDirector", true);
                SetSerializedPropertyValue(serializedObject, "updateITimeControl", true);
                SetSerializedPropertyValue(serializedObject, "searchHierarchy", false);
                if (postPlayback >= 0)
                    SetSerializedPropertyValue(serializedObject, "postPlayback", postPlayback);
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        /// <summary>
        /// 尝试设置音频 PlayableAsset 的音量。
        /// </summary>
        /// <param name="playableAsset">目标音频资产。</param>
        /// <param name="volume">音量值。</param>
        private static void TrySetAudioPlayableAssetVolume(AudioPlayableAsset playableAsset, float volume)
        {
            if (playableAsset == null)
                return;

            using (UnityEditor.SerializedObject serializedObject = new UnityEditor.SerializedObject(playableAsset))
            {
                SetSerializedPropertyValue(serializedObject, "m_ClipProperties.volume", Mathf.Clamp01(volume));
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        /// <summary>
        /// 尝试将场景中的预制体实例解析为可生成的预制体资产。
        /// </summary>
        /// <param name="sourceObject">场景源对象。</param>
        /// <param name="referenceRoot">角色根节点。</param>
        /// <param name="prefabAsset">输出的预制体资产。</param>
        /// <param name="instanceRootObject">输出的预制体实例根。</param>
        /// <returns>成功解析预制体时返回 true。</returns>
        private static bool TryResolveSpawnableControlTrackPrefab(GameObject sourceObject, Transform referenceRoot, out GameObject prefabAsset, out GameObject instanceRootObject)
        {
            prefabAsset = null;
            instanceRootObject = sourceObject;
            if (sourceObject == null)
                return false;

            if (!UnityEditor.PrefabUtility.IsPartOfPrefabInstance(sourceObject))
                return false;

            GameObject nearestInstanceRoot = UnityEditor.PrefabUtility.GetNearestPrefabInstanceRoot(sourceObject);
            if (nearestInstanceRoot == null)
                return false;

            if (referenceRoot != null && nearestInstanceRoot == referenceRoot.gameObject)
                return false;
            // Authoring may place preview VFX prefab instances outside the Reference Root hierarchy.
            // As long as we can resolve the prefab asset, export it as a spawnable runtime VFX.

            prefabAsset = UnityEditor.PrefabUtility.GetCorrespondingObjectFromSource(nearestInstanceRoot) as GameObject;
            if (prefabAsset == null)
                return false;

            instanceRootObject = nearestInstanceRoot;
            return true;
        }

        /// <summary>
        /// 构建源变换相对参考根节点的骨骼绑定：骨骼路径与位置/旋转/缩放偏移。
        /// </summary>
        /// <param name="sourceTransform">源变换。</param>
        /// <param name="referenceRoot">角色根节点。</param>
        /// <param name="referenceBone">输出的骨骼路径。</param>
        /// <param name="positionOffset">输出的位置偏移。</param>
        /// <param name="rotationOffset">输出的旋转偏移。</param>
        /// <param name="scaleOffset">输出的缩放偏移。</param>
        private static void BuildTransformBinding(Transform sourceTransform, Transform referenceRoot, out string referenceBone, out Vector3 positionOffset, out Vector3 rotationOffset, out Vector3 scaleOffset)
        {
            referenceBone = string.Empty;
            positionOffset = Vector3.zero;
            rotationOffset = Vector3.zero;
            scaleOffset = Vector3.one;

            // 无源变换时回退到参考根节点自身。
            if (sourceTransform == null)
            {
                if (referenceRoot != null)
                    referenceBone = BehaviorReferenceBoneEditorUtility.BuildRelativeBonePath(referenceRoot, referenceRoot);
                return;
            }

            // 无参考根节点时使用世界空间偏移。
            if (referenceRoot == null)
            {
                positionOffset = sourceTransform.position;
                rotationOffset = sourceTransform.rotation.eulerAngles;
                scaleOffset = sourceTransform.lossyScale;
                return;
            }

            // 源变换即参考根节点。
            if (sourceTransform == referenceRoot)
            {
                referenceBone = BehaviorReferenceBoneEditorUtility.BuildRelativeBonePath(referenceRoot, referenceRoot);
                return;
            }

            // 源变换在参考根节点层级下时，以最近父级为挂点计算相对偏移。
            if (sourceTransform.IsChildOf(referenceRoot))
            {
                Transform parent = sourceTransform.parent;
                Transform bindingTransform = referenceRoot;
                if (parent != null && (parent == referenceRoot || parent.IsChildOf(referenceRoot)))
                {
                    bindingTransform = parent;
                }

                referenceBone = BehaviorReferenceBoneEditorUtility.BuildRelativeBonePath(referenceRoot, bindingTransform);
                positionOffset = bindingTransform.InverseTransformPoint(sourceTransform.position);
                rotationOffset = (Quaternion.Inverse(bindingTransform.rotation) * sourceTransform.rotation).eulerAngles;
                scaleOffset = sourceTransform.localScale;
                return;
            }

            referenceBone = BehaviorReferenceBoneEditorUtility.BuildRelativeBonePath(referenceRoot, referenceRoot);
            positionOffset = referenceRoot.InverseTransformPoint(sourceTransform.position);
            rotationOffset = (Quaternion.Inverse(referenceRoot.rotation) * sourceTransform.rotation).eulerAngles;
            scaleOffset = sourceTransform.localScale;
        }

        /// <summary>
        /// 归一化可生成预制体的变换偏移：扣除预制体根自身的局部变换。
        /// </summary>
        /// <param name="prefab">预制体资产。</param>
        /// <param name="sourceTransform">场景源变换。</param>
        /// <param name="positionOffset">位置偏移（引用修改）。</param>
        /// <param name="rotationOffset">旋转偏移（引用修改）。</param>
        /// <param name="scaleOffset">缩放偏移（引用修改）。</param>
        private static void NormalizeSpawnablePrefabTransformOffsets(GameObject prefab, Transform sourceTransform, ref Vector3 positionOffset, ref Vector3 rotationOffset, ref Vector3 scaleOffset)
        {
            if (prefab == null || sourceTransform == null)
                return;

            if (!UnityEditor.PrefabUtility.IsPartOfPrefabInstance(sourceTransform.gameObject))
                return;

            GameObject nearestInstanceRoot = UnityEditor.PrefabUtility.GetNearestPrefabInstanceRoot(sourceTransform.gameObject);
            if (nearestInstanceRoot == null || nearestInstanceRoot != sourceTransform.gameObject)
                return;

            GameObject sourcePrefabRoot = UnityEditor.PrefabUtility.GetCorrespondingObjectFromSource(nearestInstanceRoot) as GameObject;
            if (sourcePrefabRoot == null || sourcePrefabRoot != prefab)
                return;

            // 扣除预制体根局部变换，得到相对参考的实际偏移。
            positionOffset -= prefab.transform.localPosition;
            rotationOffset = (Quaternion.Inverse(prefab.transform.localRotation) * Quaternion.Euler(rotationOffset)).eulerAngles;
            scaleOffset = DivideVector3Safely(scaleOffset, prefab.transform.localScale);
        }
    }
}
