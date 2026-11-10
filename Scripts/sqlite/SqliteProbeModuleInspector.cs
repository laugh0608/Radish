using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;

namespace Radish.SqliteDiagnostics;

internal enum SqliteProbePlatform { Windows, Linux, MacOS }

internal sealed record SqliteProbeLoadedModule(string ModuleName, string FileName, string BaseAddress);

internal sealed record SqliteProbeModuleCandidate(
    SqliteProbeLoadedModule Module, string Kind, string? Sha256 = null, string? AssemblyName = null);

internal sealed class SqliteProbeModuleInventory
{
    public required string Platform { get; init; }
    public required string ExpectedModuleName { get; init; }
    public required IReadOnlyList<SqliteProbeLoadedModule> Modules { get; init; }
    public List<SqliteProbeModuleCandidate> Candidates { get; } = [];
    public string Status { get; set; } = "captured";
    public string? Error { get; set; }
}

internal static class SqliteProbeModuleInspector
{
    private const string ProviderAssembly = "SQLitePCLRaw.provider.e_sqlite3";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static SqliteProbeModuleCandidate CaptureAndRequireSingle(string directory)
    {
        var platform = OperatingSystem.IsWindows() ? SqliteProbePlatform.Windows
            : OperatingSystem.IsLinux() ? SqliteProbePlatform.Linux
            : OperatingSystem.IsMacOS() ? SqliteProbePlatform.MacOS
            : throw new PlatformNotSupportedException("SQLite probe module enumeration is unsupported");
        var modules = new List<SqliteProbeLoadedModule>();
        if (platform == SqliteProbePlatform.MacOS)
        {
            // Process.Modules omits dylibs on macOS. Read dyld's existing image table;
            // never load a separate candidate library to establish its identity.
            var count = DyldImageCount();
            for (uint index = 0; index < count; index++)
            {
                var file = Marshal.PtrToStringUTF8(DyldImageName(index))
                    ?? throw new InvalidOperationException("dyld returned a null image path");
                modules.Add(new(Path.GetFileName(file), file, Address(DyldImageHeader(index))));
            }
        }
        else
        {
            using var process = Process.GetCurrentProcess();
            foreach (ProcessModule module in process.Modules)
                modules.Add(new(module.ModuleName, module.FileName, Address(module.BaseAddress)));
        }
        return WriteEvidenceAndRequireSingle(platform, modules, directory);
    }

    public static SqliteProbeModuleCandidate WriteEvidenceAndRequireSingle(
        SqliteProbePlatform platform, IReadOnlyList<SqliteProbeLoadedModule> modules, string directory)
    {
        var expected = platform switch
        {
            SqliteProbePlatform.Windows => "e_sqlite3.dll",
            SqliteProbePlatform.Linux => "libe_sqlite3.so",
            SqliteProbePlatform.MacOS => "libe_sqlite3.dylib",
            _ => throw new ArgumentOutOfRangeException(nameof(platform))
        };
        var inventory = new SqliteProbeModuleInventory
        {
            Platform = platform.ToString(), ExpectedModuleName = expected, Modules = modules
        };
        foreach (var module in modules.Where(module =>
                     module.ModuleName.Contains("e_sqlite3", StringComparison.OrdinalIgnoreCase)
                     || Path.GetFileName(module.FileName).Contains("e_sqlite3", StringComparison.OrdinalIgnoreCase)))
            inventory.Candidates.Add(new(module, "pending"));
        var evidence = Path.Combine(directory, "module-inventory.json");
        WriteInventory(evidence, inventory);
        try
        {
            for (var index = 0; index < inventory.Candidates.Count; index++)
            {
                using (var stream = File.OpenRead(inventory.Candidates[index].Module.FileName))
                    inventory.Candidates[index] = inventory.Candidates[index] with
                    {
                        Sha256 = Convert.ToHexStringLower(SHA256.HashData(stream))
                    };
                WriteInventory(evidence, inventory);
                inventory.Candidates[index] = InspectCandidate(platform, expected, inventory.Candidates[index]);
                WriteInventory(evidence, inventory);
            }
            if (inventory.Candidates.Any(candidate => candidate.Kind is not ("native" or "managedProvider")))
                throw new InvalidOperationException("Unexpected SQLite-related module identity; inspect module-inventory.json");
            var native = inventory.Candidates.Where(candidate => candidate.Kind == "native").ToArray();
            var comparer = platform == SqliteProbePlatform.Windows ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
            if (native.GroupBy(candidate => candidate.Module.FileName + "\0" + candidate.Module.BaseAddress, comparer)
                .Any(group => group.Count() > 1))
                throw new InvalidOperationException("Repeated SQLite module inventory entry; no entries were deduplicated");
            if (native.Length != 1)
                throw new InvalidOperationException("Expected one actually loaded SQLite native module; found " + native.Length);
            inventory.Status = "passed";
            WriteInventory(evidence, inventory);
            return native[0];
        }
        catch (Exception error)
        {
            inventory.Status = "failed";
            inventory.Error = error.ToString();
            try { WriteInventory(evidence, inventory); }
            catch (Exception evidenceError)
            {
                throw new AggregateException("SQLite module inspection and failure evidence writing failed", error, evidenceError);
            }
            throw;
        }
    }

