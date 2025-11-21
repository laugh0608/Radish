using AutoMapper;
using Radish.Model.LogModels;
using Radish.Model.ViewModels;

namespace Radish.Extension.AutoMapperExtension.CustomProfiles;

public class AuditSqlLogProfile : Profile
{
    public AuditSqlLogProfile()
    {
        // 自动识别字段前缀 Vo
        RecognizeDestinationPrefixes("Vo"); // AuditSqlLog -> AuditSqlLogVo
        CreateMap<AuditSqlLog, AuditSqlLogVo>();
        RecognizePrefixes("Vo"); // AuditSqlLogVo -> AuditSqlLog
        CreateMap<AuditSqlLogVo, AuditSqlLog>();
    }
}