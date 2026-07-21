namespace Radish.Shared.CustomEnum;

public enum ContentModerationCaseStatus
{
    Open = 0,
    Reviewing = 1,
    Resolved = 2
}

public enum ContentModerationDecision
{
    None = 0,
    NoViolation = 1,
    Violation = 2,
    InsufficientEvidence = 3
}

public enum ContentModerationTargetDisposition
{
    None = 0,
    Keep = 1,
    Restricted = 2,
    Unavailable = 3,
    ActionPending = 4,
    ActionFailed = 5
}

public enum ContentModerationEvidenceType
{
    ReportSnapshot = 1,
    CurrentTargetSnapshot = 2,
    ModeratorNote = 3,
    ActionResult = 4
}

public enum ContentModerationTargetState
{
    Available = 1,
    Deleted = 2,
    Recalled = 3,
    Disabled = 4,
    Unavailable = 5
}

public enum UserModerationPolicyType
{
    Mute = 1,
    Ban = 2
}

public enum UserModerationStateValue
{
    Inactive = 0,
    Active = 1
}
