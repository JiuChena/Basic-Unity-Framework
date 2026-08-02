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
            if (TryGetCircularHorizontalRadius(out float circularRadius)) return circularRadius;

            // 非直立或水平非等比缩放的胶囊无法保证圆形截面，保留 AABB 前缘避免低估越界距离。
            Vector3 extents = _movementCollider.bounds.extents;
            return Mathf.Abs(direction.x) * extents.x + Mathf.Abs(direction.z) * extents.z;
        }

        /// <summary>
        /// 判断主胶囊在世界水平面上是否具有可直接使用的圆形截面。
        /// </summary>
        /// <param name="radius">满足条件时返回世界空间圆形半径。</param>
        /// <returns>胶囊轴与世界向上对齐且两个水平半径等比时返回 true。</returns>
        private bool TryGetCircularHorizontalRadius(out float radius)
        {
            radius = 0f;
            if (_movementCollider == null) return false;

            // 只有胶囊轴竖直时，世界 X/Z 截面才是其端面圆形。
            Vector3 localAxis = FloatingCapsuleModule.GetCapsuleLocalAxis(_movementCollider.direction);
            Vector3 worldAxis = _movementCollider.transform.TransformDirection(localAxis).normalized;
            if (Mathf.Abs(Vector3.Dot(worldAxis, Vector3.up)) < 0.9999f) return false;

            // 胶囊半径沿两个垂直于轴的局部方向必须采用相同世界缩放。
            Vector3 scale = _movementCollider.transform.lossyScale;
            float firstRadiusScale;
            float secondRadiusScale;
            switch (_movementCollider.direction)
            {
                case 0:
                    firstRadiusScale = Mathf.Abs(scale.y);
                    secondRadiusScale = Mathf.Abs(scale.z);
                    break;
                case 1:
                    firstRadiusScale = Mathf.Abs(scale.x);
                    secondRadiusScale = Mathf.Abs(scale.z);
                    break;
                default:
                    firstRadiusScale = Mathf.Abs(scale.x);
                    secondRadiusScale = Mathf.Abs(scale.y);
                    break;
            }

            if (Mathf.Abs(firstRadiusScale - secondRadiusScale) > 0.0001f) return false;

            radius = _movementCollider.radius * firstRadiusScale;
            return radius > 0.000001f;
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

        /// <summary>
        /// 获取覆盖浮动胶囊底部无碰撞区域的瘦版接地探测体。
        /// </summary>
        /// <param name="center">返回无碰撞区域的世界中心点。</param>
        /// <param name="halfExtents">返回按接地宽度比例收窄 X/Z 后的世界半尺寸。</param>
        /// <param name="clearanceHeight">返回探测体完整竖直高度，等于实际底部无碰撞留空，单位：米。</param>
        /// <returns>浮动胶囊有效且能够构建探测体时返回 true。</returns>
        public bool TryGetFloatingClearanceProbe(
            out Vector3 center,
            out Vector3 halfExtents,
            out float clearanceHeight)
        {
            center = Vector3.zero;
            halfExtents = Vector3.zero;
            clearanceHeight = 0f;
            if (_footCollider == null || !_footCollider.enabled) return false;

            // 无碰撞区域从有效胶囊底部向下延伸到底层基础胶囊底部，Y 尺寸必须完整保留。
            clearanceHeight = GetFloatingBottomClearance();
            if (clearanceHeight <= 0.0001f) return false;

            // X/Z 复用脚底支撑宽度比例，避免加宽探测体在台阶前缘或侧面提前认定接地。
            Bounds footBounds = _footCollider.bounds;
            float widthScale = _settings != null ? _settings.FootBoxSupportWidthScale : 1f;
            center = new Vector3(
                footBounds.center.x,
                footBounds.min.y - clearanceHeight * 0.5f,
                footBounds.center.z);
            halfExtents = new Vector3(
                Mathf.Max(0.001f, footBounds.extents.x * widthScale),
                clearanceHeight * 0.5f,
                Mathf.Max(0.001f, footBounds.extents.z * widthScale));
            return true;
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
