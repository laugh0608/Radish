using System;
using System.IO;
using System.Linq;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Radish.Api.Tests.ErrorHandling;

[Collection("Runtime logging global state")]
public sealed class HttpsProtocolLoggingTests
{
    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task RealHandshake_ShouldServeRequestAndRestoreTransportWhenNextFails(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment);
        using var certificate = CreateCertificate();
        foreach (var failNext in new[] { false, true })
        {
            var calls = 0;
            var restored = false;
            await using var pipeline = new OuterHttpTestPipeline(capture, context =>
            {
                calls++;
                context.Response.ContentLength = 0;
                return Task.CompletedTask;
            });
            await using var server = new MemoryKestrelFixture(pipeline, capture, middleware: next => async connection =>
            {
                var transport = connection.Transport;
                try { await next(connection); }
                finally { restored = ReferenceEquals(transport, connection.Transport); }
            }, endpoint: listen =>
            {
                listen.UseHttps(certificate);
                if (failNext) listen.Use(_ => _ => throw new IOException(HttpBoundaryCapture.Secret));
            });
            await server.Start();
            await server.Exchange(async stream =>
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                await using var ssl = new SslStream(stream, leaveInnerStreamOpen: true,
                    (_, peer, _, _) => peer?.GetCertHashString() == certificate.Thumbprint);
                await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
                {
                    TargetHost = "example.test", EnabledSslProtocols = SslProtocols.Tls12,
                    CertificateRevocationCheckMode = X509RevocationMode.NoCheck
                }, timeout.Token);
                if (!failNext)
                    await ssl.WriteAsync(Encoding.ASCII.GetBytes("GET / HTTP/1.1\r\nHost: example.test\r\nConnection: close\r\n\r\n"), timeout.Token);
                using var response = new MemoryStream();
                await ssl.CopyToAsync(response, timeout.Token);
                if (failNext) Assert.Empty(response.ToArray());
                else Assert.StartsWith("HTTP/1.1 200", Encoding.ASCII.GetString(response.ToArray()));
            });
            Assert.True(restored);
            Assert.Equal(failNext ? 0 : 1, calls);
            if (!candidate || environment == "Development") Assert.Contains("http.tls_established", capture.Codes);
            Assert.Equal(failNext ? new[] { "http.server_unclassified" } : Array.Empty<string>(),
                capture.Events.Where(e => !e.Diagnostic).Select(e => e.Code));
            KestrelHttpLoggingTests.AssertSafeTargetEvents(capture);
            capture.Clear();
        }
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task InvalidTlsBytes_ShouldFailHandshakeWithoutRunningApplication(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment);
        using var certificate = CreateCertificate();
        var calls = 0;
        await using var pipeline = new OuterHttpTestPipeline(capture, _ => { calls++; return Task.CompletedTask; });
        await using var server = new MemoryKestrelFixture(pipeline, capture, endpoint: listen => listen.UseHttps(certificate));
        await server.Start();
        await server.Exchange(async stream =>
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await stream.WriteAsync(Encoding.ASCII.GetBytes(HttpBoundaryCapture.Secret), timeout.Token);
            await stream.CopyToAsync(Stream.Null, timeout.Token);
        });
        Assert.Equal(0, calls);
        Assert.Equal(0, pipeline.Application.DisposeCount);
        Assert.DoesNotContain(capture.Events, e => !e.Diagnostic);
        if (!candidate || environment == "Development") Assert.Contains("http.tls_handshake_failed", capture.Codes);
        KestrelHttpLoggingTests.AssertSafeTargetEvents(capture);
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public async Task HandshakeCallbackFailures_ShouldKeepConsumptionAndPropagationBranches(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment);
        using var certificate = CreateCertificate();
        foreach (var failure in new Exception[] { new OperationCanceledException(HttpBoundaryCapture.Secret),
            new IOException(HttpBoundaryCapture.Secret), new AuthenticationException(HttpBoundaryCapture.Secret),
            new InvalidOperationException(HttpBoundaryCapture.Secret) })
        {
            var calls = 0;
            await using var pipeline = new OuterHttpTestPipeline(capture, _ => { calls++; return Task.CompletedTask; });
            await using var server = new MemoryKestrelFixture(pipeline, capture, endpoint: listen => listen.UseHttps(certificate,
                options => options.OnAuthenticate = (_, _) => throw failure));
            await server.Start();
            await server.Exchange(async stream =>
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                await stream.CopyToAsync(Stream.Null, timeout.Token);
            });
            Assert.Equal(0, calls);
            Assert.Equal(0, pipeline.Application.DisposeCount);
            if (failure is InvalidOperationException)
            {
                var entry = Assert.Single(capture.Events, e => !e.Diagnostic);
                Assert.Equal("http.server_unclassified", entry.Code);
                Assert.Equal("invalid-operation", entry.FailureKind);
            }
            else
            {
                Assert.DoesNotContain(capture.Events, e => !e.Diagnostic);
                if (!candidate || environment == "Development")
                    Assert.Contains(failure is OperationCanceledException ? "http.tls_handshake_timed_out" : "http.tls_handshake_failed", capture.Codes);
            }
            KestrelHttpLoggingTests.AssertSafeTargetEvents(capture);
            capture.Clear();
        }
    }

    internal static X509Certificate2 CreateCertificate()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=" + HttpBoundaryCapture.Secret, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var names = new SubjectAlternativeNameBuilder();
        names.AddDnsName("example.test");
        request.CertificateExtensions.Add(names.Build());
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1));
    }
}
