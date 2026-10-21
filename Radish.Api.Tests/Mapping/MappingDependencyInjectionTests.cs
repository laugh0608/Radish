using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Radish.IRepository.Base;
using Radish.IService;
using Radish.IService.Base;
using Radish.Model;
using Radish.Model.LogModels;
using Radish.Model.ViewModels;
using Radish.Service;
using Radish.Service.Base;
using Radish.Service.Mapping;
using SqlSugar;
using Xunit;

namespace Radish.Api.Tests.Mapping;

public class MappingDependencyInjectionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GenericServices_ShouldResolveAndKeepScopedAttachmentUrls(bool autofac)
    {
        var services = new ServiceCollection();
        services.AddServiceMappings();
        services.AddScoped<IAttachmentUrlResolver, ScopedUrls>();
        services.AddScoped(typeof(IBaseService<,>), typeof(BaseService<,>));
        var mappingPairs = services.Where(x => x.ServiceType.IsGenericType && x.ServiceType.GetGenericTypeDefinition() == typeof(Func<,>))
            .Select(x => x.ServiceType.GenericTypeArguments).ToList();
        foreach (var pair in mappingPairs)
        {
            var repositoryType = typeof(IBaseRepository<>).MakeGenericType(pair[0]);
            var mock = (Mock)Activator.CreateInstance(typeof(Mock<>).MakeGenericType(repositoryType))!;
            services.AddSingleton(repositoryType, mock.Object);
        }
        var attachments = new Mock<IBaseRepository<Attachment>>();
        attachments.Setup(x => x.QueryByIdAsync(7)).ReturnsAsync(new Attachment { Id = 7, FileSize = 2048 });
        services.AddSingleton(attachments.Object);
        using var provider = CreateProvider(services, autofac);
        using var first = provider.CreateScope();
        using var second = provider.CreateScope();
        foreach (var pair in mappingPairs)
            Assert.NotNull(first.ServiceProvider.GetRequiredService(typeof(IBaseService<,>).MakeGenericType(pair)));
        var firstService = first.ServiceProvider.GetRequiredService<IBaseService<Attachment, AttachmentVo>>();
        var secondService = second.ServiceProvider.GetRequiredService<IBaseService<Attachment, AttachmentVo>>();
        var a = await firstService.QueryByIdAsync(7);
        var b = await firstService.QueryByIdAsync(7);
        var c = await secondService.QueryByIdAsync(7);
        Assert.NotNull(a);
        Assert.NotNull(b);
        Assert.NotNull(c);
        Assert.Equal(a.VoUrl, b.VoUrl);
        Assert.NotEqual(a.VoUrl, c.VoUrl);
        Assert.EndsWith("/7", a.VoUrl);
        Assert.EndsWith("/7/Thumbnail", a.VoThumbnailUrl);
        Assert.Equal("2 KB", a.VoFileSizeFormatted);
    }

    [Fact]
    public async Task BaseService_ShouldPreserveMissingEntityPageOrderAndTotal()
    {
        var repository = new Mock<IBaseRepository<Category>>(MockBehavior.Strict);
        repository.Setup(x => x.QueryByIdAsync(1)).ReturnsAsync((Category?)null);
        repository.Setup(x => x.QueryPageAsync(null, 2, 2, null, OrderByType.Asc))
            .ReturnsAsync((new List<Category> { new("second") { Id = 7 }, new("third") { Id = 3 } }, 17));
        var service = new BaseService<Category, CategoryVo>(ForumMapper.ToCategoryVo, repository.Object);
        Assert.Null(await service.QueryByIdAsync(1));
        var (data, total) = await service.QueryPageAsync(null, 2, 2, null, OrderByType.Asc);
        Assert.Equal(17, total);
        Assert.Equal(new long[] { 7, 3 }, data.Select(x => x.VoId));
        repository.VerifyAll();
    }

    [Fact]
    public async Task AuditRead_ShouldMapDetailsThroughActualService()
    {
        var repository = new Mock<IBaseRepository<AuditLog>>(MockBehavior.Strict);
        var source = new AuditLog { Id = 19, UserId = 42, TenantId = 8, Module = "Console", OperationType = "Read", RequestBody = "private-request", ResponseBody = "private-response" };
        repository.Setup(x => x.QueryByIdAsync(19)).ReturnsAsync(source);
        var result = await new AuditLogService(repository.Object).QueryByIdAsync(19);
        Assert.NotNull(result);
        Assert.Equal(19, result.VoId);
        Assert.Equal(42, result.VoUserId);
        Assert.Equal(8, result.VoTenantId);
        Assert.Equal("Console", result.VoModule);
        Assert.DoesNotContain("private-", System.Text.Json.JsonSerializer.Serialize(result));
        repository.VerifyAll();
    }

    private static ServiceProviderOwner CreateProvider(IServiceCollection services, bool autofac)
    {
        if (!autofac) return new ServiceProviderOwner(services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true }));
        var builder = new ContainerBuilder();
        builder.Populate(services);
        return new ServiceProviderOwner(new AutofacServiceProvider(builder.Build()));
    }

    private sealed class ServiceProviderOwner(IServiceProvider provider) : IServiceProvider, IDisposable
    {
        public object? GetService(Type serviceType) => provider.GetService(serviceType);
        public void Dispose() => ((IDisposable)provider).Dispose();
    }

    private sealed class ScopedUrls : IAttachmentUrlResolver
    {
        private readonly string _scope = Guid.NewGuid().ToString("N");
        public string ResolveAttachmentUrl(long attachmentId) => $"/{_scope}/{attachmentId}";
        public string ResolveAttachmentUrl(long attachmentId, AttachmentUrlVariant variant) => $"/{_scope}/{attachmentId}/{variant}";
    }
}
