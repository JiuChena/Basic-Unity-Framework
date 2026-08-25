#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// BattleGlobalSettings 资源的编辑器辅助工具。
/// </summary>
public static class BattleGlobalSettingsEditorUtility
{
    private const string AssetFolder = "Assets/ScriptableObjects/Global Settings";
    private const string AssetPath = AssetFolder + "/BattleGlobalSettings.asset";

    [MenuItem("Framework/Gameplay/Ensure Battle Global Settings Asset")]
    public static void EnsureBattleGlobalSettingsAsset()
    {
        EnsureFolder(AssetFolder);

        BattleGlobalSettingsSO existing = AssetDatabase.LoadAssetAtPath<BattleGlobalSettingsSO>(AssetPath);
        if (existing != null)
        {
            Selection.activeObject = existing;
            EditorGUIUtility.PingObject(existing);
            return;
        }

        BattleGlobalSettingsSO asset = ScriptableObject.CreateInstance<BattleGlobalSettingsSO>();
        asset.name = "BattleGlobalSettings";
        AssetDatabase.CreateAsset(asset, AssetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = asset;
        EditorGUIUtility.PingObject(asset);
        Debug.Log($"已创建 BattleGlobalSettings 资源：{AssetPath}。接下来请把它手动拖到场景中的 GlobalConfigManager 上。");
    }

    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
            return;

        string normalizedPath = folderPath.Replace("\\", "/");
        string[] parts = normalizedPath.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);

            current = next;
        }
    }
}
#endif
