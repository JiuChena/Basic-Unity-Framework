using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Framework.ExpandComponent.DataProvider;
using Framework.ExpandComponent.UnitMover;
using UnitMoverComponent = Framework.ExpandComponent.UnitMover.UnitMover;

namespace Framework.ExpandComponent.UnitMover.Editor
{
    /// <summary>
    /// 以材质面板风格的模块分区呈现 UnitMover 组装引用和各项独立运动配置。
    /// </summary>
    [CustomEditor(typeof(UnitMoverComponent))]
    public sealed class UnitMoverEditor : UnityEditor.Editor
    {
        // 功能模块标题栏的固定高度，保持所有分区的纵向节奏一致。
        private const float ModuleHeaderHeight = 24f;
        // 深色 Inspector 皮肤下标题栏使用的整行背景色。
        private static readonly Color ProSkinModuleHeaderColor = new Color(0.19f, 0.19f, 0.19f, 1f);
        // 浅色 Inspector 皮肤下标题栏使用的整行背景色。
        private static readonly Color LightSkinModuleHeaderColor = new Color(0.72f, 0.72f, 0.72f, 1f);
        // 当前 Editor 实例在 OnEnable 后缓存的粗体折叠标题样式。
        private GUIStyle _moduleFoldoutStyle;
        // 可供 Inspector 选择的具体移动策略类型缓存，仅在编辑器域重载后重新收集。
        private static List<Type> _movementStrategyTypes;

        // 相关组件引用模块的展开状态。
        private bool _componentsFoldout = true;
        // 运行时命令来源和模式模块的展开状态。
        private bool _commandSourcesFoldout = true;
        // 地面最大速度和加减速模块的展开状态。
        private bool _locomotionFoldout = true;
        // 普通跳跃模块的展开状态。
        private bool _jumpFoldout = true;
        // 浮动胶囊、接地、坡面和台阶模块的展开状态。
        private bool _groundAdaptationFoldout = true;
        // 空中速度和重力模块的展开状态。
        private bool _airAndGravityFoldout = true;
        // 边缘保护模块的展开状态。
        private bool _edgeProtectionFoldout = true;
        // Scene 预览模块的展开状态。
        private bool _previewFoldout = true;

        /// <summary>
        /// 以固定顺序绘制 Script、组装引用和模块化运动 Profile。
        /// </summary>
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            UnitMoverComponent mover = (UnitMoverComponent)target;

            DrawScriptReference(mover);
            DrawComponentReferences(mover);
            DrawCommandSourcePanel(mover);
            DrawLocomotionPanel();
            DrawJumpPanel();
            DrawGroundAdaptationPanel();
            DrawAirAndGravityPanel();
            DrawEdgeProtectionPanel();
            DrawPreviewPanel();

            bool propertiesChanged = serializedObject.ApplyModifiedProperties();
            if (propertiesChanged)
                mover.SynchronizeColliderShape();

            if (GUI.changed || propertiesChanged)
                SceneView.RepaintAll();
        }

        /// <summary>
        /// 绘制始终可见且不可编辑的 Script 引用行。
        /// </summary>
        /// <param name="mover">当前正在编辑的 UnitMover 组件。</param>
        private static void DrawScriptReference(UnitMoverComponent mover)
        {
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.ObjectField("Script", MonoScript.FromMonoBehaviour(mover), typeof(MonoScript), false);
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.Space(4f);
        }

