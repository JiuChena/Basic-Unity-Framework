using UnityEngine;

/// <summary>
/// 直线飞行投射物驱动。
/// 仅负责按当前飞行方向匀速前进，生命周期与命中逻辑仍由 ProjectileBase 处理。
/// </summary>
public sealed class StraightProjectileDriver : ProjectileBase
{
    [SerializeField, Tooltip("启用后让投射物在飞行时朝向当前移动方向。")]
    private bool alignForwardToMovement = true;

    protected override void MoveProjectile(float deltaTime, float speed)
    {
        Vector3 forward = CurrentDirection;
        if (forward.sqrMagnitude <= 0.0001f)
            forward = transform.forward.sqrMagnitude > 0.0001f ? transform.forward : Vector3.forward;

        Vector3 displacement = forward.normalized * speed * deltaTime;
        if (ProjectileRigidbody != null)
        {
            Vector3 nextPosition = ProjectileRigidbody.position + displacement;
            ProjectileRigidbody.MovePosition(nextPosition);
        }
        else
        {
            transform.position += displacement;
        }

        if (alignForwardToMovement && displacement.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
    }
}
