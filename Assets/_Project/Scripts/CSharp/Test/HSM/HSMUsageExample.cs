using Core.Gear;
using UnityEngine;

namespace Core.Gear.Examples
{
    /// <summary>
    /// 演示实体如何创建、注册和驱动一个 HSM。
    /// </summary>
    public sealed class HSMUsageExample : MonoBehaviour
    {
        // 该示例实体持有的运行时上下文。
        private HSMExampleContext _context;
        // 该示例实体独立持有的状态机实例。
        private HSM<HSMExampleContext> _hsm;

        /// <summary>
        /// 创建上下文、状态实例和角色专属状态图。
        /// </summary>
        private void Awake()
        {
            // 准备当前实体专属的上下文和状态机。
            _context = new HSMExampleContext();
            _hsm = new HSM<HSMExampleContext>(_context);

            HSMExampleIdleState idleState = new HSMExampleIdleState();
            HSMExampleMoveState moveState = new HSMExampleMoveState();
            HSMExampleAttackState attackState = new HSMExampleAttackState();

            // 由当前角色集中声明状态节点和每个状态的出边。
            StateGraphBuilder<HSMExampleContext> graph = _hsm.BeginBuild();
            graph.AddState(idleState).AddState(moveState).AddState(attackState);
            graph.From(idleState)
                .To(moveState, moveState.Interrupt, priority: 10)
                .To(attackState, attackState.Interrupt, priority: 20);
            graph.From(moveState)
                .To(idleState, idleState.Interrupt, priority: 10)
                .To(attackState, attackState.Interrupt, priority: 20);
            graph.From(attackState)
                .To(idleState, idleState.Interrupt, priority: 10)
                .To(moveState, moveState.Interrupt, priority: 10);
            graph.Build(idleState);
        }

        /// <summary>
        /// 读取示例输入并由实体驱动 HSM 的帧更新。
        /// </summary>
        private void Update()
        {
            // 将这一帧的输入和时间写入当前实体上下文。
            _context.MoveHeld = Input.GetKey(KeyCode.W);
            _context.AttackPressed = Input.GetKeyDown(KeyCode.Space);
            _context.DeltaTime = Time.deltaTime;

            // HSM 自己完成当前状态仲裁和生命周期调度。
            _hsm.Update();
        }

        /// <summary>
        /// 销毁示例实体的状态机并释放状态生命周期。
        /// </summary>
        private void OnDestroy()
        {
            // 防止状态机在示例对象销毁后继续持有状态实例。
            _hsm?.Dispose();
        }
    }

    /// <summary>
    /// 提供示例状态读取的实体运行时数据。
    /// </summary>
    public sealed class HSMExampleContext
    {
        // 是否正在按住示例移动键。
        public bool MoveHeld;
        // 是否在当前帧按下示例攻击键。
        public bool AttackPressed;
        // 当前帧的时间间隔，仅由示例攻击状态读取。
        public float DeltaTime;
        // 攻击状态是否正在执行。
        public bool AttackActive;
    }

    /// <summary>
    /// 演示无输入和无移动时的待机状态。
    /// </summary>
    public sealed class HSMExampleIdleState : StateBase<HSMExampleContext>
    {
        /// <summary>
        /// 判断当前上下文是否允许进入待机状态。
        /// </summary>
        /// <returns>没有移动输入且未处于攻击时返回 true。</returns>
        public override bool Interrupt()
        {
            return !Context.MoveHeld && !Context.AttackActive;
        }

        /// <summary>
        /// 进入待机状态时输出一次状态变化信息。
        /// </summary>
        public override void OnEnter()
        {
            Debug.Log("HSM 示例：进入 Idle");
        }
    }

    /// <summary>
    /// 演示按住移动输入时的移动状态。
    /// </summary>
    public sealed class HSMExampleMoveState : StateBase<HSMExampleContext>
    {
        /// <summary>
        /// 判断当前上下文是否允许进入移动状态。
        /// </summary>
        /// <returns>正在移动且没有攻击执行时返回 true。</returns>
        public override bool Interrupt()
        {
            return Context.MoveHeld && !Context.AttackActive;
        }

        /// <summary>
        /// 进入移动状态时输出一次状态变化信息。
        /// </summary>
        public override void OnEnter()
        {
            Debug.Log("HSM 示例：进入 Move");
        }

        /// <summary>
        /// 演示当前移动状态的正常帧逻辑。
        /// </summary>
        public override void OnUpdate()
        {
            // 实际项目中在这里调用移动适配器或写入移动能力上下文。
        }
    }

    /// <summary>
    /// 演示一次性输入触发并持久复用的攻击状态。
    /// </summary>
    public sealed class HSMExampleAttackState : StateBase<HSMExampleContext>
    {
        // 示例攻击持续时间，单位为秒。
        private const float AttackDuration = 0.5f;
        // 当前攻击已经持续的时间，进入状态时清零。
        private float _elapsedTime;

        /// <summary>
        /// 判断当前上下文是否允许进入攻击状态。
        /// </summary>
        /// <returns>当前帧有攻击输入且没有正在执行攻击时返回 true。</returns>
        public override bool Interrupt()
        {
            return Context.AttackPressed && !Context.AttackActive;
        }

        /// <summary>
        /// 开始一次攻击并消费当前帧攻击输入。
        /// </summary>
        public override void OnEnter()
        {
            // 目标状态进入后消费自己的请求，不在 CanEnter 中产生副作用。
            Context.AttackPressed = false;
            Context.AttackActive = true;
            _elapsedTime = 0f;
            Debug.Log("HSM 示例：进入 Attack");
        }

        /// <summary>
        /// 推进示例攻击并在持续时间结束后释放攻击状态。
        /// </summary>
        public override void OnUpdate()
        {
            // 攻击状态从实体上下文读取时间，不要求 HSM 传入 deltaTime。
            _elapsedTime += Context.DeltaTime;
            if (_elapsedTime < AttackDuration) return;

            Context.AttackActive = false;
        }

        /// <summary>
        /// 结束当前攻击状态并清理示例运行数据。
        /// </summary>
        public override void OnExit()
        {
            // 确保攻击状态离开时不会遗留执行标记。
            Context.AttackActive = false;
            _elapsedTime = 0f;
            Debug.Log("HSM 示例：离开 Attack");
        }
    }
}
