using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
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
