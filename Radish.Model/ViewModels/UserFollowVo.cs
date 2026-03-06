namespace Radish.Model.ViewModels;

/// <summary>用户关注关系视图模型</summary>
public class UserFollowVo
{
    /// <summary>关系 ID</summary>
    public long VoId { get; set; }

    /// <summary>关注者用户 ID</summary>
    public long VoFollowerUserId { get; set; }

    /// <summary>被关注用户 ID</summary>
    public long VoFollowingUserId { get; set; }

    /// <summary>关注时间</summary>
    public DateTime VoFollowTime { get; set; }

    /// <summary>租户 ID</summary>
    public long VoTenantId { get; set; }
}

/// <summary>关注列表用户项</summary>
public class UserFollowUserVo
{
    /// <summary>用户 ID</summary>
    public long VoUserId { get; set; }

    /// <summary>用户名</summary>
    public string VoUserName { get; set; } = string.Empty;

    /// <summary>显示名</summary>
    public string? VoDisplayName { get; set; }

    /// <summary>头像地址</summary>
    public string? VoAvatarUrl { get; set; }

    /// <summary>是否互关</summary>
    public bool VoIsMutualFollow { get; set; }

    /// <summary>关注时间</summary>
    public DateTime VoFollowTime { get; set; }
}

/// <summary>关注状态视图模型</summary>
public class UserFollowStatusVo
{
    /// <summary>目标用户 ID</summary>
    public long VoTargetUserId { get; set; }

    /// <summary>当前用户是否已关注目标用户</summary>
    public bool VoIsFollowing { get; set; }

    /// <summary>目标用户是否已关注当前用户</summary>
    public bool VoIsFollower { get; set; }

    /// <summary>目标用户粉丝数</summary>
    public int VoFollowerCount { get; set; }

    /// <summary>目标用户关注数</summary>
    public int VoFollowingCount { get; set; }
}

/// <summary>我的关系链汇总</summary>
public class UserFollowSummaryVo
{
    /// <summary>我的粉丝数</summary>
    public int VoFollowerCount { get; set; }

    /// <summary>我的关注数</summary>
    public int VoFollowingCount { get; set; }
}
