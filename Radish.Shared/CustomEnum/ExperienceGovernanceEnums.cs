namespace Radish.Shared.CustomEnum;

/// <summary>经验治理动作类型</summary>
public enum ExperienceGovernanceActionTypeEnum
{
    Unknown = 0,
    Review = 1,
    Freeze = 2,
    Unfreeze = 3
}

/// <summary>经验治理人工复核结论</summary>
public enum ExperienceGovernanceReviewResultEnum
{
    Unknown = 0,
    NoIssue = 1,
    Observe = 2,
    FreezeSuggest = 3
}
