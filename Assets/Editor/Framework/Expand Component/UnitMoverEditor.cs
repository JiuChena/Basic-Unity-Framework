using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Framework.Core;

namespace CoreFrameworkEditor
{
    [CustomEditor(typeof(UnitMover))]
    public class UnitMoverEditor : Editor
    {
        private MovementStrategy _strategyPreview;
        private Type _lastStrategyType;
        private readonly Dictionary<string, (FieldInfo field, GUIContent label)> _strategyFields = new();
        private static List<MonoScript> _cachedScripts;
        private MonoScript _currentScript;
        private string _validationMessage;

        // --- 折叠状态 ---
        private bool _foldoutStrategy = true;
        private bool _foldoutComponent = true;
        private bool _foldoutGround = true;
        private bool _foldoutAir = true;
        private bool _foldoutPreview = true;
        private bool _foldoutFloatingCapsule = true;

        /// <summary>
        /// 模块分组定义：Header 名称 → 序列化字段名列表。
        /// </summary>
        private static readonly Dictionary<string, string[]> ModuleGroups = new()
        {
            { "组件引用", new[] { "movementCollider", "cameraTransform", "dataProviderSource" } },
            { "地面移动", new[] { "moveSpeed", "sprintMultiplier", "groundAcceleration", "groundDeceleration", "hoverHeight", "groundProbeDistance", "slopeLimit", "springStrength", "springDamping", "stepHeight", "groundLayer" } },
            { "空中行为", new[] { "jumpSpeed", "gravityMultiplier", "airAcceleration", "airControl", "airSpeedLimit", "ledgeCheckEnabled", "maxFallHeight" } },
            { "编辑器预览", new[] { "showHoverPreview" } },
            { "Floating Capsule", new[] { "enableFloatingCapsule", "floatingBottomClearance", "floatingCapsuleDefaultCenter", "floatingCapsuleDefaultHeight", "floatingCapsuleDefaultRadius", "floatingCapsuleDefaultDirection" } },
        };

        private void OnEnable()
        {
            RefreshStrategyPreview();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var mover = (UnitMover)target;

            DrawScriptHeader(mover);
            DrawStrategyFoldout(mover);
            DrawModuleFoldouts();

            serializedObject.ApplyModifiedProperties();
            if (GUI.changed) SceneView.RepaintAll();
        }

        // ──────────────────────────── Script 只读行 ────────────────────────────

        private void DrawScriptHeader(UnitMover mover)
        {
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.ObjectField("Script", MonoScript.FromMonoBehaviour(mover), typeof(MonoScript), false);
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.Space();
        }

        // ──────────────────────────── 策略折叠 ────────────────────────────

        private void DrawStrategyFoldout(UnitMover mover)
        {
            _foldoutStrategy = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutStrategy, "Movement Strategy");
            if (!_foldoutStrategy)
            {
                EditorGUILayout.EndFoldoutHeaderGroup();
                return;
            }

            EditorGUI.indentLevel++;

            // 解析当前选中的脚本
            _currentScript = null;
            if (!string.IsNullOrEmpty(mover.StrategyTypeName))
            {
                var currentType = Type.GetType(mover.StrategyTypeName);
                if (currentType != null)
                    _currentScript = FindScriptFromType(currentType);
            }

            // 拖拽字段 + 下拉按钮
            EditorGUILayout.BeginHorizontal();
            var newScript = (MonoScript)EditorGUILayout.ObjectField(
                new GUIContent("策略脚本", "拖入继承 MovementStrategy 的 .cs 脚本文件"),
                _currentScript,
                typeof(MonoScript),
                false);
            if (GUILayout.Button("▼", GUILayout.Width(22)))
                ShowStrategyMenu(mover);
            EditorGUILayout.EndHorizontal();

            if (newScript != _currentScript)
            {
                if (newScript == null)
                {
                    Undo.RecordObject(mover, "Clear Movement Strategy");
                    mover.StrategyTypeName = "";
                    _currentScript = null;
                    _validationMessage = null;
                    RefreshStrategyPreview();
                    EditorUtility.SetDirty(mover);
                }
                else
                {
                    var type = newScript.GetClass();
                    if (IsConcreteMovementStrategy(type))
                    {
                        _validationMessage = null;
                        SelectStrategy(mover, newScript);
                    }
                    else
                    {
                        _validationMessage = $"{newScript.name} 不是可实例化的 MovementStrategy 子类。";
                    }
                }
            }

            if (!string.IsNullOrEmpty(_validationMessage))
                EditorGUILayout.HelpBox(_validationMessage, MessageType.Warning);

            // 策略公开参数字段
            if (_strategyPreview != null && _strategyFields.Count > 0)
            {
                EditorGUILayout.Space(4);
                foreach (var kvp in _strategyFields)
                {
                    var field = kvp.Value.field;
                    var label = kvp.Value.label;
                    var value = field.GetValue(_strategyPreview);
                    var newValue = DrawField(field.FieldType, label, value);
                    if (!Equals(value, newValue))
                    {
                        field.SetValue(_strategyPreview, newValue);
                        SaveStrategyParams(mover);
                        EditorUtility.SetDirty(mover);
                    }
                }
            }

            EditorGUI.indentLevel--;
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        // ──────────────────────────── 模块折叠 ────────────────────────────

        private void DrawModuleFoldouts()
        {
            DrawPropertyFoldout("组件引用", ref _foldoutComponent);
            DrawPropertyFoldout("地面移动", ref _foldoutGround);
            DrawPropertyFoldout("空中行为", ref _foldoutAir);
            DrawPropertyFoldout("Floating Capsule", ref _foldoutFloatingCapsule);
            DrawPropertyFoldout("编辑器预览", ref _foldoutPreview);
        }

