using UnityEngine;

namespace CoreFramework
{
    /// <summary>
    /// 默认玩家输入提供者，通过 Reader 和 Context Mapper 将设备输入写入标准领域数据槽。
    /// </summary>
    public class PlayerInputProvider : BaseInputProvider
    {
        private readonly Blackboard _board = new Blackboard();
        private IInputReader _inputReader;
        private IInputContextMapper _contextMapper;

        public override Blackboard Board => _board;

        protected override void Awake()
        {
            base.Awake();
            InitializeInputPipeline();
        }

        public override void Tick()
        {
            if (_inputReader == null || _contextMapper == null)
                InitializeInputPipeline();

            InputActionStateStore actions = Board.GetOrCreate<InputActionStateStore>();
            _inputReader.Tick(actions);
            _contextMapper.Write(Board, actions);
        }

        private void InitializeInputPipeline()
        {
            _inputReader = CreateInputReader();
            _contextMapper = new StandardInputContextMapper(lookSensitivity);
            _inputReader.RegisterActions(Board.GetOrCreate<InputActionStateStore>());
        }
    }
}
