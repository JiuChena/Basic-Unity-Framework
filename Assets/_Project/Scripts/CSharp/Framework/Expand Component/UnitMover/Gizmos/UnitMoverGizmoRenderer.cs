using UnityEngine;

namespace Framework.ExpandComponent.UnitMover
{
    /// <summary>
    /// 只读绘制 UnitMover 场景诊断信息的纯 C# 渲染器。
    /// </summary>
    internal static class UnitMoverGizmoRenderer
    {
        /// <summary>
        /// 绘制当前已存在的浮动胶囊、接地探测范围、接地参考线和边缘防跌落诊断快照。
        /// </summary>
        /// <param name="movementCollider">UnitMover 实际使用的主 CapsuleCollider。</param>
        /// <param name="shapeModule">提供有效形状边界与基础胶囊快照的形状模块；为 null 时不绘制形状相关预览。</param>
        /// <param name="floatingCapsuleModule">浮动胶囊配置和组件专属快照容器；为 null 时不绘制浮动间隙。</param>
        /// <param name="groundSettings">接地悬浮配置；为 null 时不绘制接地参考线。</param>
        /// <param name="edgeDebugState">本次运行的边缘检测诊断快照；为 null 时不绘制边缘检测结果。</param>
        /// <param name="showEdgeDetectionGizmos">是否绘制运行时边缘检测射线及速度诊断。</param>
        internal static void DrawAll(
            CapsuleCollider movementCollider,
            ColliderShapeModule shapeModule,
            FloatingCapsuleModule floatingCapsuleModule,
            GroundSettings groundSettings,
            EdgeProtectionDebugState edgeDebugState,
            bool showEdgeDetectionGizmos)
        {
            // 编辑模式和运行模式共用实际生效的胶囊间隙预览。
            DrawFloatingCapsuleGapPreview(movementCollider, shapeModule, floatingCapsuleModule);

            // 接地范围与 GroundProbeModule 的实际 BoxCast 参数保持一致，便于在编辑模式下核对浮动留空覆盖范围。
            DrawGroundProbeVolumePreview(shapeModule, groundSettings);

            // 接地参考线由有效碰撞底部、底部留空和额外悬浮高度共同决定。
            DrawGroundPreview(shapeModule, groundSettings);

            // 边缘射线只读取运行时保存的诊断快照，避免 Scene 绘制触发物理查询。
            DrawEdgeProtectionPreview(shapeModule, edgeDebugState, showEdgeDetectionGizmos);
        }

        /// <summary>
        /// 在基础胶囊底部和当前有效碰撞底部之间绘制浮动胶囊留下的黄色间隙。
        /// </summary>
        /// <param name="movementCollider">UnitMover 实际使用的主 CapsuleCollider。</param>
        /// <param name="shapeModule">包含基础胶囊快照的形状模块。</param>
        /// <param name="floatingCapsuleModule">包含浮动胶囊开关和留空高度的模块。</param>
        private static void DrawFloatingCapsuleGapPreview(
            CapsuleCollider movementCollider,
            ColliderShapeModule shapeModule,
            FloatingCapsuleModule floatingCapsuleModule)
        {
            if (movementCollider == null || shapeModule == null || floatingCapsuleModule == null) return;

            FloatingCapsuleAuthoringState state = shapeModule.AuthoringState;
            if (state == null || !floatingCapsuleModule.Enabled) return;
            if (!state.Captured || !state.FloatingShapeApplied) return;

            // 使用基础快照限制绘制高度，使 Scene 预览与实际形状同步逻辑一致。
            float maximumClearance = Mathf.Max(0f, state.BaseHeight - state.BaseRadius * 2f);
            float clearance = Mathf.Clamp(floatingCapsuleModule.BottomClearance, 0f, maximumClearance);
            if (clearance <= 0.0001f) return;

            Vector3 localAxis = FloatingCapsuleModule.GetCapsuleLocalAxis(movementCollider.direction);
            float diameter = movementCollider.radius * 2f;
            Vector3 effectiveBottom = movementCollider.center - localAxis * (movementCollider.height * 0.5f);
            Vector3 baseBottom = effectiveBottom - localAxis * clearance;
            Vector3 gapCenter = (effectiveBottom + baseBottom) * 0.5f;
            Vector3 gapSize = movementCollider.direction switch
            {
                0 => new Vector3(clearance, diameter, diameter),
                1 => new Vector3(diameter, clearance, diameter),
                _ => new Vector3(diameter, diameter, clearance)
            };

            Color previousColor = Gizmos.color;
            Matrix4x4 previousMatrix = Gizmos.matrix;
            Gizmos.matrix = movementCollider.transform.localToWorldMatrix;

            // 使用半透明填充和轮廓同时标识无碰撞空间的体积范围。
            Gizmos.color = new Color(1f, 0.92f, 0.016f, 0.35f);
            Gizmos.DrawCube(gapCenter, gapSize);
            Gizmos.color = new Color(1f, 0.92f, 0.016f, 0.85f);
            Gizmos.DrawWireCube(gapCenter, gapSize);

            Gizmos.matrix = previousMatrix;
            Gizmos.color = previousColor;

#if UNITY_EDITOR
            // 在世界空间显示经过动态上限约束后的实际底部留空高度。
            Vector3 labelPosition = movementCollider.transform.TransformPoint(gapCenter);
            UnityEditor.Handles.color = new Color(1f, 0.92f, 0.016f, 0.9f);
            UnityEditor.Handles.Label(labelPosition, $"{clearance:0.###} m",
                new GUIStyle(UnityEditor.EditorStyles.miniLabel)
                {
                    normal = { textColor = new Color(1f, 0.92f, 0.016f) },
                    alignment = TextAnchor.MiddleCenter
                });
#endif
        }

