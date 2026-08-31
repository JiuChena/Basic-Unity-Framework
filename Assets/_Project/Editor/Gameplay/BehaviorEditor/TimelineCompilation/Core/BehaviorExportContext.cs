using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace BehaviorEditor
{
    /// <summary>
    /// 单次 Timeline 编译期间共享的输出数据与编辑器环境。
    /// </summary>
    internal sealed class BehaviorExportContext
    {
        // 轨道数据类型 → 本次编译的唯一输出实例。
        private readonly Dictionary<Type, BehaviorTrackData> trackDataByType = new Dictionary<Type, BehaviorTrackData>();

        // 本次编译收集的可显示警告。
        private readonly List<string> warnings = new List<string>();

        // 所有动画作者轨道汇总出的运行时动画段。
        private readonly List<AnimationSegment> animationSegments = new List<AnimationSegment>();

        // 所有事件语义作者轨道汇总出的运行时事件。
        private readonly List<BehaviorEvent> behaviorEvents = new List<BehaviorEvent>();

        // 所有 Hitbox 作者轨道汇总出的运行时命中定义。
        private readonly List<HitboxDef> hitboxes = new List<HitboxDef>();

        /// <summary>正在编译的 Timeline 资产。</summary>
        public TimelineAsset Timeline { get; }
        /// <summary>Timeline 预览或绑定查询使用的 Director。</summary>
        public PlayableDirector Director { get; }
        /// <summary>骨骼与场景对象路径计算的参考根节点。</summary>
        public Transform ReferenceRoot { get; }
        /// <summary>未提供 Meta 轨道时使用的默认播放配置。</summary>
        public BehaviorMetaData FallbackMeta { get; }
        /// <summary>全部轨道片段中的最大结束时间。</summary>
        public double MaxEndTime { get; private set; }
        /// <summary>本次编译产生的诊断警告。</summary>
        public IReadOnlyList<string> Warnings => warnings;

        /// <summary>
        /// 创建单次 Timeline 编译上下文。
        /// </summary>
        /// <param name="timeline">待编译的 Timeline 资产。</param>
        /// <param name="director">预览与绑定查询使用的 Director，可为 null。</param>
        /// <param name="referenceRoot">骨骼路径基准，可为 null。</param>
        /// <param name="fallbackMeta">缺少 Meta 轨道时的回退播放配置。</param>
        public BehaviorExportContext(TimelineAsset timeline, PlayableDirector director, Transform referenceRoot,
            BehaviorMetaData fallbackMeta)
        {
            Timeline = timeline;
            Director = director;
            ReferenceRoot = referenceRoot;
            FallbackMeta = fallbackMeta ?? new BehaviorMetaData();
            trackDataByType.Add(typeof(BehaviorMetaData), new BehaviorMetaData
            {
                wrapMode = FallbackMeta.wrapMode,
                speedMultiplier = Mathf.Max(0.01f, FallbackMeta.speedMultiplier),
                priority = FallbackMeta.priority
            });
        }

        /// <summary>
        /// 获取或创建本次编译中指定类型的轨道数据。
        /// </summary>
        /// <typeparam name="TTrackData">需要获取的轨道数据类型。</typeparam>
        /// <returns>当前编译的唯一轨道数据实例。</returns>
        public TTrackData GetOrCreateTrackData<TTrackData>() where TTrackData : BehaviorTrackData, new()
        {
            if (trackDataByType.TryGetValue(typeof(TTrackData), out BehaviorTrackData existing))
                return (TTrackData)existing;

            TTrackData created = new TTrackData();
            trackDataByType.Add(typeof(TTrackData), created);
            return created;
        }

        /// <summary>
        /// 设置本次导出的行为结束时间上界。
        /// </summary>
        /// <param name="endTime">候选结束时间，负数会被忽略。</param>
        public void ConsiderEndTime(double endTime)
        {
            if (endTime > MaxEndTime)
                MaxEndTime = endTime;
        }

        /// <summary>
        /// 记录一次可显示的导出警告。
        /// </summary>
        /// <param name="warning">警告文本；为空时忽略。</param>
        public void AddWarning(string warning)
        {
            if (!string.IsNullOrWhiteSpace(warning))
                warnings.Add(warning);
        }

        /// <summary>
        /// 追加一个动画段到本次编译的动画输出。
        /// </summary>
        /// <param name="segment">需要追加的动画段；为 null 时忽略。</param>
        public void AddAnimationSegment(AnimationSegment segment)
        {
            if (segment != null)
                animationSegments.Add(segment);
        }

        /// <summary>
        /// 追加一个定时行为事件到本次编译的事件输出。
        /// </summary>
        /// <param name="behaviorEvent">需要追加的事件；为 null 时忽略。</param>
        public void AddEvent(BehaviorEvent behaviorEvent)
        {
            if (behaviorEvent != null)
                behaviorEvents.Add(behaviorEvent);
        }

        /// <summary>
        /// 追加一个命中判定定义到本次编译的 Hitbox 输出。
        /// </summary>
        /// <param name="hitbox">需要追加的 Hitbox；为 null 时忽略。</param>
        public void AddHitbox(HitboxDef hitbox)
        {
            if (hitbox != null)
                hitboxes.Add(hitbox);
        }

        /// <summary>
        /// 获取当前轨道导出的 Meta 数据，首次调用时使用回退配置创建。
        /// </summary>
        /// <returns>本次编译唯一的 Meta 轨道数据。</returns>
        public BehaviorMetaData GetMetaData()
        {
            BehaviorMetaData meta = GetOrCreateTrackData<BehaviorMetaData>();
            if (meta.speedMultiplier <= 0f)
            {
                meta.wrapMode = FallbackMeta.wrapMode;
                meta.speedMultiplier = Mathf.Max(0.01f, FallbackMeta.speedMultiplier);
                meta.priority = FallbackMeta.priority;
            }

            return meta;
        }

        /// <summary>
        /// 将本次收集的多态轨道数据提交到目标 BehaviorClip。
        /// </summary>
        /// <param name="target">需要写入的目标行为资产。</param>
        public void CommitTo(BehaviorClip target)
        {
            if (target == null)
                return;

            // 补齐未显式导出的 Meta，保证播放头始终有确定配置。
            BehaviorMetaData meta = GetMetaData();

            // 将各作者入口汇总为共享运行时语义数据。
            AnimationTrackData animation = GetOrCreateTrackData<AnimationTrackData>();
            EventTrackData events = GetOrCreateTrackData<EventTrackData>();
            HitboxTrackData hitboxData = GetOrCreateTrackData<HitboxTrackData>();
            animationSegments.Sort(CompareAnimationSegments);
            behaviorEvents.Sort(CompareBehaviorEvents);
            hitboxes.Sort(CompareHitboxes);
            animation.segments = animationSegments.ToArray();
            events.events = behaviorEvents.ToArray();
            hitboxData.hitboxes = hitboxes.ToArray();
            meta.duration = Mathf.Max(0.01f, (float)Math.Max(MaxEndTime, Timeline != null ? Timeline.duration : 0d));

            // 整体替换导出结果，避免作者期删除轨道后保留过期运行时数据。
            target.ReplaceTrackData(trackDataByType.Values);

        }

        /// <summary>
        /// 按起始时间、轨道名称、层级与动画名稳定排序动画段。
        /// </summary>
        /// <param name="left">左侧动画段。</param>
        /// <param name="right">右侧动画段。</param>
        /// <returns>排序结果。</returns>
        private static int CompareAnimationSegments(AnimationSegment left, AnimationSegment right)
        {
            if (ReferenceEquals(left, right)) return 0;
            if (left == null) return 1;
            if (right == null) return -1;

            int result = left.startTime.CompareTo(right.startTime);
            if (result != 0) return result;
            result = string.Compare(left.authoringTrackName, right.authoringTrackName, StringComparison.Ordinal);
            if (result != 0) return result;
            result = left.layer.CompareTo(right.layer);
            return result != 0 ? result : string.Compare(left.clip != null ? left.clip.name : string.Empty,
                right.clip != null ? right.clip.name : string.Empty, StringComparison.Ordinal);
        }

        /// <summary>
        /// 按触发时间、轨道名称与有效事件类型稳定排序行为事件。
        /// </summary>
        /// <param name="left">左侧行为事件。</param>
        /// <param name="right">右侧行为事件。</param>
        /// <returns>排序结果。</returns>
        private static int CompareBehaviorEvents(BehaviorEvent left, BehaviorEvent right)
        {
            if (ReferenceEquals(left, right)) return 0;
            if (left == null) return 1;
            if (right == null) return -1;

            int result = left.time.CompareTo(right.time);
            if (result != 0) return result;
            result = string.Compare(left.authoringTrackName, right.authoringTrackName, StringComparison.Ordinal);
            return result != 0 ? result : ((int)BehaviorEventResolver.ResolveEffectiveType(left))
                .CompareTo((int)BehaviorEventResolver.ResolveEffectiveType(right));
        }

        /// <summary>
        /// 按生效时间、轨道名称和调试名称稳定排序 Hitbox。
        /// </summary>
        /// <param name="left">左侧 Hitbox。</param>
        /// <param name="right">右侧 Hitbox。</param>
        /// <returns>排序结果。</returns>
        private static int CompareHitboxes(HitboxDef left, HitboxDef right)
        {
            if (ReferenceEquals(left, right)) return 0;
            if (left == null) return 1;
            if (right == null) return -1;

            int result = left.startTime.CompareTo(right.startTime);
            if (result != 0) return result;
            result = string.Compare(left.authoringTrackName, right.authoringTrackName, StringComparison.Ordinal);
            return result != 0 ? result : string.Compare(left.name, right.name, StringComparison.Ordinal);
        }
    }
}
