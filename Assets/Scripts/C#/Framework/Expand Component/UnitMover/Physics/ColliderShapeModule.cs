using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Framework.ExpandComponent.UnitMover
{
    /// <summary>
    /// 统一同步浮动胶囊形状和脚底 BoxCollider，并向所有物理模块提供实际参与检测的 Collider 边界数据。
    /// </summary>
    public sealed class ColliderShapeModule
    {
        // 参与移动和物理检测的主胶囊碰撞体。
        private readonly CapsuleCollider _movementCollider;
        // 持有所有组件的 GameObject，用于管理脚底 BoxCollider 的创建与销毁。
        private readonly GameObject _owner;
        // 浮动胶囊的可序列化开关与留空高度。
        private readonly FloatingCapsuleModule _settings;
        // 组件级基础胶囊快照。
        private readonly FloatingCapsuleAuthoringState _authoringState;
        // 浮动胶囊启用时自动管理的脚底扁平 BoxCollider。
        private BoxCollider _footCollider;

        /// <summary>
        /// 初始化与单个 CapsuleCollider 绑定的形状模块。
        /// </summary>
        /// <param name="movementCollider">参与移动检测的主 CapsuleCollider。</param>
        /// <param name="owner">持有所有组件的 GameObject。</param>
        /// <param name="settings">浮动胶囊配置。</param>
        public ColliderShapeModule(
            CapsuleCollider movementCollider,
            GameObject owner,
            FloatingCapsuleModule settings)
        {
            _movementCollider = movementCollider;
            _owner = owner;
            _settings = settings;
            _authoringState = settings != null ? settings.AuthoringState : null;
            _footCollider = _authoringState != null ? _authoringState.FootCollider : null;
        }

        /// <summary>获取实际参与移动和物理查询的主胶囊碰撞体。</summary>
        public CapsuleCollider MovementCollider => _movementCollider;

        /// <summary>
        /// 获取当前启用且由浮动胶囊自动维护的脚底 BoxCollider。
        /// </summary>
        public BoxCollider ActiveFootCollider => _footCollider != null && _footCollider.enabled
            ? _footCollider
            : null;

        /// <summary>获取当前绑定的浮动胶囊配置。</summary>
        public FloatingCapsuleModule FloatingCapsuleModule => _settings;

        /// <summary>获取与浮动胶囊关联的基础形状快照。</summary>
        public FloatingCapsuleAuthoringState AuthoringState => _authoringState;

        /// <summary>获取当前有效碰撞体的世界边界。</summary>
        public Bounds Bounds
        {
            get
            {
                if (_movementCollider == null) return new Bounds();

                Bounds bounds = _movementCollider.bounds;
                if (_footCollider != null && _footCollider.enabled)
                    bounds.Encapsulate(_footCollider.bounds);
                return bounds;
            }
        }

        /// <summary>
        /// 获取浮动胶囊相对基础胶囊底部实际抬升的世界竖直距离。
        /// </summary>
        /// <returns>当前有效支撑底部相对基础胶囊底部的竖直留空高度，单位：米。</returns>
        public float GetFloatingBottomClearance()
        {
            if (_movementCollider == null || _settings == null) return 0f;

            // 浮动留空的数学定义由 FloatingCapsuleModule 解释，形状模块只提供当前实际组件。
            return _settings.GetFloatingBottomClearance(_movementCollider);
        }

        /// <summary>
        /// 同步基础或浮动后的胶囊形状和脚底 BoxCollider。
        /// </summary>
        public void Synchronize()
        {
            if (_movementCollider == null || _settings == null || _authoringState == null)
            {
                DestroyFootCollider();
                return;
            }

            bool floatingEnabled = _settings != null && _settings.Enabled;
            FloatingCapsuleShape effectiveShape = _settings.GetEffectiveShape(_movementCollider);
            ApplyCapsuleShape(effectiveShape);

            // 浮动胶囊形状同步完成后，按需创建、更新或删除脚底 BoxCollider。
            SyncFootCollider(floatingEnabled);
        }

        /// <summary>
        /// 计算给定世界水平移动方向上的 Collider 前缘半径或半宽。
        /// </summary>
        /// <param name="worldDirection">需要评估的世界空间方向。</param>
        /// <returns>从 Collider 中心到该方向边缘的世界距离。</returns>
        public float GetHorizontalExtent(Vector3 worldDirection)
        {
            if (_movementCollider == null) return 0f;

            Vector3 direction = Vector3.ProjectOnPlane(worldDirection, Vector3.up);
            if (direction.sqrMagnitude <= 0.000001f) return 0f;

            direction.Normalize();
            Vector3 extents = _movementCollider.bounds.extents;
            return Mathf.Abs(direction.x) * extents.x + Mathf.Abs(direction.z) * extents.z;
        }

        /// <summary>
        /// 获取用于脚底宽体确认的保守球半径。
        /// </summary>
        /// <returns>不大于 Collider 水平最小半宽的球半径。</returns>
        public float GetFootSupportRadius()
        {
            if (_movementCollider == null) return 0f;

            Bounds bounds = Bounds;
            return Mathf.Max(0.001f, Mathf.Min(bounds.extents.x, bounds.extents.z) * 0.9f);
        }

        /// <summary>
        /// 获取自动脚底 BoxCollider 用于悬浮接地检测的半尺寸。
        /// 物理碰撞保持完整 Box，接地检测仅收缩水平边缘，避免台阶前缘被误判为脚下支撑。
        /// </summary>
        /// <returns>脚底辅助体参与接地检测的世界空间半尺寸；不存在时返回零。</returns>
        public Vector3 GetFootSupportProbeHalfExtents()
        {
            if (_footCollider == null) return Vector3.zero;

            Vector3 scale = _footCollider.transform.lossyScale;
            Vector3 absoluteScale = new Vector3(
                Mathf.Abs(scale.x),
                Mathf.Abs(scale.y),
                Mathf.Abs(scale.z));
            Vector3 halfExtents = Vector3.Scale(_footCollider.size * 0.5f, absoluteScale);

            float widthScale = _settings != null ? _settings.FootBoxSupportWidthScale : 1f;
            switch (_movementCollider.direction)
            {
                case 0:
                    halfExtents.y *= widthScale;
                    halfExtents.z *= widthScale;
                    break;
                case 1:
                    halfExtents.x *= widthScale;
                    halfExtents.z *= widthScale;
                    break;
                default:
                    halfExtents.x *= widthScale;
                    halfExtents.y *= widthScale;
                    break;
            }

            return halfExtents;
        }

        #region Foot BoxCollider

        /// <summary>
        /// 将浮动胶囊模块计算出的形状结果写入实际 CapsuleCollider。
        /// </summary>
        /// <param name="shape">由 FloatingCapsuleModule 生成的有效局部胶囊形状。</param>
        private void ApplyCapsuleShape(in FloatingCapsuleShape shape)
        {
            if (_movementCollider == null) return;

            _movementCollider.center = shape.Center;
            _movementCollider.height = shape.Height;
            _movementCollider.radius = shape.Radius;
            _movementCollider.direction = shape.Direction;
        }

        /// <summary>
        /// 根据浮动胶囊状态同步脚底 BoxCollider：
        /// 启用浮动且脚底厚度大于零时在有效胶囊底部创建扁平 BoxCollider；
        /// 关闭浮动或厚度为零时删除该辅助组件。
        /// </summary>
        /// <param name="floatingEnabled">浮动胶囊当前是否启用。</param>
        private void SyncFootCollider(bool floatingEnabled)
        {
            float thickness = GetFootColliderHeight();
            bool shouldExist = floatingEnabled && thickness > 0f;

            if (shouldExist)
                EnsureFootCollider(thickness);
            else
                DestroyFootCollider();
        }

        /// <summary>
        /// 确保已存在脚底 BoxCollider 并更新其尺寸和位置。
        /// </summary>
        /// <param name="thickness">脚底 BoxCollider 的竖直厚度，单位：米。</param>
        private void EnsureFootCollider(float thickness)
        {
            if (_footCollider == null)
            {
                if (_owner == null) return;

                _footCollider = _owner.AddComponent<BoxCollider>();
                _authoringState.SetFootCollider(_footCollider);
            }

            _footCollider.isTrigger = false;
            _footCollider.hideFlags = HideFlags.None;
            _footCollider.enabled = true;

            Vector3 localAxis = FloatingCapsuleModule.GetCapsuleLocalAxis(_movementCollider.direction);

            // 有效胶囊底部的局部坐标。
            float effectiveHalfHeight = _movementCollider.height * 0.5f;
            Vector3 effectiveBottomLocal = _movementCollider.center - localAxis * effectiveHalfHeight;

            // 脚底 BoxCollider 的底面与有效胶囊最低点共面，向上填充下半球区域。
            Vector3 footCenterLocal = effectiveBottomLocal + localAxis * (thickness * 0.5f);

            float capsuleDiameter = _movementCollider.radius * 2f;
            Vector3 footSize = _movementCollider.direction switch
            {
                0 => new Vector3(thickness, capsuleDiameter, capsuleDiameter),
                1 => new Vector3(capsuleDiameter, thickness, capsuleDiameter),
                _ => new Vector3(capsuleDiameter, capsuleDiameter, thickness)
            };

            _footCollider.center = footCenterLocal;
            _footCollider.size = footSize;
        }

        /// <summary>
        /// 删除脚底 BoxCollider（如果存在）。
        /// 编辑器验证回调中延后到安全时机立即销毁，避免在 OnValidate 内调用 DestroyImmediate。
        /// </summary>
        private void DestroyFootCollider()
        {
            if (_footCollider == null) return;

            BoxCollider footCollider = _footCollider;
            _footCollider = null;
            if (_authoringState != null && _authoringState.FootCollider == footCollider)
                _authoringState.SetFootCollider(null);

            footCollider.enabled = false;
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(footCollider);
                return;
            }

#if UNITY_EDITOR
            EditorApplication.delayCall += () =>
            {
                if (footCollider == null) return;

                UnityEngine.Object.DestroyImmediate(footCollider);
                SceneView.RepaintAll();
            };
#else
            UnityEngine.Object.Destroy(footCollider);
#endif
        }

        /// <summary>
        /// 获取允许脚底盒体占用的局部高度，确保它不会延伸到原始胶囊底部之外。
        /// </summary>
        private float GetFootColliderHeight()
        {
            if (_movementCollider == null || _settings == null) return 0f;
            if (_settings.FootBoxHeight <= 0f) return 0f;
            return Mathf.Min(_settings.FootBoxHeight, _movementCollider.radius);
        }

        #endregion
    }
}
