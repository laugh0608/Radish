using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Radish.Common.OptionTool;
using Radish.Infrastructure.FileStorage;
using Xunit;

namespace Radish.Api.Tests.Infrastructure;

public sealed class LocalFileStorageTest : IDisposable
{
    private readonly string _rootPath = Path.Combine(Path.GetTempPath(), $"radish-file-storage-{Guid.NewGuid():N}");
    private readonly LocalFileStorage _storage;

    public LocalFileStorageTest()
    {
        var options = new FileStorageOptions
        {
            Local = new LocalStorageOptions { BasePath = _rootPath },
            MaxFileSize = new MaxFileSizeOptions
            {
                Avatar = 4,
                Image = 4,
                Document = 16,
                Video = 16,
                Audio = 16
            }
        };

        _storage = new LocalFileStorage(Options.Create(options));
    }

    [Fact]
    public async Task UploadAsync_Should_Classify_FileTooLarge()
    {
        await using var stream = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });

        var result = await _storage.UploadAsync(stream, "large.png", "image/png");

        Assert.False(result.Success);
        Assert.Equal(FileUploadFailureKind.FileTooLarge, result.FailureKind);
    }

    [Fact]
    public async Task UploadAsync_Should_Classify_UnsupportedType()
    {
        await using var stream = new MemoryStream(new byte[] { 1, 2, 3 });

        var result = await _storage.UploadAsync(stream, "unsafe.exe", "application/octet-stream");

        Assert.False(result.Success);
        Assert.Equal(FileUploadFailureKind.UnsupportedType, result.FailureKind);
    }

    [Fact]
    public async Task UploadAsync_Should_Classify_ContentMismatch()
    {
        await using var stream = new MemoryStream(new byte[] { 1, 2, 3 });

        var result = await _storage.UploadAsync(stream, "fake.png", "image/png");

        Assert.False(result.Success);
        Assert.Equal(FileUploadFailureKind.ContentMismatch, result.FailureKind);
    }

    [Fact]
    public async Task UploadAsync_Should_Classify_StorageFailure()
    {
        await using var stream = new LengthFailingStream();

        var result = await _storage.UploadAsync(stream, "image.png", "image/png");

        Assert.False(result.Success);
        Assert.Equal(FileUploadFailureKind.StorageFailed, result.FailureKind);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, recursive: true);
        }
    }

    private sealed class LengthFailingStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new IOException("length unavailable");
        public override long Position { get => 0; set => throw new NotSupportedException(); }

        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => 0;
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
