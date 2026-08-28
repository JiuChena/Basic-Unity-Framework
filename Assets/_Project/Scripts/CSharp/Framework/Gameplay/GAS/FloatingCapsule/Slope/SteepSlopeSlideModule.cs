using UnityEngine;

namespace Framework.Gameplay.Abilities.Movement
{
    /// <summary>在超过站立限制的斜坡上按坡度施加下坡速度修正。</summary>
    internal sealed class SteepSlopeSlideModule
    {
        // 提供坡度限制、曲线下滑因数和速度上限的地面配置。
        private readonly GroundSettings _settings;
        // 当前是否已确认锁定不可行走陡坡，锁定期间持续限制上坡输入和上坡速度。
        private bool _isSteepSlopeConstraintActive;
        // 当前物理步是否为刚确认锁定陡坡的首次约束步；该步只阻止上坡，不叠加下坡速度。
        private bool _isFirstConstraintStep;
        // 连续命中进入阈值的累计时间，单位：秒。
        private float _steepSlopeEnterElapsedTime;
        // 连续命中退出阈值的累计时间，单位：秒。
        private float _steepSlopeExitElapsedTime;
        // 锁定陡坡后短暂丢失接触的累计时间，单位：秒。
        private float _lostGroundContactElapsedTime;
        // 锁定陡坡后用于下滑强度计算的坡度角，单位：度。
        private float _lockedSlopeAngle;
        // 由锁定斜面法线计算出的沿坡向下单位方向。
        private Vector3 _lockedDownhillDirection;

        /// <summary>
        /// 创建陡坡下滑模块并缓存地面配置。
        /// </summary>
        /// <param name="settings">地面、坡度和下滑参数配置；为 null 时不产生修正。</param>
        internal SteepSlopeSlideModule(GroundSettings settings)
        {
            _settings = settings;
        }

        /// <summary>
        /// 在已锁定的不可行走斜面上清除上坡速度，并在首次约束步后叠加曲线控制的下坡速度。
        /// </summary>
        /// <param name="velocity">完成重力计算后、尚未提交给刚体的速度。</param>
        /// <param name="fixedDeltaTime">当前固定物理步时长，单位：秒。</param>
        /// <returns>返回叠加本步下坡速度后的结果；没有有效下滑配置时保持原速度。</returns>
        internal Vector3 Apply(Vector3 velocity, float fixedDeltaTime)
        {
            if (!_isSteepSlopeConstraintActive) return velocity;

            // 锁定坡面后始终移除实际速度中的上坡分量，防止惯性或策略残余继续把玩家推上不可行走坡。
            velocity = RemoveUphillVelocity(velocity);
            bool isFirstConstraintStep = _isFirstConstraintStep;
            _isFirstConstraintStep = false;
            if (isFirstConstraintStep || _settings == null || fixedDeltaTime <= 0f) return velocity;

            // 锁定期间只使用缓存的斜面方向；当前接触可短暂丢失或切换到相邻网格三角面。
            if (_lockedDownhillDirection.sqrMagnitude <= 0.000001f) return velocity;
            // 以锁定时的超限坡度差、曲线倍率和下滑因数计算本步速度增量。
            float slopeDifference = Mathf.Max(0f, _lockedSlopeAngle - _settings.SlopeLimit);
            float normalizedSlopeDifference = CalculateNormalizedSlopeDifference(_lockedSlopeAngle);
            float slideRatio = _settings.EvaluateSteepSlopeSlideRatio(normalizedSlopeDifference);
            if (slideRatio <= 0f || _settings.SteepSlopeSlideFactor <= 0f) return velocity;

            float downhillSpeed = Vector3.Dot(velocity, _lockedDownhillDirection);
            float maximumSpeedIncrement = Mathf.Max(0f, _settings.SteepSlopeSlideSpeedLimit - downhillSpeed);
            if (maximumSpeedIncrement <= 0f) return velocity;

            // 上限确保叠加后不会超过配置的最大沿坡下滑速度。
            float calculatedSpeedIncrement = slopeDifference * slideRatio * _settings.SteepSlopeSlideFactor * fixedDeltaTime;
            float downhillSpeedIncrement = Mathf.Min(calculatedSpeedIncrement, maximumSpeedIncrement);
            Vector3 downhillVelocityAddition = _lockedDownhillDirection * downhillSpeedIncrement;
            return velocity + downhillVelocityAddition;
        }

