using System;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace BehaviorEditor
{
    /// <summary>
    /// BehaviorEditor 行为数据层使用的通用效果资产标记基类。
    /// 具体项目可在各自模块中继承它实现真正的效果逻辑。
    /// </summary>
    [MovedFrom("BehaviorCore")]
    public abstract class BehaviorEffectAsset : ScriptableObject
    {
    }
}
