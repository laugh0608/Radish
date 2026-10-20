using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Radish.Common.LogTool;
using Radish.Common.OptionTool;
using Serilog;

namespace Radish.Infrastructure.ImageProcessing;

/// <summary>唯一图片实现：Rust 处理，宿主负责受限调度、流和文件提交。</summary>
public sealed class RustImageProcessor : IImageProcessor
{
    private static readonly SemaphoreSlim Workers = new(2, 2);
    private static int _waiting;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly ImageProcessingOptions _options;
    private readonly TextWatermarkOptions _text;
    private readonly long _maxInputBytes;

    public RustImageProcessor(IOptions<FileStorageOptions> options)
    {
        _options = options.Value.ImageProcessing;
        _text = options.Value.Watermark.Text;
        _maxInputBytes = Math.Clamp(options.Value.MaxFileSize.Image, 1, 50 * 1024 * 1024);
    }

    public Task<ImageProcessResult> GenerateThumbnailAsync(Stream sourceStream, string outputPath,
        int width = 150, int height = 150, int quality = 85,
        ImageOutputFormat? outputFormat = null, ImageAnimationPolicy animationPolicy = ImageAnimationPolicy.FirstFrame,
        CancellationToken cancellationToken = default) => Single(sourceStream, "thumbnail",
            new Output(outputPath, Format(outputPath, outputFormat), width, height, true, quality, animationPolicy == ImageAnimationPolicy.FirstFrame), null, cancellationToken);

    public Task<ImageProcessResult> ResizeAsync(Stream sourceStream, string outputPath, int width, int height,
        bool keepAspectRatio = true, int quality = 85, ImageOutputFormat? outputFormat = null,
        ImageAnimationPolicy animationPolicy = ImageAnimationPolicy.Preserve, CancellationToken cancellationToken = default) =>
        Single(sourceStream, "resize", new Output(outputPath, Format(outputPath, outputFormat), width, height, keepAspectRatio, quality,
            animationPolicy == ImageAnimationPolicy.FirstFrame), null, cancellationToken);

    public Task<ImageProcessResult> CompressAsync(Stream sourceStream, string outputPath, int quality = 85,
        ImageOutputFormat? outputFormat = null, ImageAnimationPolicy animationPolicy = ImageAnimationPolicy.Preserve,
        CancellationToken cancellationToken = default) => Single(sourceStream, "compress",
            new Output(outputPath, Format(outputPath, outputFormat), 0, 0, true, quality, animationPolicy == ImageAnimationPolicy.FirstFrame), null, cancellationToken);

    public Task<ImageProcessResult> AddWatermarkAsync(Stream sourceStream, string outputPath, WatermarkOptions options,
        ImageOutputFormat? outputFormat = null, ImageAnimationPolicy animationPolicy = ImageAnimationPolicy.Preserve,
        CancellationToken cancellationToken = default) => Single(sourceStream, "watermark",
            new Output(outputPath, Format(outputPath, outputFormat), 0, 0, true, _options.CompressQuality, animationPolicy == ImageAnimationPolicy.FirstFrame), options, cancellationToken);

    public Task<ImageProcessResult> RemoveExifAsync(Stream sourceStream, string outputPath,
        ImageOutputFormat? outputFormat = null, ImageAnimationPolicy animationPolicy = ImageAnimationPolicy.Preserve,
        CancellationToken cancellationToken = default) => Single(sourceStream, "stripMetadata",
            new Output(outputPath, Format(outputPath, outputFormat), 0, 0, true, _options.CompressQuality, animationPolicy == ImageAnimationPolicy.FirstFrame), null, cancellationToken);

    public Task<List<ImageProcessResult>> GenerateMultipleSizesAsync(Stream sourceStream, string baseOutputPath,
        List<ImageSize> sizes, CancellationToken cancellationToken = default)
    {
        if (sizes.Count is 0 or > 8 || sizes.Select(s => s.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != sizes.Count ||
            sizes.Any(s => string.IsNullOrWhiteSpace(s.Name) || s.Name.Contains("..", StringComparison.Ordinal) ||
                s.Name.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '_' and not '-')))
            return Task.FromResult(new List<ImageProcessResult> { Failure("InvalidArgument") });
        var outputs = sizes.Select(size =>
        {
            var format = Format(baseOutputPath, size.OutputFormat);
            var path = Path.Combine(Path.GetDirectoryName(baseOutputPath) ?? ".",
                $"{Path.GetFileNameWithoutExtension(baseOutputPath)}_{size.Name}{Extension(format)}");
            return new Output(path, format, size.Width, size.Height, size.KeepAspectRatio, size.Quality,
                size.AnimationPolicy == ImageAnimationPolicy.FirstFrame);
        }).ToList();
        return Transform(sourceStream, "multiple", outputs, null, cancellationToken);
    }

