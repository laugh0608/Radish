using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace Radish.Api.Tests.ErrorHandling;

[Collection("Runtime logging global state")]
public sealed class CertificateWatcherLoggingTests
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), HttpBoundaryCapture.Secret);

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public void RealWatcher_ShouldReuseObserversAndReleaseLastFileAndDirectory(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment);
        using var scope = capture.SensitiveScope();
        var provider = new ControlledCertificateFileProvider();
        var factories = 0;
        using var watcher = CreateWatcher(capture, _ => { factories++; return provider; });
        var first = CertificateLoggingFramework.CertificateConfig("cert.pem", "key.pem");
        var second = CertificateLoggingFramework.CertificateConfig("cert.pem", "other-key.pem");
        Update(watcher, Array.Empty<object>(), new[] { first, second, first });
        Update(watcher, new[] { first }, new[] { first });
        Assert.Equal(1, factories);
        Assert.Equal(1, provider.ActiveRegistrations);
        Assert.Equal(new[] { "cert.pem" }, provider.WatchedPaths);
        Assert.Equal(2, CertificateLoggingFramework.Call(watcher, "TestGetObserverCountUnsynchronized", Path.Combine(Root, "cert.pem")));
        Assert.Equal(1, CertificateLoggingFramework.Call(watcher, "TestGetFileWatchCountUnsynchronized", Root));
        Assert.False(provider.Disposed);
        if (CertificateLoggingFramework.DiagnosticVisible(capture))
        {
            Assert.Equal(1, capture.Codes.Count(c => c == "http.certificate_watch_directory_created"));
            Assert.Equal(1, capture.Codes.Count(c => c == "http.certificate_watch_file_created"));
            Assert.Equal(2, capture.Codes.Count(c => c == "http.certificate_watch_observer_added"));
            Assert.Contains("http.certificate_watch_observer_reused", capture.Codes);
        }

        Update(watcher, new[] { first }, Array.Empty<object>());
        Assert.Equal(1, provider.ActiveRegistrations);
        Assert.False(provider.Disposed);
        Update(watcher, new[] { first }, Array.Empty<object>());
        Assert.Contains("http.certificate_watch_observer_unknown", capture.Codes);
        Update(watcher, new[] { second }, Array.Empty<object>());
        Assert.Equal(0, provider.ActiveRegistrations);
        Assert.True(provider.Disposed);
        Assert.Equal(0, CertificateLoggingFramework.Call(watcher, "TestGetDirectoryWatchCountUnsynchronized"));
        Update(watcher, new[] { second }, Array.Empty<object>());
        Assert.Contains("http.certificate_watch_file_unknown", capture.Codes);
        Assert.All(capture.Events.Where(e => !e.Diagnostic), e => Assert.Equal("Warning", e.Level));
        if (CertificateLoggingFramework.DiagnosticVisible(capture))
        {
            Assert.Contains("http.certificate_watch_file_removed", capture.Codes);
            Assert.Contains("http.certificate_watch_directory_removed", capture.Codes);
        }
        CertificateLoggingFramework.AssertSafe(capture);
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public void RealWatcher_ShouldIgnoreUnavailableFileAndNotifyOnlyTrackedAvailableFile(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment);
        using var scope = capture.SensitiveScope();
        var provider = new ControlledCertificateFileProvider();
        using var watcher = CreateWatcher(capture, _ => provider);
        var first = CertificateLoggingFramework.CertificateConfig("cert.pem");
        var second = CertificateLoggingFramework.CertificateConfig("cert.pem");
        Update(watcher, Array.Empty<object>(), new[] { first, second });
        var token = (IChangeToken)CertificateLoggingFramework.Call(watcher, "GetChangeToken")!;
        provider.Exists = false;
        provider.Signal("cert.pem");
        Assert.False(token.HasChanged);
        Assert.False(CertificateLoggingFramework.Get<bool>(first, "FileHasChanged"));
        Assert.False(CertificateLoggingFramework.Get<bool>(second, "FileHasChanged"));
        provider.Exists = true;
        provider.Signal("cert.pem");
        Assert.True(token.HasChanged);
        Assert.True(CertificateLoggingFramework.Get<bool>(first, "FileHasChanged"));
        Assert.True(CertificateLoggingFramework.Get<bool>(second, "FileHasChanged"));
        var next = (IChangeToken)CertificateLoggingFramework.Call(watcher, "GetChangeToken")!;
        Assert.NotSame(token, next);
        Assert.False(next.HasChanged);
        CertificateLoggingFramework.Call(watcher, "OnChange", Path.Combine(Root, "untracked.pem"));
        Assert.False(next.HasChanged);
        if (CertificateLoggingFramework.DiagnosticVisible(capture))
        {
            Assert.Contains("http.certificate_watch_file_unavailable", capture.Codes);
            Assert.Contains("http.certificate_watch_changed", capture.Codes);
            Assert.Contains("http.certificate_watch_untracked_event", capture.Codes);
        }
        else Assert.Empty(capture.Events);
        CertificateLoggingFramework.AssertSafe(capture);
        watcher.Dispose();
        Assert.True(provider.Disposed);
        Assert.Equal(0, provider.ActiveRegistrations);
        provider.Signal("cert.pem");
        Assert.False(next.HasChanged);
    }

    [Theory]
    [MemberData(nameof(HttpExceptionPipelineLoggingTests.OutputModes), MemberType = typeof(HttpExceptionPipelineLoggingTests))]
    public void RealWatcher_ShouldReportMissingDirectoryAndPropagateProviderFailure(bool candidate, string environment)
    {
        using var capture = new HttpBoundaryCapture(candidate, environment);
        using var scope = capture.SensitiveScope();
        using var missing = CreateWatcher(capture, _ => null);
        var config = CertificateLoggingFramework.CertificateConfig("missing/cert.pem");
        Update(missing, Array.Empty<object>(), new[] { config });
        Assert.Equal("http.certificate_watch_directory_missing", Assert.Single(capture.Events).Code);
        Assert.Equal(0, CertificateLoggingFramework.Call(missing, "TestGetDirectoryWatchCountUnsynchronized"));
        var failure = new IOException(HttpBoundaryCapture.Secret);
        using var failed = CreateWatcher(capture, _ => throw failure);
        Assert.Same(failure, Assert.Throws<IOException>(() => Update(failed, Array.Empty<object>(), new[] { config })));
        Assert.Single(capture.Events);
        Assert.Equal(0, CertificateLoggingFramework.Call(failed, "TestGetDirectoryWatchCountUnsynchronized"));
        CertificateLoggingFramework.AssertSafe(capture);
    }

    internal static IDisposable CreateWatcher(HttpBoundaryCapture capture, Func<string, IFileProvider?> factory) =>
        (IDisposable)Activator.CreateInstance(CertificateLoggingFramework.Type(CertificateLoggingFramework.Watcher),
            CertificateLoggingFramework.Members, null, new object[] { Root,
                CertificateLoggingFramework.Logger(CertificateLoggingFramework.Watcher, capture.Factory), factory }, null)!;

    private static void Update(object watcher, object[] remove, object[] add) => CertificateLoggingFramework.Call(watcher,
        "UpdateWatches", CertificateLoggingFramework.ConfigList(remove), CertificateLoggingFramework.ConfigList(add));
}

