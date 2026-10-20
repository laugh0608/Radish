using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;

namespace Radish.Infrastructure.ImageProcessing;

/// <summary>仅加载当前 RID 的受控产物；句柄由进程持有。</summary>
internal static class NativeImageLibrary
{
    private const string LibraryName = "radish_lib";
    private static readonly Lazy<string> AssetDirectory = new(Load);
    internal static string Assets => AssetDirectory.Value;
    internal static void EnsureAvailable() => _ = Assets;

    private static string Load()
    {
        var explicitPath = Environment.GetEnvironmentVariable("RADISH_TEST_NATIVE_LIBRARY");
        var filename = OperatingSystem.IsWindows() ? "radish_lib.dll" :
            OperatingSystem.IsMacOS() ? "libradish_lib.dylib" : "libradish_lib.so";
        var path = string.IsNullOrEmpty(explicitPath)
            ? Path.Combine(AppContext.BaseDirectory, "native", filename)
            : Path.IsPathFullyQualified(explicitPath) ? explicitPath
            : throw new InvalidOperationException("Native test library path must be absolute");
        var directory = Path.GetDirectoryName(path)!;
        using var manifest = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(directory, "manifest.json")));
        var root = manifest.RootElement;
        if (root.GetProperty("rid").GetString() != RuntimeInformation.RuntimeIdentifier ||
            root.GetProperty("abi").GetUInt32() != 0x00010000)
            throw new InvalidOperationException("Native artifact platform or ABI mismatch");
        using (var stream = File.OpenRead(path))
        {
            if (Convert.ToHexStringLower(SHA256.HashData(stream)) != root.GetProperty("sha256").GetString())
                throw new InvalidOperationException("Native artifact integrity mismatch");
        }
        var library = NativeLibrary.Load(path);
        NativeLibrary.SetDllImportResolver(typeof(NativeImageLibrary).Assembly,
            (name, _, _) => name == LibraryName ? library : IntPtr.Zero);
        if (AbiVersion() != 0x00010000) throw new InvalidOperationException("Native ABI mismatch");
        if (Capabilities(out var response) != 0) throw new InvalidOperationException("Native capabilities unavailable");
        using (response)
        using (var capabilities = JsonDocument.Parse(Read(response)))
        {
            var formats = capabilities.RootElement.GetProperty("formats").EnumerateArray().Select(v => v.GetString()).ToHashSet();
            if (!new[] { "JPEG", "PNG", "WEBP", "GIF", "BMP", "ICO" }.All(formats.Contains))
                throw new InvalidOperationException("Native formats incomplete");
        }
        foreach (var font in new[] { "DejaVuSans.ttf", "NotoSansSC-VF.ttf" })
        {
            using var stream = File.OpenRead(Path.Combine(directory, "fonts", font));
            if (Convert.ToHexStringLower(SHA256.HashData(stream)) != root.GetProperty("fonts").GetProperty(font).GetString())
                throw new InvalidOperationException("Native font integrity mismatch");
        }
        return directory;
    }

    internal static byte[] Run(byte[] request, CancellationToken cancellationToken)
    {
        EnsureAvailable();
        if (request.Length is 0 or > 65536) throw new InvalidOperationException("Native request limit exceeded");
        cancellationToken.ThrowIfCancellationRequested();
        if (CreateCancel(out var cancel) != 0) throw new InvalidOperationException("Native cancellation allocation failed");
        using (cancel)
        using (cancellationToken.Register(() => Cancel(cancel)))
        {
            if (Execute(request, (ulong)request.Length, cancel, out var response) != 0)
                throw new InvalidOperationException("Native invocation failed");
            using (response)
            {
                var bytes = Read(response);
                cancellationToken.ThrowIfCancellationRequested();
                return bytes;
            }
        }
    }

    private static byte[] Read(ResponseHandle response)
    {
        if (ResponseData(response, out var data, out var length) != 0 || length > 65536 || data == IntPtr.Zero)
            throw new InvalidOperationException("Native response invalid");
        var bytes = new byte[checked((int)length)];
        Marshal.Copy(data, bytes, 0, bytes.Length);
        return bytes;
    }

    internal sealed class ResponseHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public ResponseHandle() : base(true) { }
        protected override bool ReleaseHandle() => FreeResponse(handle) == 0;
    }
    internal sealed class CancelHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public CancelHandle() : base(true) { }
        protected override bool ReleaseHandle() => FreeCancel(handle) == 0;
    }

    [DllImport(LibraryName, EntryPoint = "radish_image_abi_version", CallingConvention = CallingConvention.Cdecl)]
    private static extern uint AbiVersion();
    [DllImport(LibraryName, EntryPoint = "radish_image_capabilities_v1", CallingConvention = CallingConvention.Cdecl)]
    private static extern int Capabilities(out ResponseHandle response);
    [DllImport(LibraryName, EntryPoint = "radish_image_execute_v1", CallingConvention = CallingConvention.Cdecl)]
    private static extern int Execute(byte[] request, ulong length, CancelHandle cancel, out ResponseHandle response);
    [DllImport(LibraryName, EntryPoint = "radish_image_response_data_v1", CallingConvention = CallingConvention.Cdecl)]
    private static extern int ResponseData(ResponseHandle response, out IntPtr data, out ulong length);
    [DllImport(LibraryName, EntryPoint = "radish_image_response_free_v1", CallingConvention = CallingConvention.Cdecl)]
    private static extern int FreeResponse(IntPtr response);
    [DllImport(LibraryName, EntryPoint = "radish_image_cancel_create_v1", CallingConvention = CallingConvention.Cdecl)]
    private static extern int CreateCancel(out CancelHandle cancel);
    [DllImport(LibraryName, EntryPoint = "radish_image_cancel_v1", CallingConvention = CallingConvention.Cdecl)]
    private static extern int Cancel(CancelHandle cancel);
    [DllImport(LibraryName, EntryPoint = "radish_image_cancel_free_v1", CallingConvention = CallingConvention.Cdecl)]
    private static extern int FreeCancel(IntPtr cancel);
}
