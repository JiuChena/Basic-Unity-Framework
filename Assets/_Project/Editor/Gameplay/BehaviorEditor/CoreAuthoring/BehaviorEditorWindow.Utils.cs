using System;
using UnityEngine;

namespace BehaviorEditor
{
    internal sealed partial class BehaviorEditorWindow : UnityEditor.EditorWindow
    {
        /// <summary>
        /// 克隆 Hitbox 定义，并注入时间轴起止时间与轨道名。
        /// </summary>
        /// <param name="source">源 Hitbox 定义；为 null 时创建默认定义。</param>
        /// <param name="timelineStartTime">时间轴起点，单位为秒。</param>
        /// <param name="timelineDuration">时间轴持续时间，单位为秒。</param>
        /// <param name="trackName">来源轨道名；为空时保留源定义的名称。</param>
        /// <returns>用于运行时导出的独立 Hitbox 定义。</returns>
        internal static HitboxDef CloneHitboxDef(HitboxDef source, float timelineStartTime, float timelineDuration,
            string trackName = null)
        {
            HitboxDef cloned = new HitboxDef();
            if (source != null)
            {
                // 复制作者期定义，不回写 Timeline 片段资产。
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
        /// <returns>适合作为 Unity 资产文件名的名称。</returns>
        private static string SanitizeAssetName(string rawName)
        {
            const string fallback = "TimelineBehaviorClip";
            if (string.IsNullOrWhiteSpace(rawName)) return fallback;

            // 删除 Windows 不允许出现在文件名中的字符。
            string trimmed = rawName.Trim();
            foreach (char invalidChar in System.IO.Path.GetInvalidFileNameChars())
                trimmed = trimmed.Replace(invalidChar.ToString(), string.Empty);

            return string.IsNullOrWhiteSpace(trimmed) ? fallback : trimmed;
        }

        /// <summary>
        /// 确保资产文件夹存在，缺失时逐级创建。
        /// </summary>
        /// <param name="folderPath">Unity 资产目录路径。</param>
        /// <returns>规范化后的有效资产目录路径。</returns>
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
