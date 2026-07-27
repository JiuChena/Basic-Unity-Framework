using System;
using UnityEngine;

namespace Framework.Core
{
    /// <summary>
    /// AI 数据提供器。巡逻和战斗决策直接写入与玩家相同的属性集，
    /// 下游策略和系统不感知数据来源差异。
    /// </summary>
    public sealed class AIDataProvider : DataProviderBase
    {
        [Header("AI 巡逻")]
        [Tooltip("巡逻区域的中心点世界坐标偏移")]
        public Vector3 patrolCenter = Vector3.zero;
        [Tooltip("巡逻半径，单位：米")]
        [Min(0f)] public float patrolRadius = 10f;
        [Tooltip("到达目标点后的停留时间，单位：秒")]
        [Min(0f)] public float idleDuration = 2f;
        [Tooltip("巡逻移动速度相对基础速度的倍率")]
        [Range(0.1f, 2f)] public float patrolSpeedMultiplier = 0.6f;

        [Header("AI 战斗")]
        [Tooltip("发现目标的检测距离，单位：米")]
        [Min(0f)] public float chaseRange = 15f;
        [Tooltip("两次攻击之间的最小间隔，单位：秒")]
        [Min(0.1f)] public float attackInterval = 1.5f;
        [Tooltip("是否启用闪避行为（攻击后概率位移）")]
        public bool enableDodge = true;
        [Tooltip("攻击后触发闪避的概率，0 表示不闪避，1 表示必闪避")]
        [Range(0f, 1f)] public float dodgeChance = 0.3f;

        [Header("AI 个性")]
        [Tooltip("随机跳跃的平均间隔，单位：秒")]
        [Min(0.1f)] public float jumpInterval = 3f;
        [Tooltip("是否允许 AI 在长距离移动时自动冲刺")]
        public bool enableSprint = true;
        [Tooltip("随机种子，0 表示每次启动使用随机种子")]
        public int randomSeed;
        [Tooltip("手动强制攻击键，用于测试时覆盖 AI 行为")]
        public KeyCode forceAttackKey = KeyCode.Q;

        // ── 缓存的属性实例，Tick 中直写 ──
        private MoveAttribute _move;
        private LookAttribute _look;
        private SprintAttribute _sprint;
        private CrouchAttribute _crouch;
        private JumpAttribute _jump;
        private AttackAttribute _attack;
        private TalentAttribute _talent;
        private BurstAttribute _burst;
        private AimAttribute _aim;
        private ReloadAttribute _reload;
        private InteractAttribute _interact;
        private ScrollAttribute _scroll;
        private SwitchCharacterAttribute _switchCharacter;

        // ── AI 运行时状态 ──
        private Vector3 _worldPatrolCenter;
        private Vector3 _currentTarget;
        private float _idleUntil;
        private float _nextJumpTime;
        private float _nextAttackTime;
        private bool _isMoving;
        private System.Random _random;

        #region Lifecycle

        /// <summary>
        /// 初始化巡逻中心点、随机数生成器和首轮计时器。
        /// </summary>
        protected override void Awake()
        {
            base.Awake();

            // 巡逻中心 = AI 实体位置 + 配置偏移
            _worldPatrolCenter = transform.position + patrolCenter;

            // 随机种子为 0 时使用系统时间生成
            _random = randomSeed == 0 ? new System.Random() : new System.Random(randomSeed);

            // 初始化首轮计时和目标
            RollNextPatrolTarget();
            _nextJumpTime = Time.time + jumpInterval;
            _nextAttackTime = Time.time + attackInterval;
        }

        /// <summary>
        /// 注册 AI 所需的全部 13 个属性到 Blackboard。
        /// </summary>
        protected override void RegisterAttributes(Blackboard board)
        {
            // 移动相关
            _move = Register(new MoveAttribute());
            _look = Register(new LookAttribute());
            _sprint = Register(new SprintAttribute());

            // 姿态相关
            _crouch = Register(new CrouchAttribute());
            _jump = Register(new JumpAttribute());

            // 战斗相关
            _attack = Register(new AttackAttribute());
            _talent = Register(new TalentAttribute());
            _burst = Register(new BurstAttribute());
            _aim = Register(new AimAttribute());

            // 交互相关
            _reload = Register(new ReloadAttribute());
            _interact = Register(new InteractAttribute());
            _scroll = Register(new ScrollAttribute());
            _switchCharacter = Register(new SwitchCharacterAttribute());
        }

