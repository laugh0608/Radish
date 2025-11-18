namespace Radish.Model.ViewModels;

public class AuditSqlLogVo
{
    public DateTime VoDateTime { get; set; } =  DateTime.Now;
    public string VoLevel { get; set; } = "Information";
    public string VoMessage { get; set; } = string.Empty;
    public string VoMessageTemplate { get; set; } = string.Empty;
    public string VoProperties { get; set; } = string.Empty;
}