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
        /// 按导入优先级与导入顺序重排 Timeline 根轨道顺序。
        /// </summary>
        /// <param name="timelineAsset">需要重排的 Timeline 资产。</param>
        /// <param name="importedRootTracks">本次导入的根轨道列表。</param>
        private static void ReorderRootTracksByImportOrder(TimelineAsset timelineAsset, List<TrackAsset> importedRootTracks)
        {
            if (timelineAsset == null || importedRootTracks == null || importedRootTracks.Count == 0)
                return;

            PruneInvalidRootTrackReferences(timelineAsset);
            List<TrackAsset> currentRootTracks = new List<TrackAsset>();
            foreach (TrackAsset rootTrack in timelineAsset.GetRootTracks())
                currentRootTracks.Add(rootTrack);

            if (currentRootTracks.Count <= 1)
                return;

            // 建立导入轨道到顺序索引的映射，供排序参考。
            List<TrackAsset> desiredOrder = new List<TrackAsset>(currentRootTracks.Count);
            Dictionary<TrackAsset, int> importedTrackIndexMap = new Dictionary<TrackAsset, int>();
            for (int i = 0; i < importedRootTracks.Count; i++)
            {
                TrackAsset importedTrack = importedRootTracks[i];
                if (importedTrack != null && !importedTrackIndexMap.ContainsKey(importedTrack))
                    importedTrackIndexMap.Add(importedTrack, i);
            }

            // 排序：优先级优先，其次导入顺序，最后当前顺序与名称。
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

            // 通过序列化属性按目标顺序写回轨道引用。
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

        /// <summary>
        /// 清理 Timeline 根轨道列表中无效或已废弃的轨道占位引用。
        /// </summary>
        /// <param name="timelineAsset">需要清理的 Timeline 资产。</param>
        /// <returns>是否发生了清理。</returns>
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

                    // 删除无效引用，必要时二次删除空槽位。
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

        /// <summary>
        /// 判断轨道是否为无效的作者期占位轨道（裸 TrackAsset 且名称是管理轨道名）。
        /// </summary>
        /// <param name="track">需要判断的轨道。</param>
        /// <returns>是无效占位轨道时返回 true。</returns>
        private static bool IsInvalidManagedAuthoringTrackPlaceholder(TrackAsset track)
        {
            if (track == null)
                return true;

            return track.GetType() == typeof(TrackAsset) &&
                   IsManagedAuthoringTrackName(track.name);
        }

        /// <summary>
        /// 判断轨道名称是否属于本编辑器管理的轨道名。
        /// </summary>
        /// <param name="trackName">轨道名称。</param>
        /// <returns>属于管理轨道名时返回 true。</returns>
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

        /// <summary>
        /// 删除所有无片段且属于本编辑器管理的轨道。
        /// </summary>
        /// <param name="timelineAsset">需要清理的 Timeline 资产。</param>
        private static void RemoveEmptyManagedAuthoringTracks(TimelineAsset timelineAsset)
        {
            DeleteTracksByPredicate(
                timelineAsset,
                "Remove Empty Behavior Tracks",
                track => track != null && IsManagedAuthoringTrack(track) && !HasAnyClips(track));
        }

        /// <summary>
        /// 判断轨道是否属于本编辑器管理的轨道类型。
        /// </summary>
        /// <param name="track">需要判断的轨道。</param>
        /// <returns>是管理轨道时返回 true。</returns>
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

        /// <summary>
        /// 判断轨道是否含有片段。
        /// </summary>
        /// <param name="track">需要判断的轨道。</param>
        /// <returns>含有至少一个片段时返回 true。</returns>
        private static bool HasAnyClips(TrackAsset track)
        {
            if (track == null)
                return false;

            foreach (TimelineClip _ in track.GetClips())
                return true;

            return false;
        }

        /// <summary>
        /// 按轨道优先级与排序索引比较两个轨道快照，用于导出排序。
        /// </summary>
        /// <param name="left">左侧轨道快照。</param>
        /// <param name="right">右侧轨道快照。</param>
        /// <returns>比较结果：负值左优先，正值右优先。</returns>
        private static int CompareTrackSnapshotsBySortIndex(BehaviorAuthoringTrackSnapshot left, BehaviorAuthoringTrackSnapshot right)
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

        /// <summary>
        /// 获取轨道类型对应的导入优先级，数值越小越靠前。
        /// </summary>
        /// <param name="trackKind">轨道类型。</param>
        /// <returns>导入优先级数值。</returns>
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

        /// <summary>
        /// 根据轨道实际类型获取导入优先级，未知类型返回最大值。
        /// </summary>
        /// <param name="track">需要判断的轨道。</param>
        /// <returns>导入优先级数值。</returns>
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

        /// <summary>
        /// 计算轨道内容评分：片段、标记与子轨道各加权。
        /// </summary>
        /// <param name="track">目标轨道。</param>
        /// <returns>内容评分。</returns>
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

        /// <summary>
        /// 收集 Timeline 中的全部轨道（含子轨道）。
        /// </summary>
        /// <param name="timelineAsset">目标 Timeline 资产。</param>
        /// <returns>轨道列表。</returns>
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

        /// <summary>
        /// 按谓词删除 Timeline 中的轨道。
        /// </summary>
        /// <param name="timelineAsset">目标 Timeline 资产。</param>
        /// <param name="undoName">撤销操作名。</param>
        /// <param name="shouldDelete">判定是否删除的谓词。</param>
        private static void DeleteTracksByPredicate(TimelineAsset timelineAsset, string undoName, Predicate<TrackAsset> shouldDelete)
        {
            if (timelineAsset == null || shouldDelete == null)
                return;

            // 收集满足删除条件的轨道。
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

            // 注册撤销后批量删除。
            UnityEditor.Undo.RegisterCompleteObjectUndo(timelineAsset, undoName);
            for (int i = 0; i < tracksToDelete.Count; i++)
                timelineAsset.DeleteTrack(tracksToDelete[i]);

            UnityEditor.EditorUtility.SetDirty(timelineAsset);
        }

        /// <summary>
        /// 按谓词删除轨道上的片段。
        /// </summary>
        /// <param name="track">目标轨道。</param>
        /// <param name="undoName">撤销操作名。</param>
        /// <param name="shouldDelete">判定是否删除的谓词。</param>
        private static void DeleteClipsByPredicate(
            TrackAsset track,
            string undoName,
            Predicate<TimelineClip> shouldDelete)
        {
            if (track == null || shouldDelete == null)
                return;

            // 收集满足删除条件的片段。
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

            // 注册撤销后批量删除。
            UnityEditor.Undo.RegisterCompleteObjectUndo(track, undoName);
            for (int i = 0; i < clipsToDelete.Count; i++)
                track.DeleteClip(clipsToDelete[i]);

            UnityEditor.EditorUtility.SetDirty(track);
        }

        /// <summary>
        /// 将轨道加入脏轨道集合，用于批量标记。
        /// </summary>
        /// <param name="dirtyTracks">脏轨道集合（引用修改）。</param>
        /// <param name="track">需要标记的轨道。</param>
        private static void AddDirtyTrack(ref HashSet<TrackAsset> dirtyTracks, TrackAsset track)
        {
            if (track == null)
                return;

            dirtyTracks ??= new HashSet<TrackAsset>();
            dirtyTracks.Add(track);
        }

        /// <summary>
        /// 批量标记脏轨道集合中的全部轨道。
        /// </summary>
        /// <param name="dirtyTracks">脏轨道集合。</param>
        private static void SetTracksDirty(HashSet<TrackAsset> dirtyTracks)
        {
            if (dirtyTracks == null)
                return;

            foreach (TrackAsset track in dirtyTracks)
                UnityEditor.EditorUtility.SetDirty(track);
        }

        /// <summary>
        /// 枚举 Timeline 中的全部轨道（递归展开组轨道）。
        /// </summary>
        /// <param name="timelineAsset">目标 Timeline 资产。</param>
        /// <returns>轨道序列。</returns>
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

        /// <summary>
        /// 递归枚举轨道及其子轨道，组轨道本身不产出。
        /// </summary>
        /// <param name="track">起始轨道。</param>
        /// <returns>轨道序列。</returns>
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
    }
}
