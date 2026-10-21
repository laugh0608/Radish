using System.Diagnostics.CodeAnalysis;
using Radish.Model.LogModels;
using Radish.Model.ViewModels;
using Riok.Mapperly.Abstractions;

namespace Radish.Service.Mapping;

/// <summary>业务映射；字段忽略项保留服务补全、写入保护和既有默认值。</summary>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target, UseDeepCloning = true,
    ThrowOnMappingNullMismatch = false, AutoUserMappings = false)]
internal static partial class AuditSqlLogMapper
{
    // AuditSqlLog → AuditSqlLogVo
    [MapProperty(nameof(AuditSqlLog.DateTime), nameof(AuditSqlLogVo.VoDateTime))]
    [MapProperty(nameof(AuditSqlLog.Level), nameof(AuditSqlLogVo.VoLevel))]
    [MapProperty(nameof(AuditSqlLog.Message), nameof(AuditSqlLogVo.VoMessage))]
    [MapProperty(nameof(AuditSqlLog.MessageTemplate), nameof(AuditSqlLogVo.VoMessageTemplate))]
    [MapProperty(nameof(AuditSqlLog.Properties), nameof(AuditSqlLogVo.VoProperties))]
    [return: NotNullIfNotNull(nameof(source))]
    public static partial AuditSqlLogVo? ToAuditSqlLogVo(AuditSqlLog? source);

    public static partial List<AuditSqlLogVo> ToAuditSqlLogVoList(IEnumerable<AuditSqlLog>? source);

    // AuditSqlLogVo → AuditSqlLog
    [MapProperty(nameof(AuditSqlLogVo.VoDateTime), nameof(AuditSqlLog.DateTime))]
    [MapProperty(nameof(AuditSqlLogVo.VoLevel), nameof(AuditSqlLog.Level))]
    [MapProperty(nameof(AuditSqlLogVo.VoMessage), nameof(AuditSqlLog.Message))]
    [MapProperty(nameof(AuditSqlLogVo.VoMessageTemplate), nameof(AuditSqlLog.MessageTemplate))]
    [MapProperty(nameof(AuditSqlLogVo.VoProperties), nameof(AuditSqlLog.Properties))]
    [MapperIgnoreTarget(nameof(AuditSqlLog.Id))]
    [return: NotNullIfNotNull(nameof(source))]
    public static partial AuditSqlLog? ToAuditSqlLog(AuditSqlLogVo? source);

    public static partial List<AuditSqlLog> ToAuditSqlLogList(IEnumerable<AuditSqlLogVo>? source);

}