    public async Task<ImageInfo?> GetImageInfoAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        var response = await Invoke(stream, "info", [], null, cancellationToken);
        if (response.Success) return response.Info;
        if (response.ErrorCode is "InvalidImage" or "UnsupportedFormat" or "UnsupportedFeature" or "LimitExceeded") return null;
        throw new InvalidOperationException($"Image inspection failed ({response.ErrorCode})");
    }

    public async Task<bool> IsValidImageAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        var response = await Invoke(stream, "validate", [], null, cancellationToken);
        if (response.Success) return true;
        if (response.ErrorCode is "InvalidImage" or "UnsupportedFormat" or "UnsupportedFeature" or "LimitExceeded") return false;
        throw new InvalidOperationException($"Image validation failed ({response.ErrorCode})");
    }

    private async Task<ImageProcessResult> Single(Stream stream, string operation, Output output, WatermarkOptions? watermark, CancellationToken token) =>
        (await Transform(stream, operation, [output], watermark, token))[0];

    private async Task<List<ImageProcessResult>> Transform(Stream stream, string operation, List<Output> outputs,
        WatermarkOptions? watermark, CancellationToken token)
    {
        if (outputs.Any(o => string.IsNullOrEmpty(o.Format) || o.Width < 0 || o.Height < 0 || o.Quality is < 1 or > 100))
            return outputs.Select(_ => Failure("InvalidArgument")).ToList();
        var directories = new List<string>();
        var staged = new List<Output>();
        var committed = new List<(string Target, string? Backup)>();
        var retainedDirectories = new HashSet<string>(StringComparer.Ordinal);
        try
        {
            foreach (var output in outputs)
            {
                var target = Path.GetFullPath(output.Path);
                var parent = Path.GetDirectoryName(target)!;
                EnsureNoLinks(parent);
                Directory.CreateDirectory(parent);
                var directory = Path.Combine(parent, $".radish-image-{Guid.NewGuid():N}");
                CreatePrivateDirectory(directory);
                directories.Add(directory);
                staged.Add(output with { Path = Path.Combine(directory, "output.tmp") });
            }
            if (outputs.Select(o => Path.GetFullPath(o.Path)).Distinct(StringComparer.OrdinalIgnoreCase).Count() != outputs.Count)
                return outputs.Select(_ => Failure("InvalidArgument")).ToList();
            var response = await Invoke(stream, operation, staged, watermark, token);
            if (!response.Success) return outputs.Select(_ => Failure(response.ErrorCode ?? "InternalError")).ToList();
            if (response.Outputs.Count != outputs.Count) throw new InvalidOperationException("Native output count mismatch");
            for (var i = 0; i < outputs.Count; i++)
            {
                token.ThrowIfCancellationRequested();
                var target = Path.GetFullPath(outputs[i].Path);
                EnsureNoLinks(target);
                string? backup = null;
                if (File.Exists(target))
                {
                    backup = Path.Combine(directories[i], "backup");
                    File.Copy(target, backup, false);
                }
                File.Move(staged[i].Path, target, true);
                committed.Add((target, backup));
            }
            return response.Outputs.Select((info, i) => new ImageProcessResult
            {
                Success = true, OutputPath = Path.GetFullPath(outputs[i].Path), Width = info.Width, Height = info.Height,
                FileSize = info.FileSize, Format = info.Format, FrameCount = info.FrameCount
            }).ToList();
        }
        catch (OperationCanceledException) { Rollback(committed, retainedDirectories); throw; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            Rollback(committed, retainedDirectories);
            WriteNativeEvent("native.failed", operation, "output-write", ex);
            return outputs.Select(_ => Failure("IoWrite")).ToList();
        }
        catch { Rollback(committed, retainedDirectories); throw; }
        finally
        {
            foreach (var directory in directories.Where(directory => !retainedDirectories.Contains(directory)))
                Cleanup(directory, operation);
        }
    }

    private async Task<Response> Invoke(Stream stream, string operation, List<Output> outputs, WatermarkOptions? watermark, CancellationToken token)
    {
        NativeImageLibrary.EnsureAvailable();
        if (Interlocked.Increment(ref _waiting) > 10)
        {
            Interlocked.Decrement(ref _waiting);
            return new Response { ErrorCode = "LimitExceeded" };
        }
        var entered = false;
        string? directory = null;
        var originalPosition = stream.CanSeek ? stream.Position : (long?)null;
        try
        {
            entered = await Workers.WaitAsync(TimeSpan.FromSeconds(5), token);
            if (!entered) return new Response { ErrorCode = "LimitExceeded" };
            directory = Path.Combine(Path.GetTempPath(), $"radish-image-{Guid.NewGuid():N}");
            CreatePrivateDirectory(directory);
            var input = Path.Combine(directory, "input.tmp");
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
            deadline.CancelAfter(TimeSpan.FromSeconds(30));
            if (stream.CanSeek) stream.Position = 0;
            await using (var file = new FileStream(input, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
            {
                var buffer = new byte[81920];
                long total = 0;
                int count;
                while ((count = await stream.ReadAsync(buffer, deadline.Token)) > 0)
                {
                    total += count;
                    if (total > _maxInputBytes) return new Response { ErrorCode = "LimitExceeded" };
                    await file.WriteAsync(buffer.AsMemory(0, count), deadline.Token);
                }
            }
            var response = await Task.Run(() =>
            {
                object? nativeWatermark = null;
                if (watermark != null)
                {
                    var probe = Call(input, "info", [], null, deadline.Token);
                    if (!probe.Success) return probe;
                    var fontSize = _text.Font.UseRelativeSize
                        ? Math.Clamp((int)(probe.Info!.DisplayWidth * _text.FontSizeRelative), _text.Font.MinFontSize, _text.Font.MaxFontSize)
                        : watermark.FontSize;
                    var fonts = new List<string>();
                    if (!string.IsNullOrEmpty(_text.Font.CustomFontPath)) fonts.Add(Path.GetFullPath(_text.Font.CustomFontPath));
                    var bundled = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["DejaVu Sans"] = "DejaVuSans.ttf", ["Noto Sans"] = "NotoSansSC-VF.ttf",
                        ["Noto Sans SC"] = "NotoSansSC-VF.ttf"
                    };
                    foreach (var name in _text.Font.PreferredFonts.Concat(["Noto Sans SC", "DejaVu Sans"]))
                        if (bundled.TryGetValue(name, out var filename))
                        {
                            var font = Path.Combine(NativeImageLibrary.Assets, "fonts", filename);
                            if (!fonts.Contains(font, StringComparer.Ordinal)) fonts.Add(font);
                        }
                    if (!new[] { "Regular", "Bold", "Italic", "BoldItalic" }.Contains(_text.Font.Style, StringComparer.OrdinalIgnoreCase))
                        return new Response { ErrorCode = "InvalidArgument" };
                    if (!TryColor(watermark.Color, out var color)) return new Response { ErrorCode = "InvalidArgument" };
                    nativeWatermark = new
                    {
                        Kind = watermark.Type == WatermarkType.Text ? "text" : watermark.Type == WatermarkType.Image ? "image" : "invalid",
                        watermark.Text, ImagePath = watermark.ImagePath ?? "", FontPaths = fonts,
                        FontSize = fontSize, Bold = _text.Font.Style.Contains("bold", StringComparison.OrdinalIgnoreCase),
                        Italic = _text.Font.Style.Contains("italic", StringComparison.OrdinalIgnoreCase),
                        watermark.Opacity, Position = (int)watermark.Position, Color = color.Select(value => (int)value).ToArray(), watermark.Padding, watermark.Scale
                    };
                }
                return Call(input, operation, outputs, nativeWatermark, deadline.Token);
            }, CancellationToken.None);
            if (response.ErrorCode == "Cancelled") throw new OperationCanceledException(deadline.Token);
            if (!response.Success) WriteNativeEvent("native.failed", operation, "native-result");
            return response;
        }
        finally
        {
            try { if (originalPosition.HasValue) stream.Position = originalPosition.Value; }
            finally
            {
                if (directory != null) Cleanup(directory, operation);
                if (entered) Workers.Release();
                Interlocked.Decrement(ref _waiting);
            }
        }
    }

    private Response Call(string input, string operation, List<Output> outputs, object? watermark, CancellationToken token)
    {
        var request = JsonSerializer.SerializeToUtf8Bytes(new
        { SchemaVersion = 1, Operation = operation, InputPath = input, Outputs = outputs, Watermark = watermark, Limits = new { MaxInputBytes = _maxInputBytes } }, Json);
        return JsonSerializer.Deserialize<Response>(NativeImageLibrary.Run(request, token), Json)
            ?? throw new InvalidOperationException("Native response missing");
    }

    private sealed record Output(string Path, string Format, int Width, int Height, bool KeepAspectRatio, int Quality, bool FirstFrame);
    private sealed class Response
    {
        public bool Success { get; set; }
        public string? ErrorCode { get; set; }
        public ImageInfo? Info { get; set; }
        public List<ImageInfo> Outputs { get; set; } = [];
    }
    private static ImageProcessResult Failure(string code) => new() { ErrorCode = code, ErrorMessage = $"图片处理失败（{code}）" };
    private static string Format(string path, ImageOutputFormat? format) => format?.ToString().ToUpperInvariant() ?? Path.GetExtension(path).ToLowerInvariant() switch
    { ".jpg" or ".jpeg" => "JPEG", ".png" => "PNG", ".webp" => "WEBP", ".gif" => "GIF", ".bmp" => "BMP", ".ico" => "ICO", _ => "" };
    private static string Extension(string format) => format == "JPEG" ? ".jpg" : "." + format.ToLowerInvariant();
    private static bool TryColor(string text, out byte[] color)
    {
        color = [255, 255, 255, 255];
        if (text.Length is not (7 or 9) || text[0] != '#') return false;
        try { var bytes = Convert.FromHexString(text[1..]); bytes.CopyTo(color, 0); return true; }
        catch (FormatException) { return false; }
    }
    private static void CreatePrivateDirectory(string path)
    {
        if (OperatingSystem.IsWindows()) Directory.CreateDirectory(path);
        else Directory.CreateDirectory(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }
    private static void EnsureNoLinks(string path)
    {
        for (var current = path; !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
        {
            FileSystemInfo info = Directory.Exists(current) ? new DirectoryInfo(current) : new FileInfo(current);
            // macOS supplies these root-owned aliases in Path.GetTempPath().
            if (OperatingSystem.IsMacOS() &&
                ((current == "/var" && info.LinkTarget == "private/var") ||
                 (current == "/tmp" && info.LinkTarget == "private/tmp")))
                continue;
            if (info.LinkTarget != null) throw new IOException("Image path contains a symbolic link");
        }
    }
    private static void Rollback(List<(string Target, string? Backup)> committed, ISet<string> retainedDirectories)
    {
        foreach (var (target, backup) in committed.AsEnumerable().Reverse())
        {
            try { if (backup != null) File.Move(backup, target, true); else File.Delete(target); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                if (backup != null) retainedDirectories.Add(Path.GetDirectoryName(backup)!);
                WriteNativeEvent("native.cleanup_failed", "commit", "rollback", ex);
            }
        }
    }
    private static void Cleanup(string directory, string operation)
    {
        try { Directory.Delete(directory, true); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { WriteNativeEvent("native.cleanup_failed", operation, "temporary-file", ex); }
    }
    private static void WriteNativeEvent(string code, string operation, string reason, Exception? exception = null)
    {
        var logger = Log.ForContext("EventCode", code).ForContext("SourceCategory", "infrastructure")
            .ForContext("nativeOperation", operation).ForContext("nativeReason", reason);
        if (exception != null) logger = logger.ForContext("failureKind", RuntimeFailureSummary.Classify(exception));
        if (code == "native.failed") logger.Error("Native operation failed");
        else logger.Warning("Native cleanup failed");
    }
    public static bool IsRustLibraryAvailable()
    {
        try { NativeImageLibrary.EnsureAvailable(); return true; }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or DllNotFoundException or BadImageFormatException or EntryPointNotFoundException) { return false; }
    }
    public static void EnsureAvailable() => NativeImageLibrary.EnsureAvailable();

    [DllImport("radish_lib", EntryPoint = "calculate_file_sha256", CallingConvention = CallingConvention.Cdecl)]
    private static extern int CalculateFileSha256Native([MarshalAs(UnmanagedType.LPUTF8Str)] string path, StringBuilder output, nuint length);
    public static string? CalculateFileSha256(string path)
    {
        try
        {
            NativeImageLibrary.EnsureAvailable();
            var buffer = new StringBuilder(65);
            if (CalculateFileSha256Native(path, buffer, 65) == 0) return buffer.ToString();
            WriteNativeEvent("native.failed", "hash", "native-result");
        }
        catch (Exception ex) { WriteNativeEvent("native.failed", "hash", "library-unavailable", ex); }
        return null;
    }
}
