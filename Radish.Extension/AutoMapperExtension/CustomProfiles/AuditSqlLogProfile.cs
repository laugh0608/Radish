using AutoMapper;
using Radish.Model.LogModels;
using Radish.Model.ViewModels;

namespace Radish.Extension.AutoMapperExtension.CustomProfiles;

public class AuditSqlLogProfile : Profile
{
    public AuditSqlLogProfile()
    {
        RecognizeDestinationPrefixes("Vo"); // AuditSqlLog -> AuditSqlLogVo
        CreateMap<AuditSqlLog, AuditSqlLogVo>();
        RecognizePrefixes("Vo"); // AuditSqlLogVo -> AuditSqlLog
        CreateMap<AuditSqlLogVo, AuditSqlLog>();
    }
}