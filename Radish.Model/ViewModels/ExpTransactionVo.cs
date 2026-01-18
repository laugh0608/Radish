namespace Radish.Model.ViewModels;

/// <summary>
/// 经验值交易记录视图模型
/// </summary>
public class ExpTransactionVo
{
    /// <summary>
    /// 交易 ID
    /// </summary>
    public long VoId { get; set; }

    /// <summary>
    /// 用户 ID
    /// </summary>
    public long VoUserId { get; set; }

    /// <summary>
    /// 用户名
    /// </summary>
    /// <remarks>用于前端显示，避免前端再次查询</remarks>
    public string? VoUserName { get; set; }

    /// <summary>
    /// 经验值类型
    /// </summary>
    /// <remarks>
    /// POST_CREATE, POST_LIKED, COMMENT_CREATE, COMMENT_LIKED,
    /// COMMENT_REPLIED, LIKE_OTHERS, GOD_COMMENT, SOFA_COMMENT,
    /// DAILY_LOGIN, WEEKLY_LOGIN, PROFILE_COMPLETE, FIRST_POST,
    /// FIRST_COMMENT, ADMIN_ADJUST, PENALTY
    /// </remarks>
    public string VoExpType { get; set; } = string.Empty;

    /// <summary>
    /// 经验值类型显示名称
    /// </summary>
    /// <remarks>用于前端显示中文名称</remarks>
    public string VoExpTypeDisplay { get; set; } = string.Empty;

    /// <summary>
    /// 经验值变动量
    /// </summary>
    /// <remarks>正数表示增加，负数表示扣除</remarks>
    public int VoExpAmount { get; set; }

    /// <summary>
    /// 业务类型
    /// </summary>
    public string? VoBusinessType { get; set; }

    /// <summary>
    /// 业务 ID
    /// </summary>
    public long? VoBusinessId { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    public string? VoRemark { get; set; }

    /// <summary>
    /// 变动前经验值
    /// </summary>
    public long VoExpBefore { get; set; }

    /// <summary>
    /// 变动后经验值
    /// </summary>
    public long VoExpAfter { get; set; }

    /// <summary>
    /// 变动前等级
    /// </summary>
    public int VoLevelBefore { get; set; }

    /// <summary>
    /// 变动后等级
    /// </summary>
    public int VoLevelAfter { get; set; }

    /// <summary>
    /// 是否升级
    /// </summary>
    /// <remarks>用于前端高亮显示升级记录</remarks>
    public bool VoIsLevelUp => VoLevelAfter > VoLevelBefore;

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime VoCreateTime { get; set; }
}
