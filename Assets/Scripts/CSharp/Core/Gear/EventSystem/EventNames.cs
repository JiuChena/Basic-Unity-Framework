namespace Core.Gear
{
    /// <summary>
    /// 事件名称常量表，所有事件字符串统一在此定义，消除裸字符串拼写错误。
    /// </summary>
    public static class EventNames
    {
        public const string UpdateControlCH = nameof(UpdateControlCH);
        public const string LoadSceneProgress = nameof(LoadSceneProgress);
        public const string PlayerDeath = nameof(PlayerDeath);
        public const string EnemyDeath = nameof(EnemyDeath);
        public const string HPChanged = nameof(HPChanged);
        public const string EnergyChanged = nameof(EnergyChanged);
        public const string TalentCDChanged = nameof(TalentCDChanged);
        public const string BurstCDChanged = nameof(BurstCDChanged);
        public const string BagUpdated = nameof(BagUpdated);
        public const string StorePurchased = nameof(StorePurchased);
        public const string UnitStatsUpdated = nameof(UnitStatsUpdated);
        public const string CharacterStatsUpdated = UnitStatsUpdated;
        public const string SkillLevelUpdated = nameof(SkillLevelUpdated);
        public const string InteractionChanged = nameof(InteractionChanged);
        public const string UnitSwitched = nameof(UnitSwitched);
        public const string CharacterSwitched = UnitSwitched;
        public const string UnitDeath = nameof(UnitDeath);
        public const string BuffApplied = nameof(BuffApplied);
        public const string BuffRemoved = nameof(BuffRemoved);
        public const string QuestAccepted = nameof(QuestAccepted);
        public const string QuestProgressUpdated = nameof(QuestProgressUpdated);
        public const string QuestStageAdvanced = nameof(QuestStageAdvanced);
        public const string QuestCompleted = nameof(QuestCompleted);
        public const string DialogueStarted = nameof(DialogueStarted);
        public const string DialogueEnded = nameof(DialogueEnded);
        public const string AreaEntered = nameof(AreaEntered);
        public const string InteractionPerformed = nameof(InteractionPerformed);
    }

}
