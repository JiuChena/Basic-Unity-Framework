using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 平滑法线描边工具（方案 A：描边平滑，场景挂载组件引用版）。
/// 用途：为 GTS（GeneralToonyShader）的描边提供平滑外扩法线。
/// 原理：以 Thürmer-Wüthrich 夹角加权（顶点角 × 面法线）计算平滑法线，方向与细分密度、
///       UV 切分无关；再按"合并距离 + 法线夹角"双重判定过滤贡献 —— 球形邻域内法线夹角
///       小于阈值（由角色上的 SmoothNormalBinder 配置，距离默认 2mm、角度默认 60°）才合并，
///       保留大角度硬边、平滑小角度转折；写回 mesh.normals 后描边与光照共用同一套平滑法线。
///       蒙皮时 Unity 会实时变换 NORMAL 通道，平均法线在任意动画姿势下方向都正确。
/// 方案说明：不再使用 JSON 映射文件，改为在角色根节点下的 "Smooth Normal Directions"
///      子物体上挂载 SmoothNormalBinder 组件，三列表（平滑克隆网格 ↔ 源网格 ↔ 被替换 renderer）
///      随 Prefab/场景序列化保存，引用天然安全、可随资产移动，不再依赖命名/路径反查。
/// 用法：Hierarchy 中选中角色根节点（含 MeshFilter / SkinnedMeshRenderer）→ Tools/NormalSmooth 三个入口：
///   - 生成平滑顶点网格并引用  ：克隆网格另存为独立 .asset 并替换引用，记录绑定
///   - 恢复网格引用            ：把 sharedMesh 指回原 FBX 子网格，删除克隆资产与绑定子物体
///   - 修改合并参数...         ：弹窗修改合并角度 + 合并距离（重新生成后生效）
/// 说明：
/// - 持久化生成的克隆资产命名规则：{原网格名}_SmoothNormal.asset（存于源 FBX 同目录）。
/// - 写回后光照硬边阴影会变柔和（平滑法线所致），属预期行为。
/// - 合并距离默认 2mm + 角度阈值：距离过大可能把薄壁双面结构（头发/裙摆）内外顶点卷入
///   邻域，由角度阈值兜底，避免内外法线抵消成坏法线。
/// - 尖端收边：生成时把"非尖端程度"(1 - 法线离散度)烘焙进顶点色 alpha，
///   配合 GTS 描边的 _TipTaper 在尖端收窄描边宽度、消除尖端劈叉。
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

            Vector3[] avgNormals = ComputeAverageNormals(source, binder.MergeAngle, binder.MergeDistance);
            if (avgNormals == null) continue;

            // 尖端收边因子：把"非尖端程度"烘焙进顶点色 alpha（1=平滑区，尖端趋近0）
            float[] tipFactors = ComputeTipFactors(source, binder.MergeDistance);

            // 克隆独立网格资产 → 写入平均法线 + 尖端因子 → 替换引用
            Mesh clone = Object.Instantiate(source);
            clone.name = source.name + CloneSuffix;
            clone.normals = avgNormals;
            WriteTipFactors(clone, tipFactors);

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

    [MenuItem("Tools/NormalSmooth/修改合并参数...")]
    public static void SetMergeParams()
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
        float oldDistanceMm = binder.MergeDistanceMm;
        MergeParamsWindow.Show(oldAngle, oldDistanceMm, (angle, distanceMm) =>
        {
            bool angleChanged = !Mathf.Approximately(angle, oldAngle);
            bool distanceChanged = !Mathf.Approximately(distanceMm, oldDistanceMm);
            if (!angleChanged && !distanceChanged)
            {
                Debug.Log("[AverageNormalTool] 合并参数未变化，跳过重新生成。");
                return;
            }

            if (angleChanged) binder.SetMergeAngle(angle);
            if (distanceChanged) binder.SetMergeDistance(distanceMm);
            EditorUtility.SetDirty(binder);
            if (PrefabUtility.IsPartOfPrefabInstance(binder))
                PrefabUtility.RecordPrefabInstancePropertyModifications(binder);

            Debug.Log($"[AverageNormalTool] 合并参数已更新为 角度 {binder.MergeAngle}° / 距离 {binder.MergeDistanceMm}mm，正在自动重新生成...");
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

    // ─────────────── 平均法线计算（夹角加权 + 距离/角度双重判定） ───────────────
    // 1. 夹角加权（Thürmer & Wüthrich 1998）：第一遍遍历三角形，按"顶点角 × 面法线"
    //    累积贡献 —— 方向与细分密度、UV 切分无关，取代旧版等权相加的副本数偏差。
    // 2. 第二遍以"顶点为圆心、mergeDistance 为半径"的球形邻域收集贡献。合并距离由
    //    SmoothNormalBinder 配置（单位 mm，默认 2mm，内部 ÷1000 转米）；
    //    mergeDistance = 0 时退化为同位置顶点合并。
    // 3. 距离满足后仍需法线夹角 < 阈值（默认 60°）才合并 —— 保留大角度硬边、
    //    平滑小角度转折；同时防止距离过大时把薄壁双面结构（头发片/裙摆等内外距离
    //    < 合并距离）的内外顶点误合并成反向法线抵消 → 产生 NaN/零向量的黑色色块。
    // 4. 空间哈希网格（格边长 = 合并距离）：任意半径邻域至多覆盖 3×3×3=27 个格子，
    //    邻域搜索 O(n) 而非 O(n²)。
    // 5. 未合并或结果接近零向量的顶点回退原始法线，兜底防御坏法线。

    /// <summary>
    /// 计算夹角加权平滑法线：第一遍按三角形向空间哈希格子累积"顶点角 × 面法线"贡献，
    /// 第二遍逐顶点在球形邻域内按"距离 + 法线夹角"过滤求和；未合并或零向量回退原始法线。
    /// </summary>
    /// <param name="mesh">源网格；法线缺失或无三角形数据时返回 null。</param>
    /// <param name="angleThreshold">法线夹角阈值（度），越大合并越多、描边越平滑。</param>
    /// <param name="mergeDistance">合并半径（米）；0 时退化为同位置顶点合并。</param>
    /// <returns>与 mesh.vertices 等长的平滑法线数组；数据不完整时返回 null。</returns>
    private static Vector3[] ComputeAverageNormals(Mesh mesh, float angleThreshold, float mergeDistance)
    {
        Vector3[] verts = mesh.vertices;
        Vector3[] norms = mesh.normals;
        int[] tris = mesh.triangles;
        if (norms == null || norms.Length != verts.Length)
        {
            Debug.LogError($"[AverageNormalTool] {mesh.name} 没有法线数据");
            return null;
        }
        if (verts.Length == 0) return new Vector3[0];
        if (tris == null || tris.Length == 0)
        {
            Debug.LogError($"[AverageNormalTool] {mesh.name} 没有三角形数据");
            return null;
        }

        // 法线夹角阈值（度）：越大合并越多、描边越平滑；调小则更保守保留硬边。
        float cosThreshold = Mathf.Cos(angleThreshold * Mathf.Deg2Rad);

        // 格边长 = 合并距离（保证 27 邻域覆盖半径内全部贡献）；合并半径 0 时用最小格防除零。
        float cellSize = Mathf.Max(mergeDistance, 1e-5f);
        float sqRadius = mergeDistance * mergeDistance;

        // 第一遍：遍历三角形，把"顶点角 × 面法线"贡献写入三个顶点角所在的空间哈希格子。
        var grid = new Dictionary<Vector3Int, List<Contribution>>(verts.Length);
        for (int t = 0; t < tris.Length; t += 3)
        {
            Vector3 a = verts[tris[t]];
            Vector3 b = verts[tris[t + 1]];
            Vector3 c = verts[tris[t + 2]];

            Vector3 cross = Vector3.Cross(b - a, c - a);
            float crossMag = cross.magnitude;
            if (crossMag < 1e-8f) continue; // 退化三角形（零面积/共线），无法线可贡献
            Vector3 faceNormal = cross / crossMag;

            // 顶点角 = 该顶点两条边的夹角，衡量该面在顶点周围张开的跨度。
            float angleA = CornerAngle(b - a, c - a);
            float angleB = CornerAngle(a - b, c - b);
            float angleC = CornerAngle(a - c, b - c);

            AddContribution(grid, a, angleA, faceNormal, cellSize);
            AddContribution(grid, b, angleB, faceNormal, cellSize);
            AddContribution(grid, c, angleC, faceNormal, cellSize);
        }

        // 第二遍：逐顶点在球形邻域内过滤求和。顶点自身接触的三角形贡献落点距其
        // 位置为 0，天然包含在内，无需旧版的"含自身求和"。
        var result = new Vector3[verts.Length];
        for (int i = 0; i < verts.Length; i++)
        {
            Vector3 pos = verts[i];
            Vector3 normal = norms[i];
            Vector3 sum = Vector3.zero;
            bool merged = false;

            // 遍历自身所在格子 + 周围 26 个邻格
            var center = GridKey(pos, cellSize);
            for (int gx = center.x - 1; gx <= center.x + 1; gx++)
            for (int gy = center.y - 1; gy <= center.y + 1; gy++)
            for (int gz = center.z - 1; gz <= center.z + 1; gz++)
            {
                if (!grid.TryGetValue(new Vector3Int(gx, gy, gz), out var cell)) continue;
                for (int k = 0; k < cell.Count; k++)
                {
                    Contribution c = cell[k];
                    // 距离 ≤ 合并半径 且 面法线与自身夹角 < 阈值 才合并
                    if ((c.pos - pos).sqrMagnitude > sqRadius) continue;
                    if (Vector3.Dot(normal, c.normal) < cosThreshold) continue;
                    sum += c.angle * c.normal;
                    merged = true;
                }
            }

            // 未合并或结果接近零向量 → 回退原始法线
            if (merged && sum.sqrMagnitude > 1e-6f)
                result[i] = sum.normalized;
            else
                result[i] = normal;
        }
        return result;
    }

    /// <summary>
    /// 顶点角：两条边单位化后夹角的反余弦；退化边（长度近零）返回 0，不产生贡献。
    /// </summary>
    /// <param name="e1">从顶点出发的第一条边向量。</param>
    /// <param name="e2">从顶点出发的第二条边向量。</param>
    /// <returns>夹角弧度（0 ~ PI）；任一边退化时返回 0。</returns>
    private static float CornerAngle(Vector3 e1, Vector3 e2)
    {
        float mag1 = e1.magnitude;
        float mag2 = e2.magnitude;
        if (mag1 < 1e-8f || mag2 < 1e-8f) return 0f;
        float cosAngle = Vector3.Dot(e1 / mag1, e2 / mag2);
        return Mathf.Acos(Mathf.Clamp(cosAngle, -1f, 1f));
    }

    /// <summary>
    /// 向空间哈希格子追加一条"顶点角 × 面法线"贡献；零夹角（退化顶点）无意义，直接跳过。
    /// </summary>
    /// <param name="grid">空间哈希：格子 → 贡献列表。</param>
    /// <param name="pos">贡献顶点位置（第二遍距离判定用）。</param>
    /// <param name="angle">顶点角弧度。</param>
    /// <param name="normal">已单位化的面法线。</param>
    /// <param name="cellSize">格边长（= 合并距离）。</param>
    private static void AddContribution(Dictionary<Vector3Int, List<Contribution>> grid, Vector3 pos, float angle, Vector3 normal, float cellSize)
    {
        if (angle <= 0f) return;
        var key = GridKey(pos, cellSize);
        if (!grid.TryGetValue(key, out var list))
        {
            list = new List<Contribution>(8);
            grid[key] = list;
        }
        list.Add(new Contribution { pos = pos, angle = angle, normal = normal });
    }

    // 一条夹角加权贡献：贡献顶点位置（距离判定）+ 顶点角（权重）× 面法线（方向）。
    private struct Contribution
    {
        public Vector3 pos;
        public float angle;
        public Vector3 normal;
    }

    // ─────────────────────────── 尖端收边因子（法线离散度 / 曲率） ───────────────────────────
    // 判断依据：尖端 = 顶点周围一圈法线绕成一圈互相抵消。coherence = |ΣN|/count 会降低，
    // tipFactor = 1 - coherence 趋近 1。用半径邻域（空间哈希）而非拓扑 1-ring：网格在
    // UV 缝/尖端会分裂顶点，拓扑 1-ring 只看到同一份副本，检测不到绕圈；半径邻域把分裂
    // 副本重新焊在一起分析，恰好能发现尖端。
    // 两道护栏，避免误伤：
    // - 邻域半径取 min(合并距离, 3mm)：合并距离调大时仍保持局部，不会把整段手臂/整根发丝
    //   判成尖端（曲率半径越大，同半径邻域内法线越一致）。
    // - 剔除与自身法线夹角 >150° 的邻居（近对向）：那是薄壁双面结构（头发片/裙摆内外两面），
    //   不是尖端；尖端附近法线是绕圈发散，夹角通常 <90°，不受影响。
    private const float MaxTipRadius = 0.003f;  // 尖端邻域半径上限 3mm
    private const float AntiParallelCos = -0.866f; // cos150°，夹角 >150° 视为近对向
    private static float[] ComputeTipFactors(Mesh mesh, float mergeDistance)
    {
        Vector3[] verts = mesh.vertices;
        Vector3[] norms = mesh.normals;
        if (verts.Length == 0) return new float[0];
        if (norms == null || norms.Length != verts.Length) return new float[verts.Length];

        // 尖端邻域半径：不超过合并距离，但封顶 3mm
        float tipRadius = Mathf.Min(mergeDistance, MaxTipRadius);
        float cellSize = Mathf.Max(tipRadius, 1e-5f);
        float sqRadius = tipRadius * tipRadius;

        var grid = new Dictionary<Vector3Int, List<int>>(verts.Length);
        for (int i = 0; i < verts.Length; i++)
        {
            var key = GridKey(verts[i], cellSize);
            if (!grid.TryGetValue(key, out var list))
            {
                list = new List<int>(4);
                grid[key] = list;
            }
            list.Add(i);
        }

        var result = new float[verts.Length];
        for (int i = 0; i < verts.Length; i++)
        {
            Vector3 pos = verts[i];
            Vector3 sum = norms[i]; // 含自身
            int count = 1;

            var center = GridKey(pos, cellSize);
            for (int gx = center.x - 1; gx <= center.x + 1; gx++)
            for (int gy = center.y - 1; gy <= center.y + 1; gy++)
            for (int gz = center.z - 1; gz <= center.z + 1; gz++)
            {
                if (!grid.TryGetValue(new Vector3Int(gx, gy, gz), out var cell)) continue;
                for (int k = 0; k < cell.Count; k++)
                {
                    int j = cell[k];
                    if (i == j) continue;
                    if ((verts[j] - pos).sqrMagnitude > sqRadius) continue;
                    // 剔除近对向法线（薄壁双面），避免整面误判为尖端
                    if (Vector3.Dot(norms[i], norms[j]) < AntiParallelCos) continue;
                    sum += norms[j];
                    count++;
                }
            }

            // coherence 越高越平滑；tipFactor 越接近 1 越像尖端
            float coherence = sum.magnitude / count;
            result[i] = Mathf.Clamp01(1f - coherence);
        }
        return result;
    }

    // 把"非尖端程度"烘焙进顶点色 alpha：保留原 RGB 与原 alpha，再乘上 (1 - tipFactor)。
    // 这样源模型若已有顶点色（如美术画的描边宽度遮罩）也不会被破坏。
    private static void WriteTipFactors(Mesh mesh, float[] tipFactors)
    {
        Color[] colors = mesh.colors;
        bool hasColors = colors != null && colors.Length == mesh.vertexCount;
        var dst = new Color[mesh.vertexCount];
        for (int i = 0; i < dst.Length; i++)
        {
            dst[i] = hasColors ? colors[i] : Color.white;
            dst[i].a *= Mathf.Clamp01(1f - tipFactors[i]);
        }
        mesh.colors = dst;
    }

    // 空间哈希网格坐标：格边长 = 合并距离。用 FloorToInt（而非 RoundToInt）保证负坐标安全。
    private static Vector3Int GridKey(Vector3 v, float cellSize) => new Vector3Int(
        Mathf.FloorToInt(v.x / cellSize),
        Mathf.FloorToInt(v.y / cellSize),
        Mathf.FloorToInt(v.z / cellSize));
}