    private static SqliteProbeModuleCandidate InspectCandidate(
        SqliteProbePlatform platform, string expected, SqliteProbeModuleCandidate candidate)
    {
        var module = candidate.Module;
        using var stream = File.OpenRead(module.FileName);
        Span<byte> magic = stackalloc byte[4];
        stream.ReadExactly(magic);
        var comparison = platform == SqliteProbePlatform.Windows ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var name = Path.GetFileName(module.FileName);
        var exactNativeName = module.ModuleName.Equals(expected, comparison) && name.Equals(expected, comparison);
        var nativeImage = false;
        if (magic[0] == 'M' && magic[1] == 'Z')
        {
            stream.Position = 0;
            using var pe = new PEReader(stream);
            if (pe.PEHeaders.CorHeader is { } corHeader)
            {
                var reader = pe.GetMetadataReader();
                var assemblyName = reader.GetString(reader.GetAssemblyDefinition().Name);
                var knownProvider = (corHeader.Flags & CorFlags.ILOnly) != 0 && assemblyName == ProviderAssembly
                    && module.ModuleName.Equals(ProviderAssembly + ".dll", comparison)
                    && name.Equals(ProviderAssembly + ".dll", comparison);
                return candidate with { Kind = knownProvider ? "managedProvider" : "unexpectedManagedImage", AssemblyName = assemblyName };
            }
            nativeImage = platform == SqliteProbePlatform.Windows && pe.PEHeaders.PEHeader is not null
                && (pe.PEHeaders.CoffHeader.Characteristics & Characteristics.Dll) != 0;
        }
        else if (magic.SequenceEqual(new byte[] { 0x7f, (byte)'E', (byte)'L', (byte)'F' }))
            nativeImage = platform == SqliteProbePlatform.Linux;
        else if (magic.SequenceEqual(new byte[] { 0xcf, 0xfa, 0xed, 0xfe })
                 || magic.SequenceEqual(new byte[] { 0xfe, 0xed, 0xfa, 0xcf })
                 || magic.SequenceEqual(new byte[] { 0xca, 0xfe, 0xba, 0xbe })
                 || magic.SequenceEqual(new byte[] { 0xca, 0xfe, 0xba, 0xbf }))
            nativeImage = platform == SqliteProbePlatform.MacOS;
        return candidate with { Kind = exactNativeName && nativeImage ? "native" : "unexpectedNativeImage" };
    }

    private static void WriteInventory(string file, SqliteProbeModuleInventory inventory) =>
        File.WriteAllText(file, JsonSerializer.Serialize(inventory, JsonOptions));

    private static string Address(IntPtr value) => "0x" + ((nuint)value).ToString("x");

    [DllImport("/usr/lib/libSystem.B.dylib", EntryPoint = "_dyld_image_count")]
    private static extern uint DyldImageCount();

    [DllImport("/usr/lib/libSystem.B.dylib", EntryPoint = "_dyld_get_image_name")]
    private static extern IntPtr DyldImageName(uint index);

    [DllImport("/usr/lib/libSystem.B.dylib", EntryPoint = "_dyld_get_image_header")]
    private static extern IntPtr DyldImageHeader(uint index);
}
