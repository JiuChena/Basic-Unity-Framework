using UnityEngine;

/// <summary>
/// 全局配置管理器。
/// 所有需要被运行时全局访问的配置资源都应统一挂在这里，由单例统一提供。
/// </summary>
[DefaultExecutionOrder(-1000)]
public sealed class GlobalConfigManager : MonoBehaviour
{
    private static GlobalConfigManager _instance;
    private static BattleGlobalSettingsSO _fallbackBattleSettings;
    private static bool _hasLoggedMissingManager;
    private static bool _hasLoggedMissingBattleSettings;

    [Header("Global Configs")]
    [SerializeField, Tooltip("战斗全局配置。后续所有战斗层公共参数都继续收敛到这个 SO 里。")]
    private BattleGlobalSettingsSO battleSettings;

    public static GlobalConfigManager Instance
    {
        get
        {
            if (_instance != null)
                return _instance;

            _instance = FindFirstObjectByType<GlobalConfigManager>(FindObjectsInactive.Include);
            if (_instance != null)
                return _instance;

            if (!Application.isPlaying)
                return null;

            GameObject managerObject = new GameObject(nameof(GlobalConfigManager));
            _instance = managerObject.AddComponent<GlobalConfigManager>();
            DontDestroyOnLoad(managerObject);

            if (!_hasLoggedMissingManager)
            {
                Debug.LogWarning(
                    "场景中未找到 GlobalConfigManager，已自动创建临时实例。建议在启动场景中放置一个 GlobalConfigManager，并手动拖入全局配置资源。");
                _hasLoggedMissingManager = true;
            }

            return _instance;
        }
    }

    public BattleGlobalSettingsSO BattleSettings
    {
        get
        {
            if (battleSettings != null)
                return battleSettings;

            if (_fallbackBattleSettings == null)
            {
                _fallbackBattleSettings = ScriptableObject.CreateInstance<BattleGlobalSettingsSO>();
                _fallbackBattleSettings.name = nameof(BattleGlobalSettingsSO);
            }

            if (!_hasLoggedMissingBattleSettings)
            {
                Debug.LogWarning(
                    "GlobalConfigManager 未绑定 BattleGlobalSettingsSO，当前将使用运行时默认值。请在 GlobalConfigManager 上手动拖入战斗全局配置资源。",
                    this);
                _hasLoggedMissingBattleSettings = true;
            }

            return _fallbackBattleSettings;
        }
    }

    public bool HasConfiguredBattleSettings => battleSettings != null;

    public BattleGlobalSettingsSO ConfiguredBattleSettings => battleSettings;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
    }
}