        /// <summary>
        /// 移除给定速度中沿已锁定坡面向上的分量。
        /// </summary>
        /// <param name="velocity">待约束的世界空间速度。</param>
        /// <returns>不再包含沿锁定斜面向上分量的速度。</returns>
        private Vector3 RemoveUphillVelocity(Vector3 velocity)
        {
            if (_lockedDownhillDirection.sqrMagnitude <= 0.000001f) return velocity;

            // 与下坡反向的投影即沿坡向上速度；只移除正向部分，保留横向、法线和下坡运动。
            Vector3 uphillDirection = -_lockedDownhillDirection;
            float uphillSpeed = Vector3.Dot(velocity, uphillDirection);
            return uphillSpeed > 0f ? velocity - uphillDirection * uphillSpeed : velocity;
        }

        /// <summary>
        /// 更新陡坡约束状态，并在状态锁定期间移除本步上坡输入。
        /// </summary>
        /// <param name="command">待由移动策略消费的通用移动命令；没有上坡分量时保持不变。</param>
        /// <param name="contact">当前物理步真实地面接触，用于更新陡坡状态。</param>
        /// <param name="fixedDeltaTime">当前固定物理步时长，单位：秒。</param>
        internal void ConstrainUphillInput(ref MovementCommand command, in GroundContact contact, float fixedDeltaTime)
        {
            if (_settings == null || fixedDeltaTime <= 0f) return;

            // 每个固定步只在构建命令前更新一次状态，Apply 仅消费缓存结果，避免同一帧出现相互矛盾的判定。
            UpdateSteepSlopeConstraint(contact, fixedDeltaTime);
            if (!_isSteepSlopeConstraintActive) return;

            // 以锁定下坡方向的水平投影识别输入中的上坡分量，保留横向和下坡输入。
            Vector3 planarDownhillDirection = Vector3.ProjectOnPlane(_lockedDownhillDirection, Vector3.up);
            if (planarDownhillDirection.sqrMagnitude <= 0.000001f) return;

            planarDownhillDirection.Normalize();
            float uphillInput = Vector3.Dot(command.WorldMoveDirection, -planarDownhillDirection);
            if (uphillInput <= 0f) return;

            command.WorldMoveDirection += planarDownhillDirection * uphillInput;
        }

        /// <summary>
        /// 以进入、退出滞回和丢失接触宽限维护稳定的陡坡约束状态。
        /// </summary>
        /// <param name="contact">当前物理步的实时地面接触。</param>
        /// <param name="fixedDeltaTime">当前固定物理步时长，单位：秒。</param>
        private void UpdateSteepSlopeConstraint(in GroundContact contact, float fixedDeltaTime)
        {
            // 无实时接触时，已锁定状态在短暂宽限内保持，避免浮动胶囊的接触波动反复放开输入。
            if (!contact.HasContact)
            {
                _steepSlopeEnterElapsedTime = 0f;
                _steepSlopeExitElapsedTime = 0f;
                if (!_isSteepSlopeConstraintActive) return;

                _lostGroundContactElapsedTime += fixedDeltaTime;
                if (_lostGroundContactElapsedTime > _settings.SteepSlopeLostContactGraceTime)
                    ResetRuntimeState();

                return;
            }

            // 有效接触恢复后不再累计丢失时间，并依据不同状态应用进入或退出阈值。
            _lostGroundContactElapsedTime = 0f;
            if (_isSteepSlopeConstraintActive)
            {
                UpdateActiveConstraint(contact, fixedDeltaTime);
                return;
            }

            UpdatePendingConstraint(contact, fixedDeltaTime);
        }

        /// <summary>
        /// 在未锁定状态下连续确认超过进入阈值的不可行走坡面。
        /// </summary>
        /// <param name="contact">当前物理步的实时地面接触。</param>
        /// <param name="fixedDeltaTime">当前固定物理步时长，单位：秒。</param>
        private void UpdatePendingConstraint(in GroundContact contact, float fixedDeltaTime)
        {
            // 只有明显超过进入阈值的坡面才累计确认时间，阈值附近的接触不应限制玩家输入。
            if (!IsAboveEnterThreshold(contact))
            {
                _steepSlopeEnterElapsedTime = 0f;
                return;
            }

            _steepSlopeEnterElapsedTime += fixedDeltaTime;
            if (_steepSlopeEnterElapsedTime < _settings.SteepSlopeContactConfirmTime) return;

            // 锁定首次确认的坡面几何，之后下滑方向和强度不受相邻三角面法线波动影响。
            if (!TryLockSteepSlope(contact))
            {
                _steepSlopeEnterElapsedTime = 0f;
                return;
            }

            _isSteepSlopeConstraintActive = true;
            _isFirstConstraintStep = true;
            _steepSlopeExitElapsedTime = 0f;
        }

