using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 单位侧效果承载器。负责登记“这个单位当前身上有哪些效果”，并作为行为层接入全局效果系统的入口。
/// </summary>
public readonly struct UnitEffectChangedEvent
{
    public readonly UnitEffectController Controller;
    public readonly RuntimeEffectInstance EffectInstance;
    public readonly bool Added;

    public UnitEffectChangedEvent(UnitEffectController controller, RuntimeEffectInstance effectInstance, bool added)
    {
        Controller = controller;
        EffectInstance = effectInstance;
        Added = added;
    }
}

[DisallowMultipleComponent]
[RequireComponent(typeof(StatusData))]
public class UnitEffectController : MonoBehaviour
{
    [Tooltip("关闭 GameObject 后是否仍保留在全局效果系统中。后台队友可开启，对象池敌人通常应关闭")]
    [SerializeField]
    private bool keepRegisteredWhenDisabled = true;

    [Space(8)]
    [Header("Runtime Debug")]
    [SerializeField, Tooltip("当前单位登记中的运行时效果数量，仅用于 Inspector 调试观察")]
    private int runtimeActiveEffectCount;

    [SerializeField, Tooltip("最近一次登记到当前单位身上的效果标识，仅用于 Inspector 调试观察")]
    private string runtimeLastAddedEffectKey = string.Empty;

    [SerializeField, Tooltip("最近一次从当前单位移除的效果标识，仅用于 Inspector 调试观察")]
    private string runtimeLastRemovedEffectKey = string.Empty;

    [SerializeField, Tooltip("当前单位身上的效果快照列表，仅用于 Inspector 调试观察")]
    private List<UnitEffectViewData> runtimeEffectEntries = new List<UnitEffectViewData>(8);

    public StatusData DataPanel { get; private set; }
    public UnitDriverBase Driver { get; private set; }
    public IReadOnlyList<RuntimeEffectInstance> ActiveEffects => _activeEffects;
    public IReadOnlyList<UnitEffectViewData> RuntimeEffectEntries => runtimeEffectEntries;

    public event Action<UnitEffectController, RuntimeEffectInstance> EffectAdded;
    public event Action<UnitEffectController, RuntimeEffectInstance> EffectRemoved;

    private readonly List<RuntimeEffectInstance> _activeEffects = new List<RuntimeEffectInstance>(8);
    private bool _isRegistered;
    private bool _effectViewDirty;

    private void Awake()
    {
        DataPanel = GetComponent<StatusData>();
        Driver = GetComponent<UnitDriverBase>();
        RegisterToGlobalSystem();
        SyncRuntimeDebugFields();
    }

    private void OnEnable()
    {
        if (!_isRegistered)
            RegisterToGlobalSystem();

        SyncEffectViewEntries();
    }

    private void OnDisable()
    {
        if (keepRegisteredWhenDisabled || !GlobalEffectSystem.HasInstance)
            return;

        GlobalEffectSystem.Instance.UnregisterUnit(this);
        _isRegistered = false;
    }

    private void OnDestroy()
    {
        if (GlobalEffectSystem.HasInstance)
        {
            GlobalEffectSystem.Instance.UnregisterUnit(this);
            _isRegistered = false;
        }
    }

    private void LateUpdate()
    {
        bool hasActiveEffects = _activeEffects.Count > 0;
        bool shouldRefresh = _effectViewDirty;

        if (!shouldRefresh && !hasActiveEffects && runtimeEffectEntries.Count > 0)
            shouldRefresh = true;

        if (!shouldRefresh)
            return;

        SyncEffectViewEntries();
    }

    public bool ApplyEffect(EffectDefinitionSO definition, UnitEffectController explicitTargetController, Vector3 origin)
    {
        if (definition == null)
            return false;

        EffectApplyRequest request = new EffectApplyRequest(definition, this, explicitTargetController, origin);
        return GlobalEffectSystem.Instance.ApplyEffect(request);
    }

    public void ClearAllRuntimeEffects()
    {
        if (GlobalEffectSystem.HasInstance)
            GlobalEffectSystem.Instance.RemoveAllEffectsForUnit(this);

        SyncRuntimeDebugFields();
    }

    internal void RegisterRuntimeEffect(RuntimeEffectInstance instance)
    {
        if (instance == null)
            return;

        _activeEffects.Add(instance);
        runtimeLastAddedEffectKey = instance.DebugEffectKey;
        _effectViewDirty = true;
        SyncRuntimeDebugFields();
        SyncEffectViewEntries();
        CoreFramework.TypedEventBus.Publish(new UnitEffectChangedEvent(this, instance, true));
        EffectAdded?.Invoke(this, instance);
    }

    internal void UnregisterRuntimeEffect(RuntimeEffectInstance instance)
    {
        if (instance == null)
            return;

        for (int i = _activeEffects.Count - 1; i >= 0; i--)
        {
            if (_activeEffects[i] != instance)
                continue;

            _activeEffects.RemoveAt(i);
            break;
        }

        runtimeLastRemovedEffectKey = instance.DebugEffectKey;
        _effectViewDirty = true;
        SyncRuntimeDebugFields();
        SyncEffectViewEntries();
        CoreFramework.TypedEventBus.Publish(new UnitEffectChangedEvent(this, instance, false));
        EffectRemoved?.Invoke(this, instance);
    }

    [ContextMenu("Log Active Effect Snapshot")]
    private void LogActiveEffectSnapshot()
    {
        SyncEffectViewEntries();
        if (runtimeEffectEntries.Count == 0)
        {
            Debug.Log($"[{name}] 当前没有活动效果。", this);
            return;
        }

        for (int i = 0; i < runtimeEffectEntries.Count; i++)
        {
            UnitEffectViewData entry = runtimeEffectEntries[i];
            if (entry == null)
                continue;

            Debug.Log(
                $"[{name}] Effect#{i} | Key={entry.effectKey} | Asset={entry.assetName} | Source={entry.sourceName} | Remaining={entry.remainingDuration:F2}s | Tick={entry.completedTickCount}/{entry.totalTickCount} | RuntimeId={entry.runtimeId}",
                this);
        }
    }

    private void SyncRuntimeDebugFields()
    {
        runtimeActiveEffectCount = _activeEffects.Count;
    }

    private void RegisterToGlobalSystem()
    {
        GlobalEffectSystem.Instance.RegisterUnit(this);
        _isRegistered = true;
    }

    private void SyncEffectViewEntries()
    {
        int activeCount = _activeEffects.Count;
        if (runtimeEffectEntries.Count > activeCount)
            runtimeEffectEntries.RemoveRange(activeCount, runtimeEffectEntries.Count - activeCount);

        while (runtimeEffectEntries.Count < activeCount)
            runtimeEffectEntries.Add(new UnitEffectViewData());

        for (int i = 0; i < activeCount; i++)
            runtimeEffectEntries[i].UpdateFrom(_activeEffects[i]);

        _effectViewDirty = false;
    }
}
