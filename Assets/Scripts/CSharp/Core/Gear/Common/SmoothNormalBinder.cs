using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 平滑法线描边绑定组件（通用框架能力）。
/// 挂在角色根节点下的 "Smooth Normal Directions" 子物体上，
/// 持有"平滑克隆网格 ↔ 源网格 ↔ 被替换的 renderer"三列表映射。
/// 由编辑器工具（Tools/NormalSmooth）维护；运行时仅持有引用，无 Update/逻辑。
/// 替代旧的 JSON 映射文件方案，引用跟随 Prefab/场景序列化，天然安全。
/// </summary>
public class SmoothNormalBinder : MonoBehaviour
{
    [SerializeField] private float mergeAngle = 60f;
    [SerializeField, Tooltip("合并距离（mm），计算时会自动 ÷1000 转成米")] private float mergeDistance = 2f;
    [SerializeField] private List<Mesh> smoothedMeshes = new List<Mesh>();
    [SerializeField] private List<Mesh> originalMeshes = new List<Mesh>();
    [SerializeField] private List<Renderer> boundRenderers = new List<Renderer>();

    public float MergeAngle => mergeAngle;
    /// <summary>合并距离（米），供法线合并计算使用（UI 配的 mm 值 ÷1000）。</summary>
    public float MergeDistance => mergeDistance * 0.001f;
    /// <summary>合并距离（mm），供编辑器弹窗显示/回填。</summary>
    public float MergeDistanceMm => mergeDistance;
    public List<Mesh> SmoothedMeshes => smoothedMeshes;
    public List<Mesh> OriginalMeshes => originalMeshes;
    public List<Renderer> BoundRenderers => boundRenderers;

    public void SetMergeAngle(float angle) => mergeAngle = Mathf.Clamp(angle, 0f, 180f);
    public void SetMergeDistance(float mm) => mergeDistance = Mathf.Max(0f, mm);
    public void ClearBindings()
    {
        smoothedMeshes?.Clear();
        originalMeshes?.Clear();
        boundRenderers?.Clear();
    }
}
