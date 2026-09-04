// 此文件由 BehaviorEditor 新轨道脚本工具生成，可按轨道需求修改。

using System;
using System.Collections.Generic;
using UnityEngine;

namespace BehaviorEditor
{
    /// <summary>
    /// 保存导出后的 Test 运行时数据。
    /// </summary>
    [Serializable]
    public sealed class TestTrackData : BehaviorTrackData
    {
        // 当前轨道导出的片段数据集合。
        [Tooltip("按时间窗保存的运行时片段数据。")]
        public List<TestTrackSegment> segments = new List<TestTrackSegment>();

        /// <summary>
        /// 创建 Test 轨道默认数据。
        /// </summary>
        public TestTrackData()
        {
            executionOrder = 0;
        }

        /// <summary>
        /// 获取轨道诊断名称。
        /// </summary>
        /// <returns>当前轨道的显示名称。</returns>
        public override string DisplayName => "Test";

        /// <summary>
        /// 创建当前轨道的播放执行器。
        /// </summary>
        /// <param name="context">当前行为播放上下文。</param>
        /// <returns>当前轨道的运行时执行器。</returns>
        public override IBehaviorTrackExecutor CreateExecutor(BehaviorExecutionContext context)
        {
            return new TestTrackExecutor(this, context);
        }
    }

    /// <summary>
    /// 保存一个 Test 运行时片段。
    /// </summary>
    [Serializable]
    public sealed class TestTrackSegment
    {
        // 片段开始时间，单位：秒。
        [Tooltip("片段开始时间，单位：秒。")]
        public float startTime;
        // 片段持续时间，单位：秒。
        [Tooltip("片段持续时间，单位：秒。")]
        public float duration;
        // 由 Timeline 片段导出的自定义参数。
        [Tooltip("由 Timeline 片段导出的自定义参数。")]
        public float value;
    }
}