/// <summary>只模拟文件存在性与变更 token；读取文件内容或枚举目录会立即失败。</summary>
internal sealed class ControlledCertificateFileProvider : IFileProvider, IDisposable
{
    private readonly Dictionary<string, ConfigurationReloadToken> _tokens = new();
    public readonly HashSet<string> WatchedPaths = new();
    public bool Exists = true;
    public bool Disposed;
    public int ActiveRegistrations;

    public IChangeToken Watch(string filter)
    {
        Assert.False(Disposed);
        WatchedPaths.Add(filter);
        var token = new ConfigurationReloadToken();
        _tokens[filter] = token;
        return new TrackingToken(token, this);
    }
    public void Signal(string path) => _tokens[path].OnReload();
    public IFileInfo GetFileInfo(string subpath) => new CertificateFileInfo(subpath, Exists);
    public IDirectoryContents GetDirectoryContents(string subpath) => throw new InvalidOperationException("Must not enumerate directories");
    public void Dispose() => Disposed = true;

    private sealed class CertificateFileInfo(string name, bool exists) : IFileInfo
    {
        public bool Exists => exists;
        public long Length => 0;
        public string? PhysicalPath => null;
        public string Name => name;
        public DateTimeOffset LastModified => throw new InvalidOperationException("Must not read file timestamp");
        public bool IsDirectory => false;
        public Stream CreateReadStream() => throw new InvalidOperationException("Must not read certificate contents");
    }

    private sealed class TrackingToken(ConfigurationReloadToken token, ControlledCertificateFileProvider owner) : IChangeToken
    {
        public bool HasChanged => token.HasChanged;
        public bool ActiveChangeCallbacks => true;
        public IDisposable RegisterChangeCallback(Action<object?> callback, object? state)
        {
            owner.ActiveRegistrations++;
            var registration = new Registration(owner);
            registration.Inner = token.RegisterChangeCallback(value =>
            {
                // 一次性 token 触发后已不再是活跃订阅，即使消费者未 Dispose 旧句柄。
                registration.Complete();
                callback(value);
            }, state);
            return registration;
        }
    }
    private sealed class Registration(ControlledCertificateFileProvider owner) : IDisposable
    {
        public IDisposable? Inner;
        private bool _active = true;
        public void Complete()
        {
            if (!_active) return;
            _active = false;
            owner.ActiveRegistrations--;
        }
        public void Dispose() { Inner?.Dispose(); Complete(); }
    }
}
