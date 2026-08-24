using System.Collections.Generic;
using UnityEngine;

namespace Core.Gear
{
    /// <summary>
    /// 提供任务目标 ID 的组件接口，挂载在敌人/交互物上供 QuestConditionTracker 查询。
    /// </summary>
    public interface IQuestTargetProvider
    {
        string QuestTargetID { get; }
    }
}
