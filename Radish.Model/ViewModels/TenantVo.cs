using Radish.Common.TenantTool;

namespace Radish.Model.ViewModels;

public class TenantVo
{
    public string VoTenantName { get; set; } = "System";
    public TenantTypeEnum VoTenantType { get; set; }
    public string VoTenantConfigId { get; set; } = string.Empty;
    public string VoTenantHost { get; set; } = string.Empty;
    public SqlSugar.DbType? VoDbType { get; set; }
    public string VoDbConnectionStr { get; set; } = string.Empty;
    public bool VoIsEnable { get; set; } = false;
    public string VoTenantRemark { get; set; } = "There is no remark";
}