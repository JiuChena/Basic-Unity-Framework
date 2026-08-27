using UnityEngine;

namespace Framework.Gameplay.Abilities.Movement
{
    /// <summary>
    /// 描述一次经过层、Trigger 和自身过滤后的地面命中及其可站立性。
    /// </summary>
    public readonly struct GroundContact
    {
        /// <summary>
        /// 创建地面命中结果。
        /// </summary>
        /// <param name="hasContact">是否存在有效地面命中。</param>
        /// <param name="hit">距离最近的有效地面命中。</param>
        public GroundContact(bool hasContact, RaycastHit hit)
            : this(
                hasContact,
                hasContact,
                hit,
                hit.distance,
                hasContact ? Vector3.Angle(hit.normal, Vector3.up) : 0f)
        {
        }

        /// <summary>
        /// 创建地面命中结果，并保存从有效碰撞体底部换算后的接地距离。
        /// </summary>
        /// <param name="hasContact">是否存在有效地面命中。</param>
        /// <param name="isWalkable">命中地面是否在允许站立的坡度范围内。</param>
        /// <param name="hit">距离最近的有效地面命中。</param>
        /// <param name="distance">有效碰撞体底部至地面的距离，单位：米。</param>
        /// <param name="slopeAngle">命中法线相对世界向上的坡度角，单位：度。</param>
        public GroundContact(bool hasContact, bool isWalkable, RaycastHit hit, float distance, float slopeAngle)
        {
            HasContact = hasContact;
            IsWalkable = hasContact && isWalkable;
            Hit = hit;
            Distance = distance;
            SlopeAngle = hasContact ? slopeAngle : 0f;
        }

        /// <summary>是否命中有效地面。</summary>
        public bool HasContact { get; }

        /// <summary>命中地面是否允许作为正常站立地面。</summary>
        public bool IsWalkable { get; }

        /// <summary>是否可作为当前物理步的正常接地结果。</summary>
        public bool IsGrounded => HasContact && IsWalkable;

        /// <summary>距离最近的有效地面命中。</summary>
        public RaycastHit Hit { get; }

        /// <summary>有效碰撞体底部至地面的距离。</summary>
        public float Distance { get; }

        /// <summary>命中斜面相对世界向上的坡度角，单位：度。</summary>
        public float SlopeAngle { get; }
    }
}

