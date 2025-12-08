using System.Collections.Generic;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Radish.Common.HttpContextTool;
using Xunit;

namespace Radish.Api.Tests;

[TestSubject(typeof(HttpContextUser))]
public class HttpContextUserTests
{
    private static DefaultHttpContext CreateHttpContextWithClaims(IEnumerable<Claim> claims)
    {
        var context = new DefaultHttpContext();
        var identity = new ClaimsIdentity(claims, "TestAuth");
        context.User = new ClaimsPrincipal(identity);
        return context;
    }

    [Fact]
    public void Should_Read_Oidc_Style_Claims_Correctly()
    {
        // Arrange: OIDC 风格 token，使用 sub/name/tenant_id
        var claims = new List<Claim>
        {
            new("sub", "20002"),
            new("name", "test-user"),
            new("tenant_id", "30000"),
            new(ClaimTypes.Role, "System")
        };

        var httpContext = CreateHttpContextWithClaims(claims);
        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        var httpContextUser = new HttpContextUser(accessor, NullLogger<HttpContextUser>.Instance);

        // Act
        var userId = httpContextUser.UserId;
        var userName = httpContextUser.UserName;
        var tenantId = httpContextUser.TenantId;

        // Assert
        Assert.Equal(20002, userId);
        Assert.Equal("test-user", userName);
        Assert.Equal(30000, tenantId);
    }

    [Fact]
    public void Should_Fallback_To_Legacy_Jwt_Claims_When_Oidc_Claims_Missing()
    {
        // Arrange: 老 JWT 风格 token，只包含 jti/TenantId/ClaimTypes.Name
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Jti, "123"),
            new("TenantId", "456"),
            new(ClaimTypes.Name, "legacy-user"),
            new(ClaimTypes.Role, "Admin")
        };

        var httpContext = CreateHttpContextWithClaims(claims);
        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        var httpContextUser = new HttpContextUser(accessor, NullLogger<HttpContextUser>.Instance);

        // Act
        var userId = httpContextUser.UserId;
        var userName = httpContextUser.UserName;
        var tenantId = httpContextUser.TenantId;

        // Assert
        Assert.Equal(123, userId);
        Assert.Equal("legacy-user", userName);
        Assert.Equal(456, tenantId);
    }

    [Fact]
    public void Should_Return_Defaults_When_Not_Authenticated()
    {
        // Arrange: 空 HttpContext，无认证用户
        var httpContext = new DefaultHttpContext();
        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        var httpContextUser = new HttpContextUser(accessor, NullLogger<HttpContextUser>.Instance);

        // Act
        var isAuthenticated = httpContextUser.IsAuthenticated();
        var userId = httpContextUser.UserId;
        var userName = httpContextUser.UserName;
        var tenantId = httpContextUser.TenantId;

        // Assert
        Assert.False(isAuthenticated);
        Assert.Equal(0, userId);
        Assert.Equal(string.Empty, userName);
        Assert.Equal(0, tenantId);
    }
}
