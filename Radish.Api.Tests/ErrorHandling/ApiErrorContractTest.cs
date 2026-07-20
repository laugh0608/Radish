using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Radish.Api.ErrorHandling;
using Radish.Api.Filters;
using Radish.Common.Exceptions;
using Radish.Model;
using Radish.Shared.Constants;
using Xunit;

namespace Radish.Api.Tests.ErrorHandling;

public class ApiErrorContractTest
{
    [Fact]
    public void GenericFailed_ShouldUseBadRequestAndOmitDeveloperMessage()
    {
        var response = MessageModel<string>.Failed("请求失败");

        Assert.False(response.IsSuccess);
        Assert.Equal(StatusCodes.Status400BadRequest, response.StatusCode);
        Assert.Null(response.MessageInfoDev);
    }

    [Fact]
    public async Task ExceptionHandler_ShouldHideUnknownExceptionAndReturnTraceId()
    {
        var handler = new ApiExceptionHandler(NullLogger<ApiExceptionHandler>.Instance);
        var context = CreateApiContext();

        var handled = await handler.TryHandleAsync(
            context,
            new InvalidOperationException("password=secret; SQL SELECT * FROM User"),
            CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.Equal(context.TraceIdentifier, context.Response.Headers["X-Correlation-ID"]);

        var json = await ReadResponseJsonAsync(context);
        Assert.Equal(ApiErrorCodes.UnexpectedError, json.RootElement.GetProperty("code").GetString());
        Assert.Equal(context.TraceIdentifier, json.RootElement.GetProperty("traceId").GetString());
        Assert.DoesNotContain("secret", json.RootElement.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SELECT", json.RootElement.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.False(json.RootElement.TryGetProperty("messageArguments", out _));
    }

    [Fact]
    public async Task ExceptionHandler_ShouldPreserveExplicitBusinessContract()
    {
        var handler = new ApiExceptionHandler(NullLogger<ApiExceptionHandler>.Instance);
        var context = CreateApiContext();
        var exception = new BusinessException(
            "资源状态已发生变化，请刷新后重试",
            StatusCodes.Status409Conflict,
            ApiErrorCodes.Conflict,
            "error.common.conflict");

        var handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status409Conflict, context.Response.StatusCode);
        var json = await ReadResponseJsonAsync(context);
        Assert.Equal(ApiErrorCodes.Conflict, json.RootElement.GetProperty("code").GetString());
        Assert.Equal("error.common.conflict", json.RootElement.GetProperty("messageKey").GetString());
    }

    [Fact]
    public async Task ExceptionHandler_Should_Expose_Only_Normalized_Scalar_MessageArguments()
    {
        var handler = new ApiExceptionHandler(NullLogger<ApiExceptionHandler>.Instance);
        var context = CreateApiContext();
        var exception = new BusinessException(
            "上传限制",
            StatusCodes.Status429TooManyRequests,
            "Attachment.UploadFrequencyLimitReached",
            "error.attachment.upload_frequency_limit_reached",
            5,
            new { Password = "secret" },
            double.NaN,
            "safe\0value");

        var handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        Assert.True(handled);
        var json = await ReadResponseJsonAsync(context);
        var arguments = json.RootElement.GetProperty("messageArguments");
        Assert.Equal(5, arguments[0].GetInt32());
        Assert.Equal("[unsupported]", arguments[1].GetString());
        Assert.Equal("[unsupported]", arguments[2].GetString());
        Assert.Equal("safe value", arguments[3].GetString());
        Assert.DoesNotContain("secret", json.RootElement.GetRawText(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MessageArgumentNormalizer_Should_Limit_Argument_Count_And_Sanitize_Control_Characters()
    {
        var arguments = ApiMessageArgumentNormalizer.Normalize(
            new object[] { '\0', 1, 2, 3, 4, 5, 6, 7, 8, 9 });

        Assert.Equal(8, arguments.Length);
        Assert.Equal(" ", arguments[0]);
        Assert.Equal(7, arguments[^1]);
    }

    [Fact]
    public async Task ExceptionPipeline_ShouldBuildAndHandleUnknownApiException()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton<ApiExceptionHandler>();
        await using var application = builder.Build();
        application.UseApiExceptionHandler();
        application.Run(_ => throw new InvalidOperationException("secret startup regression"));
        var pipeline = ((IApplicationBuilder)application).Build();
        var context = CreateApiContext();
        context.RequestServices = application.Services;

        await pipeline(context);

        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        var json = await ReadResponseJsonAsync(context);
        Assert.Equal(ApiErrorCodes.UnexpectedError, json.RootElement.GetProperty("code").GetString());
        Assert.DoesNotContain("secret", json.RootElement.GetRawText(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ResultFilter_ShouldSynchronizeHttpStatusAndFillStableContract()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "trace-filter";
        var actionContext = new ActionContext(
            httpContext,
            new RouteData(),
            new ActionDescriptor());
        var response = new MessageModel
        {
            IsSuccess = false,
            StatusCode = StatusCodes.Status404NotFound,
            MessageInfo = "资源不存在"
        };
        var objectResult = new ObjectResult(response);
        var controller = new object();
        var filters = new List<IFilterMetadata>();
        var executingContext = new ResultExecutingContext(
            actionContext,
            filters,
            objectResult,
            controller);
        var attribute = new ApiErrorContractAttribute();

        await attribute.OnResultExecutionAsync(
            executingContext,
            () => Task.FromResult(new ResultExecutedContext(
                actionContext,
                filters,
                objectResult,
                controller)));

        Assert.Equal(StatusCodes.Status404NotFound, objectResult.StatusCode);
        Assert.Equal(ApiErrorCodes.NotFound, response.Code);
        Assert.Equal("error.common.not_found", response.MessageKey);
        Assert.Equal("trace-filter", response.TraceId);
    }

    private static DefaultHttpContext CreateApiContext()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/Test/Failure";
        context.Response.Body = new MemoryStream();
        context.TraceIdentifier = "trace-api-error";
        return context;
    }

    private static async Task<JsonDocument> ReadResponseJsonAsync(HttpContext context)
    {
        context.Response.Body.Position = 0;
        return await JsonDocument.ParseAsync(context.Response.Body);
    }
}