        #endregion

        #region Tick

        /// <summary>
        /// 执行 AI 决策，将巡逻和战斗计算结果写入 Blackboard 属性。
        /// </summary>
        public override void Tick()
        {
            // ── 巡逻移动 ──
            Vector3 targetOffset = _currentTarget - transform.position;
            targetOffset.y = 0f;
            bool hasPatrolTarget = targetOffset.sqrMagnitude > 0.25f;

            if (hasPatrolTarget)
            {
                _isMoving = true;
                Vector3 direction = targetOffset.normalized * patrolSpeedMultiplier;
                _move.Value = new Vector2(direction.x, direction.z);

                // 距离较远且启用冲刺时自动加速
                _sprint.Value = enableSprint && targetOffset.sqrMagnitude > patrolRadius * patrolRadius * 0.5f;
            }
            else
            {
                _move.Value = Vector2.zero;
                _sprint.Value = false;

                // 到达目标 → 进入停留状态
                if (_isMoving)
                {
                    _isMoving = false;
                    _idleUntil = Time.time + idleDuration;
                }
                // 停留时间到 → 生成下一个巡逻点
                else if (Time.time >= _idleUntil)
                {
                    RollNextPatrolTarget();
                }
            }

            // ── 随机跳跃 ──
            bool jumpPressed = Time.time >= _nextJumpTime;
            if (jumpPressed)
            {
                // 下次跳跃时间带 ±1s 随机偏移，增加自然感
                _nextJumpTime = Time.time + Mathf.Max(0.1f, jumpInterval + RandomRange(-1f, 1f));
            }

            // ── 战斗逻辑 ──
            bool hasTarget = Physics.CheckSphere(transform.position, chaseRange, LayerMask.GetMask("Character"));
            bool attackPressed = hasTarget && Time.time >= _nextAttackTime;

#if ENABLE_LEGACY_INPUT_MANAGER
            // 手动强制攻击键覆盖
            attackPressed |= Input.GetKeyDown(forceAttackKey);
#endif

            if (attackPressed)
            {
                _nextAttackTime = Time.time + attackInterval;

                // 攻击后按概率触发闪避（重新选择一个巡逻点作为闪避方向）
                if (enableDodge && RandomRange(0f, 1f) < dodgeChance)
                    RollNextPatrolTarget();
            }

            // ── 写入全部属性 ──
            _look.Value = Vector2.zero;
            _crouch.SetState(false, false, false);
            _jump.SetState(jumpPressed, jumpPressed, false);
            _attack.SetState(attackPressed, attackPressed, false);
            _talent.SetState(false, false, false);
            _burst.SetState(false, false, false);
            _aim.Value = hasTarget;
            _reload.SetState(false, false, false);
            _interact.SetState(false, false, false);
            _scroll.Value = 0;
        }

        #endregion

        #region Patrol

        /// <summary>
        /// 在巡逻半径内随机生成下一个目标点。
        /// </summary>
        private void RollNextPatrolTarget()
        {
            float angle = RandomRange(0f, Mathf.PI * 2f);
            float radius = RandomRange(0f, patrolRadius);
            _currentTarget = _worldPatrolCenter + new Vector3(
                Mathf.Cos(angle) * radius,
                0f,
                Mathf.Sin(angle) * radius);
        }

        #endregion

        #region Helpers

        /// <summary>
        /// 生成 [min, max) 范围内的随机浮点数。
        /// </summary>
        private float RandomRange(float min, float max)
        {
            return min + (float)_random.NextDouble() * (max - min);
        }

        #endregion

        #region Editor

#if UNITY_EDITOR
        /// <summary>
        /// Scene 视图中绘制巡逻范围（青色）和战斗检测范围（红色）。
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            Vector3 center = Application.isPlaying ? _worldPatrolCenter : transform.position + patrolCenter;

            // 巡逻范围
            Gizmos.color = new Color(0f, 0.8f, 1f, 0.3f);
            Gizmos.DrawWireSphere(center, patrolRadius);

            // 战斗检测范围
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, chaseRange);
        }
#endif

        #endregion
    }
}
