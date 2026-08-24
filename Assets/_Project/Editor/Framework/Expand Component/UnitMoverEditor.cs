using System;
using System.Collections.Generic;
using System.Reflection;
using Framework.ExpandComponent.DataProvider;
using Framework.ExpandComponent.UnitMover;
using UnityEditor;
using UnityEngine;
using UnitMoverComponent = Framework.ExpandComponent.UnitMover.UnitMover;

namespace Framework.ExpandComponent.UnitMover.Editor
{
    /// <summary>
    /// 以模块化深色折叠栏展示 UnitMover 引用、策略与策略持有的序列化配置。
    /// </summary>
    [CustomEditor(typeof(UnitMoverComponent))]
    public sealed class UnitMoverEditor : UnityEditor.Editor
    {
        // 功能模块标题栏固定高度，保证 Inspector 各分区视觉节奏一致。
        private const float ModuleHeaderHeight = 24f;
        // 深色 Inspector 皮肤下的标题栏背景颜色。
        private static readonly Color ProSkinModuleHeaderColor = new Color(0.19f, 0.19f, 0.19f, 1f);
        // 浅色 Inspector 皮肤下的标题栏背景颜色。
        private static readonly Color LightSkinModuleHeaderColor = new Color(0.72f, 0.72f, 0.72f, 1f);
        // 可供 Inspector 选择的具体策略类型缓存。
        private static List<Type> _movementStrategyTypes;
        // 策略类型和字段名到自定义模块标题的编辑器域缓存。
        private static readonly Dictionary<string, string> _strategyModuleTitles = new Dictionary<string, string>();
        // 当前 Inspector GUI 可用后按需创建的粗体标题样式。
        private GUIStyle _moduleFoldoutStyle;
        // 相关组件引用分区的展开状态。
        private bool _componentsFoldout = true;
        // 初始策略选择和运行时诊断分区的展开状态。
        private bool _strategyFoldout = true;
        // 策略一级序列化字段路径到展开状态的缓存。
        private readonly Dictionary<string, bool> _strategyFieldFoldouts = new Dictionary<string, bool>();
        // Scene 预览开关分区的展开状态。
        private bool _previewFoldout = true;

        /// <summary>
        /// 以固定顺序绘制脚本、组件引用、策略配置和只读运行时诊断。
        /// </summary>
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            UnitMoverComponent mover = (UnitMoverComponent)target;

            // 外壳字段与策略字段分开显示，避免将纯 C# 模块塞回 UnitMover 的序列化面板。
            DrawScriptReference(mover);
            DrawComponentReferences(mover);
            DrawStrategyPanel(mover);
            DrawPreviewPanel();

            // 改动策略 Authoring 数据后立即同步形状，确保编辑模式 Scene 视图即时反映胶囊变化。
            bool propertiesChanged = serializedObject.ApplyModifiedProperties();
            if (propertiesChanged) mover.SynchronizeColliderShape();
            if (GUI.changed || propertiesChanged) SceneView.RepaintAll();
        }

        /// <summary>
        /// 绘制始终可见且不可编辑的脚本引用。
        /// </summary>
        /// <param name="mover">当前编辑的 UnitMover 组件。</param>
        private static void DrawScriptReference(UnitMoverComponent mover)
        {
            // Script 引用遵循 Unity 默认 Inspector 的只读呈现方式。
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.ObjectField("Script", MonoScript.FromMonoBehaviour(mover), typeof(MonoScript), false);
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.Space(4f);
        }

