using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 材质关键字对比工具：选中多个材质（或带渲染器的物体），列出各自启用的 shader keyword，
/// 并标记与基准（第一个材质）不同的差异项。用于排查 SRP Batcher 合批失败的变体差异。
/// </summary>
public static class MaterialKeywordCompareTool
{
    /// <summary>
    /// 对比选中材质的启用的 shader keyword：Hierarchy/Project 中选中材质或物体后执行。
    /// 基准取第一个材质，其余材质与其比较，结果输出到 Console。
    /// </summary>
    [MenuItem("Tools/Material Keywords Compare")]
    public static void Compare()
    {
        // 收集选中的材质（材质本体 + 物体上的 renderer 材质）
        var materials = CollectSelectedMaterials();
        if (materials.Count < 2)
        {
            Debug.LogWarning("[MaterialKeywordCompare] 请选中至少两个材质（或含渲染器的物体）");
            return;
        }

        // 每个材质的 keyword 列表（排序后拼接，便于文本对比）
        var entries = new List<KeyValuePair<string, string>>(materials.Count);
        foreach (var mat in materials)
        {
            var keywords = mat.shaderKeywords.OrderBy(k => k, System.StringComparer.Ordinal);
            entries.Add(new KeyValuePair<string, string>(mat.name, string.Join(" ", keywords)));
        }

        // 以第一个材质为基准对比，输出相同/差异清单
        string baseline = entries[0].Value;
        var sb = new StringBuilder();
        sb.AppendLine($"[MaterialKeywordCompare] 共 {entries.Count} 个材质，基准: {entries[0].Key}");
        int sameCount = 0;
        foreach (var e in entries)
        {
            bool same = e.Value == baseline;
            if (same) sameCount++;
            sb.AppendLine($"{(same ? "[相同]" : "[差异]")} {e.Key}: {e.Value}");
        }
        sb.AppendLine($"相同: {sameCount}/{entries.Count}");
        Debug.Log(sb.ToString());
    }

    /// <summary>
    /// 从当前选中对象收集材质：材质本体直接加入；GameObject 收集其下所有 renderer 的 sharedMaterial（去重）。
    /// </summary>
    /// <returns>去重后的材质列表；无选中或无材质时返回空列表。</returns>
    private static List<Material> CollectSelectedMaterials()
    {
        var materials = new List<Material>();
        foreach (var obj in Selection.objects)
        {
            if (obj is Material mat)
            {
                if (!materials.Contains(mat)) materials.Add(mat);
            }
            else if (obj is GameObject go)
            {
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                {
                    if (r.sharedMaterial != null && !materials.Contains(r.sharedMaterial))
                        materials.Add(r.sharedMaterial);
                }
            }
        }
        return materials;
    }
}
