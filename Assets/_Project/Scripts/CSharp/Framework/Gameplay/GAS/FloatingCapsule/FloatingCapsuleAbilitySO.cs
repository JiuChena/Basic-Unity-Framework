using UnityEngine;
using Framework.Gameplay.Abilities.Movement;

namespace Framework.Gameplay.Abilities.Configuration
{
    /// <summary>保存浮动胶囊能力的可复用静态配置。</summary>
    [CreateAssetMenu(fileName = "FloatingCapsuleAbility", menuName = "Framework/Gameplay/Abilities/Floating Capsule")]
    public sealed class FloatingCapsuleAbilitySO : AbilityDefinitionSO
    {
        // 是否在 Scene 窗口绘制浮动胶囊预览。
        [Tooltip("是否在 Scene 窗口绘制基础胶囊和浮动后的胶囊预览")]
        [SerializeField] private bool _drawGizmo = true;
        // 浮动胶囊形状配置。
        [Header("浮动胶囊")]
        [Tooltip("顶部对齐的胶囊缩短、底部留空和脚底 BoxCollider 参数")]
        [SerializeField] private FloatingCapsuleModule _floatingCapsule = new FloatingCapsuleModule();
        // 浮动胶囊接地和悬浮配置。
        [Tooltip("浮动胶囊接地探测和悬浮修正使用的地面配置")]
        [SerializeField] private GroundSettings _ground = new GroundSettings();

        /// <summary>创建浮动胶囊形状和悬浮配置的运行时副本。</summary>
        /// <param name="floatingCapsule">返回独立的浮动胶囊形状配置。</param>
        /// <param name="ground">返回独立的接地和悬浮配置。</param>
        public void CreateRuntimeCopies(
            out FloatingCapsuleModule floatingCapsule,
            out GroundSettings ground)
        {
            floatingCapsule = _floatingCapsule != null
                ? _floatingCapsule.CreateRuntimeCopy()
                : new FloatingCapsuleModule();
            ground = _ground != null ? _ground.CreateRuntimeCopy() : new GroundSettings();
        }

        /// <summary>创建浮动胶囊能力运行时。</summary>
        /// <returns>使用当前浮动胶囊配置的能力运行时。</returns>
        public override AbilityRuntime CreateRuntime()
        {
            return new FloatingCapsuleAbility(this);
        }

        /// <summary>绘制当前浮动胶囊配置对应的基础形状和预览形状。</summary>
        /// <param name="owner">挂载 AbilityComponent 的单位对象；为 null 或缺少 CapsuleCollider 时不绘制。</param>
        public override void GizmoDraw(GameObject owner)
        {
            if (!_drawGizmo) return;
            if (Application.isPlaying || owner == null) return;

            CapsuleCollider capsule = owner.GetComponent<CapsuleCollider>();
            if (capsule == null || _floatingCapsule == null) return;

            // 编辑器预览只从配置和当前碰撞体计算数据，不写入组件状态。
            FloatingCapsuleShape baseShape = new FloatingCapsuleShape(
                capsule.center,
                capsule.height,
                capsule.radius,
                capsule.direction);
            FloatingCapsuleShape previewShape = _floatingCapsule.GetPreviewShape(capsule);

            // 浮动关闭时只绘制当前基础胶囊，避免重复绘制相同形状。
            if (_floatingCapsule.Enabled)
                DrawCapsuleGizmo(capsule, baseShape, new Color(1f, 0.7f, 0.2f, 0.35f));
            DrawCapsuleGizmo(capsule, previewShape, new Color(0.2f, 0.9f, 1f, 0.9f));
        }

        /// <summary>绘制局部胶囊形状的线框预览。</summary>
        /// <param name="capsule">提供单位变换矩阵的主胶囊。</param>
        /// <param name="shape">需要绘制的局部胶囊形状。</param>
        /// <param name="color">线框颜色。</param>
        private void DrawCapsuleGizmo(CapsuleCollider capsule, in FloatingCapsuleShape shape, Color color)
        {
            // 保存 Scene 绘制状态，避免影响其他 Gizmo。
            Matrix4x4 previousMatrix = Gizmos.matrix;
            Color previousColor = Gizmos.color;
            Gizmos.matrix = capsule.transform.localToWorldMatrix;
            Gizmos.color = color;

            Vector3 axis = FloatingCapsuleModule.GetCapsuleLocalAxis(shape.Direction);
            float capHalfDistance = Mathf.Max(0f, shape.Height * 0.5f - shape.Radius);
            Vector3 top = shape.Center + axis * capHalfDistance;
            Vector3 bottom = shape.Center - axis * capHalfDistance;
            Vector3 firstSide = Vector3.Cross(axis, Vector3.up);
            if (firstSide.sqrMagnitude <= 0.0001f) firstSide = Vector3.Cross(axis, Vector3.right);
            firstSide.Normalize();
            Vector3 secondSide = Vector3.Cross(axis, firstSide).normalized;

            // 两端球面和四条侧线构成胶囊线框。
            Gizmos.DrawWireSphere(top, shape.Radius);
            Gizmos.DrawWireSphere(bottom, shape.Radius);
            Gizmos.DrawLine(top + firstSide * shape.Radius, bottom + firstSide * shape.Radius);
            Gizmos.DrawLine(top - firstSide * shape.Radius, bottom - firstSide * shape.Radius);
            Gizmos.DrawLine(top + secondSide * shape.Radius, bottom + secondSide * shape.Radius);
            Gizmos.DrawLine(top - secondSide * shape.Radius, bottom - secondSide * shape.Radius);

            // 恢复 Scene 绘制状态。
            Gizmos.matrix = previousMatrix;
            Gizmos.color = previousColor;
        }
    }
}
