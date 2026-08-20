using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 全局效果系统。统一维护场景中所有运行时效果实例，并负责逐帧推进。
/// </summary>
[DisallowMultipleComponent]
public sealed class GlobalEffectSystem : MonoBehaviour
{
    private static GlobalEffectSystem instance;

    public static bool HasInstance => instance != null;

    public static GlobalEffectSystem Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject systemObject = new GameObject("GlobalEffectSystem");
                instance = systemObject.AddComponent<GlobalEffectSystem>();
                DontDestroyOnLoad(systemObject);
            }

            return instance;
        }
    }

    public IReadOnlyList<UnitEffectController> RegisteredUnits => _registeredUnits;

    private readonly List<UnitEffectController> _registeredUnits = new List<UnitEffectController>(32);
    private readonly List<RuntimeEffectInstance> _activeEffects = new List<RuntimeEffectInstance>(64);
    private readonly List<RuntimeEffectInstance> _pendingEffects = new List<RuntimeEffectInstance>(16);
    private readonly List<UnitEffectController> _targetBuffer = new List<UnitEffectController>(16);
    private int _nextRuntimeId = 1;
    private bool _isTicking;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        float deltaTime = Time.deltaTime;
        _isTicking = true;

        for (int i = _activeEffects.Count - 1; i >= 0; i--)
        {
            RuntimeEffectInstance effect = _activeEffects[i];
            if (effect == null)
            {
                _activeEffects.RemoveAt(i);
                continue;
            }

            if (effect.TargetController == null || effect.TargetData == null)
            {
                effect.ForceFinish();
            }
            else
            {
                effect.Tick(deltaTime);
            }

            if (effect.IsFinished)
                RemoveEffectAt(i);
        }

        _isTicking = false;
        FlushPendingEffects();
    }

    public void RegisterUnit(UnitEffectController controller)
    {
        if (controller == null)
            return;

        for (int i = 0; i < _registeredUnits.Count; i++)
        {
            if (_registeredUnits[i] == controller)
                return;
        }

        _registeredUnits.Add(controller);
    }

    public void UnregisterUnit(UnitEffectController controller)
    {
        if (controller == null)
            return;

        for (int i = _registeredUnits.Count - 1; i >= 0; i--)
        {
            if (_registeredUnits[i] == controller)
                _registeredUnits.RemoveAt(i);
        }

        RemoveAllEffectsForUnit(controller);
    }

    public bool ApplyEffect(EffectApplyRequest request)
    {
        if (request.Definition == null)
            return false;

        _targetBuffer.Clear();
        request.Definition.ResolveTargets(this, request, _targetBuffer);
        if (_targetBuffer.Count == 0)
            return false;

        bool applied = false;
        for (int i = 0; i < _targetBuffer.Count; i++)
        {
            UnitEffectController targetController = _targetBuffer[i];
            if (targetController == null || targetController.DataPanel == null)
                continue;

            EffectBuildContext buildContext = new EffectBuildContext(this, request, targetController);
            EffectSchedule schedule = request.Definition.BuildSchedule(buildContext);
            RuntimeEffectInstance instance = new RuntimeEffectInstance(
                _nextRuntimeId++, request.Definition, request, targetController, schedule, this);

            QueueOrActivateEffect(instance);
            applied = true;
        }

        _targetBuffer.Clear();
        return applied;
    }

    public void RemoveAllEffectsForUnit(UnitEffectController controller)
    {
        if (controller == null)
            return;

        for (int i = _pendingEffects.Count - 1; i >= 0; i--)
        {
            RuntimeEffectInstance pending = _pendingEffects[i];
            if (pending == null)
            {
                _pendingEffects.RemoveAt(i);
                continue;
            }

            if (pending.SourceController == controller || pending.TargetController == controller)
                _pendingEffects.RemoveAt(i);
        }

        for (int i = _activeEffects.Count - 1; i >= 0; i--)
        {
            RuntimeEffectInstance effect = _activeEffects[i];
            if (effect == null)
            {
                _activeEffects.RemoveAt(i);
                continue;
            }

            if (effect.SourceController != controller && effect.TargetController != controller)
                continue;

            effect.ForceFinish();
            if (!_isTicking)
                RemoveEffectAt(i);
        }
    }

    private void QueueOrActivateEffect(RuntimeEffectInstance instance)
    {
        if (instance == null)
            return;

        if (_isTicking)
        {
            _pendingEffects.Add(instance);
            return;
        }

        ActivateEffect(instance);
    }

    private void ActivateEffect(RuntimeEffectInstance instance)
    {
        _activeEffects.Add(instance);
        instance.TargetController?.RegisterRuntimeEffect(instance);
        instance.Begin();

        if (instance.IsFinished)
            RemoveEffectInstance(instance);
    }

    private void FlushPendingEffects()
    {
        if (_pendingEffects.Count == 0)
            return;

        for (int i = 0; i < _pendingEffects.Count; i++)
            ActivateEffect(_pendingEffects[i]);

        _pendingEffects.Clear();
    }

    private void RemoveEffectInstance(RuntimeEffectInstance instance)
    {
        if (instance == null)
            return;

        for (int i = _activeEffects.Count - 1; i >= 0; i--)
        {
            if (_activeEffects[i] == instance)
            {
                RemoveEffectAt(i);
                return;
            }
        }
    }

    private void RemoveEffectAt(int index)
    {
        if (index < 0 || index >= _activeEffects.Count)
            return;

        RuntimeEffectInstance effect = _activeEffects[index];
        _activeEffects.RemoveAt(index);
        effect?.TargetController?.UnregisterRuntimeEffect(effect);
    }
}

