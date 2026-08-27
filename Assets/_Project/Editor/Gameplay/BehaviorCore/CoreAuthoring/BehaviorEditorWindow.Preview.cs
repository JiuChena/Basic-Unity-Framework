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
        /// 打开 Timeline 预览：绑定 Director、Animator 与音频源并求值一次。
        /// </summary>
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

            // 绑定预览 Animator 与音频源。
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

            // 重置时间并重建求值。
            previewDirector.time = 0d;
            previewDirector.RebuildGraph();
            previewDirector.Evaluate();
            RefreshTimelineEditor(sourceTimeline, true, previewDirector);
            UnityEditor.Selection.activeObject = previewDirector.gameObject;
        }

        /// <summary>
        /// 清理本次作者期会话：停止预览、销毁临时创建的对象并恢复上下文。
        /// </summary>
        private void CleanupAuthoringSession()
        {
            // 停止并解绑预览 Director。
            if (previewDirector != null)
            {
                previewDirector.playOnAwake = false;
                previewDirector.Stop();
                previewDirector.time = 0d;
                previewDirector.playableAsset = null;
                previewDirector.RebuildGraph();

                if (removePreviewDirectorOnFinish) UnityEditor.Undo.DestroyObjectImmediate(previewDirector);
            }

            // 销毁本次创建的预览音频源。
            for (int i = createdPreviewAudioSources.Count - 1; i >= 0; i--)
            {
                AudioSource createdPreviewAudioSource = createdPreviewAudioSources[i];
                if (createdPreviewAudioSource == null) continue;

                UnityEditor.Undo.DestroyObjectImmediate(createdPreviewAudioSource);
            }
            createdPreviewAudioSources.Clear();

            // 销毁自动创建的 Animator。
            if (removePreviewAnimatorOnFinish &&
                previewAnimator != null &&
                previewAnimator.gameObject != null)
            {
                UnityEditor.Undo.DestroyObjectImmediate(previewAnimator);
            }

            // 清空自动指定的 Reference Root 与预览缓存。
            if (autoAssignedReferenceRoot) previewReferenceRoot = null;

            previewDirector = null;
            previewAnimator = null;
            removePreviewDirectorOnFinish = false;
            removePreviewAnimatorOnFinish = false;
            autoAssignedReferenceRoot = false;
            BehaviorEditorContext.ReferenceRootObject = previewReferenceRoot;

            if (sourceTimeline != null) RefreshTimelineEditor(sourceTimeline, true, null);
        }

        /// <summary>
        /// 刷新 Timeline 编辑器窗口，必要时排队延迟刷新。
        /// </summary>
        /// <param name="timelineAsset">需要刷新的 Timeline 资产。</param>
        /// <param name="contentsChanged">内容是否发生了增删。</param>
        /// <param name="preferredDirector">优先使用的 Director。</param>
        private static void RefreshTimelineEditor(TimelineAsset timelineAsset, bool contentsChanged, PlayableDirector preferredDirector)
        {
            UnityEditor.Timeline.TimelineEditorWindow timelineWindow = UnityEditor.Timeline.TimelineEditor.GetOrCreateWindow();
            RestoreTimelineWindowContext(timelineWindow, timelineAsset, preferredDirector);

            // 组合刷新原因。
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

            // 缓存延迟刷新参数，避免同一帧重复排队。
            pendingDelayedTimelineAsset = timelineAsset;
            pendingDelayedTimelineDirector = preferredDirector;
            pendingDelayedTimelineReason = reason;
            if (pendingDelayedTimelineRefresh)
                return;

            pendingDelayedTimelineRefresh = true;
            UnityEditor.EditorApplication.delayCall += ExecuteDelayedTimelineRefresh;
        }

        /// <summary>
        /// 延迟执行的 Timeline 刷新回调，消费缓存的刷新参数。
        /// </summary>
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

            // 恢复窗口上下文并重新刷新。
            UnityEditor.Timeline.TimelineEditorWindow delayedWindow = UnityEditor.Timeline.TimelineEditor.GetOrCreateWindow();
            RestoreTimelineWindowContext(
                delayedWindow,
                delayedTimelineAsset,
                delayedTimelineDirector);
            UnityEditor.Timeline.TimelineEditor.Refresh(delayedTimelineReason);
            UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }

        /// <summary>
        /// 将 Timeline 窗口恢复到目标资产与 Director 的编辑上下文。
        /// </summary>
        /// <param name="timelineWindow">目标 Timeline 窗口。</param>
        /// <param name="timelineAsset">需要显示的 Timeline 资产。</param>
        /// <param name="preferredDirector">优先使用的 Director。</param>
        private static void RestoreTimelineWindowContext(UnityEditor.Timeline.TimelineEditorWindow timelineWindow, TimelineAsset timelineAsset, PlayableDirector preferredDirector)
        {
            if (timelineWindow == null || timelineAsset == null)
                return;

            // 优先使用解析出的 Director。
            PlayableDirector resolvedDirector = ResolvePreviewDirectorForRefresh(timelineAsset, preferredDirector);
            if (resolvedDirector != null)
            {
                timelineWindow.SetTimeline(resolvedDirector);
                return;
            }

            // 回退到当前检查中的 Director 或资产。
            PlayableDirector inspectedDirector = UnityEditor.Timeline.TimelineEditor.inspectedDirector;
            if (inspectedDirector != null && inspectedDirector.playableAsset == timelineAsset)
            {
                timelineWindow.SetTimeline(inspectedDirector);
                return;
            }

            if (UnityEditor.Timeline.TimelineEditor.inspectedAsset == timelineAsset)
                return;

            // 编辑器尚无上下文时直接设置资产。
            if (UnityEditor.Timeline.TimelineEditor.inspectedAsset == null &&
                UnityEditor.Timeline.TimelineEditor.inspectedDirector == null)
            {
                timelineWindow.SetTimeline(timelineAsset);
            }
        }

        /// <summary>
        /// 解析用于打开预览的 PlayableDirector：优先缓存，其次选中对象，最后场景搜索。
        /// </summary>
        /// <param name="timelineAsset">目标 Timeline 资产。</param>
        /// <param name="preferredDirector">优先使用的缓存 Director。</param>
        /// <returns>解析出的 Director；未找到时返回 null。</returns>
        private static PlayableDirector ResolvePreviewDirectorForOpen(TimelineAsset timelineAsset, PlayableDirector preferredDirector)
        {
            // 缓存 Director 可用时直接复用。
            if (preferredDirector != null &&
                (preferredDirector.playableAsset == null || preferredDirector.playableAsset == timelineAsset))
            {
                return preferredDirector;
            }

            // 选中对象上挂有匹配的 Director。
            if (UnityEditor.Selection.activeGameObject != null &&
                UnityEditor.Selection.activeGameObject.TryGetComponent(out PlayableDirector selectedDirector) &&
                selectedDirector.playableAsset == timelineAsset)
            {
                return selectedDirector;
            }

            // 在场景中搜索绑定该资产的 Director。
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

        /// <summary>
        /// 解析用于刷新预览的 PlayableDirector：优先缓存与选中，再委托打开路径。
        /// </summary>
        /// <param name="timelineAsset">目标 Timeline 资产。</param>
        /// <param name="preferredDirector">优先使用的缓存 Director。</param>
        /// <returns>解析出的 Director；未找到时返回 null。</returns>
        private static PlayableDirector ResolvePreviewDirectorForRefresh(TimelineAsset timelineAsset, PlayableDirector preferredDirector)
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

        /// <summary>
        /// 确保目标对象上存在可用的 PlayableDirector，缺失时自动补齐。
        /// </summary>
        /// <param name="target">作者期目标对象。</param>
        /// <param name="createdByTool">是否由工具自动创建了 Director。</param>
        /// <returns>可用的 PlayableDirector。</returns>
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

        /// <summary>
        /// 确保目标对象上存在可用的 Animator，缺失时自动补齐。
        /// </summary>
        /// <param name="target">作者期目标对象。</param>
        /// <param name="createdByTool">是否由工具自动创建了 Animator。</param>
        /// <returns>可用的 Animator。</returns>
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

        /// <summary>
        /// 解析预览使用的 Animator：优先缓存，其次 Director 同对象或子物体。
        /// </summary>
        /// <param name="director">当前预览 Director。</param>
        /// <param name="preferredAnimator">优先使用的缓存 Animator。</param>
        /// <returns>解析出的 Animator；未找到时返回 null。</returns>
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

        /// <summary>
        /// 解析预览使用的音频源：优先 Director 同对象或子物体。
        /// </summary>
        /// <param name="director">当前预览 Director。</param>
        /// <returns>解析出的音频源；未找到时返回 null。</returns>
        private static AudioSource ResolvePreviewAudioSource(PlayableDirector director)
        {
            if (director == null)
                return null;

            if (director.TryGetComponent(out AudioSource sameObjectAudioSource))
                return sameObjectAudioSource;

            return director.GetComponentInChildren<AudioSource>(true);
        }

        /// <summary>
        /// 将预览 Animator 绑定到 Timeline 的全部动画轨道。
        /// </summary>
        /// <param name="director">当前预览 Director。</param>
        /// <param name="timelineAsset">目标 Timeline 资产。</param>
        /// <param name="animator">需要绑定的 Animator。</param>
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

        /// <summary>
        /// 将预览音频源绑定到 Timeline 的全部音频轨道。
        /// </summary>
        /// <param name="director">当前预览 Director。</param>
        /// <param name="timelineAsset">目标 Timeline 资产。</param>
        /// <param name="audioSource">需要绑定的音频源。</param>
        private static void BindPreviewAudioSource(PlayableDirector director, TimelineAsset timelineAsset, AudioSource audioSource)
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
    }
}
