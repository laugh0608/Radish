using System.Diagnostics.CodeAnalysis;
using Radish.Model.LogModels;
using Radish.Model.ViewModels;
using Riok.Mapperly.Abstractions;

namespace Radish.Service.Mapping;

/// <summary>审计查询输出；不映射请求体、响应体和额外数据。</summary>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target, ThrowOnMappingNullMismatch = false)]
internal static partial class AuditLogMapper
{
    [MapProperty(nameof(AuditLog.Id), nameof(AuditLogVo.VoId))]
    [MapProperty(nameof(AuditLog.DateTime), nameof(AuditLogVo.VoDateTime))]
    [MapProperty(nameof(AuditLog.UserId), nameof(AuditLogVo.VoUserId))]
    [MapProperty(nameof(AuditLog.UserName), nameof(AuditLogVo.VoUserName))]
    [MapProperty(nameof(AuditLog.TenantId), nameof(AuditLogVo.VoTenantId))]
    [MapProperty(nameof(AuditLog.OperationType), nameof(AuditLogVo.VoOperationType))]
    [MapProperty(nameof(AuditLog.Module), nameof(AuditLogVo.VoModule))]
    [MapProperty(nameof(AuditLog.Description), nameof(AuditLogVo.VoDescription))]
    [MapProperty(nameof(AuditLog.IpAddress), nameof(AuditLogVo.VoIpAddress))]
    [MapProperty(nameof(AuditLog.UserAgent), nameof(AuditLogVo.VoUserAgent))]
    [MapProperty(nameof(AuditLog.RequestPath), nameof(AuditLogVo.VoRequestPath))]
    [MapProperty(nameof(AuditLog.RequestMethod), nameof(AuditLogVo.VoRequestMethod))]
    [MapProperty(nameof(AuditLog.ResponseStatusCode), nameof(AuditLogVo.VoResponseStatusCode))]
    [MapProperty(nameof(AuditLog.Duration), nameof(AuditLogVo.VoDuration))]
    [MapProperty(nameof(AuditLog.IsSuccess), nameof(AuditLogVo.VoIsSuccess))]
    [MapProperty(nameof(AuditLog.ErrorMessage), nameof(AuditLogVo.VoErrorMessage))]
    [MapProperty(nameof(AuditLog.Level), nameof(AuditLogVo.VoLevel))]
    [return: NotNullIfNotNull(nameof(source))]
    public static partial AuditLogVo? ToAuditLogVo(AuditLog? source);

    public static partial List<AuditLogVo> ToAuditLogVoList(IEnumerable<AuditLog>? source);
}
