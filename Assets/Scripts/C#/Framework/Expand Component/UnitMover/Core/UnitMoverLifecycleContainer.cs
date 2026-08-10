using System;
using System.Collections.Generic;

namespace Framework.ExpandComponent.UnitMover
{
    /// <summary>
    /// 由 UnitMover 持有并供当前策略按顺序注入无参生命周期回调的可重绑容器。
    /// </summary>
    public sealed class UnitMoverLifecycleContainer
    {
        // 策略首次创建并完成模块构造后执行一次的初始化回调。
        private readonly List<Action> _initializeCallbacks = new List<Action>();
        // 当前策略成为活动策略后执行的回调。
        private readonly List<Action> _activatedCallbacks = new List<Action>();
        // 当前策略活动期间每帧执行的回调。
        private readonly List<Action> _updateCallbacks = new List<Action>();
        // 当前策略活动期间每个物理步执行的回调。
        private readonly List<Action> _fixedUpdateCallbacks = new List<Action>();
        // 当前策略活动期间 LateUpdate 阶段执行的回调。
        private readonly List<Action> _lateUpdateCallbacks = new List<Action>();
        // Provider 或移动参考等可变依赖刷新后执行的回调。
        private readonly List<Action> _dependenciesChangedCallbacks = new List<Action>();
        // 当前策略切走前执行的共享状态归还回调。
        private readonly List<Action> _deactivatedCallbacks = new List<Action>();
        // UnitMover 完整释放当前策略时执行的最终清理回调。
        private readonly List<Action> _disposedCallbacks = new List<Action>();
        // 编辑器 Authoring 数据验证或配置变更后执行的回调。
        private readonly List<Action> _authoringValidateCallbacks = new List<Action>();
        // Inspector 替换初始策略前执行的 Authoring 状态归还回调。
        private readonly List<Action> _authoringRestoreCallbacks = new List<Action>();
        // 调用方显式重捕获浮动胶囊基础形状时执行的 Authoring 回调。
        private readonly List<Action> _authoringRecaptureCallbacks = new List<Action>();
        // Scene 窗口选中实体时执行的 Gizmo 绘制回调。
        private readonly List<Action> _drawGizmosSelectedCallbacks = new List<Action>();

        /// <summary>获取当前帧的普通更新时长，单位：秒。</summary>
        public float DeltaTime { get; private set; }

        /// <summary>获取当前帧的固定物理步时长，单位：秒。</summary>
        public float FixedDeltaTime { get; private set; }

        /// <summary>获取 UnitMover 更新容器上下文时记录的当前时间，单位：秒。</summary>
        public float CurrentTime { get; private set; }

        /// <summary>获取当前 Gizmo 回调是否允许绘制 Scene 预览。</summary>
        public bool IsScenePreviewEnabled { get; private set; }

        /// <summary>获取当前 Gizmo 回调是否允许绘制边缘检测诊断。</summary>
        public bool IsEdgeDetectionGizmosEnabled { get; private set; }

        /// <summary>
        /// 更新无参帧回调读取的时间上下文。
        /// </summary>
        /// <param name="deltaTime">当前普通帧时长，单位：秒。</param>
        /// <param name="fixedDeltaTime">当前固定物理步时长，单位：秒。</param>
        /// <param name="currentTime">当前 Unity 时间，单位：秒。</param>
        public void SetFrameContext(float deltaTime, float fixedDeltaTime, float currentTime)
        {
            DeltaTime = deltaTime;
            FixedDeltaTime = fixedDeltaTime;
            CurrentTime = currentTime;
        }

        /// <summary>
        /// 更新 Gizmo 回调读取的预览开关上下文。
        /// </summary>
        /// <param name="isScenePreviewEnabled">是否允许绘制 Scene 预览。</param>
        /// <param name="isEdgeDetectionGizmosEnabled">是否允许绘制边缘检测诊断。</param>
        public void SetGizmoContext(bool isScenePreviewEnabled, bool isEdgeDetectionGizmosEnabled)
        {
            IsScenePreviewEnabled = isScenePreviewEnabled;
            IsEdgeDetectionGizmosEnabled = isEdgeDetectionGizmosEnabled;
        }