        /// <summary>
        /// 在已锁定状态下仅在连续稳定回到退出阈值以下后解除约束。
        /// </summary>
        /// <param name="contact">当前物理步的实时地面接触。</param>
        /// <param name="fixedDeltaTime">当前固定物理步时长，单位：秒。</param>
        private void UpdateActiveConstraint(in GroundContact contact, float fixedDeltaTime)
        {
            // 坡度重新明显超过进入阈值或停留在滞回带内时，继续保持已锁定的方向和约束。
            if (!IsBelowExitThreshold(contact))
            {
                _steepSlopeExitElapsedTime = 0f;
                return;
            }

            // 只有连续确认处于明显可行走区域后才退出，避免边缘平面与斜坡命中交替导致抖动。
            _steepSlopeExitElapsedTime += fixedDeltaTime;
            if (_steepSlopeExitElapsedTime >= _settings.SteepSlopeContactConfirmTime)
                ResetRuntimeState();
        }

        /// <summary>
        /// 判断接触是否足够陡，可以开始进入陡坡约束确认。
        /// </summary>
        /// <param name="contact">当前物理步的实时地面接触。</param>
        /// <returns>坡度达到进入阈值且不允许站立时返回 true。</returns>
        private bool IsAboveEnterThreshold(in GroundContact contact)
        {
            return !contact.IsWalkable
                   && contact.SlopeAngle >= _settings.SlopeLimit + _settings.SteepSlopeEnterAngleMargin;
        }

        /// <summary>
        /// 判断接触是否已稳定回落到可以解除陡坡约束的坡度。
        /// </summary>
        /// <param name="contact">当前物理步的实时地面接触。</param>
        /// <returns>坡度低于退出阈值时返回 true。</returns>
        private bool IsBelowExitThreshold(in GroundContact contact)
        {
            float exitThreshold = Mathf.Max(0f, _settings.SlopeLimit - _settings.SteepSlopeExitAngleMargin);
            return contact.SlopeAngle <= exitThreshold;
        }

        /// <summary>
        /// 缓存当前陡坡的几何信息，供锁定期间稳定地约束输入和叠加下滑速度。
        /// </summary>
        /// <param name="contact">满足进入阈值的当前地面接触。</param>
        /// <returns>成功得到有效沿坡下滑方向时返回 true。</returns>
        private bool TryLockSteepSlope(in GroundContact contact)
        {
            // 将重力方向投影到锁定斜面上，得到严格沿坡面向下的修正方向。
            Vector3 downhillDirection = Vector3.ProjectOnPlane(Vector3.down, contact.Hit.normal);
            if (downhillDirection.sqrMagnitude <= 0.000001f) return false;

            _lockedDownhillDirection = downhillDirection.normalized;
            _lockedSlopeAngle = contact.SlopeAngle;
            return true;
        }

        /// <summary>
        /// 清除陡坡进入确认、锁定和接触宽限状态。
        /// </summary>
        internal void ResetRuntimeState()
        {
            _isSteepSlopeConstraintActive = false;
            _isFirstConstraintStep = false;
            _steepSlopeEnterElapsedTime = 0f;
            _steepSlopeExitElapsedTime = 0f;
            _lostGroundContactElapsedTime = 0f;
            _lockedSlopeAngle = 0f;
            _lockedDownhillDirection = Vector3.zero;
        }

        /// <summary>
        /// 将超出可行走限制的坡度差转换为动画曲线的归一化输入。
        /// </summary>
        /// <param name="slopeAngle">当前接触面相对世界向上的坡度角，单位：度。</param>
        /// <returns>位于 0 到 1 的归一化超限坡度差；未超过限制时返回 0。</returns>
        private float CalculateNormalizedSlopeDifference(float slopeAngle)
        {
            // 只负责将实际角度归一化，曲线如何塑形由 GroundSettings 的 Inspector 配置决定。
            float maximumSlopeDifference = Mathf.Max(0.0001f, 90f - _settings.SlopeLimit);
            return Mathf.Clamp01((slopeAngle - _settings.SlopeLimit) / maximumSlopeDifference);
        }
    }
}