        /// <summary>
        /// 绘制刚体、胶囊、Provider、摄像机参考和刚体旋转接管选项。
        /// </summary>
        /// <param name="mover">当前编辑的 UnitMover 组件。</param>
        private void DrawComponentReferences(UnitMoverComponent mover)
        {
            _componentsFoldout = BeginModulePanel(_componentsFoldout, "相关组件引用");
            if (_componentsFoldout)
            {
                // 这些字段属于 Unity 外壳本身，策略只会收到解析完成后的直接引用。
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_rigidbody"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_movementCollider"));
                SerializedProperty dataProvider = serializedObject.FindProperty("_dataProvider");
                EditorGUILayout.PropertyField(dataProvider);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_movementReferenceCamera"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_freezeRigidbodyRotation"));
                DrawDataProviderValidation(dataProvider, mover);
                EditorGUI.indentLevel--;
            }
            EndModulePanel();
        }

        /// <summary>
        /// 对手动指定的 DataProvider 做同对象与接口类型的编辑期提示。
        /// </summary>
        /// <param name="dataProvider">DataProvider 序列化引用属性。</param>
        /// <param name="mover">当前编辑的 UnitMover 组件。</param>
        private static void DrawDataProviderValidation(SerializedProperty dataProvider, UnitMoverComponent mover)
        {
            if (dataProvider == null || dataProvider.objectReferenceValue == null) return;

            // UnitMover 只验证引用资格，不读取或验证具体 Blackboard 的业务输入契约。
            if (dataProvider.objectReferenceValue is not IDataProvider)
                EditorGUILayout.HelpBox("Data Provider 必须是实现 IDataProvider 的组件。", MessageType.Warning);
            else if (dataProvider.objectReferenceValue is MonoBehaviour behaviour
                     && behaviour.gameObject != mover.gameObject)
                EditorGUILayout.HelpBox("Data Provider 必须挂载在当前 UnitMover 所在的 GameObject 上。", MessageType.Warning);
        }

        /// <summary>
        /// 绘制策略类型选择器、当前策略诊断和策略所有一级序列化字段的自动模块面板。
        /// </summary>
        /// <param name="mover">当前编辑的 UnitMover 组件。</param>
        private void DrawStrategyPanel(UnitMoverComponent mover)
        {
            _strategyFoldout = BeginModulePanel(_strategyFoldout, "移动策略");
            if (_strategyFoldout)
            {
                // 策略类型是唯一需要专用创建逻辑的 SerializeReference 字段。
                EditorGUI.indentLevel++;
                SerializedProperty strategyProperty = serializedObject.FindProperty("_movementStrategy");
                DrawMovementStrategySelector(strategyProperty, mover);
                DrawRuntimeDiagnostics(mover);
                EditorGUI.indentLevel--;
            }
            EndModulePanel();

            // 策略内部模块完全由递归序列化字段驱动，新增策略字段无需改此 Editor。
            SerializedProperty strategy = serializedObject.FindProperty("_movementStrategy");
            DrawStrategyConfiguration(strategy, mover);
        }

        /// <summary>
        /// 绘制当前策略类型的下拉选择器，并在切换后创建新的序列化策略实例。
        /// </summary>
        /// <param name="strategyProperty">UnitMover 的 SerializeReference 策略属性。</param>
        /// <param name="mover">当前编辑的 UnitMover 组件。</param>
        private static void DrawMovementStrategySelector(
            SerializedProperty strategyProperty,
            UnitMoverComponent mover)
        {
            if (strategyProperty == null) return;

            // TypeCache 只在编辑器域重载后重新扫描，避免每次 Inspector 重绘执行反射。
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

            // 编辑模式替换初始配置；播放模式则必须调用 UnitMover 的真实运行时切换入口。
            EditorGUI.BeginChangeCheck();
            string selectorLabel = Application.isPlaying ? "运行时移动策略" : "初始移动策略";
            int selectedIndex = EditorGUILayout.Popup(selectorLabel, currentIndex, displayNames);
            if (EditorGUI.EndChangeCheck())
            {
                if (Application.isPlaying)
                {
                    // 播放中不能只替换 SerializeReference；真实活动策略必须同步切换并执行停用生命周期。
                    UnitMovementStrategy activeStrategy = mover != null
                        ? mover.UseMovementStrategy(strategyTypes[selectedIndex])
                        : null;
                    if (activeStrategy != null) strategyProperty.managedReferenceValue = activeStrategy;
                    return;
                }

                // 编辑模式替换初始策略前，旧策略必须归还其 Authoring 共享组件状态。
                mover?.RestoreInitialStrategyAuthoring();
                strategyProperty.managedReferenceValue = (UnitMovementStrategy)Activator.CreateInstance(strategyTypes[selectedIndex]);
            }
        }

        /// <summary>
        /// 绘制策略所有直接一级序列化字段，每个字段使用与现有 Inspector 一致的全宽模块标题栏。
        /// </summary>
        /// <param name="strategyProperty">当前 SerializeReference 策略属性。</param>
        /// <param name="mover">当前编辑的 UnitMover 组件。</param>
        private void DrawStrategyConfiguration(SerializedProperty strategyProperty, UnitMoverComponent mover)
        {
            if (strategyProperty == null || strategyProperty.managedReferenceValue == null) return;

            // 只遍历策略的直接子字段，PropertyField 的 includeChildren 负责字段内部递归显示。
            SerializedProperty iterator = strategyProperty.Copy();
            SerializedProperty end = iterator.GetEndProperty();
            Type strategyType = strategyProperty.managedReferenceValue.GetType();
            int childDepth = strategyProperty.depth + 1;
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren)
                   && !SerializedProperty.EqualContents(iterator, end))
            {
                enterChildren = false;
                if (iterator.depth != childDepth) continue;

                // 每个策略一级字段独立成为一条完整宽度的模块面板，新增模块无需 Editor 硬编码。
                string key = iterator.propertyPath;
                bool expanded = GetStrategyFieldFoldout(key);
                string title = GetStrategyModuleTitle(strategyType, iterator);
                expanded = BeginModulePanel(expanded, title);
                _strategyFieldFoldouts[key] = expanded;
                if (expanded)
                {
                    EditorGUI.indentLevel++;
                    DrawStrategyField(iterator, mover, title);
                    EditorGUI.indentLevel--;
                }
                EndModulePanel();
            }
        }

        /// <summary>
        /// 绘制一个策略一级字段，并仅对浮动胶囊的底部留空应用动态合法上限。
        /// </summary>
        /// <param name="property">需要递归绘制的策略一级字段。</param>
        /// <param name="mover">当前编辑的 UnitMover 组件。</param>
        /// <param name="displayName">当前字段应显示在内层属性折叠栏中的模块标题。</param>
        private static void DrawStrategyField(
            SerializedProperty property,
            UnitMoverComponent mover,
            string displayName)
        {
            if (property.name != "_floatingCapsuleModule")
            {
                EditorGUILayout.PropertyField(property, new GUIContent(displayName), true);
                return;
            }

            // 浮动胶囊仅替换底部留空的控件，其他新增字段仍按 Unity 的递归规则自动显示。
            DrawFloatingCapsuleFields(property, mover);
        }

        /// <summary>
        /// 绘制浮动胶囊的直接子字段，并将底部留空限制为基础胶囊允许的最大高度。
        /// </summary>
        /// <param name="floatingCapsule">浮动胶囊序列化模块字段。</param>
        /// <param name="mover">当前编辑的 UnitMover 组件。</param>
        private static void DrawFloatingCapsuleFields(SerializedProperty floatingCapsule, UnitMoverComponent mover)
        {
            // 此处只遍历一层，避免 Authoring 快照等隐藏子数据破坏 Inspector 的正常递归表现。
            SerializedProperty iterator = floatingCapsule.Copy();
            SerializedProperty end = iterator.GetEndProperty();
            int childDepth = floatingCapsule.depth + 1;
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren)
                   && !SerializedProperty.EqualContents(iterator, end))
            {
                enterChildren = false;
                if (iterator.depth != childDepth) continue;

                if (iterator.name == "_bottomClearance") DrawBottomClearanceProperty(iterator, floatingCapsule, mover);
                else EditorGUILayout.PropertyField(iterator, true);
            }
        }

        /// <summary>
        /// 以滑条绘制底部无碰撞留空，并保证有效胶囊高度始终不低于直径。
        /// </summary>
        /// <param name="clearance">浮动胶囊底部留空字段。</param>
        /// <param name="mover">当前编辑的 UnitMover 组件。</param>
        private static void DrawBottomClearanceProperty(
            SerializedProperty clearance,
            SerializedProperty floatingCapsule,
            UnitMoverComponent mover)
        {
            if (clearance == null) return;

            // 上限优先读取浮动前基础快照，防止启用后按已缩短高度重复扣减。
            float maximumClearance = GetMaximumBottomClearance(floatingCapsule, mover);
            clearance.floatValue = Mathf.Clamp(clearance.floatValue, 0f, maximumClearance);
            EditorGUILayout.Slider(
                clearance,
                0f,
                maximumClearance,
                new GUIContent("底部留空 / 最大台阶高度", "单位：米；同时决定浮动胶囊留空与可通过的最大台阶高度"));
        }

        /// <summary>
        /// 根据当前策略的基础胶囊快照或指定的主胶囊计算合法底部留空上限。
        /// </summary>
        /// <param name="floatingCapsule">当前策略的浮动胶囊序列化模块属性。</param>
        /// <param name="mover">当前编辑的 UnitMover 组件。</param>
        /// <returns>在保持胶囊最小直径高度前提下允许移除的最大底部高度，单位：米。</returns>
        private static float GetMaximumBottomClearance(
            SerializedProperty floatingCapsule,
            UnitMoverComponent mover)
        {
            SerializedProperty authoringState = floatingCapsule != null
                ? floatingCapsule.FindPropertyRelative("_authoringState")
                : null;
            if (authoringState != null
                && authoringState.FindPropertyRelative("_captured").boolValue)
            {
                // 快照是浮动前形状，能够准确限定顶部对齐收缩的合法范围。
                float baseHeight = authoringState.FindPropertyRelative("_baseHeight").floatValue;
                float baseRadius = authoringState.FindPropertyRelative("_baseRadius").floatValue;
                return Mathf.Max(0f, baseHeight - baseRadius * 2f);
            }

            // 尚未建立快照时使用 UnitMover 已解析的主胶囊，避免 Editor 重新扫描组件。
            CapsuleCollider capsule = mover != null ? mover.MovementCollider : null;
            return capsule != null ? Mathf.Max(0f, capsule.height - capsule.radius * 2f) : 0f;
        }

        /// <summary>
        /// 绘制当前策略的只读运行状态，便于确认策略缓存与输入消费是否已生效。
        /// </summary>
        /// <param name="mover">当前编辑的 UnitMover 组件。</param>
        private static void DrawRuntimeDiagnostics(UnitMoverComponent mover)
        {
            // 诊断只读取策略结果，UnitMover 不再显示旧 Runtime 或命令来源注册表概念。
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.TextField("当前移动策略", mover.ActiveMovementStrategyName ?? "未配置");
            EditorGUILayout.TextField("当前运动模式", mover.IsRuntimeReady ? mover.State.Mode.ToString() : "仅运行时创建");
            EditorGUILayout.Vector3Field("策略输入方向", mover.LastCommand.WorldMoveDirection);
            EditorGUILayout.FloatField("策略输入倍率", mover.LastCommand.SpeedScale);
            EditorGUILayout.Vector3Field("策略候选速度", mover.LastCandidateVelocity);
            EditorGUILayout.Vector3Field("刚体提交速度", mover.LastCommittedVelocity);
            EditorGUILayout.TextField("Rigidbody 约束", mover.RigidbodyConstraints.ToString());
            EditorGUI.EndDisabledGroup();
        }

        /// <summary>
        /// 绘制 Scene 预览与边缘检测 Gizmo 开关。
        /// </summary>
        private void DrawPreviewPanel()
        {
            _previewFoldout = BeginModulePanel(_previewFoldout, "编辑器预览");
            if (_previewFoldout)
            {
                // Gizmo 开关属于 Unity 外壳，策略仅提供可绘制的只读模块状态。
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_showScenePreview"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_showEdgeDetectionGizmos"));
                EditorGUI.indentLevel--;
            }
            EndModulePanel();
        }

        /// <summary>
        /// 获取所有可由 Inspector 创建的具体移动策略类型，并按名称稳定排序。
        /// </summary>
        /// <returns>拥有公开无参构造函数的非抽象策略类型列表。</returns>
        private static List<Type> GetMovementStrategyTypes()
        {
            if (_movementStrategyTypes != null) return _movementStrategyTypes;

            // 类型收集只发生在编辑器域初始化后，绘制阶段直接复用缓存结果。
            _movementStrategyTypes = new List<Type>();
            foreach (Type strategyType in TypeCache.GetTypesDerivedFrom<UnitMovementStrategy>())
            {
                if (strategyType.IsAbstract || strategyType.ContainsGenericParameters) continue;
                if (strategyType.GetConstructor(Type.EmptyTypes) == null) continue;
                _movementStrategyTypes.Add(strategyType);
            }

            _movementStrategyTypes.Sort((left, right) => string.Compare(left.Name, right.Name, StringComparison.Ordinal));
            return _movementStrategyTypes;
        }

        /// <summary>
        /// 获取策略字段在当前 Inspector 实例中的展开状态，首次显示默认展开。
        /// </summary>
        /// <param name="propertyPath">策略字段完整序列化路径。</param>
        /// <returns>当前字段模块是否处于展开状态。</returns>
        private bool GetStrategyFieldFoldout(string propertyPath)
        {
            if (_strategyFieldFoldouts.TryGetValue(propertyPath, out bool expanded)) return expanded;

            // 新增策略字段首次显示时展开，确保配置不会因为缺少专用 Editor 而被隐藏。
            _strategyFieldFoldouts.Add(propertyPath, true);
            return true;
        }

        /// <summary>
        /// 获取策略字段声明的中文模块标题；没有特性时回退到 Unity 默认字段显示名称。
        /// </summary>
        /// <param name="strategyType">当前 SerializeReference 策略的实际类型。</param>
        /// <param name="property">当前策略一级序列化属性。</param>
        /// <returns>特性声明的模块标题，或默认字段显示名称。</returns>
        private static string GetStrategyModuleTitle(Type strategyType, SerializedProperty property)
        {
            if (property == null) return string.Empty;
            if (strategyType == null || string.IsNullOrEmpty(property.name)) return property.displayName;

            // 缓存键使用完整属性路径，避免 SerializeReference 内部属性名与用户字段名偶发重名。
            string cacheKey = strategyType.AssemblyQualifiedName + ":" + property.propertyPath;
            if (_strategyModuleTitles.TryGetValue(cacheKey, out string title))
                return title;

            // 优先按 Unity 提供的属性名精确查找；正常策略字段会在此路径命中。
            FieldInfo field = FindStrategyField(strategyType, property.name);
            if (field == null)
                field = FindStrategyFieldByPropertyPath(strategyType, property.propertyPath);
            UnitMovementModuleNameAttribute attribute = field != null
                ? field.GetCustomAttribute<UnitMovementModuleNameAttribute>()
                : null;
            title = attribute != null && !string.IsNullOrWhiteSpace(attribute.DisplayName)
                ? attribute.DisplayName
                : property.displayName;
            _strategyModuleTitles[cacheKey] = title;
            return title;
        }

        /// <summary>
        /// 在策略继承链中查找声明指定字段的类型成员。
        /// </summary>
        /// <param name="strategyType">需要检索的具体策略类型。</param>
        /// <param name="fieldName">目标字段名称。</param>
        /// <returns>找到的字段反射信息；未找到时返回 null。</returns>
        private static FieldInfo FindStrategyField(Type strategyType, string fieldName)
        {
            // 逐层检索可序列化策略的私有字段，支持未来由中间策略基类声明模块字段。
            for (Type currentType = strategyType;
                 currentType != null && typeof(UnitMovementStrategy).IsAssignableFrom(currentType);
                 currentType = currentType.BaseType)
            {
                FieldInfo field = currentType.GetField(
                    fieldName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null) return field;
            }

            return null;
        }

        /// <summary>
        /// 在 Unity 的完整序列化路径中反查策略字段，兼容 SerializeReference 暴露内部属性名的情况。
        /// </summary>
        /// <param name="strategyType">需要检索的具体策略类型。</param>
        /// <param name="propertyPath">Unity 提供的完整序列化属性路径。</param>
        /// <returns>与路径末尾字段匹配的策略字段；未找到时返回 null。</returns>
        private static FieldInfo FindStrategyFieldByPropertyPath(Type strategyType, string propertyPath)
        {
            if (string.IsNullOrEmpty(propertyPath)) return null;

            // 仅在精确名称未命中时遍历策略字段，避免常规 Inspector 重绘承担额外反射开销。
            for (Type currentType = strategyType;
                 currentType != null && typeof(UnitMovementStrategy).IsAssignableFrom(currentType);
                 currentType = currentType.BaseType)
            {
                FieldInfo[] fields = currentType.GetFields(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                foreach (FieldInfo field in fields)
                {
                    if (propertyPath.EndsWith("." + field.Name, StringComparison.Ordinal)
                        || string.Equals(propertyPath, field.Name, StringComparison.Ordinal))
                        return field;
                }
            }

            return null;
        }

        /// <summary>
        /// 开始一个带完整横向标题栏的可折叠模块面板。
        /// </summary>
        /// <param name="expanded">该模块当前展开状态。</param>
        /// <param name="title">标题栏显示文本。</param>
        /// <returns>用户操作后的展开状态。</returns>
        private bool BeginModulePanel(bool expanded, string title)
        {
            // 标题栏独占完整宽度，折叠箭头预留左边距以避免 Unity 内置样式向外溢出。
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            Rect headerRect = GUILayoutUtility.GetRect(
                GUIContent.none,
                GUIStyle.none,
                GUILayout.ExpandWidth(true),
                GUILayout.Height(ModuleHeaderHeight));
            EditorGUI.DrawRect(
                headerRect,
                EditorGUIUtility.isProSkin ? ProSkinModuleHeaderColor : LightSkinModuleHeaderColor);
            Rect foldoutRect = new Rect(
                headerRect.x + 20f,
                headerRect.y + 3f,
                headerRect.width - 25f,
                EditorGUIUtility.singleLineHeight);
            bool foldout = EditorGUI.Foldout(foldoutRect, expanded, title, true, GetModuleFoldoutStyle());
            EditorGUILayout.Space(2f);
            return foldout;
        }

        /// <summary>
        /// 在 Inspector GUI 样式可用时首次创建并缓存粗体折叠标题样式。
        /// </summary>
        /// <returns>可安全用于当前 Inspector 绘制的折叠标题样式。</returns>
        private GUIStyle GetModuleFoldoutStyle()
        {
            if (_moduleFoldoutStyle != null) return _moduleFoldoutStyle;

            // 样式仅在实际绘制阶段读取 EditorStyles，避免 ScriptableObject 构造期间访问皮肤。
            _moduleFoldoutStyle = new GUIStyle(EditorStyles.foldout)
            {
                fontStyle = FontStyle.Bold
            };
            return _moduleFoldoutStyle;
        }

        /// <summary>
        /// 结束当前模块面板并保留稳定的分区间距。
        /// </summary>
        private static void EndModulePanel()
        {
            // 与 BeginModulePanel 成对闭合，防止下一个分区嵌入当前边框。
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2f);
        }
    }
}
