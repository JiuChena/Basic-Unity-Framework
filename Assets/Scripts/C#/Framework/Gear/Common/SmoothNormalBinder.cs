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
    [SerializeField] private List<Mesh> smoothedMeshes = new List<Mesh>();
    [SerializeField] private List<Mesh> originalMeshes = new List<Mesh>();
    [SerializeField] private List<Renderer> boundRenderers = new List<Renderer>();

    public float MergeAngle => mergeAngle;
    public List<Mesh> SmoothedMeshes => smoothedMeshes;
    public List<Mesh> OriginalMeshes => originalMeshes;
    public List<Renderer> BoundRenderers => boundRenderers;

    public void SetMergeAngle(float angle) => mergeAngle = Mathf.Clamp(angle, 0f, 180f);
    public void ClearBindings()
    {
        smoothedMeshes?.Clear();
        originalMeshes?.Clear();
        boundRenderers?.Clear();
    }
}
