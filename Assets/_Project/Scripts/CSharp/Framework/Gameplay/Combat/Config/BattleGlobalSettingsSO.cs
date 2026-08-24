using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 战斗全局设置。
/// 当前先集中管理索敌相关参数，后续所有战斗层公共配置都应继续收敛到这里。
/// </summary>
[CreateAssetMenu(fileName = "BattleGlobalSettings", menuName = "Framework/Gameplay/Combat/Config/Battle Global Settings")]
public class BattleGlobalSettingsSO : ScriptableObject
{
    [Header("Projectile Targeting")]
    [Range(0f, 1f)]
    [Tooltip("投射物索敌优先级权重。0 代表完全按最近距离，1 代表完全按当前范围内最低血量，0~1 之间按两者混合评分。")]
    public float projectileTargetPriorityWeight = 0f;
    [Tooltip("启用后，投射物索敌会优先在当前摄像机正对的范围内搜索目标。若该范围内没有有效目标，才会回退到半径内所有目标。")]
    public bool prioritizeCameraFacingTargets = true;
    [Range(1f, 89f)]
    [Tooltip("当启用摄像机优先索敌时，视作“当前摄像机正对范围”的半角，单位为度。")]
    public float cameraFacingHalfAngle = 28f;
    [Range(0f, 1f)]
    [Tooltip("摄像机优先索敌时，屏幕中心区域偏好权重。0 代表只看是否在正对范围内，1 代表更强烈地偏向屏幕中心附近目标。")]
    public float cameraCenterPreferenceWeight = 0.35f;
    [Range(0.05f, 0.5f)]
    [Tooltip("视作屏幕中心区域的归一化半径。越小越强调准星附近的目标。")]
    public float cameraCenterViewportRadius = 0.18f;
    [FormerlySerializedAs("rotateCharacterTowardProjectileTarget")]
    [Tooltip("启用后，发射投射物前会让当前行为宿主单位朝目标方向转向。")]
    public bool rotateHostUnitTowardProjectileTarget = true;
    [Range(0f, 90f)]
    [Tooltip("投射物索敌修正允许相对原始前方向发生的最大偏转角度，单位为度。")]
    public float maxProjectileTargetCorrectionAngle = 30f;

    /// <summary>
    /// 旧字段兼容入口。新代码应改用 rotateHostUnitTowardProjectileTarget。
    /// </summary>
    public bool rotateCharacterTowardProjectileTarget
    {
        get => rotateHostUnitTowardProjectileTarget;
        set => rotateHostUnitTowardProjectileTarget = value;
    }
}
