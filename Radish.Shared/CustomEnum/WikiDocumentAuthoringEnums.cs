namespace Radish.Shared.CustomEnum;

public enum WikiDocumentDraftState
{
    Editing = 0,
    Submitted = 1,
    ChangesRequested = 2,
    Applied = 3,
    Rejected = 4,
    Withdrawn = 5
}

public enum WikiDocumentCollaboratorRole
{
    Editor = 1
}

public enum WikiDocumentCollaboratorState
{
    Pending = 0,
    Accepted = 1,
    Declined = 2,
    Revoked = 3
}

public static class WikiDocumentReviewActions
{
    public const string Submit = "Submit";
    public const string Withdraw = "Withdraw";
    public const string RequestChanges = "RequestChanges";
    public const string Reject = "Reject";
    public const string Apply = "Apply";
}
