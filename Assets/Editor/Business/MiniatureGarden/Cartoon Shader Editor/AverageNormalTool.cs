using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 平滑法线描边工具（方案 A：描边平滑，场景挂载组件引用版）。
/// 用途：为 Cartoon.shader 的描边（_OUTLINETYPE_NORMAL 模式）提供平滑外扩方向。
/// 原理：按"位置 + 法线夹角"双重判定合并硬边顶点 —— 组内法线夹角小于阈值（由角色上的
///       SmoothNormalBinder.MergeAngle 配置，默认 60°）才平均，保留大角度硬边、平滑小角度
///       转折；写回 mesh.normals 后描边与光照共用同一套平滑法线。
///       蒙皮时 Unity 会实时变换 NORMAL 通道，平均法线在任意动画姿势下方向都正确。
/// 方案说明：不再使用 JSON 映射文件，改为在角色根节点下的 "Smooth Normal Directions"
///      子物体上挂载 SmoothNormalBinder 组件，三列表（平滑克隆网格 ↔ 源网格 ↔ 被替换 renderer）
///      随 Prefab/场景序列化保存，引用天然安全、可随资产移动，不再依赖命名/路径反查。
/// 用法：Hierarchy 中选中角色根节点（含 MeshFilter / SkinnedMeshRenderer）→ Tools/NormalSmooth 三个入口：
///   - 生成平滑顶点网格并引用  ：克隆网格另存为独立 .asset 并替换引用，记录绑定
///   - 恢复网格引用            ：把 sharedMesh 指回原 FBX 子网格，删除克隆资产与绑定子物体
///   - 修改合并角度...         ：弹窗修改 SmoothNormalBinder.MergeAngle（重新生成后生效）
/// 说明：
/// - 持久化生成的克隆资产命名规则：{原网格名}_SmoothNormal.asset（存于源 FBX 同目录）。
/// - 写回后光照硬边阴影会变柔和（平滑法线所致），属预期行为。
/// - 量化精度 0.1mm + 角度阈值：避免薄壁双面结构（头发/裙摆）内外法线抵消成坏法线。
/// </summary>
public class AverageNormalTool
{
    private const string CloneSuffix = "_SmoothNormal";
    private const string BinderName = "Smooth Normal Directions";

    // ─────────────────────────── 菜单入口 ───────────────────────────

    [MenuItem("Tools/NormalSmooth/生成平滑顶点网格并引用")]
    public static void GenerateAndBind()
    {
        GameObject root = Selection.activeGameObject;
        if (root == null)
        {
            Debug.LogError("[AverageNormalTool] 请先在 Hierarchy 选中角色根节点");
            return;
        }
        GenerateAndBind(root);
    }

    public static void GenerateAndBind(GameObject root)
    {
        var binder = GetOrCreateBinder(root);

        // 若已有绑定 → 先恢复清空，避免重复绑定累积
        if (binder.SmoothedMeshes.Count > 0 || binder.BoundRenderers.Count > 0)
            RestoreBindings(binder);

        var refs = CollectMeshRefs(root);
        if (refs.Count == 0)
        {
            Debug.LogError("[AverageNormalTool] 未找到任何网格");
            return;
        }

        int savedCount = 0;
        foreach (var r in refs)
        {
            Mesh source = r.sourceMesh;
            if (source == null) continue;

            Vector3[] avgNormals = ComputeAverageNormals(source, binder.MergeAngle);
            if (avgNormals == null) continue;

            // 克隆独立网格资产 → 写入平均法线 → 替换引用
            Mesh clone = Object.Instantiate(source);
            clone.name = source.name + CloneSuffix;
            clone.normals = avgNormals;

            string sourcePath = AssetDatabase.GetAssetPath(source);
            string dir = string.IsNullOrEmpty(sourcePath) ? "Assets" : Path.GetDirectoryName(sourcePath);
            string path = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(dir, clone.name + ".asset"));
            AssetDatabase.CreateAsset(clone, path);

            if (r.skinned != null)
            {
                r.skinned.sharedMesh = clone;
                EditorUtility.SetDirty(r.skinned);
                if (PrefabUtility.IsPartOfPrefabInstance(r.skinned))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(r.skinned);
            }
            if (r.meshFilter != null)
            {
                r.meshFilter.sharedMesh = clone;
                EditorUtility.SetDirty(r.meshFilter);
                if (PrefabUtility.IsPartOfPrefabInstance(r.meshFilter))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(r.meshFilter);
            }

            binder.SmoothedMeshes.Add(clone);
            binder.OriginalMeshes.Add(source);
            binder.BoundRenderers.Add(r.skinned != null ? (Renderer)r.skinned : r.meshFilter.GetComponent<MeshRenderer>());

            AssetDatabase.SaveAssets();
            savedCount++;
            Debug.Log($"[AverageNormalTool] {r.label} → {path}");
        }

