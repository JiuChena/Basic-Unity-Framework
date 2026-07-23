using System;
using System.Collections.Generic;
using System.IO;
using CoreFramework;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace BehaviorCore
{
    internal sealed partial class BehaviorEditorWindow : UnityEditor.EditorWindow
    {
        private const string NativeAnimationTrackName = "Behavior Animation L0";
        private const string NativeAudioTrackName = "Behavior Audio";
        private const string NativeVfxTrackName = "Behavior VFX";
        private const string NativeActivationVfxTrackName = "Behavior Active VFX";
        private const string MetaTrackName = "Behavior Meta";
        private const string EventTrackName = "Behavior Events";
        private const string HitboxTrackName = "Behavior Hitboxes";
        private const string TransitionTrackName = "Behavior Transitions";

        private TimelineAsset sourceTimeline;
        private BehaviorClip targetBehaviorClip;
        private string outputFolder = "Assets/BehaviorEditor";
        private string outputAssetName = "TimelineBehaviorClip";
        private WrapMode wrapMode = WrapMode.Once;
        private float speedMultiplier = 1f;
        private InterruptPriority priority = InterruptPriority.Normal;
        private PlayableDirector previewDirector;
        private Animator previewAnimator;
        private GameObject previewReferenceRoot;
        private bool removePreviewDirectorOnFinish;
        private bool removePreviewAnimatorOnFinish;
        private bool autoAssignedReferenceRoot;
        private bool showAuthoringHitboxGizmos = true;
        private bool syncTimelineFromTargetBehaviorOnBegin = true;
        private readonly List<AudioSource> createdPreviewAudioSources = new List<AudioSource>();
        private static bool pendingDelayedTimelineRefresh;
        private static TimelineAsset pendingDelayedTimelineAsset;
        private static PlayableDirector pendingDelayedTimelineDirector;
        private static UnityEditor.Timeline.RefreshReason pendingDelayedTimelineReason;

        [UnityEditor.MenuItem("Framework/Behavior Editor/Timeline Exporter")]
        private static void Open()
        {
            BehaviorEditorWindow window =
                GetWindow<BehaviorEditorWindow>("Behavior Editor Timeline");
            window.minSize = new Vector2(460f, 420f);
        }

        private void OnGUI()
        {
            UnityEditor.EditorGUILayout.LabelField("Timeline -> BehaviorClip", UnityEditor.EditorStyles.boldLabel);
            UnityEditor.EditorGUILayout.HelpBox(
                "动画、音频和特效预览优先走原生 Timeline 轨道；Hitbox、Transition 和玩法数据继续走自定义轨。导出时会统一编译为运行时使用的 BehaviorClip。",
                UnityEditor.MessageType.Info);

            sourceTimeline = (TimelineAsset)UnityEditor.EditorGUILayout.ObjectField(
                "Source Timeline", sourceTimeline, typeof(TimelineAsset), false);
            targetBehaviorClip = (BehaviorClip)UnityEditor.EditorGUILayout.ObjectField(
                "Target BehaviorClip", targetBehaviorClip, typeof(BehaviorClip), false);

            if (targetBehaviorClip == null)
            {
                outputFolder = UnityEditor.EditorGUILayout.TextField("Output Folder", outputFolder);
                outputAssetName = UnityEditor.EditorGUILayout.TextField("Output Asset Name", outputAssetName);
            }

            GUILayout.Space(8f);
            UnityEditor.EditorGUILayout.LabelField("Behavior Meta", UnityEditor.EditorStyles.boldLabel);
            UnityEditor.EditorGUILayout.HelpBox(
                "如果 Timeline 中存在 Behavior Meta 轨道并放置了片段，导出时会优先使用轨道里的配置。下面这些字段会作为回退默认值保留。",
                UnityEditor.MessageType.None);
            wrapMode = (WrapMode)UnityEditor.EditorGUILayout.EnumPopup("Wrap Mode", wrapMode);
            speedMultiplier = Mathf.Max(0.01f,
                UnityEditor.EditorGUILayout.FloatField("Speed Multiplier", speedMultiplier));
            priority = (InterruptPriority)UnityEditor.EditorGUILayout.EnumPopup("Priority", priority);

            GUILayout.Space(8f);
            UnityEditor.EditorGUILayout.LabelField("Authoring", UnityEditor.EditorStyles.boldLabel);
            UnityEditor.EditorGUILayout.HelpBox(
                "Reference Root 就是当前行为作者期使用的角色根节点，同时也是骨骼路径计算基准。开始编辑后会自动查找或补齐 PlayableDirector，并自动绑定 Animator。结束编辑时会导出 BehaviorClip，并清理本次作者期使用的 Director。",
                UnityEditor.MessageType.None);
            previewReferenceRoot = (GameObject)UnityEditor.EditorGUILayout.ObjectField(
                "Reference Root", previewReferenceRoot, typeof(GameObject), true);
            if (BehaviorEditorContext.ReferenceRootObject != previewReferenceRoot)
                BehaviorEditorContext.ReferenceRootObject = previewReferenceRoot;
            showAuthoringHitboxGizmos = BehaviorEditorContext.ShowAuthoringHitboxGizmos;
            bool nextShowAuthoringHitboxGizmos = UnityEditor.EditorGUILayout.ToggleLeft(
                "Show Authoring Hitbox Gizmos",
                showAuthoringHitboxGizmos);
            if (nextShowAuthoringHitboxGizmos != BehaviorEditorContext.ShowAuthoringHitboxGizmos)
                BehaviorEditorContext.ShowAuthoringHitboxGizmos = nextShowAuthoringHitboxGizmos;
            showAuthoringHitboxGizmos = BehaviorEditorContext.ShowAuthoringHitboxGizmos;
            syncTimelineFromTargetBehaviorOnBegin = UnityEditor.EditorGUILayout.ToggleLeft(
                "Sync Timeline From Target BehaviorClip On Begin",
                syncTimelineFromTargetBehaviorOnBegin);

            GUILayout.Space(10f);
            bool blockAuthoringInPlayMode = UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode;
            if (blockAuthoringInPlayMode)
            {
                UnityEditor.EditorGUILayout.HelpBox(
                    "Behavior authoring is disabled while Unity is in Play Mode.",
                    UnityEditor.MessageType.Warning);
            }

            using (new UnityEditor.EditorGUI.DisabledScope(sourceTimeline == null || blockAuthoringInPlayMode))
            {
                if (GUILayout.Button("Begin Behavior Authoring", GUILayout.Height(28f)))
                    BeginBehaviorAuthoring();

                if (GUILayout.Button("End Editing And Export", GUILayout.Height(32f)))
                    EndBehaviorAuthoring();
            }
        }

        private void OnEnable()
        {
            BehaviorEditorContext.ReferenceRootObject = previewReferenceRoot;
            BehaviorEditorContext.ShowAuthoringHitboxGizmos = showAuthoringHitboxGizmos;
            BehaviorEditorContext.RetainHitboxScenePreview();
        }

        private void OnDisable()
        {
            CleanupAuthoringSession();
            BehaviorEditorContext.ReferenceRootObject = null;
            BehaviorEditorContext.ReleaseHitboxScenePreview();
        }

        /// <summary>
        /// 开始编辑行为
        /// </summary>
        private void BeginBehaviorAuthoring()
        {
            if (sourceTimeline == null)
                return;
            if (!EnsureEditModeOperationAllowed("Begin Behavior Authoring"))
                return;

            GameObject target = ResolveAuthoringTarget();
            if (target == null)
            {
                Debug.LogWarning("没有可用于编辑行为的角色模型。请先在场景中选中一个角色对象，或在 Reference Root 中指定角色根节点。", this);
                return;
            }

            if (removePreviewDirectorOnFinish &&
                previewDirector != null &&
                previewDirector.gameObject != null &&
                previewDirector.gameObject != target)
            {
                CleanupAuthoringSession();
            }

            previewDirector = EnsurePreviewDirector(target, out removePreviewDirectorOnFinish);
            previewAnimator = EnsurePreviewAnimator(target, out removePreviewAnimatorOnFinish);

            if (previewReferenceRoot == null || autoAssignedReferenceRoot)
            {
                previewReferenceRoot = target;
                autoAssignedReferenceRoot = true;
            }

            BehaviorEditorContext.ReferenceRootObject = previewReferenceRoot;
            PruneInvalidRootTrackReferences(sourceTimeline);

            previewDirector.playableAsset = sourceTimeline;
            previewDirector.playOnAwake = false;
            previewDirector.time = 0d;
            previewDirector.Stop();

            EnsureAuthoringTracks();
            if (syncTimelineFromTargetBehaviorOnBegin && targetBehaviorClip != null)
                RebuildTimelineFromBehaviorClip();
            OpenTimelineForPreview();
        }

        private void EndBehaviorAuthoring()
        {
            if (sourceTimeline == null)
                return;
            if (!EnsureEditModeOperationAllowed("End Editing And Export"))
                return;

            try
            {
                ExportToBehaviorClip();
            }
            finally
            {
                CleanupAuthoringSession();
            }
        }

        private bool EnsureEditModeOperationAllowed(string operationName)
        {
            if (!UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                return true;

            Debug.LogWarning($"{operationName} is unavailable while Unity is in Play Mode.", this);
            return false;
        }

        private void EnsureAuthoringTracks()
        {
            if (sourceTimeline == null)
                return;

            UnityEditor.Undo.RegisterCompleteObjectUndo(sourceTimeline, "Ensure Behavior Authoring Tracks");
            bool changed = PruneInvalidRootTrackReferences(sourceTimeline);
            List<TrackAsset> timelineTracks = CollectTimelineTracks(sourceTimeline);
            changed |= EnsureMetaTrack(sourceTimeline, timelineTracks);
            EnsureTrack<AnimationTrack>(sourceTimeline, NativeAnimationTrackName, timelineTracks, out bool animationTrackChanged);
            EnsureTrack<AudioTrack>(sourceTimeline, NativeAudioTrackName, timelineTracks, out bool audioTrackChanged);
            EnsureTrack<ControlTrack>(sourceTimeline, NativeVfxTrackName, timelineTracks, out bool controlTrackChanged);
            EnsureTrack<ActivationTrack>(sourceTimeline, NativeActivationVfxTrackName, timelineTracks, out bool activationTrackChanged);
            EnsureTrack<BehaviorTimelineEventTrack>(sourceTimeline, EventTrackName, timelineTracks, out bool eventTrackChanged);
            EnsureTrack<BehaviorTimelineHitboxTrack>(sourceTimeline, HitboxTrackName, timelineTracks, out bool hitboxTrackChanged);
            EnsureTrack<BehaviorTimelineTransitionTrack>(sourceTimeline, TransitionTrackName, timelineTracks, out bool transitionTrackChanged);
            changed |= animationTrackChanged ||
                       audioTrackChanged ||
                       controlTrackChanged ||
                       activationTrackChanged ||
                       eventTrackChanged ||
                       hitboxTrackChanged ||
                       transitionTrackChanged;

            if (changed)
            {
                UnityEditor.EditorUtility.SetDirty(sourceTimeline);
                UnityEditor.AssetDatabase.SaveAssets();
            }

            Repaint();
            UnityEditor.EditorGUIUtility.PingObject(sourceTimeline);
            RefreshTimelineEditor(sourceTimeline, changed, previewDirector);
            Debug.Log($"已确保 Timeline 轨道存在：{sourceTimeline.name}", sourceTimeline);
        }

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

        private static void ReorderRootTracksByImportOrder(
            TimelineAsset timelineAsset,
            List<TrackAsset> importedRootTracks)
        {
            if (timelineAsset == null || importedRootTracks == null || importedRootTracks.Count == 0)
                return;

            PruneInvalidRootTrackReferences(timelineAsset);
            List<TrackAsset> currentRootTracks = new List<TrackAsset>();
            foreach (TrackAsset rootTrack in timelineAsset.GetRootTracks())
                currentRootTracks.Add(rootTrack);

            if (currentRootTracks.Count <= 1)
                return;

            List<TrackAsset> desiredOrder = new List<TrackAsset>(currentRootTracks.Count);
            Dictionary<TrackAsset, int> importedTrackIndexMap = new Dictionary<TrackAsset, int>();
            for (int i = 0; i < importedRootTracks.Count; i++)
            {
                TrackAsset importedTrack = importedRootTracks[i];
                if (importedTrack != null && !importedTrackIndexMap.ContainsKey(importedTrack))
                    importedTrackIndexMap.Add(importedTrack, i);
            }

            List<TrackAsset> sortedRootTracks = new List<TrackAsset>(currentRootTracks);
            sortedRootTracks.Sort((left, right) =>
            {
                if (ReferenceEquals(left, right))
                    return 0;
                if (left == null)
                    return 1;
                if (right == null)
                    return -1;

                int result = GetActualTrackImportPriority(left).CompareTo(GetActualTrackImportPriority(right));
                if (result != 0)
                    return result;

                bool leftImported = importedTrackIndexMap.TryGetValue(left, out int leftImportedIndex);
                bool rightImported = importedTrackIndexMap.TryGetValue(right, out int rightImportedIndex);
                if (leftImported && rightImported)
                {
                    result = leftImportedIndex.CompareTo(rightImportedIndex);
                    if (result != 0)
                        return result;
                }
                else if (leftImported != rightImported)
                {
                    return leftImported ? -1 : 1;
                }

                int leftCurrentIndex = currentRootTracks.IndexOf(left);
                int rightCurrentIndex = currentRootTracks.IndexOf(right);
                result = leftCurrentIndex.CompareTo(rightCurrentIndex);
                return result != 0
                    ? result
                    : string.Compare(left.name, right.name, StringComparison.Ordinal);
            });

            for (int i = 0; i < sortedRootTracks.Count; i++)
                desiredOrder.Add(sortedRootTracks[i]);

            if (desiredOrder.Count != currentRootTracks.Count)
                return;

            using (UnityEditor.SerializedObject serializedObject = new UnityEditor.SerializedObject(timelineAsset))
            {
                UnityEditor.SerializedProperty tracksProperty = serializedObject.FindProperty("m_Tracks");
                if (tracksProperty == null || !tracksProperty.isArray || tracksProperty.arraySize != desiredOrder.Count)
                    return;

                for (int i = 0; i < desiredOrder.Count; i++)
                {
                    UnityEditor.SerializedProperty element = tracksProperty.GetArrayElementAtIndex(i);
                    if (element == null)
                        continue;

                    element.objectReferenceValue = desiredOrder[i];
                }

                serializedObject.ApplyModifiedPropertiesWithoutUndo();
            }
            UnityEditor.EditorUtility.SetDirty(timelineAsset);
        }

        private static bool PruneInvalidRootTrackReferences(TimelineAsset timelineAsset)
        {
            if (timelineAsset == null)
                return false;

            using (UnityEditor.SerializedObject serializedObject = new UnityEditor.SerializedObject(timelineAsset))
            {
                UnityEditor.SerializedProperty tracksProperty = serializedObject.FindProperty("m_Tracks");
                if (tracksProperty == null || !tracksProperty.isArray)
                    return false;

                bool changed = false;
                for (int i = tracksProperty.arraySize - 1; i >= 0; i--)
                {
                    UnityEditor.SerializedProperty element = tracksProperty.GetArrayElementAtIndex(i);
                    UnityEngine.Object referencedObject = element?.objectReferenceValue;
                    if (referencedObject is TrackAsset track && !IsInvalidManagedAuthoringTrackPlaceholder(track))
                        continue;

                    int previousArraySize = tracksProperty.arraySize;
                    tracksProperty.DeleteArrayElementAtIndex(i);
                    if (tracksProperty.arraySize == previousArraySize)
                        tracksProperty.DeleteArrayElementAtIndex(i);

                    changed = true;
                }

                if (!changed)
                    return false;

                serializedObject.ApplyModifiedPropertiesWithoutUndo();
            }
            UnityEditor.EditorUtility.SetDirty(timelineAsset);
            return true;
        }

        private static bool IsInvalidManagedAuthoringTrackPlaceholder(TrackAsset track)
        {
            if (track == null)
                return true;

            return track.GetType() == typeof(TrackAsset) &&
                   IsManagedAuthoringTrackName(track.name);
        }

        private static bool IsManagedAuthoringTrackName(string trackName)
        {
            if (string.IsNullOrWhiteSpace(trackName))
                return false;

            return string.Equals(trackName, MetaTrackName, StringComparison.Ordinal) ||
                   string.Equals(trackName, EventTrackName, StringComparison.Ordinal) ||
                   string.Equals(trackName, HitboxTrackName, StringComparison.Ordinal) ||
                   string.Equals(trackName, TransitionTrackName, StringComparison.Ordinal) ||
                   string.Equals(trackName, NativeAudioTrackName, StringComparison.Ordinal) ||
                   string.Equals(trackName, NativeVfxTrackName, StringComparison.Ordinal) ||
                   string.Equals(trackName, NativeActivationVfxTrackName, StringComparison.Ordinal) ||
                   trackName.StartsWith("Behavior Animation L", StringComparison.Ordinal);
        }

        private static void RemoveEmptyManagedAuthoringTracks(TimelineAsset timelineAsset)
        {
            DeleteTracksByPredicate(
                timelineAsset,
                "Remove Empty Behavior Tracks",
                track => track != null && IsManagedAuthoringTrack(track) && !HasAnyClips(track));
        }

        private static bool IsManagedAuthoringTrack(TrackAsset track)
        {
            if (track == null)
                return false;

            return track is AnimationTrack ||
                   track is AudioTrack ||
                   track is ControlTrack ||
                   track is ActivationTrack ||
                   track is BehaviorTimelineMetaTrack ||
                   track is BehaviorTimelineEventTrack ||
                   track is BehaviorTimelineHitboxTrack ||
                   track is BehaviorTimelineTransitionTrack;
        }

        private static bool HasAnyClips(TrackAsset track)
        {
            if (track == null)
                return false;

            foreach (TimelineClip _ in track.GetClips())
                return true;

            return false;
        }

        private static int CompareTrackSnapshotsBySortIndex(
            BehaviorAuthoringTrackSnapshot left,
            BehaviorAuthoringTrackSnapshot right)
        {
            if (ReferenceEquals(left, right))
                return 0;
            if (left == null)
                return 1;
            if (right == null)
                return -1;

            int result = GetTrackImportPriority(left.trackKind).CompareTo(GetTrackImportPriority(right.trackKind));
            if (result != 0)
                return result;

            result = left.sortIndex.CompareTo(right.sortIndex);
            return result != 0 ? result : string.Compare(left.trackName, right.trackName, StringComparison.Ordinal);
        }

        private static int GetTrackImportPriority(BehaviorAuthoringTrackKind trackKind)
        {
            return trackKind switch
            {
                BehaviorAuthoringTrackKind.Meta => 0,
                BehaviorAuthoringTrackKind.Animation => 1,
                BehaviorAuthoringTrackKind.Audio => 2,
                BehaviorAuthoringTrackKind.VfxControl => 3,
                BehaviorAuthoringTrackKind.VfxActivation => 4,
                BehaviorAuthoringTrackKind.Event => 5,
                BehaviorAuthoringTrackKind.Hitbox => 6,
                BehaviorAuthoringTrackKind.Transition => 7,
                _ => 8,
            };
        }

        private static int GetActualTrackImportPriority(TrackAsset track)
        {
            if (track == null)
                return int.MaxValue;

            if (track is BehaviorTimelineMetaTrack)
                return 0;

            if (track is AnimationTrack)
                return 1;

            if (track is AudioTrack)
                return 2;

            if (track is ControlTrack)
                return 3;

            if (track is ActivationTrack)
                return 4;

            if (track is BehaviorTimelineEventTrack)
                return 5;

            if (track is BehaviorTimelineHitboxTrack)
                return 6;

            if (track is BehaviorTimelineTransitionTrack)
                return 7;

            return 8;
        }

        private static bool TryBuildAuthoringTrackSnapshot(
            TrackAsset track,
            PlayableDirector director,
            Transform referenceRoot,
            int sortIndex,
            List<string> exportWarnings,
            out BehaviorAuthoringTrackSnapshot snapshot)
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

        private static List<BehaviorAuthoringClipSnapshot> BuildAuthoringClipSnapshotsForTrack(
            TrackAsset track,
            PlayableDirector director,
            Transform referenceRoot,
            List<string> exportWarnings)
        {
            List<BehaviorAuthoringClipSnapshot> results = new List<BehaviorAuthoringClipSnapshot>();
            if (track == null)
                return results;

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

            if (track is ControlTrack controlTrack)
            {
                AppendVfxControlSnapshots(results, controlTrack, director, referenceRoot, exportWarnings);
                return results;
            }

            if (track is ActivationTrack activationTrack)
            {
                AppendVfxActivationSnapshots(results, activationTrack, director, referenceRoot, exportWarnings);
                return results;
            }

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

        private static void AppendVfxControlSnapshots(
            List<BehaviorAuthoringClipSnapshot> results,
            ControlTrack track,
            PlayableDirector director,
            Transform referenceRoot,
            List<string> exportWarnings)
        {
            if (results == null || track == null)
                return;

            foreach (TimelineClip clip in track.GetClips())
            {
                if (clip?.asset is not ControlPlayableAsset playableAsset)
                    continue;

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

        private static void AppendVfxActivationSnapshots(
            List<BehaviorAuthoringClipSnapshot> results,
            ActivationTrack track,
            PlayableDirector director,
            Transform referenceRoot,
            List<string> exportWarnings)
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

        private void ExportToBehaviorClip()
        {
            if (sourceTimeline == null)
                return;

            BehaviorClip target = ResolveTargetBehaviorClip();
            if (target == null)
                return;

            List<AnimationSegmentEntry> segmentEntries = new List<AnimationSegmentEntry>();
            List<BehaviorEvent> behaviorEvents = new List<BehaviorEvent>();
            List<HitboxDef> hitboxes = new List<HitboxDef>();
            List<BehaviorTransitionDefinition> transitions = new List<BehaviorTransitionDefinition>();
            List<BehaviorAuthoringTrackSnapshot> authoringTrackSnapshots = new List<BehaviorAuthoringTrackSnapshot>();
            List<string> exportWarnings = new List<string>();
            PlayableDirector exportDirector = ResolvePreviewDirectorForOpen(sourceTimeline, previewDirector);
            Transform exportReferenceRoot = ResolveExportReferenceRoot();

            double maxEndTime = 0d;
            int trackSortIndex = 0;
            foreach (TrackAsset track in EnumerateTimelineTracks(sourceTimeline))
            {
                if (track == null || track.mutedInHierarchy)
                    continue;

                if (TryBuildAuthoringTrackSnapshot(
                        track,
                        exportDirector,
                        exportReferenceRoot,
                        trackSortIndex++,
                        exportWarnings,
                        out BehaviorAuthoringTrackSnapshot trackSnapshot))
                {
                    authoringTrackSnapshots.Add(trackSnapshot);
                }

                if (track is AnimationTrack animationTrack)
                {
                    ExportNativeAnimationTrack(animationTrack, segmentEntries, exportWarnings, ref maxEndTime);
                    continue;
                }

                if (track is AudioTrack audioTrack)
                {
                    ExportNativeAudioTrack(audioTrack, exportDirector, exportReferenceRoot, behaviorEvents, exportWarnings,
                        ref maxEndTime);
                    continue;
                }

                if (track is ControlTrack controlTrack)
                {
                    ExportNativeVfxTrack(controlTrack, exportDirector, exportReferenceRoot, behaviorEvents, exportWarnings,
                        ref maxEndTime);
                    continue;
                }

                if (track is ActivationTrack activationTrack)
                {
                    ExportNativeActivationTrack(activationTrack, exportDirector, exportReferenceRoot, behaviorEvents,
                        exportWarnings, ref maxEndTime);
                    continue;
                }

                foreach (TimelineClip clip in track.GetClips())
                {
                    if (clip == null)
                        continue;

                    if (clip.end > maxEndTime)
                        maxEndTime = clip.end;

                    if (track is BehaviorTimelineEventTrack)
                    {
                        BehaviorTimelineEventClipAsset clipAsset = clip.asset as BehaviorTimelineEventClipAsset;
                        if (clipAsset == null)
                            continue;

                        BehaviorEvent normalizedEvent =
                            BehaviorEventResolver.CreateNormalizedClone(clipAsset.eventData, (float)clip.start, track.name);
                        if (normalizedEvent != null &&
                            BehaviorEventResolver.ResolveEffectiveType(normalizedEvent) == BehaviorEventType.PlayAudio)
                        {
                            exportWarnings.Add(
                                $"Behavior Events 轨道中的片段 '{clip.displayName}' 仍被配置为 PlayAudio。该作者入口已废弃，请改用原生 AudioTrack；当前片段已跳过导出。");
                            continue;
                        }

                        behaviorEvents.Add(normalizedEvent);
                        continue;
                    }

                    if (track is BehaviorTimelineHitboxTrack)
                    {
                        BehaviorTimelineHitboxClipAsset clipAsset = clip.asset as BehaviorTimelineHitboxClipAsset;
                        if (clipAsset == null)
                            continue;

                        hitboxes.Add(CloneHitboxDef(
                            clipAsset.hitboxData,
                            (float)clip.start,
                            (float)clip.duration,
                            track.name));
                        continue;
                    }

                    if (track is BehaviorTimelineTransitionTrack)
                    {
                        BehaviorTimelineTransitionClipAsset clipAsset = clip.asset as BehaviorTimelineTransitionClipAsset;
                        if (clipAsset == null)
                            continue;

                        transitions.Add(CloneTransitionDefinition(
                            clipAsset.transitionData,
                            (float)clip.start,
                            (float)clip.duration,
                            track.name));
                        continue;
                    }
                }
            }

            segmentEntries.Sort(CompareAnimationSegmentEntries);
            behaviorEvents.Sort(CompareBehaviorEvents);
            hitboxes.Sort(CompareHitboxes);
            transitions.Sort(CompareTransitions);

            AnimationSegment[] exportedSegments = new AnimationSegment[segmentEntries.Count];
            for (int i = 0; i < segmentEntries.Count; i++)
                exportedSegments[i] = segmentEntries[i].segment;

            BehaviorTimelineMetaClipAsset exportedMeta = ResolveTimelineMeta(sourceTimeline, exportWarnings);
            BehaviorEvent[] exportedEvents = behaviorEvents.ToArray();
            HitboxDef[] exportedHitboxes = hitboxes.ToArray();
            BehaviorTransitionDefinition[] exportedTransitions = transitions.ToArray();
            BehaviorAuthoringTrackSnapshot[] exportedAuthoringTracks = authoringTrackSnapshots.ToArray();

            UnityEditor.Undo.RegisterCompleteObjectUndo(target, "Export BehaviorClip");
            target.animationSegments = exportedSegments;
            target.events = exportedEvents;
            target.hitboxes = exportedHitboxes;
            target.transitions = exportedTransitions;
            target.authoringTracks = exportedAuthoringTracks;
            target.totalDuration = Mathf.Max(0.01f, (float)Math.Max(maxEndTime, sourceTimeline.duration));
            target.wrapMode = exportedMeta != null ? exportedMeta.wrapMode : wrapMode;
            target.speedMultiplier = Mathf.Max(0.01f, exportedMeta != null ? exportedMeta.speedMultiplier : speedMultiplier);
            target.priority = exportedMeta != null ? exportedMeta.priority : priority;

            UnityEditor.EditorUtility.SetDirty(target);
            UnityEditor.AssetDatabase.SaveAssets();
            UnityEditor.AssetDatabase.Refresh();
            UnityEditor.Selection.activeObject = target;

            for (int i = 0; i < exportWarnings.Count; i++)
                Debug.LogWarning($"[Timeline Export] {exportWarnings[i]}", sourceTimeline);

            Debug.Log(
                $"Timeline 已导出到 BehaviorClip：{target.name}\n" +
                $"Segments={target.animationSegments.Length}, Events={target.events.Length}, Hitboxes={target.hitboxes.Length}, Duration={target.totalDuration:F2}s",
                target);
        }

        private void OpenTimelineForPreview()
        {
            if (sourceTimeline == null)
                return;

            PlayableDirector resolvedDirector = ResolvePreviewDirectorForOpen(sourceTimeline, previewDirector);
            if (resolvedDirector == null)
            {
                Debug.LogWarning(
                    "没有找到可用于预览的 PlayableDirector。请在场景里选择或指定一个挂有 PlayableDirector 的对象后再打开预览。",
                    sourceTimeline);
                return;
            }

            previewDirector = resolvedDirector;
            previewDirector.playableAsset = sourceTimeline;

            Animator resolvedAnimator = ResolvePreviewAnimator(previewDirector, previewAnimator);
            if (resolvedAnimator != null)
            {
                previewAnimator = resolvedAnimator;
                BindPreviewAnimator(previewDirector, sourceTimeline, resolvedAnimator);
            }

            AudioSource resolvedAudioSource = ResolvePreviewAudioSource(previewDirector);
            if (resolvedAudioSource != null)
            {
                BindPreviewAudioSource(previewDirector, sourceTimeline, resolvedAudioSource);
            }

            previewDirector.time = 0d;
            previewDirector.RebuildGraph();
            previewDirector.Evaluate();
            RefreshTimelineEditor(sourceTimeline, true, previewDirector);
            UnityEditor.Selection.activeObject = previewDirector.gameObject;
        }

        private void CleanupAuthoringSession()
        {
            if (previewDirector != null)
            {
                previewDirector.playOnAwake = false;
                previewDirector.Stop();
                previewDirector.time = 0d;
                previewDirector.playableAsset = null;
                previewDirector.RebuildGraph();

                if (removePreviewDirectorOnFinish)
                    UnityEditor.Undo.DestroyObjectImmediate(previewDirector);
            }

            for (int i = createdPreviewAudioSources.Count - 1; i >= 0; i--)
            {
                AudioSource createdPreviewAudioSource = createdPreviewAudioSources[i];
                if (createdPreviewAudioSource == null)
                    continue;

                UnityEditor.Undo.DestroyObjectImmediate(createdPreviewAudioSource);
            }
            createdPreviewAudioSources.Clear();

            if (removePreviewAnimatorOnFinish &&
                previewAnimator != null &&
                previewAnimator.gameObject != null)
            {
                UnityEditor.Undo.DestroyObjectImmediate(previewAnimator);
            }

            if (autoAssignedReferenceRoot)
                previewReferenceRoot = null;

            previewDirector = null;
            previewAnimator = null;
            removePreviewDirectorOnFinish = false;
            removePreviewAnimatorOnFinish = false;
            autoAssignedReferenceRoot = false;
            BehaviorEditorContext.ReferenceRootObject = previewReferenceRoot;

            if (sourceTimeline != null)
                RefreshTimelineEditor(sourceTimeline, true, null);
        }

        private static void RefreshTimelineEditor(TimelineAsset timelineAsset, bool contentsChanged, PlayableDirector preferredDirector)
        {
            UnityEditor.Timeline.TimelineEditorWindow timelineWindow = UnityEditor.Timeline.TimelineEditor.GetOrCreateWindow();
            RestoreTimelineWindowContext(timelineWindow, timelineAsset, preferredDirector);

            UnityEditor.Timeline.RefreshReason reason = UnityEditor.Timeline.RefreshReason.WindowNeedsRedraw;
            reason |= UnityEditor.Timeline.RefreshReason.SceneNeedsUpdate;
            reason |= contentsChanged
                ? UnityEditor.Timeline.RefreshReason.ContentsAddedOrRemoved
                : UnityEditor.Timeline.RefreshReason.ContentsModified;

            UnityEditor.Timeline.TimelineEditor.Refresh(reason);
            UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();

            if (timelineAsset == null)
                return;

            pendingDelayedTimelineAsset = timelineAsset;
            pendingDelayedTimelineDirector = preferredDirector;
            pendingDelayedTimelineReason = reason;
            if (pendingDelayedTimelineRefresh)
                return;

            pendingDelayedTimelineRefresh = true;
            UnityEditor.EditorApplication.delayCall += ExecuteDelayedTimelineRefresh;
        }

        private static void ExecuteDelayedTimelineRefresh()
        {
            pendingDelayedTimelineRefresh = false;
            TimelineAsset delayedTimelineAsset = pendingDelayedTimelineAsset;
            PlayableDirector delayedTimelineDirector = pendingDelayedTimelineDirector;
            UnityEditor.Timeline.RefreshReason delayedTimelineReason = pendingDelayedTimelineReason;
            pendingDelayedTimelineAsset = null;
            pendingDelayedTimelineDirector = null;
            if (delayedTimelineAsset == null)
                return;

            UnityEditor.Timeline.TimelineEditorWindow delayedWindow = UnityEditor.Timeline.TimelineEditor.GetOrCreateWindow();
            RestoreTimelineWindowContext(
                delayedWindow,
                delayedTimelineAsset,
                delayedTimelineDirector);
            UnityEditor.Timeline.TimelineEditor.Refresh(delayedTimelineReason);
            UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }

        private static void RestoreTimelineWindowContext(UnityEditor.Timeline.TimelineEditorWindow timelineWindow,
            TimelineAsset timelineAsset, PlayableDirector preferredDirector)
        {
            if (timelineWindow == null || timelineAsset == null)
                return;

            PlayableDirector resolvedDirector = ResolvePreviewDirectorForRefresh(timelineAsset, preferredDirector);
            if (resolvedDirector != null)
            {
                timelineWindow.SetTimeline(resolvedDirector);
                return;
            }

            PlayableDirector inspectedDirector = UnityEditor.Timeline.TimelineEditor.inspectedDirector;
            if (inspectedDirector != null && inspectedDirector.playableAsset == timelineAsset)
            {
                timelineWindow.SetTimeline(inspectedDirector);
                return;
            }

            if (UnityEditor.Timeline.TimelineEditor.inspectedAsset == timelineAsset)
                return;

            if (UnityEditor.Timeline.TimelineEditor.inspectedAsset == null &&
                UnityEditor.Timeline.TimelineEditor.inspectedDirector == null)
            {
                timelineWindow.SetTimeline(timelineAsset);
            }
        }

        private static PlayableDirector ResolvePreviewDirectorForOpen(TimelineAsset timelineAsset,
            PlayableDirector preferredDirector)
        {
            if (preferredDirector != null &&
                (preferredDirector.playableAsset == null || preferredDirector.playableAsset == timelineAsset))
            {
                return preferredDirector;
            }

            if (UnityEditor.Selection.activeGameObject != null &&
                UnityEditor.Selection.activeGameObject.TryGetComponent(out PlayableDirector selectedDirector) &&
                selectedDirector.playableAsset == timelineAsset)
            {
                return selectedDirector;
            }

            PlayableDirector[] directors = UnityEngine.Resources.FindObjectsOfTypeAll<PlayableDirector>();
            for (int i = 0; i < directors.Length; i++)
            {
                PlayableDirector director = directors[i];
                if (director == null || !director.gameObject.scene.IsValid())
                    continue;

                if (director.playableAsset == timelineAsset)
                    return director;
            }

            return null;
        }

        private static PlayableDirector ResolvePreviewDirectorForRefresh(TimelineAsset timelineAsset,
            PlayableDirector preferredDirector)
        {
            if (preferredDirector != null &&
                (preferredDirector.playableAsset == null || preferredDirector.playableAsset == timelineAsset))
            {
                return preferredDirector;
            }

            if (UnityEditor.Selection.activeGameObject != null &&
                UnityEditor.Selection.activeGameObject.TryGetComponent(out PlayableDirector selectedDirector) &&
                selectedDirector.playableAsset == timelineAsset)
            {
                return selectedDirector;
            }

            return ResolvePreviewDirectorForOpen(timelineAsset, null);
        }

        private GameObject ResolveAuthoringTarget()
        {
            if (previewReferenceRoot != null)
                return previewReferenceRoot;

            return UnityEditor.Selection.activeGameObject;
        }

        private static PlayableDirector EnsurePreviewDirector(GameObject target, out bool createdByTool)
        {
            createdByTool = false;
            if (target == null)
                return null;

            PlayableDirector director = target.GetComponent<PlayableDirector>();
            if (director != null)
            {
                director.playOnAwake = false;
                return director;
            }

            director = UnityEditor.Undo.AddComponent<PlayableDirector>(target);
            director.playOnAwake = false;
            createdByTool = true;
            return director;
        }

        private static Animator EnsurePreviewAnimator(GameObject target, out bool createdByTool)
        {
            createdByTool = false;
            if (target == null)
                return null;

            Animator animator = target.GetComponent<Animator>();
            if (animator != null)
                return animator;

            animator = target.GetComponentInChildren<Animator>(true);
            if (animator != null)
                return animator;

            animator = UnityEditor.Undo.AddComponent<Animator>(target);
            createdByTool = true;
            Debug.LogWarning($"角色对象 '{target.name}' 缺少 Animator，工具已自动补齐一个 Animator 组件。", target);
            return animator;
        }

        private static Animator ResolvePreviewAnimator(PlayableDirector director, Animator preferredAnimator)
        {
            if (preferredAnimator != null)
                return preferredAnimator;

            if (director == null)
                return null;

            if (director.TryGetComponent(out Animator sameObjectAnimator))
                return sameObjectAnimator;

            return director.GetComponentInChildren<Animator>(true);
        }

        private static AudioSource ResolvePreviewAudioSource(PlayableDirector director)
        {
            if (director == null)
                return null;

            if (director.TryGetComponent(out AudioSource sameObjectAudioSource))
                return sameObjectAudioSource;

            return director.GetComponentInChildren<AudioSource>(true);
        }

        private static void BindPreviewAnimator(PlayableDirector director, TimelineAsset timelineAsset, Animator animator)
        {
            if (director == null || timelineAsset == null || animator == null)
                return;

            foreach (TrackAsset track in EnumerateTimelineTracks(timelineAsset))
            {
                if (track is not AnimationTrack)
                    continue;

                if (director.GetGenericBinding(track) == animator)
                    continue;

                director.SetGenericBinding(track, animator);
            }
        }

        private static void BindPreviewAudioSource(PlayableDirector director, TimelineAsset timelineAsset,
            AudioSource audioSource)
        {
            if (director == null || timelineAsset == null || audioSource == null)
                return;

            foreach (TrackAsset track in EnumerateTimelineTracks(timelineAsset))
            {
                if (track is not AudioTrack)
                    continue;

                if (director.GetGenericBinding(track) == audioSource)
                    continue;

                director.SetGenericBinding(track, audioSource);
            }
        }

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

        private static void ExportNativeAnimationTrack(AnimationTrack track, List<AnimationSegmentEntry> segmentEntries,
            List<string> exportWarnings, ref double maxEndTime)
        {
            if (track == null || segmentEntries == null)
                return;

            int layer = ResolveAnimationLayerFromTrackName(track.name);
            foreach (TimelineClip clip in track.GetClips())
            {
                if (clip == null)
                    continue;

                AnimationClip animationClip = null;
                float crossFadeDuration;
                bool isLegacyClipAsset = false;
                AnimationPlayableAsset playableAsset = clip.asset as AnimationPlayableAsset;
                if (playableAsset != null)
                {
                    animationClip = playableAsset.clip;
                    crossFadeDuration = ResolveNativeAnimationCrossFade(clip);
                }
                else if (clip.asset is BehaviorTimelineAnimationClipAsset legacyPlayableAsset)
                {
                    animationClip = legacyPlayableAsset.animationClip;
                    crossFadeDuration = Mathf.Max(0f, legacyPlayableAsset.crossFadeDuration);
                    isLegacyClipAsset = true;
                }
                else
                {
                    continue;
                }

                if (animationClip == null)
                    continue;

                if (Math.Abs(clip.clipIn) > 0.0001d)
                {
                    exportWarnings?.Add(
                        $"AnimationTrack '{track.name}' 的片段 '{clip.displayName}' 使用了 Clip In={clip.clipIn:F2}s，当前运行时不会精确复现该裁切。");
                }

                if (Math.Abs(clip.timeScale - 1d) > 0.0001d)
                {
                    exportWarnings?.Add(
                        $"AnimationTrack '{track.name}' 的片段 '{clip.displayName}' 使用了 Time Scale={clip.timeScale:F2}，当前运行时不会精确复现该变速。");
                }

                if (!isLegacyClipAsset &&
                    (playableAsset.position != Vector3.zero || playableAsset.eulerAngles != Vector3.zero))
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

        private static void ExportNativeAudioTrack(AudioTrack track, PlayableDirector director, Transform referenceRoot,
            List<BehaviorEvent> behaviorEvents, List<string> exportWarnings, ref double maxEndTime)
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

        private static void ExportNativeVfxTrack(ControlTrack track, PlayableDirector director, Transform referenceRoot,
            List<BehaviorEvent> behaviorEvents, List<string> exportWarnings, ref double maxEndTime)
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

        private static void ExportNativeActivationTrack(ActivationTrack track, PlayableDirector director,
            Transform referenceRoot, List<BehaviorEvent> behaviorEvents, List<string> exportWarnings, ref double maxEndTime)
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

        private static float ResolveNativeAnimationCrossFade(TimelineClip clip)
        {
            if (clip == null)
                return 0f;

            double blendDuration = Math.Max(clip.blendInDuration, clip.easeInDuration);
            double clipDuration = Math.Max(0.0001d, clip.duration);
            double normalizedDuration = blendDuration > 0.0001d ? blendDuration / clipDuration : 0d;
            return Mathf.Clamp01((float)normalizedDuration);
        }

        private static int ResolveAnimationLayerFromTrackName(string trackName)
        {
            if (string.IsNullOrWhiteSpace(trackName))
                return 0;

            string[] tokens = trackName.Split(new[] { ' ', '_', '-', '/' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < tokens.Length; i++)
            {
                string token = tokens[i];
                if (token.Length >= 2 &&
                    (token[0] == 'L' || token[0] == 'l') &&
                    int.TryParse(token.Substring(1), out int tokenLayer))
                {
                    return Mathf.Max(0, tokenLayer);
                }

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

        private static AudioSource ResolveBoundAudioSource(AudioTrack track, PlayableDirector director)
        {
            if (track == null || director == null)
                return null;

            return director.GetGenericBinding(track) as AudioSource;
        }

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

        private static float ReadClampedFloatSerializedProperty(
            UnityEngine.Object targetObject,
            string propertyName,
            float fallbackValue)
        {
            return ReadSerializedPropertyValue(
                targetObject,
                propertyName,
                fallbackValue,
                property => Mathf.Clamp01(property.floatValue));
        }

        private static int ReadIntSerializedProperty(
            UnityEngine.Object targetObject,
            string propertyName,
            int fallbackValue)
        {
            return ReadSerializedPropertyValue(targetObject, propertyName, fallbackValue, property => property.intValue);
        }

        private static void ConfigureControlPlayableAsset(
            ControlPlayableAsset playableAsset,
            GameObject prefab,
            int postPlayback)
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

        private static TResult ReadSerializedPropertyValue<TResult>(
            UnityEngine.Object targetObject,
            string propertyName,
            TResult fallbackValue,
            Func<UnityEditor.SerializedProperty, TResult> readValue)
        {
            if (targetObject == null || readValue == null)
                return fallbackValue;

            using (UnityEditor.SerializedObject serializedObject = new UnityEditor.SerializedObject(targetObject))
            {
                return TryGetSerializedProperty(serializedObject, propertyName, out UnityEditor.SerializedProperty property)
                    ? readValue(property)
                    : fallbackValue;
            }
        }

        private static void SetSerializedPropertyValue(
            UnityEditor.SerializedObject serializedObject,
            string propertyName,
            UnityEngine.Object value)
        {
            if (!TryGetSerializedProperty(serializedObject, propertyName, out UnityEditor.SerializedProperty property))
                return;

            property.objectReferenceValue = value;
        }

        private static void SetSerializedPropertyValue(
            UnityEditor.SerializedObject serializedObject,
            string propertyName,
            bool value)
        {
            if (!TryGetSerializedProperty(serializedObject, propertyName, out UnityEditor.SerializedProperty property))
                return;

            property.boolValue = value;
        }

        private static void SetSerializedPropertyValue(
            UnityEditor.SerializedObject serializedObject,
            string propertyName,
            int value)
        {
            if (!TryGetSerializedProperty(serializedObject, propertyName, out UnityEditor.SerializedProperty property))
                return;

            property.intValue = value;
        }

        private static void SetSerializedPropertyValue(
            UnityEditor.SerializedObject serializedObject,
            string propertyName,
            float value)
        {
            if (!TryGetSerializedProperty(serializedObject, propertyName, out UnityEditor.SerializedProperty property))
                return;

            property.floatValue = value;
        }

        private static bool TryGetSerializedProperty(
            UnityEditor.SerializedObject serializedObject,
            string propertyName,
            out UnityEditor.SerializedProperty property)
        {
            property = null;
            if (serializedObject == null || string.IsNullOrWhiteSpace(propertyName))
                return false;

            property = serializedObject.FindProperty(propertyName);
            return property != null;
        }

        private static string BuildRelativeAuthoringObjectPath(Transform referenceRoot, GameObject targetObject)
        {
            if (referenceRoot == null || targetObject == null)
                return string.Empty;

            return BehaviorReferenceBoneEditorUtility.BuildRelativeBonePath(referenceRoot, targetObject.transform);
        }

        private static bool TryResolveSpawnableControlTrackPrefab(GameObject sourceObject, Transform referenceRoot,
            out GameObject prefabAsset, out GameObject instanceRootObject)
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

        private static void BuildTransformBinding(Transform sourceTransform, Transform referenceRoot,
            out string referenceBone, out Vector3 positionOffset, out Vector3 rotationOffset, out Vector3 scaleOffset)
        {
            referenceBone = string.Empty;
            positionOffset = Vector3.zero;
            rotationOffset = Vector3.zero;
            scaleOffset = Vector3.one;

            if (sourceTransform == null)
            {
                if (referenceRoot != null)
                    referenceBone = BehaviorReferenceBoneEditorUtility.BuildRelativeBonePath(referenceRoot, referenceRoot);
                return;
            }

            if (referenceRoot == null)
            {
                positionOffset = sourceTransform.position;
                rotationOffset = sourceTransform.rotation.eulerAngles;
                scaleOffset = sourceTransform.lossyScale;
                return;
            }

            if (sourceTransform == referenceRoot)
            {
                referenceBone = BehaviorReferenceBoneEditorUtility.BuildRelativeBonePath(referenceRoot, referenceRoot);
                return;
            }

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

        private static void NormalizeSpawnablePrefabTransformOffsets(GameObject prefab, Transform sourceTransform,
            ref Vector3 positionOffset, ref Vector3 rotationOffset, ref Vector3 scaleOffset)
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

            positionOffset -= prefab.transform.localPosition;
            rotationOffset = (Quaternion.Inverse(prefab.transform.localRotation) * Quaternion.Euler(rotationOffset)).eulerAngles;
            scaleOffset = DivideVector3Safely(scaleOffset, prefab.transform.localScale);
        }

        private static Vector3 DivideVector3Safely(Vector3 value, Vector3 divisor)
        {
            return new Vector3(
                Mathf.Abs(divisor.x) > 0.0001f ? value.x / divisor.x : value.x,
                Mathf.Abs(divisor.y) > 0.0001f ? value.y / divisor.y : value.y,
                Mathf.Abs(divisor.z) > 0.0001f ? value.z / divisor.z : value.z);
        }

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

            BehaviorClip created = CreateInstance<BehaviorClip>();
            created.name = assetName;
            UnityEditor.AssetDatabase.CreateAsset(created, assetPath);
            targetBehaviorClip = created;
            return created;
        }

        private static T EnsureTrack<T>(
            TimelineAsset timelineAsset,
            string trackName,
            IReadOnlyList<TrackAsset> timelineTracks,
            out bool changed)
            where T : TrackAsset, new()
        {
            changed = false;
            if (timelineAsset == null)
                return null;

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

            T created = timelineAsset.CreateTrack<T>(null, trackName);
            UnityEditor.Undo.RegisterCreatedObjectUndo(created, "Create Behavior Track");
            UnityEditor.EditorUtility.SetDirty(created);
            changed = true;
            return created;
        }

        private static T GetOrCreateExactTrack<T>(TimelineAsset timelineAsset, string trackName)
            where T : TrackAsset, new()
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

        private static void ClearTrackClips(TrackAsset track)
        {
            DeleteClipsByPredicate(track, "Clear Timeline Track Clips", _ => true);
        }

        private static string ResolveImportedClipDisplayName(
            BehaviorAuthoringClipSnapshot clipSnapshot,
            string fallbackDisplayName)
        {
            return clipSnapshot != null && !string.IsNullOrWhiteSpace(clipSnapshot.displayName)
                ? clipSnapshot.displayName
                : fallbackDisplayName;
        }

        private static string ResolveTrackNameOrDefault(string authoringTrackName, string defaultTrackName)
        {
            return !string.IsNullOrWhiteSpace(authoringTrackName) ? authoringTrackName : defaultTrackName;
        }

        private static string BuildAudioTrackName(string referenceBone)
        {
            if (string.IsNullOrWhiteSpace(referenceBone))
                return NativeAudioTrackName;

            return $"{NativeAudioTrackName} [{referenceBone.Replace('/', '_')}]";
        }

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

        private static double ResolveImportedMetaClipDuration(BehaviorClip behaviorClip)
        {
            double totalDuration = behaviorClip != null ? Math.Max(0.01f, behaviorClip.totalDuration) : 0.1d;
            return Math.Max(0.01d, Math.Min(0.1d, totalDuration));
        }

        private static float ResolveImportedAnimationSegmentDuration(
            BehaviorClip behaviorClip,
            AnimationSegment[] segments,
            int currentIndex,
            float currentStartTime)
        {
            if (segments == null || currentIndex < 0 || currentIndex >= segments.Length)
                return 0.1f;

            AnimationSegment currentSegment = segments[currentIndex];
            float speed = Mathf.Max(0.01f, behaviorClip != null ? behaviorClip.speedMultiplier : 1f);
            float fallbackDuration = currentSegment?.clip != null ? currentSegment.clip.length / speed : 0.1f;
            if (behaviorClip == null)
                return Mathf.Max(0.01f, fallbackDuration);

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

        private GameObject ResolveAuthoringBoundObjectForImport(
            BehaviorAuthoringClipSnapshot clipSnapshot,
            BehaviorEvent behaviorEvent)
        {
            Transform targetTransform = null;
            if (clipSnapshot != null && !string.IsNullOrWhiteSpace(clipSnapshot.boundObjectPath))
                targetTransform = ResolveTrackBindingTransformForImport(clipSnapshot.boundObjectPath);

            if (targetTransform == null && behaviorEvent != null && !string.IsNullOrWhiteSpace(behaviorEvent.referenceBone))
                targetTransform = ResolveReferenceTransformForImport(behaviorEvent.referenceBone);

            return targetTransform != null ? targetTransform.gameObject : null;
        }

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

        private static bool TryGetTrackClipAsset<TClipAsset>(TrackAsset track, out TClipAsset clipAsset)
            where TClipAsset : class
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

        private static int GetTrackContentScore(TrackAsset track)
        {
            if (track == null)
                return int.MinValue;

            int score = 0;
            foreach (TimelineClip _ in track.GetClips())
                score += 10;

            foreach (IMarker _ in track.GetMarkers())
                score += 2;

            foreach (TrackAsset _ in track.GetChildTracks())
                score += 1;

            return score;
        }

        private static void RemoveEmptyDuplicateTracks<T>(TimelineAsset timelineAsset, T keepTrack, string trackName)
            where T : TrackAsset
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

        private static List<TrackAsset> CollectTimelineTracks(TimelineAsset timelineAsset)
        {
            List<TrackAsset> tracks = new List<TrackAsset>();
            if (timelineAsset == null)
                return tracks;

            foreach (TrackAsset track in EnumerateTimelineTracks(timelineAsset))
            {
                if (track != null)
                    tracks.Add(track);
            }

            return tracks;
        }

        private static void DeleteTracksByPredicate(
            TimelineAsset timelineAsset,
            string undoName,
            Predicate<TrackAsset> shouldDelete)
        {
            if (timelineAsset == null || shouldDelete == null)
                return;

            List<TrackAsset> tracksToDelete = null;
            foreach (TrackAsset track in EnumerateTimelineTracks(timelineAsset))
            {
                if (!shouldDelete(track))
                    continue;

                tracksToDelete ??= new List<TrackAsset>();
                tracksToDelete.Add(track);
            }

            if (tracksToDelete == null)
                return;

            UnityEditor.Undo.RegisterCompleteObjectUndo(timelineAsset, undoName);
            for (int i = 0; i < tracksToDelete.Count; i++)
                timelineAsset.DeleteTrack(tracksToDelete[i]);

            UnityEditor.EditorUtility.SetDirty(timelineAsset);
        }

        private static void DeleteClipsByPredicate(
            TrackAsset track,
            string undoName,
            Predicate<TimelineClip> shouldDelete)
        {
            if (track == null || shouldDelete == null)
                return;

            List<TimelineClip> clipsToDelete = null;
            foreach (TimelineClip clip in track.GetClips())
            {
                if (!shouldDelete(clip))
                    continue;

                clipsToDelete ??= new List<TimelineClip>();
                clipsToDelete.Add(clip);
            }

            if (clipsToDelete == null)
                return;

            UnityEditor.Undo.RegisterCompleteObjectUndo(track, undoName);
            for (int i = 0; i < clipsToDelete.Count; i++)
                track.DeleteClip(clipsToDelete[i]);

            UnityEditor.EditorUtility.SetDirty(track);
        }

        private static void AddDirtyTrack(ref HashSet<TrackAsset> dirtyTracks, TrackAsset track)
        {
            if (track == null)
                return;

            dirtyTracks ??= new HashSet<TrackAsset>();
            dirtyTracks.Add(track);
        }

        private static void SetTracksDirty(HashSet<TrackAsset> dirtyTracks)
        {
            if (dirtyTracks == null)
                return;

            foreach (TrackAsset track in dirtyTracks)
                UnityEditor.EditorUtility.SetDirty(track);
        }

        private static bool TryCompareTimedTrackItems<T>(
            T left,
            T right,
            float leftStartTime,
            float rightStartTime,
            string leftTrackName,
            string rightTrackName,
            out int result)
            where T : class
        {
            if (TryCompareNullReferences(left, right, out result))
                return true;

            result = leftStartTime.CompareTo(rightStartTime);
            if (result != 0)
                return true;

            result = CompareNullableStrings(leftTrackName, rightTrackName);
            return result != 0;
        }

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

        private static IEnumerable<TrackAsset> EnumerateTimelineTracks(TimelineAsset timelineAsset)
        {
            if (timelineAsset == null)
                yield break;

            foreach (TrackAsset rootTrack in timelineAsset.GetRootTracks())
            {
                foreach (TrackAsset track in EnumerateTrackRecursive(rootTrack))
                    yield return track;
            }
        }

        private static IEnumerable<TrackAsset> EnumerateTrackRecursive(TrackAsset track)
        {
            if (track == null)
                yield break;

            if (track is not GroupTrack)
                yield return track;

            foreach (TrackAsset childTrack in track.GetChildTracks())
            {
                foreach (TrackAsset nestedTrack in EnumerateTrackRecursive(childTrack))
                    yield return nestedTrack;
            }
        }

        private static int CompareAnimationSegmentEntries(AnimationSegmentEntry left, AnimationSegmentEntry right)
        {
            AnimationSegment leftSegment = left.segment;
            AnimationSegment rightSegment = right.segment;
            if (TryCompareTimedTrackItems(
                    left,
                    right,
                    left != null ? left.startTime : 0f,
                    right != null ? right.startTime : 0f,
                    leftSegment?.authoringTrackName,
                    rightSegment?.authoringTrackName,
                    out int result))
            {
                return result;
            }

            result = (leftSegment?.layer ?? 0).CompareTo(rightSegment?.layer ?? 0);
            if (result != 0)
                return result;

            return CompareNullableStrings(leftSegment?.clip?.name, rightSegment?.clip?.name);
        }

        private static int CompareBehaviorEvents(BehaviorEvent left, BehaviorEvent right)
        {
            if (TryCompareTimedTrackItems(
                    left,
                    right,
                    left != null ? left.time : 0f,
                    right != null ? right.time : 0f,
                    left?.authoringTrackName,
                    right?.authoringTrackName,
                    out int result))
            {
                return result;
            }

            result = ((int)BehaviorEventResolver.ResolveEffectiveType(left))
                .CompareTo((int)BehaviorEventResolver.ResolveEffectiveType(right));
            if (result != 0)
                return result;

            result = CompareNullableStrings(left.referenceBone, right.referenceBone);
            if (result != 0)
                return result;

            return CompareNullableStrings(left.targetObjectPath, right.targetObjectPath);
        }

        private static int CompareHitboxes(HitboxDef left, HitboxDef right)
        {
            if (TryCompareTimedTrackItems(
                    left,
                    right,
                    left != null ? left.startTime : 0f,
                    right != null ? right.startTime : 0f,
                    left?.authoringTrackName,
                    right?.authoringTrackName,
                    out int result))
            {
                return result;
            }

            return CompareNullableStrings(left.name, right.name);
        }

        private static int CompareTransitions(BehaviorTransitionDefinition left, BehaviorTransitionDefinition right)
        {
            if (TryCompareTimedTrackItems(
                    left,
                    right,
                    left != null ? left.startTime : 0f,
                    right != null ? right.startTime : 0f,
                    left?.authoringTrackName,
                    right?.authoringTrackName,
                    out int result))
            {
                return result;
            }

            return CompareNullableStrings(left.targetBehaviorKey, right.targetBehaviorKey);
        }

        private static bool TryCompareNullReferences<T>(T left, T right, out int result) where T : class
        {
            if (ReferenceEquals(left, right))
            {
                result = 0;
                return true;
            }

            if (left == null)
            {
                result = 1;
                return true;
            }

            if (right == null)
            {
                result = -1;
                return true;
            }

            result = 0;
            return false;
        }

        private static int CompareNullableStrings(string left, string right)
        {
            if (string.IsNullOrEmpty(left) && string.IsNullOrEmpty(right))
                return 0;
            if (string.IsNullOrEmpty(left))
                return 1;
            if (string.IsNullOrEmpty(right))
                return -1;

            return string.Compare(left, right, StringComparison.Ordinal);
        }

        private static HitboxDef CloneHitboxDef(
            HitboxDef source,
            float timelineStartTime,
            float timelineDuration,
            string trackName = null)
        {
            HitboxDef cloned = new HitboxDef();
            if (source != null)
            {
                cloned.authoringTrackName = !string.IsNullOrWhiteSpace(trackName)
                    ? trackName
                    : source.authoringTrackName;
                cloned.name = source.name;
                cloned.shape = source.shape;
                cloned.hitGroupId = source.hitGroupId;
                cloned.referenceBone = source.referenceBone;
                cloned.positionOffset = source.positionOffset;
                cloned.rotationOffset = source.rotationOffset;
                cloned.scaleOffset = source.scaleOffset;
                cloned.size = source.size;
                cloned.numericKey = source.numericKey;
                cloned.damageMultiplier = source.damageMultiplier;
                cloned.hitStunDuration = source.hitStunDuration;
                cloned.knockbackForce = source.knockbackForce;
                cloned.onHitBuff = source.onHitBuff;
            }

            cloned.startTime = Mathf.Max(0f, timelineStartTime);
            cloned.duration = Mathf.Max(0f, timelineDuration);
            return cloned;
        }

        private static BehaviorTransitionDefinition CloneTransitionDefinition(
            BehaviorTransitionDefinition source,
            float timelineStartTime,
            float timelineDuration,
            string trackName = null)
        {
            BehaviorTransitionDefinition cloned = new BehaviorTransitionDefinition();
            if (source != null)
            {
                cloned.authoringTrackName = !string.IsNullOrWhiteSpace(trackName)
                    ? trackName
                    : source.authoringTrackName;
                cloned.targetBehaviorKey = source.targetBehaviorKey;
                cloned.crossFadeDuration = Mathf.Clamp01(source.crossFadeDuration);
            }

            cloned.startTime = Mathf.Max(0f, timelineStartTime);
            cloned.endTime = Mathf.Max(cloned.startTime, timelineStartTime + Mathf.Max(0f, timelineDuration));
            return cloned;
        }

        private static string SanitizeAssetName(string rawName)
        {
            string fallback = "TimelineBehaviorClip";
            if (string.IsNullOrWhiteSpace(rawName))
                return fallback;

            string trimmed = rawName.Trim();
            foreach (char invalidChar in System.IO.Path.GetInvalidFileNameChars())
                trimmed = trimmed.Replace(invalidChar.ToString(), string.Empty);

            return string.IsNullOrWhiteSpace(trimmed) ? fallback : trimmed;
        }

        private static string EnsureFolder(string folderPath)
        {
            string normalized = string.IsNullOrWhiteSpace(folderPath)
                ? "Assets"
                : folderPath.Replace("\\", "/").TrimEnd('/');
            string[] parts = normalized.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!UnityEditor.AssetDatabase.IsValidFolder(next))
                    UnityEditor.AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }

            return current;
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

            for (int i = 0; i < snapshots.Length; i++)
            {
                BehaviorAuthoringTrackSnapshot snapshot = snapshots[i];
                if (snapshot == null)
                    continue;

                TrackAsset importedTrack = ImportAuthoringTrackSnapshot(timelineAsset, snapshot, trackCache);
                if (importedTrack != null && !importedRootTracks.Contains(importedTrack))
                    importedRootTracks.Add(importedTrack);
            }

            RemoveEmptyManagedAuthoringTracks(timelineAsset);
            ReorderRootTracksByImportOrder(timelineAsset, importedRootTracks);
        }

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

                TimelineClip timelineClip = activationTrack.CreateDefaultClip();
                timelineClip.displayName = ResolveImportedClipDisplayName(
                    clipSnapshot,
                    behaviorEvent?.prefabRef != null ? behaviorEvent.prefabRef.name : $"Active VFX {i}");
                timelineClip.start = clipSnapshot.startTime;
                timelineClip.duration = Math.Max(0.01d, clipSnapshot.duration);

                if (!bindingResolved)
                {
                    boundObject = ResolveAuthoringBoundObjectForImport(clipSnapshot, behaviorEvent);
                    bindingResolved = true;
                }
            }

            if (previewDirector != null && activationTrack != null && boundObject != null)
                previewDirector.SetGenericBinding(activationTrack, boundObject);

            if (activationTrack != null)
                UnityEditor.EditorUtility.SetDirty(activationTrack);
            return activationTrack;
        }

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

            if (timelineClip.asset is AnimationPlayableAsset animationPlayableAsset)
                animationPlayableAsset.clip = segment.clip;
        }

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

                    if (!previewAudioSourceResolved && previewDirector != null)
                    {
                        previewAudioSource = ResolveOrCreatePreviewAudioSource(behaviorEvent.referenceBone);
                        previewAudioSourceResolved = true;
                    }
                });

            if (previewDirector != null && audioTrack != null && previewAudioSource != null)
                previewDirector.SetGenericBinding(audioTrack, previewAudioSource);

            return audioTrack;
        }

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

            if (timelineClip.asset is AudioPlayableAsset audioPlayableAsset)
            {
                audioPlayableAsset.clip = behaviorEvent.audioRef;
                audioPlayableAsset.loop = behaviorEvent.audioLoop;
                TrySetAudioPlayableAssetVolume(audioPlayableAsset, Mathf.Clamp01(behaviorEvent.audioVolume));
            }
        }

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

        private void BindPreviewAudioTrack(AudioTrack audioTrack, string referenceBone)
        {
            if (previewDirector == null || audioTrack == null)
                return;

            AudioSource previewAudioSource = ResolveOrCreatePreviewAudioSource(referenceBone);
            if (previewAudioSource != null)
                previewDirector.SetGenericBinding(audioTrack, previewAudioSource);
        }

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

            if (timelineClip.asset is BehaviorTimelineHitboxClipAsset clipAsset)
                clipAsset.hitboxData = CloneHitboxDef(hitbox, hitbox.startTime, hitbox.duration);
        }

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

            if (timelineClip.asset is BehaviorTimelineTransitionClipAsset clipAsset)
                clipAsset.transitionData = CloneTransitionDefinition(transition, startTime, duration);
        }

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

            if (!clearedTrack)
            {
                ClearTrackClips(track);
                clearedTrack = true;
            }

            return track;
        }

        private sealed class AnimationSegmentEntry
        {
            public float startTime;
            public AnimationSegment segment;
        }
    }
}
