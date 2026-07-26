namespace Radish.Shared.Constants;

public static class WikiAttachmentErrorCodes
{
    public const string InvalidReference = "WikiAttachment.InvalidReference";
    public const string ReferenceForbidden = "WikiAttachment.ReferenceForbidden";
    public const string CrossTenant = "WikiAttachment.CrossTenant";
    public const string TypeMismatch = "WikiAttachment.TypeMismatch";
    public const string ReferenceConflict = "WikiAttachment.ReferenceConflict";
    public const string SourceNotFound = "WikiAttachment.SourceNotFound";
    public const string AccessUnavailable = "WikiAttachment.AccessUnavailable";

    public static string ResolveMessageKey(string code) => code switch
    {
        InvalidReference => "error.wiki_attachment.invalid_reference",
        ReferenceForbidden => "error.wiki_attachment.reference_forbidden",
        CrossTenant => "error.wiki_attachment.cross_tenant",
        TypeMismatch => "error.wiki_attachment.type_mismatch",
        ReferenceConflict => "error.wiki_attachment.reference_conflict",
        SourceNotFound => "error.wiki_attachment.source_not_found",
        AccessUnavailable => "error.wiki_attachment.access_unavailable",
        _ => "error.wiki_attachment.access_unavailable"
    };
}