        /// <summary>
        /// 绘制 Rigidbody、运动 Collider 和移动数据 Provider 引用。
        /// </summary>
        /// <param name="mover">当前正在编辑的 UnitMover 组件。</param>
        private void DrawComponentReferences(UnitMoverComponent mover)
        {
            _componentsFoldout = BeginModulePanel(_componentsFoldout, "相关组件引用");
            if (_componentsFoldout)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_rigidbody"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_movementCollider"));
                SerializedProperty dataProvider = serializedObject.FindProperty("_dataProvider");
                EditorGUILayout.PropertyField(dataProvider);
                if (dataProvider.objectReferenceValue != null
                    && dataProvider.objectReferenceValue is not IDataProvider)
                    EditorGUILayout.HelpBox(
                        "Data Provider 必须是实现 IDataProvider 的同对象组件。",
                        MessageType.Warning);
                else if (dataProvider.objectReferenceValue is MonoBehaviour dataProviderComponent
                         && dataProviderComponent.gameObject != mover.gameObject)
                    EditorGUILayout.HelpBox(
                        "Data Provider 必须挂载在当前 UnitMover 所在的 GameObject 上。",
                        MessageType.Warning);
                EditorGUI.indentLevel--;
            }
            EndModulePanel();
        }

        /// <summary>
        /// 绘制 Inspector 初始移动策略、业务层命令来源和运行时自动状态说明。
        /// </summary>
        /// <param name="mover">当前正在编辑的 UnitMover 组件。</param>
        private void DrawCommandSourcePanel(UnitMoverComponent mover)
        {
            _commandSourcesFoldout = BeginModulePanel(_commandSourcesFoldout, "移动策略与命令来源");
            if (_commandSourcesFoldout)
            {
                EditorGUI.indentLevel++;
                DrawMovementStrategySelector();
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.TextField("当前移动策略", mover.ActiveMovementStrategyName ?? "未配置");
                EditorGUILayout.TextField("当前命令输入", GetCommandSourceStatus(mover));
                EditorGUILayout.TextField("当前运动模式", GetMovementModeStatus(mover));
                DrawRuntimeMotionDiagnostics(mover);
                EditorGUI.EndDisabledGroup();
                EditorGUILayout.HelpBox("Inspector 选择首次启用的移动策略；业务层可在运行时通过 UseMovementStrategy<TStrategy>() 切换，UnitMover 会按策略类型复用缓存实例。指定 Data Provider 后，UnitMover 会在每个物理步主动提交其黑板输入；AI、Root Motion、网络等独立持续来源仍可通过命令来源 API 注册。", MessageType.Info);
                EditorGUI.indentLevel--;
            }
            EndModulePanel();
        }

        /// <summary>
        /// 绘制可序列化移动策略的类型选择器和当前策略的附属配置字段。
        /// </summary>
        private void DrawMovementStrategySelector()
        {
            SerializedProperty strategyProperty = serializedObject.FindProperty("_movementStrategy");
            if (strategyProperty == null) return;

            List<Type> strategyTypes = GetMovementStrategyTypes();
            if (strategyTypes.Count == 0)
            {
                EditorGUILayout.HelpBox("项目中没有可选择的 UnitMovementStrategy 派生类。", MessageType.Warning);
                return;
            }

            UnitMovementStrategy currentStrategy = strategyProperty.managedReferenceValue as UnitMovementStrategy;
            Type currentType = currentStrategy != null ? currentStrategy.GetType() : null;
            int currentIndex = Mathf.Max(0, strategyTypes.IndexOf(currentType));
            string[] displayNames = new string[strategyTypes.Count];
            for (int index = 0; index < strategyTypes.Count; index++)
                displayNames[index] = ObjectNames.NicifyVariableName(strategyTypes[index].Name);

            EditorGUI.BeginChangeCheck();
            int selectedIndex = EditorGUILayout.Popup("初始移动策略", currentIndex, displayNames);
            if (EditorGUI.EndChangeCheck())
                strategyProperty.managedReferenceValue = (UnitMovementStrategy)Activator.CreateInstance(
                    strategyTypes[selectedIndex]);

            EditorGUILayout.PropertyField(strategyProperty, new GUIContent("策略配置"), true);
        }

        /// <summary>
        /// 获取所有可由 Inspector 创建的具体移动策略类型，并以稳定名称顺序排列。
        /// </summary>
        /// <returns>具有公开无参构造函数的非抽象移动策略类型列表。</returns>
        private static List<Type> GetMovementStrategyTypes()
        {
            if (_movementStrategyTypes != null) return _movementStrategyTypes;

            _movementStrategyTypes = new List<Type>();
            foreach (Type strategyType in TypeCache.GetTypesDerivedFrom<UnitMovementStrategy>())
            {
                if (strategyType.IsAbstract || strategyType.ContainsGenericParameters) continue;
                if (strategyType.GetConstructor(Type.EmptyTypes) == null) continue;

                _movementStrategyTypes.Add(strategyType);
            }

            _movementStrategyTypes.Sort((left, right) => string.Compare(
                left.Name,
                right.Name,
                StringComparison.Ordinal));
            return _movementStrategyTypes;
        }

        /// <summary>
        /// 获取当前 Inspector 应显示的命令输入状态，区分默认通用命令接口和可选连续来源两种情况。
        /// </summary>
        /// <param name="mover">当前正在编辑的 UnitMover 组件。</param>
        /// <returns>供只读 Inspector 字段显示的命令输入状态文本。</returns>
        private static string GetCommandSourceStatus(UnitMoverComponent mover)
        {
            if (mover == null || !mover.IsRuntimeReady) return "仅运行时创建";
            if (mover.IsDataProviderInputActive) return "DataProvider";
            return string.IsNullOrEmpty(mover.ActiveCommandSourceId)
                ? "通用命令接口（SubmitCommand）"
                : mover.ActiveCommandSourceId;
        }

        /// <summary>
        /// 获取当前 Inspector 应显示的自动运动模式状态。
        /// </summary>
        /// <param name="mover">当前正在编辑的 UnitMover 组件。</param>
        /// <returns>供只读 Inspector 字段显示的自动运动模式状态文本。</returns>
        private static string GetMovementModeStatus(UnitMoverComponent mover)
        {
            if (mover == null || !mover.IsRuntimeReady) return "仅运行时自动选择";
            return mover.State.Mode == MovementMode.Ground && !mover.State.IsGrounded
                ? "等待首次物理步"
                : mover.State.Mode.ToString();
        }

        /// <summary>
        /// 绘制从命令输入到刚体提交的关键速度诊断，便于定位移动链路在哪一阶段归零。
        /// </summary>
        /// <param name="mover">当前正在编辑的 UnitMover 组件。</param>
        private static void DrawRuntimeMotionDiagnostics(UnitMoverComponent mover)
        {
            if (mover == null || !mover.IsRuntimeReady) return;

            UnitMovementCommand command = mover.LastCommand;
            EditorGUILayout.Vector3Field("命令世界方向", command.WorldMoveDirection);
            EditorGUILayout.FloatField("命令速度倍率", command.SpeedScale);
            EditorGUILayout.Vector3Field("策略候选速度", mover.LastCandidateVelocity);
            EditorGUILayout.Vector3Field("刚体提交速度", mover.LastCommittedVelocity);
            EditorGUILayout.TextField("Rigidbody 约束", mover.RigidbodyConstraints.ToString());
        }

        /// <summary>
        /// 绘制地面速度、加速和减速配置。
        /// </summary>
        private void DrawLocomotionPanel()
        {
            SerializedProperty locomotion = GetProfileModule("_locomotion");
            if (locomotion == null) return;

            _locomotionFoldout = BeginModulePanel(_locomotionFoldout, "基础移动");
            if (_locomotionFoldout)
            {
                EditorGUI.indentLevel++;
                DrawRelativeProperty(locomotion, "_groundMaxSpeed");
                DrawRelativeProperty(locomotion, "_groundAcceleration");
                DrawRelativeProperty(locomotion, "_groundDeceleration");
                EditorGUI.indentLevel--;
            }
            EndModulePanel();
        }

        /// <summary>
        /// 绘制跳跃启用开关，并只在启用时显示其从属手感参数。
        /// </summary>
        private void DrawJumpPanel()
        {
            SerializedProperty jump = GetProfileModule("_jump");
            if (jump == null) return;

            _jumpFoldout = BeginModulePanel(_jumpFoldout, "跳跃功能");
            if (_jumpFoldout)
            {
                EditorGUI.indentLevel++;
                SerializedProperty enabled = jump.FindPropertyRelative("_enabled");
                EditorGUILayout.PropertyField(enabled);
                if (enabled.boolValue)
                {
                    DrawRelativeProperty(jump, "_initialSpeed");
                    DrawRelativeProperty(jump, "_coyoteTime");
                    DrawRelativeProperty(jump, "_bufferTime");
                    DrawRelativeProperty(jump, "_cutMultiplier");
                }
                EditorGUI.indentLevel--;
            }
            EndModulePanel();
        }

        /// <summary>
        /// 绘制浮动胶囊、接地悬浮、坡面和台阶配置。
        /// </summary>
        private void DrawGroundAdaptationPanel()
        {
            SerializedProperty floatingCapsule = GetProfileModule("_floatingCapsule");
            SerializedProperty ground = GetProfileModule("_ground");
            SerializedProperty step = GetProfileModule("_step");
            if (floatingCapsule == null || ground == null || step == null) return;

            _groundAdaptationFoldout = BeginModulePanel(
                _groundAdaptationFoldout,
                "浮动胶囊、接地与台阶");
            if (_groundAdaptationFoldout)
            {
                EditorGUI.indentLevel++;
                SerializedProperty enabled = floatingCapsule.FindPropertyRelative("_enabled");
                EditorGUILayout.PropertyField(enabled);
                if (enabled.boolValue)
                {
                    DrawRelativeProperty(floatingCapsule, "_bottomClearance");
                    DrawRelativeProperty(floatingCapsule, "_footBoxHeight");
                    DrawRelativeProperty(floatingCapsule, "_footBoxSupportWidthScale");
                }

                EditorGUILayout.Space(3f);
                DrawRelativeProperty(ground, "_groundLayer");
                DrawRelativeProperty(ground, "_slopeLimit");
                DrawRelativeProperty(ground, "_hoverHeight");
                DrawRelativeProperty(ground, "_probeDistance");
                DrawRelativeProperty(ground, "_springStrength");
                DrawRelativeProperty(ground, "_springDamping");

                EditorGUILayout.Space(3f);
                DrawRelativeProperty(step, "_maxHeight");
                DrawRelativeProperty(step, "_probePadding");
                DrawRelativeProperty(step, "_maxUpSpeed");
                EditorGUI.indentLevel--;
            }
            EndModulePanel();
        }

        /// <summary>
        /// 绘制空中速度、空中控制、基础重力、下落重力和最大下落速度配置。
        /// </summary>
        private void DrawAirAndGravityPanel()
        {
            SerializedProperty locomotion = GetProfileModule("_locomotion");
            SerializedProperty gravity = GetProfileModule("_gravity");
            if (locomotion == null || gravity == null) return;

            _airAndGravityFoldout = BeginModulePanel(_airAndGravityFoldout, "空中行为与重力");
            if (_airAndGravityFoldout)
            {
                EditorGUI.indentLevel++;
                DrawRelativeProperty(locomotion, "_airMaxSpeed");
                DrawRelativeProperty(locomotion, "_airAcceleration");
                DrawRelativeProperty(locomotion, "_airControl");
                DrawRelativeProperty(gravity, "_multiplier");
                DrawRelativeProperty(gravity, "_fallMultiplier");
                DrawRelativeProperty(gravity, "_maxFallSpeed");
                EditorGUI.indentLevel--;
            }
            EndModulePanel();
        }

        /// <summary>
        /// 绘制边缘保护开关，并只在启用时显示预测支撑、短缝和回退参数。
        /// </summary>
        private void DrawEdgeProtectionPanel()
        {
            SerializedProperty edgeProtection = GetProfileModule("_edgeProtection");
            if (edgeProtection == null) return;

            _edgeProtectionFoldout = BeginModulePanel(_edgeProtectionFoldout, "边缘防跌落");
            if (_edgeProtectionFoldout)
            {
                EditorGUI.indentLevel++;
                SerializedProperty enabled = edgeProtection.FindPropertyRelative("_enabled");
                EditorGUILayout.PropertyField(enabled);
                if (enabled.boolValue)
                {
                    DrawRelativeProperty(edgeProtection, "_maxFallHeight");
                    DrawRelativeProperty(edgeProtection, "_fallRecoveryEnabled");
                    DrawRelativeProperty(edgeProtection, "_recoverUnexpectedFallsOnly");
                    DrawRelativeProperty(edgeProtection, "_maxBridgeableGapWidth");
                }

                EditorGUILayout.Space(3f);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_showEdgeDetectionGizmos"));
                EditorGUI.indentLevel--;
            }
            EndModulePanel();
        }

        /// <summary>
        /// 绘制编辑模式下浮动、接地与运行时边缘诊断 Gizmos 的开关。
        /// </summary>
        private void DrawPreviewPanel()
        {
            _previewFoldout = BeginModulePanel(_previewFoldout, "编辑器预览");
            if (_previewFoldout)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_showScenePreview"));
                EditorGUI.indentLevel--;
            }
            EndModulePanel();
        }

        /// <summary>
        /// 获取 UnitMovementProfile 内某个配置模块的序列化属性。
        /// </summary>
        /// <param name="relativeName">配置模块在 Profile 中的私有序列化字段名。</param>
        /// <returns>找到时返回模块属性，否则返回 null。</returns>
        private SerializedProperty GetProfileModule(string relativeName)
        {
            SerializedProperty profile = serializedObject.FindProperty("_profile");
            return profile != null ? profile.FindPropertyRelative(relativeName) : null;
        }

        /// <summary>
        /// 开始一个带边框、全宽标题栏与可折叠内容的功能模块面板。
        /// </summary>
        /// <param name="expanded">该功能模块在当前 Inspector 中的展开状态。</param>
        /// <param name="title">显示在模块顶部的功能名称。</param>
        /// <returns>用户操作后的模块展开状态。</returns>
        private bool BeginModulePanel(bool expanded, string title)
        {
            // 外层边框负责划分模块，标题栏单独占满面板的可用宽度。
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            Rect headerRect = GUILayoutUtility.GetRect(
                GUIContent.none,
                GUIStyle.none,
                GUILayout.ExpandWidth(true),
                GUILayout.Height(ModuleHeaderHeight));
            EditorGUI.DrawRect(
                headerRect,
                EditorGUIUtility.isProSkin ? ProSkinModuleHeaderColor : LightSkinModuleHeaderColor);

            // Unity 内置 foldout 会把箭头向 Rect 左侧溢出，额外预留左边距使箭头和标题都落在 Header 内。
            Rect foldoutRect = new Rect(
                headerRect.x + 20f,
                headerRect.y + 3f,
                headerRect.width - 25f,
                EditorGUIUtility.singleLineHeight);
            bool foldout = EditorGUI.Foldout(
                foldoutRect,
                expanded,
                title,
                true,
                GetModuleFoldoutStyle());
            EditorGUILayout.Space(2f);
            return foldout;
        }

        /// <summary>
        /// 在 Inspector 的 GUI 皮肤已准备完成后首次创建并缓存粗体折叠标题样式。
        /// </summary>
        /// <returns>可安全用于当前 Inspector 绘制的折叠标题样式。</returns>
        private GUIStyle GetModuleFoldoutStyle()
        {
            if (_moduleFoldoutStyle != null) return _moduleFoldoutStyle;

            // OnEnable 可能早于 EditorStyles 初始化；仅在 OnInspectorGUI 的绘制阶段访问当前皮肤样式。
            _moduleFoldoutStyle = new GUIStyle(EditorStyles.foldout)
            {
                fontStyle = FontStyle.Bold
            };
            return _moduleFoldoutStyle;
        }

        /// <summary>
        /// 结束当前功能模块面板并为下一分区保留稳定间距。
        /// </summary>
        private static void EndModulePanel()
        {
            // 与 BeginModulePanel 成对闭合，避免后续分区嵌入当前边框。
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2f);
        }

        /// <summary>
        /// 绘制指定模块内单个私有序列化字段，并保留其 Tooltip、Undo 与 Prefab Override 行为。
        /// </summary>
        /// <param name="module">包含目标字段的配置模块属性。</param>
        /// <param name="relativeName">需要绘制的私有序列化字段名。</param>
        private static void DrawRelativeProperty(SerializedProperty module, string relativeName)
        {
            SerializedProperty property = module.FindPropertyRelative(relativeName);
            if (property != null) EditorGUILayout.PropertyField(property);
        }
    }
}
