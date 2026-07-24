using UnityEngine;

namespace CoreFramework
{
    /// <summary>
    /// AI 输入提供者。不读取物理设备，程序化生成逻辑动作并通过标准 Mapper 写入 Blackboard。
    /// </summary>
    public class AIInputProvider : BaseInputProvider, IInputReader
    {
        private readonly Blackboard _board = new Blackboard();

        [Header("AI 巡逻")]
        [Tooltip("巡逻中心点偏移")]
        public Vector3 patrolCenter = Vector3.zero;

        [Tooltip("巡逻半径，单位：米")]
        [Min(0f)]
        public float patrolRadius = 10f;

        [Tooltip("到达目标后停留时间，单位：秒")]
        [Min(0f)]
        public float idleDuration = 2f;

        [Tooltip("巡逻移动速度倍率")]
        [Range(0.1f, 2f)]
        public float patrolSpeedMultiplier = 0.6f;

        [Header("AI 战斗")]
        [Tooltip("发现目标后进入战斗的距离")]
        [Min(0f)]
        public float chaseRange = 15f;

        [Tooltip("攻击间隔，单位：秒")]
        [Min(0.1f)]
        public float attackInterval = 1.5f;

        [Tooltip("是否启用闪避行为")]
        public bool enableDodge = true;

        [Tooltip("闪避概率，0-1")]
        [Range(0f, 1f)]
        public float dodgeChance = 0.3f;

        [Header("AI 个性")]
        [Tooltip("随机跳跃的平均间隔，单位：秒")]
        [Min(0.1f)]
        public float jumpInterval = 3f;

        [Tooltip("启用冲刺")]
        public bool enableSprint = true;

        [Tooltip("行为随机种子，0 表示每次随机")]
        public int randomSeed;

        [Header("AI 专属键位")]
        [Tooltip("AI 强制攻击键，可用于手动覆盖攻击目标")]
        public KeyCode forceAttackKey = KeyCode.Q;

        [Tooltip("AI 巡逻重置键")]
        public KeyCode resetPatrolKey = KeyCode.T;

        [Tooltip("AI 切换跟随模式")]
        public KeyCode toggleFollowKey = KeyCode.G;

        // ── 运行时状态 ──
        private Vector3 _worldPatrolCenter;
        private Vector3 _currentTarget;
        private float _idleUntil;
        private float _nextJumpTime;
        private float _nextAttackTime;
        private bool _isMoving;
        private System.Random _rng;
        private IInputContextMapper _contextMapper;

        public override Blackboard Board => _board;

        protected override void Awake()
        {
            base.Awake();
            _worldPatrolCenter = transform.position + patrolCenter;
            _rng = randomSeed != 0 ? new System.Random(randomSeed) : new System.Random();
            RollNextPatrolTarget();
            _nextAttackTime = Time.time + attackInterval;
            InitializeInputPipeline();
        }

        public override void Tick()
        {
            if (_contextMapper == null) InitializeInputPipeline();

            InputActionStateStore actions = Board.GetOrCreate<InputActionStateStore>();
            Tick(actions);
            _contextMapper.Write(Board, actions);
        }

        public void RegisterActions(InputActionStateStore stateStore)
        {
            StandardInputActionRegistration.Register(stateStore);
        }

        /// <summary>
        /// 根据 AI 状态生成标准逻辑动作，不直接接触领域输入数据槽。
        /// </summary>
        public void Tick(InputActionStateStore stateStore)
        {
            Vector2 move = Vector2.zero;
            bool isSprinting = false;

            // ── 巡逻移动 ──
            Vector3 toTarget = _currentTarget - transform.position;
            toTarget.y = 0f;

            if (toTarget.sqrMagnitude > 0.25f)
            {
                _isMoving = true;
                Vector3 direction = toTarget.normalized * patrolSpeedMultiplier;
                move = new Vector2(direction.x, direction.z);
                isSprinting = enableSprint && toTarget.sqrMagnitude > patrolRadius * 0.5f;
            }
            else if (_isMoving && Time.time < _idleUntil)
            {
                _isMoving = false;
            }
            else if (!_isMoving)
            {
                RollNextPatrolTarget();
            }

            stateStore.SetVector2(StandardInputActions.Move, move);
            stateStore.SetVector2(StandardInputActions.Look, Vector2.zero);
            stateStore.SetFloat(StandardInputActions.Scroll, 0f);
            stateStore.SetButton(StandardInputActions.Sprint, false, isSprinting, false);
            stateStore.SetButton(StandardInputActions.Crouch, false, false, false);
            stateStore.SetButton(StandardInputActions.Reload, false, false, false);
            stateStore.SetButton(StandardInputActions.Interact, false, false, false);
            stateStore.SetButton(StandardInputActions.Switch1, false, false, false);
            stateStore.SetButton(StandardInputActions.Switch2, false, false, false);
            stateStore.SetButton(StandardInputActions.Switch3, false, false, false);
            stateStore.SetButton(StandardInputActions.Switch4, false, false, false);

            // ── 随机跳跃 ──
            bool jumpPressed = false;
            if (Time.time >= _nextJumpTime)
            {
                _nextJumpTime = Time.time + jumpInterval + (float)(_rng.NextDouble() * 2f - 1f);
                jumpPressed = true;
            }
            stateStore.SetButton(StandardInputActions.Jump, jumpPressed, jumpPressed, false);

            // ── 战斗逻辑 ──
            bool hasTarget = Physics.CheckSphere(transform.position, chaseRange, LayerMask.GetMask("Character"));
            bool attackPressed = false;

#if ENABLE_LEGACY_INPUT_MANAGER
            attackPressed |= Input.GetKeyDown(forceAttackKey);
#endif

            if (hasTarget && Time.time >= _nextAttackTime)
            {
                _nextAttackTime = Time.time + attackInterval;
                attackPressed = true;

                // 闪避：攻击后有概率请求随机移动
                if (enableDodge && _rng.NextDouble() < dodgeChance)
                {
                    RollNextPatrolTarget();
                }
            }
            stateStore.SetButton(StandardInputActions.Attack, attackPressed, attackPressed, false);
            stateStore.SetButton(StandardInputActions.Aim, false, hasTarget, false);
        }

        private void InitializeInputPipeline()
        {
            _contextMapper = new StandardInputContextMapper();
            RegisterActions(Board.GetOrCreate<InputActionStateStore>());
        }

        private void RollNextPatrolTarget()
        {
            float angle = (float)(_rng.NextDouble() * Mathf.PI * 2.0);
            float radius = (float)(_rng.NextDouble() * patrolRadius);
            _currentTarget = _worldPatrolCenter + new Vector3(
                Mathf.Cos(angle) * radius,
                0f,
                Mathf.Sin(angle) * radius);
            _idleUntil = Time.time + idleDuration;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Vector3 center = Application.isPlaying ? _worldPatrolCenter : transform.position + patrolCenter;
            Gizmos.color = new Color(0f, 0.8f, 1f, 0.3f);
            Gizmos.DrawWireSphere(center, patrolRadius);

            if (Application.isPlaying)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(_currentTarget, 0.3f);
                Gizmos.DrawLine(transform.position, _currentTarget);

                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(transform.position, chaseRange);
            }
        }
#endif
    }
}
