namespace CoreFramework
{
    /// <summary>
    /// 黑板：输入数据的共享 POCO。各 IInputProvider 写入，Strategy/Controller 读取。
    /// 每帧开始时需调用 ResetFrameData() 清除单帧数据残留。
    /// </summary>
    [System.Serializable]
    public class Blackboard
    {
        // ── 持续输入（按住期间每帧为 true） ──
        public UnityEngine.Vector2 MoveInput;
        public UnityEngine.Vector2 LookInput;
        public bool AttackHeld;
        public bool TalentHeld;
        public bool BurstHeld;
        public bool IsShooting;
        public bool IsAiming;
        public bool IsSprinting;

        // ── 单帧输入（按下当帧为 true，下帧自动清零） ──
        public bool AttackPressed;
        public bool AttackReleased;
        public bool JumpPressed;
        public bool CrouchPressed;
        public bool TalentPressed;
        public bool TalentReleased;
        public bool BurstPressed;
        public bool BurstReleased;
        public bool ReloadPressed;
        public bool InteractPressed;

        // ── 角色切换与滚轮信息 ──
        public int SwitchIndex = -1;   // 1~4 切角色，-1 无输入
        public int ScrollDelta;        // 预留滚轮增量；当前不再用于角色切换

        /// <summary>
        /// 每帧开始时调用，清零单帧数据（JumpPressed 等）。
        /// 持续输入（MoveInput、AttackHeld 等）由 Provider 每帧覆盖，无需手动清零。
        /// </summary>
        public void ResetFrameData()
        {
            AttackPressed = false;
            AttackReleased = false;
            JumpPressed = false;
            CrouchPressed = false;
            TalentPressed = false;
            TalentReleased = false;
            BurstPressed = false;
            BurstReleased = false;
            ReloadPressed = false;
            InteractPressed = false;
            SwitchIndex = -1;
            ScrollDelta = 0;
        }

        /// <summary>
        /// 清空所有输入数据，包括持续输入和单帧输入。
        /// 当角色本帧没有收到任何玩家或 AI 控制命令时，可用作空命令。
        /// </summary>
        public void ClearAllData()
        {
            MoveInput = UnityEngine.Vector2.zero;
            LookInput = UnityEngine.Vector2.zero;
            AttackHeld = false;
            TalentHeld = false;
            BurstHeld = false;
            IsShooting = false;
            IsAiming = false;
            IsSprinting = false;
            ResetFrameData();
        }

        /// <summary>
        /// 复制另一份黑板数据，常用于玩家输入分发到当前受控角色。
        /// </summary>
        public void CopyFrom(Blackboard other)
        {
            if (other == null)
            {
                ClearAllData();
                return;
            }

            MoveInput = other.MoveInput;
            LookInput = other.LookInput;
            AttackHeld = other.AttackHeld;
            TalentHeld = other.TalentHeld;
            BurstHeld = other.BurstHeld;
            IsShooting = other.IsShooting;
            IsAiming = other.IsAiming;
            IsSprinting = other.IsSprinting;
            AttackPressed = other.AttackPressed;
            AttackReleased = other.AttackReleased;
            JumpPressed = other.JumpPressed;
            CrouchPressed = other.CrouchPressed;
            TalentPressed = other.TalentPressed;
            TalentReleased = other.TalentReleased;
            BurstPressed = other.BurstPressed;
            BurstReleased = other.BurstReleased;
            ReloadPressed = other.ReloadPressed;
            InteractPressed = other.InteractPressed;
            SwitchIndex = other.SwitchIndex;
            ScrollDelta = other.ScrollDelta;
        }
    }
}
