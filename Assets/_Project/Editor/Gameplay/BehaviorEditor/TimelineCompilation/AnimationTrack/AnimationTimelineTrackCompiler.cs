using System;
using UnityEngine;
using UnityEngine.Timeline;

namespace BehaviorEditor
{
    /// <summary>
    /// 编译原生 AnimationTrack 为运行时动画轨道数据。
    /// </summary>
    [BehaviorTrackCompiler(typeof(AnimationTrack))]
    internal sealed class AnimationTimelineTrackCompiler : IBehaviorTimelineTrackCompiler
    {
        /// <summary>
        /// 获取原生动画轨道类型。
        /// </summary>
        /// <returns>AnimationTrack 类型。</returns>
        public System.Type TrackType => typeof(AnimationTrack);

        /// <summary>
        /// 导出原生动画片段。
        /// </summary>
        /// <param name="track">待导出的动画轨道。</param>
        /// <param name="context">当前导出上下文。</param>
        public void Export(TrackAsset track, BehaviorExportContext context)
        {
            if (track is not AnimationTrack animationTrack || context == null)
                return;

            // 按原生 AnimationTrack 的片段顺序构建独立动画段。
            int layer = ResolveAnimationLayerFromTrackName(animationTrack.name);
            foreach (TimelineClip clip in animationTrack.GetClips())
            {
                if (clip?.asset is not AnimationPlayableAsset playableAsset || playableAsset.clip == null)
                    continue;

                // 记录当前运行时无法精确复现的原生 Timeline 配置。
                if (Math.Abs(clip.clipIn) > 0.0001d)
                    context.AddWarning($"AnimationTrack '{animationTrack.name}' 的片段 '{clip.displayName}' 使用了 Clip In={clip.clipIn:F2}s，当前运行时不会精确复现该裁切。");

                if (Math.Abs(clip.timeScale - 1d) > 0.0001d)
                    context.AddWarning($"AnimationTrack '{animationTrack.name}' 的片段 '{clip.displayName}' 使用了 Time Scale={clip.timeScale:F2}，当前运行时不会精确复现该变速。");

                if (playableAsset.position != Vector3.zero || playableAsset.eulerAngles != Vector3.zero)
                    context.AddWarning($"AnimationTrack '{animationTrack.name}' 的片段 '{clip.displayName}' 配置了位置或旋转偏移，当前运行时不会导出这部分偏移。");

                context.ConsiderEndTime(clip.end);
                context.AddAnimationSegment(new AnimationSegment
                {
                    authoringTrackName = animationTrack.name,
                    clip = playableAsset.clip,
                    crossFadeDuration = Mathf.Clamp01((float)(Math.Max(clip.blendInDuration, clip.easeInDuration) /
                        Math.Max(0.0001d, clip.duration))),
                    layer = layer,
                    startTime = Mathf.Max(0f, (float)clip.start)
                });
            }
        }

        /// <summary>
        /// 从轨道名解析动画层索引，支持 "L0" 与 "Layer 0" 两种写法。
        /// </summary>
        /// <param name="trackName">需要解析的 Timeline 轨道名。</param>
        /// <returns>解析出的层索引；无法解析时返回 0。</returns>
        private static int ResolveAnimationLayerFromTrackName(string trackName)
        {
            if (string.IsNullOrWhiteSpace(trackName)) return 0;

            // 逐段读取约定的层编号标记。
            string[] tokens = trackName.Split(new[] { ' ', '_', '-', '/' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < tokens.Length; i++)
            {
                string token = tokens[i];
                if (token.Length >= 2 && (token[0] == 'L' || token[0] == 'l') &&
                    int.TryParse(token.Substring(1), out int tokenLayer))
                {
                    return Mathf.Max(0, tokenLayer);
                }

                if ((string.Equals(token, "Layer", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(token, "L", StringComparison.OrdinalIgnoreCase)) &&
                    i + 1 < tokens.Length && int.TryParse(tokens[i + 1], out int nextLayer))
                {
                    return Mathf.Max(0, nextLayer);
                }
            }

            return 0;
        }
    }
}
