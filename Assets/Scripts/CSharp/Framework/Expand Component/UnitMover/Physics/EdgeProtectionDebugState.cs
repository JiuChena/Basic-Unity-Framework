using UnityEngine;

namespace Framework.ExpandComponent.UnitMover
{
    /// <summary>
    /// 保存仅供编辑器可视化读取的最近一次边缘保护诊断数据。
    /// </summary>
    public sealed class EdgeProtectionDebugState
    {
        // 预测支撑采样的世界位置。
        private readonly Vector3[] _supportPoints = new Vector3[3];
        // 预测支撑采样是否命中可行走地面。
        private readonly bool[] _supportResults = new bool[3];
        // 局部危险采样的世界位置。
        private readonly Vector3[] _hazardPoints = new Vector3[8];
        // 局部危险采样是否缺少支撑。
        private readonly bool[] _hazardResults = new bool[8];
        // 前缘支撑采样射线的最大长度。
        private float _supportRayDistance;
        // 环形危险采样射线的最大长度。
        private float _hazardRayDistance;

        /// <summary>获取预测支撑采样位置。</summary>
        public Vector3[] SupportPoints => _supportPoints;

        /// <summary>获取预测支撑采样结果。</summary>
        public bool[] SupportResults => _supportResults;

        /// <summary>获取局部危险采样位置。</summary>
        public Vector3[] HazardPoints => _hazardPoints;

        /// <summary>获取局部危险采样结果。</summary>
        public bool[] HazardResults => _hazardResults;

        /// <summary>获取前缘支撑采样射线的最大长度。</summary>
        public float SupportRayDistance => _supportRayDistance;

        /// <summary>获取环形危险采样射线的最大长度。</summary>
        public float HazardRayDistance => _hazardRayDistance;

        /// <summary>获取最近一次推导出的悬崖外法线。</summary>
        public Vector3 EdgeOutNormal { get; internal set; }

        /// <summary>获取边缘保护后的候选水平速度。</summary>
        public Vector3 ConstrainedVelocity { get; internal set; }

        /// <summary>获取最近一次预测支撑状态。</summary>
        public SupportStatus SupportStatus { get; internal set; }

        /// <summary>
        /// 清空上一物理步的射线诊断数据，避免 Gizmos 显示已经不再执行的边缘检测。
        /// </summary>
        internal void ClearRayData()
        {
            _supportRayDistance = 0f;
            _hazardRayDistance = 0f;
            for (int index = 0; index < _supportPoints.Length; index++)
            {
                _supportPoints[index] = Vector3.zero;
                _supportResults[index] = false;
            }

            for (int index = 0; index < _hazardPoints.Length; index++)
            {
                _hazardPoints[index] = Vector3.zero;
                _hazardResults[index] = false;
            }
        }

        /// <summary>
        /// 记录前缘支撑采样射线的最大长度。
        /// </summary>
        /// <param name="distance">本次前缘支撑采样使用的最大射线长度。</param>
        internal void SetSupportRayDistance(float distance)
        {
            _supportRayDistance = Mathf.Max(0f, distance);
        }

        /// <summary>
        /// 记录环形危险采样射线的最大长度。
        /// </summary>
        /// <param name="distance">本次危险采样使用的最大射线长度。</param>
        internal void SetHazardRayDistance(float distance)
        {
            _hazardRayDistance = Mathf.Max(0f, distance);
        }
    }
}
