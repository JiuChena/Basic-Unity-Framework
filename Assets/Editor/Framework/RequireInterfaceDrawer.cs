using UnityEngine;
using UnityEditor;
using CoreFramework;

namespace CoreFrameworkEditor
{
    [CustomPropertyDrawer(typeof(RequireInterfaceAttribute))]
    public class RequireInterfaceDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var attr = (RequireInterfaceAttribute)attribute;

            EditorGUI.BeginProperty(position, label, property);

            float singleHeight = EditorGUIUtility.singleLineHeight;
            Rect fieldRect = new Rect(position.x, position.y, position.width, singleHeight);

            EditorGUI.BeginChangeCheck();
            var newValue = EditorGUI.ObjectField(fieldRect, label, property.objectReferenceValue, typeof(MonoBehaviour), true);
            if (EditorGUI.EndChangeCheck())
                property.objectReferenceValue = newValue;

            if (property.objectReferenceValue != null)
            {
                var obj = property.objectReferenceValue;
                var objType = obj.GetType();
                if (!attr.InterfaceType.IsAssignableFrom(objType))
                {
                    Rect helpRect = new Rect(position.x, position.y + singleHeight + 2, position.width, singleHeight * 2);
                    EditorGUI.HelpBox(helpRect,
                        $"\"{obj.name}\" ({objType.Name}) 未实现 {attr.InterfaceType.Name} 接口。",
                        MessageType.Error);
                }
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float height = EditorGUIUtility.singleLineHeight;

            if (property.objectReferenceValue != null)
            {
                var attr = (RequireInterfaceAttribute)attribute;
                var objType = property.objectReferenceValue.GetType();
                if (!attr.InterfaceType.IsAssignableFrom(objType))
                    height += EditorGUIUtility.singleLineHeight * 2 + 2;
            }

            return height;
        }
    }
}
