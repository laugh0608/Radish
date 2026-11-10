namespace Radish.Model.ViewModels;

/// <summary>仅表示已提交计数；失败响应可能包含先前内部批次的提交结果。</summary>
public sealed class RuntimeLogIngestVo
{
    public int VoAccepted { get; set; }
    public int VoDuplicate { get; set; }
    public int VoRejected { get; set; }
    public string VoState { get; set; } = "unavailable";
}