        /// <summary>
        /// 注册策略首次初始化时执行的无参回调。
        /// </summary>
        /// <param name="callback">策略按注册顺序执行的初始化方法；为 null 时忽略。</param>
        public void RegisterInitialize(Action callback)
        {
            Register(_initializeCallbacks, callback);
        }

        /// <summary>
        /// 注册策略激活时执行的无参回调。
        /// </summary>
        /// <param name="callback">策略按注册顺序执行的激活方法；为 null 时忽略。</param>
        public void RegisterActivated(Action callback)
        {
            Register(_activatedCallbacks, callback);
        }

        /// <summary>
        /// 注册每帧 Update 阶段执行的无参回调。
        /// </summary>
        /// <param name="callback">策略按注册顺序执行的 Update 方法；为 null 时忽略。</param>
        public void RegisterUpdate(Action callback)
        {
            Register(_updateCallbacks, callback);
        }

        /// <summary>
        /// 注册每个 FixedUpdate 阶段执行的无参回调。
        /// </summary>
        /// <param name="callback">策略按注册顺序执行的固定步方法；为 null 时忽略。</param>
        public void RegisterFixedUpdate(Action callback)
        {
            Register(_fixedUpdateCallbacks, callback);
        }

        /// <summary>
        /// 注册每帧 LateUpdate 阶段执行的无参回调。
        /// </summary>
        /// <param name="callback">策略按注册顺序执行的 LateUpdate 方法；为 null 时忽略。</param>
        public void RegisterLateUpdate(Action callback)
        {
            Register(_lateUpdateCallbacks, callback);
        }

        /// <summary>
        /// 注册可变外部依赖更新后执行的无参回调。
        /// </summary>
        /// <param name="callback">策略按注册顺序执行的依赖刷新方法；为 null 时忽略。</param>
        public void RegisterDependenciesChanged(Action callback)
        {
            Register(_dependenciesChangedCallbacks, callback);
        }

        /// <summary>
        /// 注册策略停用时执行的无参回调。
        /// </summary>
        /// <param name="callback">策略按注册顺序执行的共享状态归还方法；为 null 时忽略。</param>
        public void RegisterDeactivated(Action callback)
        {
            Register(_deactivatedCallbacks, callback);
        }

        /// <summary>
        /// 注册策略完整释放时执行的无参回调。
        /// </summary>
        /// <param name="callback">策略按注册顺序执行的最终清理方法；为 null 时忽略。</param>
        public void RegisterDisposed(Action callback)
        {
            Register(_disposedCallbacks, callback);
        }

        /// <summary>
        /// 注册编辑器 Authoring 验证时执行的无参回调。
        /// </summary>
        /// <param name="callback">策略按注册顺序执行的 Authoring 同步方法；为 null 时忽略。</param>
        public void RegisterAuthoringValidate(Action callback)
        {
            Register(_authoringValidateCallbacks, callback);
        }

        /// <summary>
        /// 注册编辑器替换初始策略前执行的无参回调。
        /// </summary>
        /// <param name="callback">策略按注册顺序执行的 Authoring 归还方法；为 null 时忽略。</param>
        public void RegisterAuthoringRestore(Action callback)
        {
            Register(_authoringRestoreCallbacks, callback);
        }

        /// <summary>
        /// 注册显式重捕获 Authoring 基础形状时执行的无参回调。
        /// </summary>
        /// <param name="callback">策略按注册顺序执行的基础形状重捕获方法；为 null 时忽略。</param>
        public void RegisterAuthoringRecapture(Action callback)
        {
            Register(_authoringRecaptureCallbacks, callback);
        }

        /// <summary>
        /// 注册 Scene 窗口选中对象时执行的无参 Gizmo 绘制回调。
        /// </summary>
        /// <param name="callback">策略按注册顺序执行的 Gizmo 绘制方法；为 null 时忽略。</param>
        public void RegisterDrawGizmosSelected(Action callback)
        {
            Register(_drawGizmosSelectedCallbacks, callback);
        }

