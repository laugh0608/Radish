using Radish.Model.ViewModels;

namespace Radish.IService;

/// <summary>公开身份装饰只读服务。</summary>
public interface IUserAdornmentService
{
    /// <summary>批量读取用户当前有效的公开身份装饰。</summary>
    Task<IReadOnlyDictionary<long, UserAdornmentVo>> GetUserAdornmentsAsync(
        IReadOnlyCollection<long> userIds);

    /// <summary>读取单个用户当前有效的公开身份装饰。</summary>
    Task<UserAdornmentVo?> GetUserAdornmentAsync(long userId);
}
