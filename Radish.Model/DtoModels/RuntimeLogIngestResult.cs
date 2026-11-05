using Radish.Model.ViewModels;

namespace Radish.Model.DtoModels;

/// <summary>Service 到 HTTP 适配边界的状态；不携带请求原文。</summary>
public sealed record RuntimeLogIngestResult(int StatusCode, RuntimeLogIngestVo Summary);
