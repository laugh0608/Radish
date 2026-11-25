using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Radish.Api.Hubs;
using Radish.Api.Security;
using Xunit;

namespace Radish.Api.Tests.Security;

public sealed class WebSessionHubBoundaryTest
{
    [Theory]
    [InlineData("/hub/web/comment", true)]
    [InlineData("/hub/web/chat/negotiate", true)]
    [InlineData("/hub/notification/negotiate", true)]
    [InlineData("/hub/web/comment/other", false)]
    [InlineData("/hub/web/unknown", false)]
    [InlineData("/api/v1/User/GetUserByHttpContext", false)]
    public async Task QueryBearerOnlyAppliesToHubRequests(string path, bool accepts)
    {
        var http = new DefaultHttpContext();
        http.Request.Path = path;
        http.Request.QueryString = new QueryString("?access_token=test-bearer");
        var scheme = new AuthenticationScheme(JwtBearerDefaults.AuthenticationScheme, null, typeof(JwtBearerHandler));
        var context = new MessageReceivedContext(http, scheme, new JwtBearerOptions());

        await new ApiJwtBearerEvents().MessageReceived(context);

        Assert.Equal(accepts ? "test-bearer" : null, context.Token);
    }

    [Theory]
    [InlineData("radish-web", "/hub/comment", false)]
    [InlineData("radish-web", "/hub/chat/negotiate", false)]
    [InlineData("radish-web", "/hub/web/notification", true)]
    [InlineData("radish-client", "/hub/chat", true)]
    [InlineData("radish-console", "/hub/notification", true)]
    [InlineData(null, "/hub/comment", true)]
    public async Task LegacyHubsKeepTheirClientsAndGuestAccess(string? client, string path, bool forwarded)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.User = client is null ? new ClaimsPrincipal(new ClaimsIdentity()) :
            new ClaimsPrincipal(new ClaimsIdentity([new Claim("client_id", client)], "Bearer"));
        var invoked = false;

        await WebSessionHubBoundary.EnforceLegacyBoundaryAsync(context, () =>
        {
            invoked = true;
            return Task.CompletedTask;
        });

        Assert.Equal(forwarded, invoked);
        Assert.Equal(forwarded ? 200 : 403, context.Response.StatusCode);
    }

    [Fact]
    public async Task OnlyWebEndpointUsesWebIdentityAndAuthenticationExpiration()
    {
        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions { Args = [] });
        builder.Services.AddSignalR();
        await using var app = builder.Build();
        app.MapWebSessionHub<CommentHub>("comment");
        app.MapHub<CommentHub>("/hub/comment");

        var endpoints = ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints).OfType<RouteEndpoint>().ToArray();
        var web = endpoints.Single(endpoint => endpoint.RoutePattern.RawText == "/hub/web/comment");
        var legacy = endpoints.Single(endpoint => endpoint.RoutePattern.RawText == "/hub/comment");
        var webNegotiate = endpoints.Single(endpoint => endpoint.RoutePattern.RawText == "/hub/web/comment/negotiate");
        var legacyNegotiate = endpoints.Single(endpoint => endpoint.RoutePattern.RawText == "/hub/comment/negotiate");
        Assert.True(webNegotiate.Metadata.GetMetadata<HttpConnectionDispatcherOptions>()?.CloseOnAuthenticationExpiration);
        Assert.False(legacyNegotiate.Metadata.GetMetadata<HttpConnectionDispatcherOptions>()?.CloseOnAuthenticationExpiration);
        Assert.Contains(web.Metadata.GetOrderedMetadata<IAuthorizeData>(), requirement =>
            requirement.Policy == WebSessionHubBoundary.AuthorizationPolicy);
        Assert.Contains(webNegotiate.Metadata.GetOrderedMetadata<IAuthorizeData>(), requirement =>
            requirement.Policy == WebSessionHubBoundary.AuthorizationPolicy);
        Assert.Empty(legacy.Metadata.GetOrderedMetadata<IAuthorizeData>());
    }

}
