namespace Radish.Shared.Constants;

/// <summary>经验治理与等级配置权威写入的稳定错误码。</summary>
public static class ExperienceGovernanceErrorCodes
{
    public const string TargetUnavailable = "Experience.TargetUnavailable";
    public const string VersionConflict = "Experience.VersionConflict";
    public const string AdjustmentWouldNotChange = "Experience.AdjustmentWouldNotChange";
    public const string AdjustmentProcessing = "Experience.AdjustmentProcessing";
    public const string AdjustmentIdempotencyConflict = "Experience.AdjustmentIdempotencyConflict";
    public const string AdjustmentIdempotencyInvalid = "Experience.AdjustmentIdempotencyInvalid";
    public const string AdjustmentReplayUnavailable = "Experience.AdjustmentReplayUnavailable";
    public const string ReviewProcessing = "Experience.ReviewProcessing";
    public const string ReviewIdempotencyConflict = "Experience.ReviewIdempotencyConflict";
    public const string ReviewIdempotencyInvalid = "Experience.ReviewIdempotencyInvalid";
    public const string ReviewReplayUnavailable = "Experience.ReviewReplayUnavailable";
    public const string LevelPreviewConflict = "Experience.LevelPreviewConflict";
    public const string LevelNoChanges = "Experience.LevelNoChanges";
}
