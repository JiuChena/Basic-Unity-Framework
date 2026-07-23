using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using CoreFramework;

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

        private void OnEnable()
        {
            RefreshStrategyPreview();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var mover = (UnitMover)target;

            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.ObjectField("Script", MonoScript.FromMonoBehaviour(mover), typeof(MonoScript), false);
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Movement Strategy", EditorStyles.boldLabel);

            // 解析当前选中的脚本
            Type currentType = null;
            _currentScript = null;
            if (!string.IsNullOrEmpty(mover.StrategyTypeName))
            {
                currentType = Type.GetType(mover.StrategyTypeName);
                if (currentType != null)
                    _currentScript = FindScriptFromType(currentType);
            }

            // ── 策略选择行：拖拽字段 + 下拉按钮，双向同步 ──
            EditorGUILayout.BeginHorizontal();

            // 拖拽字段
            var newScript = (MonoScript)EditorGUILayout.ObjectField(
                new GUIContent("Movement Strategy", "拖入继承 MovementStrategy 的 .cs 脚本文件"),
                _currentScript,
                typeof(MonoScript),
                false);

            // 下拉按钮
            if (GUILayout.Button("▼", GUILayout.Width(22)))
                ShowStrategyMenu(mover);

            EditorGUILayout.EndHorizontal();

            // 拖拽变更检测
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

            // ── 策略参数字段 ──
            if (_strategyPreview != null && _strategyFields.Count > 0)
            {
                EditorGUI.indentLevel++;
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
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();

            // 排除内部序列化字段
            var excluded = new[] { "m_Script", "_rigidbody", "_strategyTypeName", "_strategyParams" };
            DrawPropertiesExcluding(serializedObject, excluded);

            serializedObject.ApplyModifiedProperties();
            if (GUI.changed) SceneView.RepaintAll();
        }

        private void ShowStrategyMenu(UnitMover mover)
        {
            var scripts = GetMovementStrategyScripts();
            var menu = new GenericMenu();
            foreach (var script in scripts)
            {
                var captured = script;
                bool isCurrent = _currentScript == captured;
                menu.AddItem(new GUIContent(captured.name), isCurrent, () =>
                {
                    SelectStrategy(mover, captured);
                });
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
                    var label = new GUIContent(ObjectNames.NicifyVariableName(field.Name),
                        tooltip?.tooltip ?? "");
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
