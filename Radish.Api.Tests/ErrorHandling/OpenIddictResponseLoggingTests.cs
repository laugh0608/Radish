using System;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading;
using System.Threading.Tasks;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using OpenIddict.Server.AspNetCore;
using Xunit;
using static OpenIddict.Server.OpenIddictServerEvents;
using ResponseHandlers = OpenIddict.Server.AspNetCore.OpenIddictServerAspNetCoreHandlers;

namespace Radish.Api.Tests.ErrorHandling;

[Collection("Runtime logging global state")]
public sealed class OpenIddictResponseLoggingTests
{
    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task BodyWriters_ShouldKeepPayloadHeadersAndFailureBeforeHandleRequest(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, "auth");
        using var fixture = new OpenIddictLoggingFixture(capture);
        using var scope = capture.SensitiveScope();
        foreach (var format in new[] { "json", "text", "form" })
        foreach (var outcome in new[] { "success", "io", "cancel" })
        {
            var transaction = fixture.Transaction();
            transaction.Response = new OpenIddictResponse { State = HttpBoundaryCapture.Secret, Error = "invalid_request", ErrorDescription = HttpBoundaryCapture.Secret };
            transaction.Response.SetParameter("custom", HttpBoundaryCapture.Secret);
            var http = OpenIddictLoggingFixture.AttachHttp(transaction);
            using var cancellation = new CancellationTokenSource();
            var failure = outcome == "cancel" ? (Exception)new OperationCanceledException(cancellation.Token) : new IOException(HttpBoundaryCapture.Secret);
            var writes = 0;
            using var body = new ProtocolResponseStream((bytes, token) =>
            {
                writes++;
                // 上游已经发出了 prepared 事件，但尚未把 request 标记 handled。
                Assert.Equal(candidate && environment == "Production" ? 0 : 1, capture.Events.Length);
                if (format != "form") Assert.Equal(cancellation.Token, token);
                else Assert.Equal(CancellationToken.None, token); // 7.2.0 的 form writer 不传入 context token。
                if (outcome == "cancel") cancellation.Cancel();
                return outcome == "success" ? ValueTask.CompletedTask : ValueTask.FromException(failure);
            });
            if (outcome != "success") http.Response.Body = body;
            BaseRequestContext context;
            Func<ValueTask> write;
            if (format == "form")
            {
                var response = new ApplyAuthorizationResponseContext(transaction) { RedirectUri = "https://client.invalid/" + HttpBoundaryCapture.Secret, ResponseMode = "form_post", CancellationToken = cancellation.Token };
                context = response;
                write = () => new ResponseHandlers.Authentication.ProcessFormPostResponse(HtmlEncoder.Default).HandleAsync(response);
            }
            else
            {
                var response = new ProcessErrorContext(transaction) { CancellationToken = cancellation.Token };
                context = response;
                write = format == "json"
                    ? () => new ResponseHandlers.ProcessJsonResponse<ProcessErrorContext>(new OpenIddictOptions<OpenIddictServerAspNetCoreOptions>(new())).HandleAsync(response)
                    : () => new ResponseHandlers.ProcessLocalErrorResponse<ProcessErrorContext>().HandleAsync(response);
            }
            var error = await Record.ExceptionAsync(async () => await write());
            Assert.Equal(outcome == "success", context.IsRequestHandled);
            if (outcome == "success")
            {
                Assert.Null(error);
                var text = Encoding.UTF8.GetString(((MemoryStream)http.Response.Body).ToArray());
                Assert.Contains(HttpBoundaryCapture.Secret, text); // 输出协议内容保持原样，日志不带该内容。
                Assert.True(http.Response.ContentLength > 0);
            }
            else { Assert.Same(failure, error); Assert.Equal(1, writes); }
            Assert.Equal(format == "json" ? "application/json;charset=UTF-8" : format == "text" ? "text/plain;charset=UTF-8" : "text/html;charset=UTF-8", http.Response.ContentType);
            if (format == "text") Assert.Null(transaction.Response.State);
            else Assert.Equal(HttpBoundaryCapture.Secret, transaction.Response.State);
            if (format == "form") Assert.Equal("no-store", http.Response.Headers.CacheControl.ToString());
            var code = format == "json" ? "oidc.server.json_response_prepared" : format == "text" ? "oidc.server.text_response_prepared" : "oidc.server.form_post_response_prepared";
            Assert.Equal(candidate && environment == "Production" ? Array.Empty<string>() : new[] { code }, capture.Codes);
            OpenIddictLoggingFixture.AssertSafe(capture);
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task RedirectsAndEmptyResponses_ShouldPreserveProtocolOutputs(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment, "auth");
        using var fixture = new OpenIddictLoggingFixture(capture);
        foreach (var mode in new[] { "query", "fragment", "end-session", "challenge", "empty" })
        {
            var transaction = fixture.Transaction();
            transaction.Response = new OpenIddictResponse { State = HttpBoundaryCapture.Secret, Code = HttpBoundaryCapture.Secret };
            var http = OpenIddictLoggingFixture.AttachHttp(transaction);
            BaseRequestContext context;
            string code;
            if (mode is "query" or "fragment")
            {
                var response = new ApplyAuthorizationResponseContext(transaction) { RedirectUri = "https://client.invalid/callback", ResponseMode = mode };
                context = response;
                if (mode == "query") await new ResponseHandlers.Authentication.ProcessQueryResponse().HandleAsync(response);
                else await new ResponseHandlers.Authentication.ProcessFragmentResponse().HandleAsync(response);
                code = mode == "query" ? "oidc.server.query_response_prepared" : "oidc.server.fragment_response_prepared";
            }
            else if (mode == "end-session")
            {
                var response = new ApplyEndSessionResponseContext(transaction) { PostLogoutRedirectUri = "https://client.invalid/callback" };
                context = response;
                await new ResponseHandlers.Session.ProcessQueryResponse().HandleAsync(response);
                code = "oidc.server.end_session_redirect_prepared";
            }
            else
            {
                var response = new ProcessErrorContext(transaction);
                context = response;
                if (mode == "challenge")
                {
                    http.Response.Headers.WWWAuthenticate = "Bearer " + HttpBoundaryCapture.Secret;
                    await new ResponseHandlers.ProcessChallengeErrorResponse<ProcessErrorContext>().HandleAsync(response);
                    Assert.Contains(HttpBoundaryCapture.Secret, http.Response.Headers.WWWAuthenticate.ToString());
                }
                else await new ResponseHandlers.ProcessEmptyResponse<ProcessErrorContext>().HandleAsync(response);
                code = mode == "challenge" ? "oidc.server.challenge_response_prepared" : "oidc.server.empty_response_prepared";
            }
            Assert.True(context.IsRequestHandled);
            if (mode is "query" or "fragment" or "end-session")
            {
                Assert.Equal(302, http.Response.StatusCode);
                Assert.Contains(HttpBoundaryCapture.Secret, http.Response.Headers.Location.ToString());
                Assert.Contains(mode == "fragment" ? "#" : "?", http.Response.Headers.Location.ToString());
            }
            Assert.Equal(candidate && environment == "Production" ? Array.Empty<string>() : new[] { code }, capture.Codes);
            OpenIddictLoggingFixture.AssertSafe(capture);
            capture.Clear();
        }
    }

    // 不继承 MemoryStream，避免 CopyToAsync 的 MemoryStream 专用同步快路径绕过写入屏障。
    private sealed class ProtocolResponseStream(Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask> write) : Stream
    {
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new InvalidOperationException("Unexpected synchronous write");
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) => write(buffer, cancellationToken);
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => write(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    }
}
