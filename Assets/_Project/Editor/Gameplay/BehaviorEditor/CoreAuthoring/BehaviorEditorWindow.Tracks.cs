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
        /// 确保 Timeline 中存在指定类型的轨道：优先名称精确匹配，其次空轨道回退，缺失时创建。
        /// </summary>
        /// <typeparam name="T">轨道类型。</typeparam>
        /// <param name="timelineAsset">目标 Timeline 资产。</param>
        /// <param name="trackName">轨道名称。</param>
        /// <param name="timelineTracks">已收集的轨道列表；为 null 时重新枚举。</param>
        /// <param name="changed">是否发生了创建或改名。</param>
        /// <returns>解析或创建出的轨道。</returns>
        internal static T EnsureTrack<T>(
            TimelineAsset timelineAsset,
            string trackName,
            IReadOnlyList<TrackAsset> timelineTracks,
            out bool changed)
            where T : TrackAsset, new()
        {
            changed = false;
            if (timelineAsset == null) return null;

            // 在已有轨道中查找名称精确匹配或空轨道回退。
            T exactNameMatch = null;
            int exactNameScore = int.MinValue;
            T fallbackMatch = null;
            IEnumerable<TrackAsset> tracks = timelineTracks ?? EnumerateTimelineTracks(timelineAsset);
            foreach (TrackAsset track in tracks)
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
        /// 删除同名且无内容的重复轨道，保留指定轨道。
        /// </summary>
        /// <typeparam name="T">轨道类型。</typeparam>
        /// <param name="timelineAsset">目标 Timeline 资产。</param>
        /// <param name="keepTrack">需要保留的轨道。</param>
        /// <param name="trackName">目标轨道名。</param>
        private static void RemoveEmptyDuplicateTracks<T>(TimelineAsset timelineAsset, T keepTrack, string trackName)
            where T : TrackAsset
        {
            if (timelineAsset == null || keepTrack == null || string.IsNullOrEmpty(trackName)) return;

            // 清理与保留轨道同名但没有内容的重复轨道。
            DeleteTracksByPredicate(
                timelineAsset,
                "Remove Duplicate Behavior Tracks",
                track => !ReferenceEquals(track, keepTrack) &&
                         track is T &&
                         string.Equals(track.name, trackName, StringComparison.Ordinal) &&
                         GetTrackContentScore(track) <= 0);
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
                   string.Equals(trackName, NativeAudioTrackName, StringComparison.Ordinal) ||
                   string.Equals(trackName, NativeVfxTrackName, StringComparison.Ordinal) ||
                   string.Equals(trackName, NativeActivationVfxTrackName, StringComparison.Ordinal) ||
                   trackName.StartsWith("Behavior Animation L", StringComparison.Ordinal);
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
