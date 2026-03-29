#nullable enable
using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Radish.Common.HttpContextTool;
using Radish.Extension.AuditLogExtension;
using Radish.IService.Base;
using Radish.Model.LogModels;
using Radish.Model.ViewModels;
using Shouldly;
using Xunit;

namespace Radish.Api.Tests;

public class AuditLogMiddlewareTests
{
    [Fact(DisplayName = "关闭响应体记录时审计中间件不应替换原始响应流")]
    public async Task InvokeAsync_Should_Keep_Original_Response_Stream_When_ResponseBodyLogging_Disabled()
    {
        using var originalBody = new MemoryStream();
        using var serviceProvider = CreateServiceProvider();

        var context = CreateContext(serviceProvider, originalBody);
        var middleware = new AuditLogMiddleware(async ctx =>
        {
            ReferenceEquals(ctx.Response.Body, originalBody).ShouldBeTrue();
            ctx.Response.ContentType = "image/png";
            await ctx.Response.Body.WriteAsync(new byte[] { 1, 2, 3 });
        }, new AuditLogOptions
        {
            Enable = true,
            LogResponseBody = false
        });

        await middleware.InvokeAsync(context, serviceProvider);

        originalBody.ToArray().ShouldBe(new byte[] { 1, 2, 3 });
    }

    [Fact(DisplayName = "开启响应体记录时应对二进制响应写入摘要而不是正文")]
    public async Task InvokeAsync_Should_Record_Binary_Response_Summary_When_ResponseBodyLogging_Enabled()
    {
        AuditLog? capturedAuditLog = null;
        var auditLogServiceMock = new Mock<IBaseService<AuditLog, AuditLogVo>>();
        auditLogServiceMock
            .Setup(service => service.AddSplitAsync(It.IsAny<AuditLog>()))
            .Callback<AuditLog>(auditLog => capturedAuditLog = auditLog)
            .ReturnsAsync([1L]);

        using var originalBody = new MemoryStream();
        using var serviceProvider = CreateServiceProvider(auditLogServiceMock.Object);

        var context = CreateContext(serviceProvider, originalBody);
        var middleware = new AuditLogMiddleware(async ctx =>
        {
            ctx.Response.ContentType = "image/png";
            await ctx.Response.Body.WriteAsync(new byte[] { 9, 8, 7, 6 });
        }, new AuditLogOptions
        {
            Enable = true,
            LogResponseBody = true
        });

        await middleware.InvokeAsync(context, serviceProvider);

        originalBody.ToArray().ShouldBe(new byte[] { 9, 8, 7, 6 });
        capturedAuditLog.ShouldNotBeNull();
        capturedAuditLog.ResponseBody.ShouldBe("[binary response omitted] contentType=image/png; length=4");
    }

    [Fact(DisplayName = "multipart 请求体应记录摘要而不是按文本读取")]
    public async Task InvokeAsync_Should_Record_Binary_Request_Summary_For_Multipart_Request()
    {
        AuditLog? capturedAuditLog = null;
        var auditLogServiceMock = new Mock<IBaseService<AuditLog, AuditLogVo>>();
        auditLogServiceMock
            .Setup(service => service.AddSplitAsync(It.IsAny<AuditLog>()))
            .Callback<AuditLog>(auditLog => capturedAuditLog = auditLog)
            .ReturnsAsync([1L]);

        using var originalBody = new MemoryStream();
        using var serviceProvider = CreateServiceProvider(auditLogServiceMock.Object);

        var context = CreateContext(serviceProvider, originalBody);
        context.Request.Method = HttpMethods.Post;
        context.Request.ContentType = "multipart/form-data; boundary=radish";
        context.Request.Body = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
        context.Request.ContentLength = 5;

        var middleware = new AuditLogMiddleware(async ctx =>
        {
            ctx.Response.ContentType = "application/json";
            await ctx.Response.WriteAsync("{\"ok\":true}");
        }, new AuditLogOptions
        {
            Enable = true,
            LogResponseBody = true
        });

        await middleware.InvokeAsync(context, serviceProvider);

        capturedAuditLog.ShouldNotBeNull();
        capturedAuditLog.RequestBody.ShouldBe("[binary request omitted] contentType=multipart/form-data; boundary=radish; length=5");
        capturedAuditLog.ResponseBody.ShouldBe("{\"ok\":true}");
    }

    private static ServiceProvider CreateServiceProvider(IBaseService<AuditLog, AuditLogVo>? auditLogService = null)
    {
        var services = new ServiceCollection();

        var currentUserAccessorMock = new Mock<ICurrentUserAccessor>();
        currentUserAccessorMock.SetupGet(accessor => accessor.Current).Returns(new CurrentUser
        {
            IsAuthenticated = true,
            UserId = 10001,
            UserName = "Tester",
            TenantId = 0
        });

        services.AddSingleton(currentUserAccessorMock.Object);

        if (auditLogService != null)
        {
            services.AddSingleton(auditLogService);
        }

        return services.BuildServiceProvider();
    }

    private static DefaultHttpContext CreateContext(IServiceProvider serviceProvider, Stream responseBody)
    {
        return new DefaultHttpContext
        {
            RequestServices = serviceProvider,
            Response =
            {
                Body = responseBody
            },
            Request =
            {
                Method = HttpMethods.Get,
                Path = "/_assets/attachments/72001"
            }
        };
    }
}