        /// <summary>
        /// 绘制当前有效碰撞底部到期望支撑距离的接地参考线。
        /// </summary>
        /// <param name="shapeModule">提供当前有效碰撞边界和底部留空高度的形状模块。</param>
        /// <param name="groundSettings">提供额外悬浮高度的接地配置。</param>
        private static void DrawGroundPreview(ColliderShapeModule shapeModule, GroundSettings groundSettings)
        {
            if (shapeModule == null || groundSettings == null) return;

            // 有效碰撞底部是 GroundProbe 和 HoverModule 的共同参考起点。
            Bounds bounds = shapeModule.Bounds;
            Vector3 origin = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            float supportDistance = shapeModule.GetFloatingBottomClearance() + groundSettings.HoverHeight;
            Color previousColor = Gizmos.color;
            Gizmos.color = new Color(1f, 0.85f, 0.15f, 0.75f);
            Gizmos.DrawLine(origin, origin + Vector3.down * supportDistance);
            Gizmos.color = previousColor;
        }

        /// <summary>
        /// 绘制浮动胶囊接地时用于确认支撑的完整无碰撞区 BoxCast 扫掠范围。
        /// </summary>
        /// <param name="shapeModule">提供脚底辅助体、无碰撞区尺寸和实际接地探测半尺寸的形状模块。</param>
        /// <param name="groundSettings">提供额外悬浮高度和向下探测长度的接地配置。</param>
        private static void DrawGroundProbeVolumePreview(
            ColliderShapeModule shapeModule,
            GroundSettings groundSettings)
        {
            if (shapeModule == null || groundSettings == null) return;
            if (!shapeModule.TryGetFloatingClearanceProbe(
                    out Vector3 probeCenter,
                    out Vector3 halfExtents,
                    out _)) return;

            BoxCollider footCollider = shapeModule.ActiveFootCollider;
            if (footCollider == null) return;

            // 此距离与 GroundProbeModule.GroundCheckDistance 相同，BoxCast 从上端扫至下端以覆盖下陷和悬浮两种修正范围。
            float castDistance = shapeModule.GetFloatingBottomClearance()
                                 + groundSettings.HoverHeight
                                 + groundSettings.ProbeDistance;
            if (castDistance <= 0f) return;

            Vector3 castStartCenter = probeCenter + Vector3.up * castDistance;
            Vector3 castEndCenter = probeCenter - Vector3.up * castDistance;
            Quaternion rotation = footCollider.transform.rotation;
            Color previousColor = Gizmos.color;
            Matrix4x4 previousMatrix = Gizmos.matrix;

            // 实心青色盒表示 BoxCast 的起始体积，浅蓝轮廓表示扫描结束时的体积。
            DrawOrientedBox(castStartCenter, halfExtents, rotation, new Color(0.1f, 0.75f, 1f, 0.16f), true);
            DrawOrientedBox(castStartCenter, halfExtents, rotation, new Color(0.1f, 0.75f, 1f, 0.9f), false);
            DrawOrientedBox(castEndCenter, halfExtents, rotation, new Color(0.3f, 0.55f, 1f, 0.85f), false);

            // 连接八个对应角点，明确显示实际参与接地确认的整段扫掠体，而非仅显示脚底的薄层区域。
            for (int index = 0; index < 8; index++)
            {
                Vector3 localCorner = new Vector3(
                    (index & 1) == 0 ? -halfExtents.x : halfExtents.x,
                    (index & 2) == 0 ? -halfExtents.y : halfExtents.y,
                    (index & 4) == 0 ? -halfExtents.z : halfExtents.z);
                Vector3 startCorner = castStartCenter + rotation * localCorner;
                Vector3 endCorner = castEndCenter + rotation * localCorner;
                Gizmos.color = new Color(0.15f, 0.65f, 1f, 0.48f);
                Gizmos.DrawLine(startCorner, endCorner);
            }

            Gizmos.matrix = previousMatrix;
            Gizmos.color = previousColor;
        }

