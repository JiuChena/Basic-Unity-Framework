using System;
using System.Collections.Generic;
using System.IO;
using Core.Gear;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace BehaviorEditor
{
    internal sealed partial class BehaviorEditorWindow : UnityEditor.EditorWindow
    {

        /// <summary>
        /// 读取对象的浮点序列化属性并钳制到 0-1。
        /// </summary>
        /// <param name="targetObject">目标对象。</param>
        /// <param name="propertyName">属性名。</param>
        /// <param name="fallbackValue">属性缺失时的回退值。</param>
        /// <returns>读取并钳制后的浮点值。</returns>
        private static float ReadClampedFloatSerializedProperty( UnityEngine.Object targetObject, string propertyName, float fallbackValue)
        {
            return ReadSerializedPropertyValue(
                targetObject,
                propertyName,
                fallbackValue,
                property => Mathf.Clamp01(property.floatValue));
        }

        /// <summary>
        /// 读取对象的整型序列化属性。
        /// </summary>
        /// <param name="targetObject">目标对象。</param>
        /// <param name="propertyName">属性名。</param>
        /// <param name="fallbackValue">属性缺失时的回退值。</param>
        /// <returns>读取的整型值。</returns>
        private static int ReadIntSerializedProperty( UnityEngine.Object targetObject, string propertyName, int fallbackValue)
        {
            return ReadSerializedPropertyValue(targetObject, propertyName, fallbackValue, property => property.intValue);
        }

        /// <summary>
        /// 读取序列化属性值并应用读取委托；属性缺失时返回回退值。
        /// </summary>
        /// <typeparam name="TResult">返回值类型。</typeparam>
        /// <param name="targetObject">目标对象。</param>
        /// <param name="propertyName">属性名。</param>
        /// <param name="fallbackValue">属性缺失时的回退值。</param>
        /// <param name="readValue">属性读取委托。</param>
        /// <returns>读取结果或回退值。</returns>
        private static TResult ReadSerializedPropertyValue<TResult>( UnityEngine.Object targetObject, string propertyName, TResult fallbackValue, Func<UnityEditor.SerializedProperty, TResult> readValue)
        {
            if (targetObject == null || readValue == null)
                return fallbackValue;

            using (UnityEditor.SerializedObject serializedObject = new UnityEditor.SerializedObject(targetObject))
            {
                return TryGetSerializedProperty(serializedObject, propertyName, out UnityEditor.SerializedProperty property)
                    ? readValue(property)
                    : fallbackValue;
            }
        }

        /// <summary>
        /// 设置对象引用类型的序列化属性值。
        /// </summary>
        /// <param name="serializedObject">序列化对象。</param>
        /// <param name="propertyName">属性名。</param>
        /// <param name="value">对象引用值。</param>
        private static void SetSerializedPropertyValue(UnityEditor.SerializedObject serializedObject, string propertyName, UnityEngine.Object value)
        {
            if (!TryGetSerializedProperty(serializedObject, propertyName, out UnityEditor.SerializedProperty property))
                return;

            property.objectReferenceValue = value;
        }

        /// <summary>
        /// 设置布尔类型的序列化属性值。
        /// </summary>
        /// <param name="serializedObject">序列化对象。</param>
        /// <param name="propertyName">属性名。</param>
        /// <param name="value">布尔值。</param>
        private static void SetSerializedPropertyValue(UnityEditor.SerializedObject serializedObject, string propertyName, bool value)
        {
            if (!TryGetSerializedProperty(serializedObject, propertyName, out UnityEditor.SerializedProperty property))
                return;

            property.boolValue = value;
        }

        /// <summary>
        /// 设置整型序列化属性值。
        /// </summary>
        /// <param name="serializedObject">序列化对象。</param>
        /// <param name="propertyName">属性名。</param>
        /// <param name="value">整型值。</param>
        private static void SetSerializedPropertyValue(UnityEditor.SerializedObject serializedObject, string propertyName, int value)
        {
            if (!TryGetSerializedProperty(serializedObject, propertyName, out UnityEditor.SerializedProperty property))
                return;

            property.intValue = value;
        }

        /// <summary>
        /// 设置浮点序列化属性值。
        /// </summary>
        /// <param name="serializedObject">序列化对象。</param>
        /// <param name="propertyName">属性名。</param>
        /// <param name="value">浮点值。</param>
        private static void SetSerializedPropertyValue(UnityEditor.SerializedObject serializedObject, string propertyName, float value)
        {
            if (!TryGetSerializedProperty(serializedObject, propertyName, out UnityEditor.SerializedProperty property))
                return;

            property.floatValue = value;
        }

        /// <summary>
        /// 尝试获取序列化属性。
        /// </summary>
        /// <param name="serializedObject">序列化对象。</param>
        /// <param name="propertyName">属性名。</param>
        /// <param name="property">输出的属性。</param>
        /// <returns>属性存在时返回 true。</returns>
        private static bool TryGetSerializedProperty(UnityEditor.SerializedObject serializedObject, string propertyName, out UnityEditor.SerializedProperty property)
        {
            property = null;
            if (serializedObject == null || string.IsNullOrWhiteSpace(propertyName))
                return false;

            property = serializedObject.FindProperty(propertyName);
            return property != null;
        }

        /// <summary>
        /// 构建目标对象相对参考根节点的层级路径。
        /// </summary>
        /// <param name="referenceRoot">参考根节点。</param>
        /// <param name="targetObject">目标对象。</param>
        /// <returns>相对路径；参数无效时返回空字符串。</returns>
        private static string BuildRelativeAuthoringObjectPath(Transform referenceRoot, GameObject targetObject)
        {
            if (referenceRoot == null || targetObject == null)
                return string.Empty;

            return BehaviorReferenceBoneEditorUtility.BuildRelativeBonePath(referenceRoot, targetObject.transform);
        }

        /// <summary>
        /// 安全地逐分量相除，除数接近零时保留原值。
        /// </summary>
        /// <param name="value">被除数。</param>
        /// <param name="divisor">除数。</param>
        /// <returns>逐分量相除结果。</returns>
        private static Vector3 DivideVector3Safely(Vector3 value, Vector3 divisor)
        {
            return new Vector3(
                Mathf.Abs(divisor.x) > 0.0001f ? value.x / divisor.x : value.x,
                Mathf.Abs(divisor.y) > 0.0001f ? value.y / divisor.y : value.y,
                Mathf.Abs(divisor.z) > 0.0001f ? value.z / divisor.z : value.z);
        }

        /// <summary>
        /// 比较两个时间轴条目：先比起点，再比轨道名；引用比较先行。
        /// </summary>
        /// <typeparam name="T">条目类型。</typeparam>
        /// <param name="left">左侧条目。</param>
        /// <param name="right">右侧条目。</param>
        /// <param name="leftStartTime">左侧起点。</param>
        /// <param name="rightStartTime">右侧起点。</param>
        /// <param name="leftTrackName">左侧轨道名。</param>
        /// <param name="rightTrackName">右侧轨道名。</param>
        /// <param name="result">输出的比较结果。</param>
        /// <returns>已得出明确结果时返回 true。</returns>
        private static bool TryCompareTimedTrackItems<T>(
            T left,
            T right,
            float leftStartTime,
            float rightStartTime,
            string leftTrackName,
            string rightTrackName,
            out int result)
            where T : class
        {
            if (TryCompareNullReferences(left, right, out result))
                return true;

            result = leftStartTime.CompareTo(rightStartTime);
            if (result != 0)
                return true;

            result = CompareNullableStrings(leftTrackName, rightTrackName);
            return result != 0;
        }

        /// <summary>
        /// 比较两个动画段条目：先比起点与轨道名，再比层与片段名。
        /// </summary>
        /// <param name="left">左侧条目。</param>
        /// <param name="right">右侧条目。</param>
        /// <returns>比较结果。</returns>
        internal static int CompareAnimationSegmentEntries(AnimationSegmentEntry left, AnimationSegmentEntry right)
        {
            AnimationSegment leftSegment = left.segment;
            AnimationSegment rightSegment = right.segment;
            if (TryCompareTimedTrackItems(
                    left,
                    right,
                    left != null ? left.startTime : 0f,
                    right != null ? right.startTime : 0f,
                    leftSegment?.authoringTrackName,
                    rightSegment?.authoringTrackName,
                    out int result))
            {
                return result;
            }

            result = (leftSegment?.layer ?? 0).CompareTo(rightSegment?.layer ?? 0);
            if (result != 0)
                return result;

            return CompareNullableStrings(leftSegment?.clip?.name, rightSegment?.clip?.name);
        }

        /// <summary>
        /// 比较两个行为事件：先比时间与轨道名，再比有效类型、骨骼与目标路径。
        /// </summary>
        /// <param name="left">左侧事件。</param>
        /// <param name="right">右侧事件。</param>
        /// <returns>比较结果。</returns>
        internal static int CompareBehaviorEvents(BehaviorEvent left, BehaviorEvent right)
        {
            if (TryCompareTimedTrackItems(
                    left,
                    right,
                    left != null ? left.time : 0f,
                    right != null ? right.time : 0f,
                    left?.authoringTrackName,
                    right?.authoringTrackName,
                    out int result))
            {
                return result;
            }

            result = ((int)BehaviorEventResolver.ResolveEffectiveType(left))
                .CompareTo((int)BehaviorEventResolver.ResolveEffectiveType(right));
            if (result != 0)
                return result;

            result = CompareNullableStrings(left.referenceBone, right.referenceBone);
            if (result != 0)
                return result;

            return CompareNullableStrings(left.targetObjectPath, right.targetObjectPath);
        }

        /// <summary>
        /// 比较两个 Hitbox：先比起点与轨道名，再比名称。
        /// </summary>
        /// <param name="left">左侧 Hitbox。</param>
        /// <param name="right">右侧 Hitbox。</param>
        /// <returns>比较结果。</returns>
        internal static int CompareHitboxes(HitboxDef left, HitboxDef right)
        {
            if (TryCompareTimedTrackItems(
                    left,
                    right,
                    left != null ? left.startTime : 0f,
                    right != null ? right.startTime : 0f,
                    left?.authoringTrackName,
                    right?.authoringTrackName,
                    out int result))
            {
                return result;
            }

            return CompareNullableStrings(left.name, right.name);
        }
        /// <summary>
        /// 比较两个引用是否相同或其一为空。
        /// </summary>
        /// <typeparam name="T">引用类型。</typeparam>
        /// <param name="left">左侧引用。</param>
        /// <param name="right">右侧引用。</param>
        /// <param name="result">输出的比较结果。</param>
        /// <returns>已得出明确结果时返回 true。</returns>
        private static bool TryCompareNullReferences<T>(T left, T right, out int result) where T : class
        {
            if (ReferenceEquals(left, right))
            {
                result = 0;
                return true;
            }

            if (left == null)
            {
                result = 1;
                return true;
            }

            if (right == null)
            {
                result = -1;
                return true;
            }

            result = 0;
            return false;
        }

        /// <summary>
        /// 比较两个可空字符串，空值排在后面。
        /// </summary>
        /// <param name="left">左侧字符串。</param>
        /// <param name="right">右侧字符串。</param>
        /// <returns>比较结果。</returns>
        private static int CompareNullableStrings(string left, string right)
        {
            if (string.IsNullOrEmpty(left) && string.IsNullOrEmpty(right))
                return 0;
            if (string.IsNullOrEmpty(left))
                return 1;
            if (string.IsNullOrEmpty(right))
                return -1;

            return string.Compare(left, right, StringComparison.Ordinal);
        }

        /// <summary>
        /// 克隆 Hitbox 定义，并注入时间轴起止时间与轨道名。
        /// </summary>
        /// <param name="source">源 Hitbox 定义。</param>
        /// <param name="timelineStartTime">时间轴起点。</param>
        /// <param name="timelineDuration">时间轴时长。</param>
        /// <param name="trackName">来源轨道名。</param>
        /// <returns>克隆出的 Hitbox 定义。</returns>
        internal static HitboxDef CloneHitboxDef(
            HitboxDef source,
            float timelineStartTime,
            float timelineDuration,
            string trackName = null)
        {
            HitboxDef cloned = new HitboxDef();
            if (source != null)
            {
                cloned.authoringTrackName = !string.IsNullOrWhiteSpace(trackName)
                    ? trackName
                    : source.authoringTrackName;
                cloned.name = source.name;
                cloned.shape = source.shape;

                cloned.referenceBone = source.referenceBone;
                cloned.positionOffset = source.positionOffset;
                cloned.rotationOffset = source.rotationOffset;
                cloned.scaleOffset = source.scaleOffset;
                cloned.size = source.size;
                cloned.execute = source.execute;
            }

            cloned.startTime = Mathf.Max(0f, timelineStartTime);
            cloned.duration = Mathf.Max(0f, timelineDuration);
            return cloned;
        }
        /// <summary>
        /// 清理资产名中的非法文件名字符，空名回退到默认名。
        /// </summary>
        /// <param name="rawName">原始资产名。</param>
        /// <returns>清理后的资产名。</returns>
        private static string SanitizeAssetName(string rawName)
        {
            string fallback = "TimelineBehaviorClip";
            if (string.IsNullOrWhiteSpace(rawName))
                return fallback;

            string trimmed = rawName.Trim();
            foreach (char invalidChar in System.IO.Path.GetInvalidFileNameChars())
                trimmed = trimmed.Replace(invalidChar.ToString(), string.Empty);

            return string.IsNullOrWhiteSpace(trimmed) ? fallback : trimmed;
        }

        /// <summary>
        /// 确保资产文件夹存在，缺失时逐级创建。
        /// </summary>
        /// <param name="folderPath">目标文件夹路径。</param>
        /// <returns>规范化后的文件夹路径。</returns>
        private static string EnsureFolder(string folderPath)
        {
            string normalized = string.IsNullOrWhiteSpace(folderPath)
                ? "Assets"
                : folderPath.Replace("\\", "/").TrimEnd('/');
            string[] parts = normalized.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!UnityEditor.AssetDatabase.IsValidFolder(next))
                    UnityEditor.AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }

            return current;
        }
    }
}
