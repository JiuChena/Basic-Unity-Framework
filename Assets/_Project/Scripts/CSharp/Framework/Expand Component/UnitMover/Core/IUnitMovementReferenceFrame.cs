using UnityEngine;

namespace Framework.ExpandComponent.UnitMover
{
    /// <summary>
    /// 允许 UnitMover 向输入黑板注入世界空间移动方向的参考 Transform。
    /// </summary>
    public interface IUnitMovementReferenceFrame
    {
        /// <summary>获取或设置用于将平面输入转换为世界方向的参考 Transform；为 null 时由实现自行回退。</summary>
        Transform MovementReference { get; set; }
    }
}