        /// <summary>
        /// 以给定世界中心、半尺寸和旋转绘制一个与 Physics.BoxCast 同方向的盒体。
        /// </summary>
        /// <param name="center">盒体的世界空间中心点。</param>
        /// <param name="halfExtents">盒体在旋转前局部坐标系中的半尺寸。</param>
        /// <param name="rotation">盒体相对世界空间的旋转。</param>
        /// <param name="color">绘制盒体时使用的颜色。</param>
        /// <param name="filled">为 true 时绘制半透明实心盒，否则仅绘制轮廓。</param>
        private static void DrawOrientedBox(
            Vector3 center,
            Vector3 halfExtents,
            Quaternion rotation,
            Color color,
            bool filled)
        {
            // 使用 Gizmos 矩阵保留检测盒的实际旋转，避免对象旋转后预览与物理查询脱节。
            Matrix4x4 previousMatrix = Gizmos.matrix;
            Color previousColor = Gizmos.color;
            Gizmos.matrix = Matrix4x4.TRS(center, rotation, Vector3.one);
            Gizmos.color = color;
            if (filled) Gizmos.DrawCube(Vector3.zero, halfExtents * 2f);
            else Gizmos.DrawWireCube(Vector3.zero, halfExtents * 2f);
            Gizmos.matrix = previousMatrix;
            Gizmos.color = previousColor;
        }

        /// <summary>
        /// 绘制最近一次边缘支撑、危险方向和受约束速度的运行时诊断数据。
        /// </summary>
        /// <param name="shapeModule">提供当前有效碰撞边界的形状模块。</param>
        /// <param name="edgeDebugState">由边缘防跌落模块保存的只读诊断快照。</param>
        /// <param name="showEdgeDetectionGizmos">是否允许绘制边缘检测结果。</param>
        private static void DrawEdgeProtectionPreview(
            ColliderShapeModule shapeModule,
            EdgeProtectionDebugState edgeDebugState,
            bool showEdgeDetectionGizmos)
        {
            if (!showEdgeDetectionGizmos || shapeModule == null || edgeDebugState == null) return;

            // 前缘三点支撑采样命中可行走地面时绘制为绿色。
            if (edgeDebugState.SupportRayDistance > 0f)
            {
                for (int index = 0; index < edgeDebugState.SupportPoints.Length; index++)
                {
                    DrawEdgeDetectionRay(
                        edgeDebugState.SupportPoints[index],
                        edgeDebugState.SupportRayDistance,
                        edgeDebugState.SupportResults[index]);
                }
            }

            // 环形危险采样只在缺少支撑的位置绘制为红色。
            if (edgeDebugState.HazardRayDistance > 0f)
            {
                for (int index = 0; index < edgeDebugState.HazardPoints.Length; index++)
                {
                    DrawEdgeDetectionRay(
                        edgeDebugState.HazardPoints[index],
                        edgeDebugState.HazardRayDistance,
                        !edgeDebugState.HazardResults[index]);
                }
            }

            // 红色表示边缘外法线，青色表示约束后保留的移动速度。
            Bounds bounds = shapeModule.Bounds;
            Color previousColor = Gizmos.color;
            Gizmos.color = Color.red;
            Gizmos.DrawRay(bounds.center, edgeDebugState.EdgeOutNormal * 0.75f);
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(bounds.center, edgeDebugState.ConstrainedVelocity.normalized * 0.75f);
            Gizmos.color = previousColor;
        }

        /// <summary>
        /// 使用安全状态颜色绘制一条向下的边缘检测射线及其起止点。
        /// </summary>
        /// <param name="origin">检测射线的世界空间起点。</param>
        /// <param name="distance">检测射线向下延伸的最大长度。</param>
        /// <param name="isSafe">命中可行走支撑时为 true，缺少支撑时为 false。</param>
        private static void DrawEdgeDetectionRay(Vector3 origin, float distance, bool isSafe)
        {
            Color color = isSafe
                ? new Color(0.15f, 0.95f, 0.25f, 0.9f)
                : new Color(1f, 0.15f, 0.1f, 0.9f);
            Vector3 end = origin + Vector3.down * distance;
            Color previousColor = Gizmos.color;
            Gizmos.color = color;
            Gizmos.DrawRay(origin, Vector3.down * distance);
            Gizmos.DrawSphere(origin, 0.025f);
            Gizmos.DrawSphere(end, 0.02f);
            Gizmos.color = previousColor;
        }
    }
}
