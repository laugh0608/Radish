using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OpenIddict.Abstractions;
using Radish.Api.Controllers;
using Radish.Model;
using Radish.Model.ViewModels.Client;
using Xunit;

namespace Radish.Api.Tests.Controllers;

[TestSubject(typeof(ClientController))]
public class ClientControllerTest
{
    private static ClientController CreateController(Mock<IOpenIddictApplicationManager> managerMock)
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim("sub", "20002"),
                new Claim(ClaimTypes.Role, "System")
            }, "TestAuth"))
        };

        var logger = NullLogger<ClientController>.Instance;
        var controller = new ClientController(managerMock.Object, logger)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            }
        };

        return controller;
    }

    [Fact]
    public async Task GetClients_Should_Exclude_SoftDeleted_And_Apply_Pagination()
    {
        // Arrange
        var clients = new List<FakeClient>
        {
            new()
            {
                Id = "1",
                ClientId = "client-1",
                DisplayName = "Client 1",
                Permissions =
                {
                    OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
                    OpenIddictConstants.Permissions.Prefixes.Scope + "openid",
                    OpenIddictConstants.Permissions.Prefixes.Scope + "radish-api"
                }
            },
            new()
            {
                Id = "2",
                ClientId = "client-2",
                DisplayName = "Client 2",
                Permissions =
                {
                    OpenIddictConstants.Permissions.GrantTypes.ClientCredentials,
                    OpenIddictConstants.Permissions.Prefixes.Scope + "radish-api"
                },
                Properties = { ["IsDeleted"] = JsonSerializer.SerializeToElement("true") } // 软删除
            }
        };

        var managerMock = CreateManagerMock(clients);
        var controller = CreateController(managerMock);

        // Act
        var result = await controller.GetClients(page: 1, pageSize: 10, keyword: null);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.ResponseData);
        Assert.Single(result.ResponseData.Data); // 已删除的客户端被过滤掉
        Assert.Equal("client-1", result.ResponseData.Data[0].ClientId);
    }

    [Fact]
    public async Task CreateClient_Should_Fail_When_ClientId_Already_Exists_And_Not_Deleted()
    {
        // Arrange
        var clients = new List<FakeClient>
        {
            new()
            {
                Id = "1",
                ClientId = "existing-client",
                DisplayName = "Existing"
            }
        };

        var managerMock = CreateManagerMock(clients);
        var controller = CreateController(managerMock);

        var dto = new CreateClientDto
        {
            ClientId = "existing-client",
            DisplayName = "Duplicate",
            GrantTypes = new List<string> { "authorization_code" },
            Scopes = new List<string> { "openid", "radish-api" }
        };

        // Act
        var result = await controller.CreateClient(dto);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("已存在", result.MessageInfo);
    }

    [Fact]
    public async Task CreateClient_Should_Succeed_And_Return_Secret_When_Required()
    {
        // Arrange
        var clients = new List<FakeClient>();
        var managerMock = CreateManagerMock(clients);
        var controller = CreateController(managerMock);

        var dto = new CreateClientDto
        {
            ClientId = "new-client",
            DisplayName = "New Client",
            RequireClientSecret = true,
            GrantTypes = new List<string> { "client_credentials" },
            Scopes = new List<string> { "radish-api" }
        };

        // Act
        var result = await controller.CreateClient(dto);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.ResponseData);
        Assert.Equal("new-client", result.ResponseData.ClientId);
        Assert.NotNull(result.ResponseData.ClientSecret);
        Assert.NotEqual("(无需密钥)", result.ResponseData.ClientSecret);

        // 验证管理器中确实创建了客户端
        Assert.Contains(clients, c => c.ClientId == "new-client");
    }

    [Fact]
    public async Task DeleteClient_Should_Mark_Client_As_Deleted()
    {
        // Arrange
        var clients = new List<FakeClient>
        {
            new()
            {
                Id = "1",
                ClientId = "to-delete",
                DisplayName = "To Delete"
            }
        };

        var managerMock = CreateManagerMock(clients);
        var controller = CreateController(managerMock);

        // Act
        var result = await controller.DeleteClient("1");

        // Assert
        Assert.True(result.IsSuccess);

        var client = clients.Single(c => c.Id == "1");
        Assert.True(client.Properties.TryGetValue("IsDeleted", out var value));
        Assert.Equal("true", value.GetString());
    }

    [Fact]
    public async Task ResetClientSecret_Should_Update_Secret_And_Return_New_Value()
    {
        // Arrange
        var clients = new List<FakeClient>
        {
            new()
            {
                Id = "1",
                ClientId = "reset-secret-client",
                DisplayName = "Reset Secret Client",
                ClientSecret = "old-secret"
            }
        };

        var managerMock = CreateManagerMock(clients);
        var controller = CreateController(managerMock);

        // Act
        var result = await controller.ResetClientSecret("1");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.ResponseData);
        Assert.Equal("reset-secret-client", result.ResponseData.ClientId);
        Assert.NotEqual("old-secret", result.ResponseData.ClientSecret);

        var client = clients.Single(c => c.Id == "1");
        Assert.Equal(result.ResponseData.ClientSecret, client.ClientSecret);
    }

    #region Helpers

    private sealed class FakeClient
    {
        public string? Id { get; set; }
        public string? ClientId { get; set; }
        public string? DisplayName { get; set; }
        public string? ClientSecret { get; set; }
        public HashSet<string> Permissions { get; } = new();
        public HashSet<Uri> RedirectUris { get; } = new();
        public HashSet<Uri> PostLogoutRedirectUris { get; } = new();
        public HashSet<string> Requirements { get; } = new();
        public Dictionary<string, JsonElement> Properties { get; } = new();
    }

    private static Mock<IOpenIddictApplicationManager> CreateManagerMock(List<FakeClient> clients)
    {
        var mock = new Mock<IOpenIddictApplicationManager>();

        // 列表查询（过滤软删除）
        mock.Setup(m => m.ListAsync(It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .Returns(() => clients
                .Where(c => !c.Properties.TryGetValue("IsDeleted", out var v) || v.GetString() != "true")
                .Cast<object>()
                .ToAsyncEnumerable());

        // 通过 Id/ClientId 查找
        mock.Setup(m => m.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string id, CancellationToken _) =>
            {
                object? app = clients.FirstOrDefault(c => c.Id == id);
                return new ValueTask<object?>(app);
            });

        mock.Setup(m => m.FindByClientIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string clientId, CancellationToken _) =>
            {
                object? app = clients.FirstOrDefault(c => c.ClientId == clientId);
                return new ValueTask<object?>(app);
            });

        // 读取字段
        mock.Setup(m => m.GetIdAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns((object app, CancellationToken _) => new ValueTask<string?>(((FakeClient)app).Id));

        mock.Setup(m => m.GetClientIdAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns((object app, CancellationToken _) => new ValueTask<string?>(((FakeClient)app).ClientId));

        mock.Setup(m => m.GetDisplayNameAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns((object app, CancellationToken _) => new ValueTask<string?>(((FakeClient)app).DisplayName));

        mock.Setup(m => m.GetClientTypeAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<string?>((string?)null));

        mock.Setup(m => m.GetConsentTypeAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<string?>((string?)null));

        mock.Setup(m => m.GetPermissionsAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns((object app, CancellationToken _) =>
            {
                var client = (FakeClient)app;
                return new ValueTask<System.Collections.Immutable.ImmutableArray<string>>(
                    System.Collections.Immutable.ImmutableArray.CreateRange(client.Permissions));
            });

        mock.Setup(m => m.GetRedirectUrisAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns((object app, CancellationToken _) =>
            {
                var client = (FakeClient)app;
                return new ValueTask<System.Collections.Immutable.ImmutableArray<string>>(
                    System.Collections.Immutable.ImmutableArray.CreateRange(client.RedirectUris.Select(u => u.ToString())));
            });

        mock.Setup(m => m.GetPostLogoutRedirectUrisAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns((object app, CancellationToken _) =>
            {
                var client = (FakeClient)app;
                return new ValueTask<System.Collections.Immutable.ImmutableArray<string>>(
                    System.Collections.Immutable.ImmutableArray.CreateRange(client.PostLogoutRedirectUris.Select(u => u.ToString())));
            });

        mock.Setup(m => m.GetRequirementsAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns((object app, CancellationToken _) =>
            {
                var client = (FakeClient)app;
                return new ValueTask<System.Collections.Immutable.ImmutableArray<string>>(
                    System.Collections.Immutable.ImmutableArray.CreateRange(client.Requirements));
            });

        mock.Setup(m => m.GetPropertiesAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns((object app, CancellationToken _) =>
            {
                var client = (FakeClient)app;
                return new ValueTask<System.Collections.Immutable.ImmutableDictionary<string, JsonElement>>(
                    System.Collections.Immutable.ImmutableDictionary.CreateRange(client.Properties));
            });

        // Populate：从 FakeClient 填充到 descriptor（供 Delete/ResetSecret/Update 使用）
        mock.Setup(m => m.PopulateAsync(It.IsAny<OpenIddictApplicationDescriptor>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns((OpenIddictApplicationDescriptor descriptor, object app, CancellationToken _) =>
            {
                var client = (FakeClient)app;
                descriptor.ClientId = client.ClientId;
                descriptor.DisplayName = client.DisplayName;
                descriptor.ClientSecret = client.ClientSecret;

                descriptor.Permissions.Clear();
                foreach (var p in client.Permissions) descriptor.Permissions.Add(p);

                descriptor.RedirectUris.Clear();
                foreach (var uri in client.RedirectUris) descriptor.RedirectUris.Add(uri);

                descriptor.PostLogoutRedirectUris.Clear();
                foreach (var uri in client.PostLogoutRedirectUris) descriptor.PostLogoutRedirectUris.Add(uri);

                descriptor.Requirements.Clear();
                foreach (var r in client.Requirements) descriptor.Requirements.Add(r);

                descriptor.Properties.Clear();
                foreach (var kv in client.Properties) descriptor.Properties[kv.Key] = kv.Value;

                return ValueTask.CompletedTask;
            });

        // Create：从 descriptor 创建新 FakeClient
        mock.Setup(m => m.CreateAsync(It.IsAny<OpenIddictApplicationDescriptor>(), It.IsAny<CancellationToken>()))
            .Returns((OpenIddictApplicationDescriptor descriptor, CancellationToken _) =>
            {
                var id = (descriptor.ClientId ?? System.Guid.NewGuid().ToString()) + "-id";
                var client = new FakeClient
                {
                    Id = id,
                    ClientId = descriptor.ClientId,
                    DisplayName = descriptor.DisplayName,
                    ClientSecret = descriptor.ClientSecret
                };

                foreach (var p in descriptor.Permissions) client.Permissions.Add(p);
                foreach (var uri in descriptor.RedirectUris) client.RedirectUris.Add(uri);
                foreach (var uri in descriptor.PostLogoutRedirectUris) client.PostLogoutRedirectUris.Add(uri);
                foreach (var r in descriptor.Requirements) client.Requirements.Add(r);
                foreach (var kv in descriptor.Properties) client.Properties[kv.Key] = kv.Value;

                clients.Add(client);
                return new ValueTask<object>(client);
            });

        // Update：用 descriptor 覆盖现有 FakeClient
        mock.Setup(m => m.UpdateAsync(It.IsAny<object>(), It.IsAny<OpenIddictApplicationDescriptor>(), It.IsAny<CancellationToken>()))
            .Returns((object app, OpenIddictApplicationDescriptor descriptor, CancellationToken _) =>
            {
                var client = (FakeClient)app;
                client.ClientId = descriptor.ClientId;
                client.DisplayName = descriptor.DisplayName;
                client.ClientSecret = descriptor.ClientSecret;

                client.Permissions.Clear();
                foreach (var p in descriptor.Permissions) client.Permissions.Add(p);

                client.RedirectUris.Clear();
                foreach (var uri in descriptor.RedirectUris) client.RedirectUris.Add(uri);

                client.PostLogoutRedirectUris.Clear();
                foreach (var uri in descriptor.PostLogoutRedirectUris) client.PostLogoutRedirectUris.Add(uri);

                client.Requirements.Clear();
                foreach (var r in descriptor.Requirements) client.Requirements.Add(r);

                client.Properties.Clear();
                foreach (var kv in descriptor.Properties) client.Properties[kv.Key] = kv.Value;

                return ValueTask.CompletedTask;
            });

        return mock;
    }

    #endregion
}