        EditorUtility.SetDirty(binder);
        if (PrefabUtility.IsPartOfPrefabInstance(binder))
            PrefabUtility.RecordPrefabInstancePropertyModifications(binder);
        if (PrefabUtility.IsPartOfPrefabInstance(root))
            PrefabUtility.RecordPrefabInstancePropertyModifications(root);

        Debug.Log($"[AverageNormalTool] 完成：{savedCount} 个网格已生成平滑法线资产并引用绑定。可用 Tools/NormalSmooth/恢复网格引用 回退。");
    }

    [MenuItem("Tools/NormalSmooth/恢复网格引用")]
    public static void RestoreOriginal()
    {
        GameObject root = Selection.activeGameObject;
        if (root == null)
        {
            Debug.LogError("[AverageNormalTool] 请先在 Hierarchy 选中角色根节点");
            return;
        }

        var binder = FindBinder(root);
        if (binder == null)
        {
            Debug.Log("[AverageNormalTool] 未找到平滑法线绑定，无需恢复。");
            return;
        }

        RestoreBindings(binder);

        // 删除绑定子物体（含 SmoothNormalBinder 组件）
        Object.DestroyImmediate(binder.gameObject);
        EditorUtility.SetDirty(root);
        if (PrefabUtility.IsPartOfPrefabInstance(root))
            PrefabUtility.RecordPrefabInstancePropertyModifications(root);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[AverageNormalTool] 已完成恢复：renderer 已指回原网格，克隆资产已删除，绑定子物体已移除。");
    }

    [MenuItem("Tools/NormalSmooth/修改合并角度...")]
    public static void SetMergeAngle()
    {
        GameObject root = Selection.activeGameObject;
        if (root == null)
        {
            Debug.LogError("[AverageNormalTool] 请先在 Hierarchy 选中角色根节点");
            return;
        }

        var binder = FindBinder(root);
        if (binder == null)
        {
            Debug.LogWarning("[AverageNormalTool] 未找到平滑法线绑定，请先执行 Tools/NormalSmooth/生成平滑顶点网格并引用。");
            return;
        }

        float oldAngle = binder.MergeAngle;
        AngleInputWindow.Show(oldAngle, angle =>
        {
            if (Mathf.Approximately(angle, oldAngle))
            {
                Debug.Log("[AverageNormalTool] 合并角度未变化，跳过重新生成。");
                return;
            }

            binder.SetMergeAngle(angle);
            EditorUtility.SetDirty(binder);
            if (PrefabUtility.IsPartOfPrefabInstance(binder))
                PrefabUtility.RecordPrefabInstancePropertyModifications(binder);

            Debug.Log($"[AverageNormalTool] 合并角度已更新为 {binder.MergeAngle}°，正在自动重新生成...");
            GenerateAndBind(root);
        });
    }

    // ─────────────────────────── 绑定组件操作 ───────────────────────────

    private static SmoothNormalBinder GetOrCreateBinder(GameObject root)
    {
        var t = root.transform.Find(BinderName);
        if (t == null)
        {
            var go = new GameObject(BinderName);
            go.transform.SetParent(root.transform, false);
            t = go.transform;
        }
        return t.GetComponent<SmoothNormalBinder>() ?? t.gameObject.AddComponent<SmoothNormalBinder>();
    }

    private static SmoothNormalBinder FindBinder(GameObject root)
    {
        var t = root.transform.Find(BinderName);
        if (t == null) return null;
        return t.GetComponent<SmoothNormalBinder>();
    }

    // 按三列表还原 renderer 引用并删除克隆资产，最后清空绑定
    private static void RestoreBindings(SmoothNormalBinder binder)
    {
        int count = Mathf.Min(binder.BoundRenderers.Count, binder.OriginalMeshes.Count);
        for (int i = 0; i < count; i++)
        {
            if (binder.BoundRenderers[i] == null || binder.OriginalMeshes[i] == null) continue;
            var r = binder.BoundRenderers[i];
            if (r is SkinnedMeshRenderer smr)
            {
                smr.sharedMesh = binder.OriginalMeshes[i];
                EditorUtility.SetDirty(smr);
                if (PrefabUtility.IsPartOfPrefabInstance(smr))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(smr);
            }
            else
            {
                var mf = r.GetComponent<MeshFilter>();
                if (mf != null)
                {
                    mf.sharedMesh = binder.OriginalMeshes[i];
                    EditorUtility.SetDirty(mf);
                    if (PrefabUtility.IsPartOfPrefabInstance(mf))
                        PrefabUtility.RecordPrefabInstancePropertyModifications(mf);
                }
            }
        }

        foreach (var mesh in binder.SmoothedMeshes)
        {
            if (mesh == null) continue;
            string path = AssetDatabase.GetAssetPath(mesh);
            if (!string.IsNullOrEmpty(path))
                AssetDatabase.DeleteAsset(path);
        }

        binder.ClearBindings();
        EditorUtility.SetDirty(binder);
        if (PrefabUtility.IsPartOfPrefabInstance(binder))
            PrefabUtility.RecordPrefabInstancePropertyModifications(binder);
    }

    // 收集角色下所有可替换网格的引用者（renderer + 处理前的源网格）
    private static List<MeshRef> CollectMeshRefs(GameObject root)
    {
        var refs = new List<MeshRef>();
        foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null) continue;
            if (mf.GetComponent<MeshRenderer>() == null)
            {
                Debug.LogWarning($"[AverageNormalTool] {mf.gameObject.name} 有 MeshFilter 但无 MeshRenderer，已跳过");
                continue;
            }
            refs.Add(new MeshRef { meshFilter = mf, skinned = null, sourceMesh = mf.sharedMesh, label = mf.gameObject.name });
        }
        foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (smr.sharedMesh == null) continue;
            refs.Add(new MeshRef { meshFilter = null, skinned = smr, sourceMesh = smr.sharedMesh, label = smr.gameObject.name });
        }
        return refs;
    }

    private struct MeshRef
    {
        public MeshFilter meshFilter;
        public SkinnedMeshRenderer skinned;
        public Mesh sourceMesh;
        public string label;
    }

    // ─────────────────────────── 平均法线计算（角度阈值版） ───────────────────────────
    // 1. 按"位置 + 法线夹角"双重判定：位置相同的顶点组内，只有法线夹角小于
    //    阈值（由 SmoothNormalBinder.MergeAngle 传入，默认 60°）的顶点才合并平均 ——
    //    保留大角度硬边，平滑小角度转折。
    // 2. 量化精度 0.1mm：避免把薄壁双面结构（头发片/裙摆等内外距离 < 1mm）
    //    的内外顶点误合并成反向法线抵消 → 产生 NaN/零向量的黑色色块。
    // 3. 零向量或未合并的顶点回退原始法线，兜底防御坏法线。
    private static Vector3[] ComputeAverageNormals(Mesh mesh, float angleThreshold)
    {
        Vector3[] verts = mesh.vertices;
        Vector3[] norms = mesh.normals;
        if (norms == null || norms.Length != verts.Length)
        {
            Debug.LogError($"[AverageNormalTool] {mesh.name} 没有法线数据");
            return null;
        }

        // 法线夹角阈值（度）：越大合并越多、描边越平滑；调小则更保守保留硬边。
        float cosThreshold = Mathf.Cos(angleThreshold * Mathf.Deg2Rad);

        // 按位置分组：key → 组内所有 (法线)
        var map = new Dictionary<Vector3Int, List<Vector3>>(verts.Length);
        var keys = new Vector3Int[verts.Length];
        for (int i = 0; i < verts.Length; i++)
        {
            keys[i] = Quantize(verts[i]);
            if (!map.TryGetValue(keys[i], out var list))
            {
                list = new List<Vector3>(4);
                map[keys[i]] = list;
            }
            list.Add(norms[i]);
        }

        var result = new Vector3[verts.Length];
        for (int i = 0; i < verts.Length; i++)
        {
            var list = map[keys[i]];
            Vector3 sum = Vector3.zero;
            int count = 0;

            foreach (var n in list)
            {
                // 只有法线夹角 < 阈值的才合并（Dot >= cos(阈值)）
                if (Vector3.Dot(norms[i], n) >= cosThreshold)
                {
                    sum += n;
                    count++;
                }
            }

            // 未合并或结果接近零向量 → 回退原始法线
            if (count > 0 && sum.sqrMagnitude > 1e-6f)
                result[i] = sum.normalized;
            else
                result[i] = norms[i];
        }
        return result;
    }

    // 0.1mm 精度量化：位置差 < 0.0001 的顶点视为同一顶点（薄壁内外不会误合并）
    private static Vector3Int Quantize(Vector3 v) => new Vector3Int(
        Mathf.RoundToInt(v.x * 10000f),
        Mathf.RoundToInt(v.y * 10000f),
        Mathf.RoundToInt(v.z * 10000f));
}