        private void DrawPropertyFoldout(string header, ref bool foldoutState)
        {
            if (!ModuleGroups.TryGetValue(header, out string[] fieldNames))
                return;

            foldoutState = EditorGUILayout.BeginFoldoutHeaderGroup(foldoutState, header);
            if (foldoutState)
            {
                EditorGUI.indentLevel++;
                foreach (string fieldName in fieldNames)
                {
                    SerializedProperty sp = serializedObject.FindProperty(fieldName);
                    if (sp != null)
                        EditorGUILayout.PropertyField(sp, true);
                }
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        // ──────────────────────────── 策略选择器 ────────────────────────────

        private void ShowStrategyMenu(UnitMover mover)
        {
            var scripts = GetMovementStrategyScripts();
            var menu = new GenericMenu();
            foreach (var script in scripts)
            {
                var captured = script;
                bool isCurrent = _currentScript == captured;
                menu.AddItem(new GUIContent(captured.name), isCurrent, () => SelectStrategy(mover, captured));
            }
            if (scripts.Count == 0)
                menu.AddDisabledItem(new GUIContent("未找到 MovementStrategy 子类"));
            menu.ShowAsContext();
        }

        private void SelectStrategy(UnitMover mover, MonoScript script)
        {
            var type = script.GetClass();
            if (!IsConcreteMovementStrategy(type)) return;

            Undo.RecordObject(mover, "Change Movement Strategy");
            var prop = serializedObject.FindProperty("_strategyTypeName");
            prop.stringValue = type.AssemblyQualifiedName;
            serializedObject.ApplyModifiedProperties();
            mover.StrategyTypeName = type.AssemblyQualifiedName;
            _currentScript = script;
            _strategyPreview = null;
            _lastStrategyType = null;
            RefreshStrategyPreview();
            EditorUtility.SetDirty(mover);
        }

        private void RefreshStrategyPreview()
        {
            var mover = (UnitMover)target;
            var typeName = mover.StrategyTypeName;
            if (string.IsNullOrEmpty(typeName))
            {
                _strategyPreview = null;
                _lastStrategyType = null;
                _strategyFields.Clear();
                return;
            }

            var type = Type.GetType(typeName);
            if (!IsConcreteMovementStrategy(type))
            {
                _strategyPreview = null;
                _lastStrategyType = null;
                _strategyFields.Clear();
                return;
            }

            if (_lastStrategyType != type || _strategyPreview == null)
            {
                _strategyPreview = (MovementStrategy)Activator.CreateInstance(type);
                _lastStrategyType = type;
                LoadStrategyParams(mover);
                _strategyFields.Clear();
                foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (field.IsLiteral || field.IsInitOnly) continue;
                    var tooltip = field.GetCustomAttribute<TooltipAttribute>();
                    var label = new GUIContent(ObjectNames.NicifyVariableName(field.Name), tooltip?.tooltip ?? "");
                    _strategyFields[field.Name] = (field, label);
                }
            }
        }

        private void LoadStrategyParams(UnitMover mover)
        {
            if (_strategyPreview == null) return;
            var type = _strategyPreview.GetType();
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (field.IsLiteral || field.IsInitOnly) continue;
                var savedValue = mover.GetStrategyParam(field.Name, field.FieldType);
                if (savedValue != null)
                    field.SetValue(_strategyPreview, savedValue);
            }
        }

        private void SaveStrategyParams(UnitMover mover)
        {
            if (_strategyPreview == null) return;
            var type = _strategyPreview.GetType();
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (field.IsLiteral || field.IsInitOnly) continue;
                mover.SetStrategyParam(field.Name, field.GetValue(_strategyPreview));
            }
        }

        private static List<MonoScript> GetMovementStrategyScripts()
        {
            if (_cachedScripts != null) return _cachedScripts;
            _cachedScripts = new List<MonoScript>();
            var guids = AssetDatabase.FindAssets("t:script");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (script == null) continue;
                var type = script.GetClass();
                if (IsConcreteMovementStrategy(type))
                    _cachedScripts.Add(script);
            }
            return _cachedScripts;
        }

        private static MonoScript FindScriptFromType(Type type)
        {
            foreach (var script in GetMovementStrategyScripts())
            {
                if (script.GetClass() == type)
                    return script;
            }
            return null;
        }

        private static bool IsConcreteMovementStrategy(Type type)
        {
            return type != null
                && typeof(MovementStrategy).IsAssignableFrom(type)
                && type != typeof(MovementStrategy)
                && !type.IsAbstract;
        }

        private static object DrawField(Type fieldType, GUIContent label, object value)
        {
            return fieldType switch
            {
                _ when fieldType == typeof(bool) =>
                    EditorGUILayout.Toggle(label, value is bool b ? b : false),

                _ when fieldType == typeof(float) =>
                    EditorGUILayout.FloatField(label, value is float f ? f : 0f),

                _ when fieldType == typeof(int) =>
                    EditorGUILayout.IntField(label, value is int i ? i : 0),

                _ when fieldType == typeof(string) =>
                    EditorGUILayout.TextField(label, value as string ?? ""),

                _ when fieldType.IsEnum =>
                    EditorGUILayout.EnumPopup(label, value as Enum ?? (Enum)Activator.CreateInstance(fieldType)),

                _ => value
            };
        }
    }
}
