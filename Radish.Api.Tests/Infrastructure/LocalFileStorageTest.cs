using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Radish.Common.OptionTool;
using Radish.Infrastructure.FileStorage;
using Radish.Model.DtoModels;
using Radish.Shared.Constants;
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
                Image = 32,
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
        await using var stream = new MemoryStream(new byte[33]);

        var result = await _storage.UploadAsync(stream, "large.png");

        Assert.False(result.Success);
        Assert.Equal(FileUploadFailureKind.FileTooLarge, result.FailureKind);
    }

    [Fact]
    public async Task UploadAsync_Should_Classify_UnsupportedType()
    {
        await using var stream = new MemoryStream(new byte[] { 1, 2, 3 });

        var result = await _storage.UploadAsync(stream, "unsafe.exe");

        Assert.False(result.Success);
        Assert.Equal(FileUploadFailureKind.UnsupportedType, result.FailureKind);
    }

    [Fact]
    public async Task UploadAsync_Should_Reject_Document_For_ImageOnly_BusinessType()
    {
        await using var stream = new MemoryStream("%PDF"u8.ToArray());

        var result = await _storage.UploadAsync(
            stream,
            "avatar.pdf",
            new FileUploadOptionsDto { BusinessType = AttachmentBusinessTypes.Avatar });

        Assert.False(result.Success);
        Assert.Equal(FileUploadFailureKind.UnsupportedType, result.FailureKind);
    }

    [Fact]
    public async Task UploadAsync_Should_Classify_ContentMismatch()
    {
        await using var stream = new MemoryStream(new byte[] { 1, 2, 3 });

        var result = await _storage.UploadAsync(stream, "fake.png");

        Assert.False(result.Success);
        Assert.Equal(FileUploadFailureKind.ContentMismatch, result.FailureKind);
    }

    [Fact]
    public async Task UploadAsync_Should_Classify_StorageFailure()
    {
        await using var stream = new LengthFailingStream();

        var result = await _storage.UploadAsync(stream, "image.png");

        Assert.False(result.Success);
        Assert.Equal(FileUploadFailureKind.StorageFailed, result.FailureKind);
        Assert.Equal("文件存储失败，请稍后重试", result.ErrorMessage);
        Assert.DoesNotContain("length unavailable", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UploadAsync_Should_Derive_MimeType_From_Validated_Content()
    {
        await using var stream = new MemoryStream(
            new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4 });

        var result = await _storage.UploadAsync(stream, "image.png");

        Assert.True(result.Success);
        Assert.Equal("image/png", result.ContentType);
        Assert.StartsWith("General/", result.StoragePath, StringComparison.Ordinal);
        Assert.True(File.Exists(_storage.GetFullPath(result.StoragePath)));
    }

    [Fact]
    public async Task UploadAsync_Should_Reject_Svg_Even_When_Config_Would_Allow_It()
    {
        await using var stream = new MemoryStream("<svg></svg>"u8.ToArray());

        var result = await _storage.UploadAsync(stream, "unsafe.svg");

        Assert.False(result.Success);
        Assert.Equal(FileUploadFailureKind.UnsupportedType, result.FailureKind);
    }

    [Fact]
    public async Task UploadAsync_Should_Reject_Unknown_BusinessType_Before_Path_Construction()
    {
        await using var stream = new MemoryStream(
            new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });

        var result = await _storage.UploadAsync(
            stream,
            "image.png",
            new FileUploadOptionsDto { BusinessType = "../../outside" });

        Assert.False(result.Success);
        Assert.Equal(FileUploadFailureKind.InvalidBusinessType, result.FailureKind);
        Assert.False(Directory.Exists(Path.Combine(Path.GetDirectoryName(_rootPath)!, "outside")));
    }

    [Fact]
    public async Task StorageOperations_Should_Reject_Traversal_And_Preserve_External_File()
    {
        var outsidePath = Path.Combine(Path.GetDirectoryName(_rootPath)!, $"outside-{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(outsidePath, "outside", TestContext.Current.CancellationToken);
        var traversalPath = $"../{Path.GetFileName(outsidePath)}";

        try
        {
            Assert.Throws<InvalidOperationException>(() => _storage.GetFullPath(traversalPath));
            Assert.False(await _storage.ExistsAsync(traversalPath));
            Assert.False(await _storage.DeleteAsync(traversalPath));
            Assert.Null(await _storage.DownloadAsync(traversalPath));
            Assert.True(File.Exists(outsidePath));
        }
        finally
        {
            File.Delete(outsidePath);
        }
    }

    [Theory]
    [InlineData("fake.webp")]
    [InlineData("fake.mp4")]
    [InlineData("fake.mp3")]
    public async Task UploadAsync_Should_Reject_Allowed_Extension_With_Mismatched_Content(string fileName)
    {
        await using var stream = new MemoryStream(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 });

        var result = await _storage.UploadAsync(stream, fileName);

        Assert.False(result.Success);
        Assert.Equal(FileUploadFailureKind.ContentMismatch, result.FailureKind);
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
