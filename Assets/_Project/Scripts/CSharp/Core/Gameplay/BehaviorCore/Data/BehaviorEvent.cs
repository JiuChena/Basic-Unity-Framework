using System;
using UnityEngine;

namespace BehaviorCore
{
    /// <summary>
    /// 行为时间轴上的单个运行时事件。
    /// </summary>
    [Serializable]
    public class BehaviorEvent
    {
        [Tooltip("作者期来源的 Timeline 轨道名。用于把 BehaviorClip 回填到 Timeline 时恢复原来的轨道分组。")]
        public string authoringTrackName;

        [Tooltip("事件触发时间，单位为秒，基于行为起点计算")]
        public float time;

        [Tooltip("该事件在运行时执行的具体类型")]
        public BehaviorEventType type;

        [Tooltip("参照骨骼的层级路径，留空时使用世界空间，不挂到任何宿主骨骼下")]
        public string referenceBone;

        [Tooltip("相对参照骨骼的局部位置偏移，单位为米")]
        public Vector3 positionOffset;

        [Tooltip("相对参照骨骼的局部旋转偏移，单位为度")]
        public Vector3 rotationOffset;

        [Tooltip("特效实例的局部缩放倍率")]
        public Vector3 scaleOffset = Vector3.one;

        [Tooltip("事件关联的预制体引用，如特效或投射物")]
        public GameObject prefabRef;

        [Tooltip("运行时要直接控制激活状态的目标层级路径。用于 ControlTrack/ActivationTrack 对现有物体的激活控制。")]
        public string targetObjectPath;

        [Tooltip("SetObjectActive 事件触发时要设置到的激活状态。")]
        public bool activeState = true;

        [Tooltip("特效自动回收时间，单位为秒，小于等于 0 时使用系统默认值")]
        public float autoRecycleTime = 1f;

        [Tooltip("事件播放的音频资源")]
        public AudioClip audioRef;

        [Tooltip("音频是否循环播放，适合持续施法等行为")]
        public bool audioLoop;

        [Tooltip("音频音量，0 为静音，1 为原始音量")]
        [Range(0f, 1f)]
        public float audioVolume = 1f;

        [Tooltip("事件携带的效果资产，命中或自施加时使用")]
        public BehaviorEffectAsset buffRef;

        [Tooltip("事件引用的技能数值条目 key。配置后会优先按数值定义表解析，而不是直接使用 damageMultiplier")]
        public string numericKey;

        [Tooltip("事件执行的玩法效果配置，如治疗、回能、群体增益等")]
        public BehaviorEffectAsset gameplayEffectRef;

        [Tooltip("投射物或事件附加的伤害倍率。未配置 numericKey 时作为直接倍率使用")]
        [Min(0f)]
        public float damageMultiplier = 1f;

        [Tooltip("镜头震动振幅，数值越大晃动越明显")]
        [Min(0f)]
        public float cameraShakeAmplitude;

        [Tooltip("镜头震动频率，数值越大晃动越快")]
        [Min(0f)]
        public float cameraShakeFrequency;

        [Tooltip("镜头震动持续时间，单位为秒")]
        [Min(0f)]
        public float cameraShakeDuration;
    }
}
