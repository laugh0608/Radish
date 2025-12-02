using Microsoft.EntityFrameworkCore;
using OpenIddict.EntityFrameworkCore;

namespace Radish.Auth.OpenIddict;

/// <summary>
/// EF Core DbContext 专门用于承载 OpenIddict 的实体（客户端、授权、作用域、令牌等）。
/// 仅负责身份/授权相关数据，业务数据仍由 SqlSugar 负责。
/// </summary>
public class AuthOpenIddictDbContext : DbContext
{
    public AuthOpenIddictDbContext(DbContextOptions<AuthOpenIddictDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // 注册 OpenIddict 默认实体模型（使用内置实体类型和映射）
        builder.UseOpenIddict();
    }
}