        /// <summary>按策略注册顺序调用首次初始化回调。</summary>
        public void InvokeInitialize()
        {
            Invoke(_initializeCallbacks);
        }

        /// <summary>按策略注册顺序调用激活回调。</summary>
        public void InvokeActivated()
        {
            Invoke(_activatedCallbacks);
        }

        /// <summary>按策略注册顺序调用 Update 回调。</summary>
        public void InvokeUpdate()
        {
            Invoke(_updateCallbacks);
        }

        /// <summary>按策略注册顺序调用 FixedUpdate 回调。</summary>
        public void InvokeFixedUpdate()
        {
            Invoke(_fixedUpdateCallbacks);
        }

        /// <summary>按策略注册顺序调用 LateUpdate 回调。</summary>
        public void InvokeLateUpdate()
        {
            Invoke(_lateUpdateCallbacks);
        }

        /// <summary>按策略注册顺序调用依赖刷新回调。</summary>
        public void InvokeDependenciesChanged()
        {
            Invoke(_dependenciesChangedCallbacks);
        }

        /// <summary>按策略注册顺序调用停用回调。</summary>
        public void InvokeDeactivated()
        {
            Invoke(_deactivatedCallbacks);
        }

        /// <summary>按策略注册顺序调用最终释放回调。</summary>
        public void InvokeDisposed()
        {
            Invoke(_disposedCallbacks);
        }

        /// <summary>按策略注册顺序调用 Authoring 验证回调。</summary>
        public void InvokeAuthoringValidate()
        {
            Invoke(_authoringValidateCallbacks);
        }

        /// <summary>按策略注册顺序调用 Authoring 状态归还回调。</summary>
        public void InvokeAuthoringRestore()
        {
            Invoke(_authoringRestoreCallbacks);
        }

        /// <summary>按策略注册顺序调用基础形状重捕获回调。</summary>
        public void InvokeAuthoringRecapture()
        {
            Invoke(_authoringRecaptureCallbacks);
        }

        /// <summary>按策略注册顺序调用选中对象 Gizmo 绘制回调。</summary>
        public void InvokeDrawGizmosSelected()
        {
            Invoke(_drawGizmosSelectedCallbacks);
        }

        /// <summary>
        /// 清空当前策略注入的全部阶段回调，容器随后可由下一策略重新注入。
        /// </summary>
        public void Clear()
        {
            _initializeCallbacks.Clear();
            _activatedCallbacks.Clear();
            _updateCallbacks.Clear();
            _fixedUpdateCallbacks.Clear();
            _lateUpdateCallbacks.Clear();
            _dependenciesChangedCallbacks.Clear();
            _deactivatedCallbacks.Clear();
            _disposedCallbacks.Clear();
            _authoringValidateCallbacks.Clear();
            _authoringRestoreCallbacks.Clear();
            _authoringRecaptureCallbacks.Clear();
            _drawGizmosSelectedCallbacks.Clear();
        }

        /// <summary>
        /// 将有效无参回调追加到指定阶段的稳定顺序列表。
        /// </summary>
        /// <param name="callbacks">目标生命周期阶段的回调列表。</param>
        /// <param name="callback">需要追加的回调；为 null 时不执行操作。</param>
        private static void Register(List<Action> callbacks, Action callback)
        {
            if (callback == null) return;

            // 切换阶段完成后容器会整体清空，列表只保存当前策略的一次注入结果。
            callbacks.Add(callback);
        }

        /// <summary>
        /// 使用索引循环调用阶段回调，避免热路径中的枚举器和临时集合分配。
        /// </summary>
        /// <param name="callbacks">需要按注册顺序执行的回调列表。</param>
        private static void Invoke(List<Action> callbacks)
        {
            // 回调注册仅发生在生命周期切换期，调用期不允许修改当前列表。
            for (int index = 0; index < callbacks.Count; index++) callbacks[index]?.Invoke();
        }
    }
}
