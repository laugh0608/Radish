using System;
using System.Buffers.Binary;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Radish.SqliteDiagnostics;
using Xunit;

namespace Radish.Api.Tests.Repositories;

public sealed partial class SqliteNativeContractTests
{
    [Theory]
    [InlineData("Windows", "e_sqlite3.dll")]
    [InlineData("Linux", "libe_sqlite3.so")]
    [InlineData("MacOS", "libe_sqlite3.dylib")]
    public void ModuleInspection_ShouldSelectTheNativeImageAndRetainManagedProviderIdentity(string platform, string name)
    {
        using var fixture = new SqliteModuleFixture();
        var target = Enum.Parse<SqliteProbePlatform>(platform);
        var native = fixture.Native(target, name);
        var provider = fixture.Managed("SQLitePCLRaw.provider.e_sqlite3.dll",
            Assembly.Load("SQLitePCLRaw.provider.e_sqlite3").Location);

        var selected = SqliteProbeModuleInspector.WriteEvidenceAndRequireSingle(target, [native, provider], fixture.Root);

        Assert.Equal(native, selected.Module);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(native.FileName))), selected.Sha256);
        using var report = fixture.ReadReport();
        Assert.Equal("passed", report.RootElement.GetProperty("status").GetString());
        var candidates = report.RootElement.GetProperty("candidates").EnumerateArray().ToArray();
        Assert.Equal(2, candidates.Length);
        Assert.Equal("native", candidates[0].GetProperty("kind").GetString());
        Assert.Equal("managedProvider", candidates[1].GetProperty("kind").GetString());
        Assert.Equal("SQLitePCLRaw.provider.e_sqlite3", candidates[1].GetProperty("assemblyName").GetString());
        Assert.Equal(64, candidates[1].GetProperty("sha256").GetString()!.Length);
    }

    [Fact]
    public void ModuleInspection_ShouldRejectTwoNativeImagesEvenWithIdenticalBytes()
    {
        using var fixture = new SqliteModuleFixture();
        var first = fixture.Native(SqliteProbePlatform.Windows, "e_sqlite3.dll", "first");
        var second = fixture.Native(SqliteProbePlatform.Windows, "e_sqlite3.dll", "second") with { BaseAddress = "0x2000" };

        var error = Assert.Throws<InvalidOperationException>(() =>
            SqliteProbeModuleInspector.WriteEvidenceAndRequireSingle(SqliteProbePlatform.Windows, [first, second], fixture.Root));

        Assert.Contains("found 2", error.Message);
        using var report = fixture.ReadReport();
        Assert.Equal("failed", report.RootElement.GetProperty("status").GetString());
        var candidates = report.RootElement.GetProperty("candidates").EnumerateArray().ToArray();
        Assert.Equal(2, candidates.Length);
        Assert.Equal(first.FileName, candidates[0].GetProperty("module").GetProperty("fileName").GetString());
        Assert.Equal(second.FileName, candidates[1].GetProperty("module").GetProperty("fileName").GetString());
        Assert.Equal(candidates[0].GetProperty("sha256").GetString(), candidates[1].GetProperty("sha256").GetString());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ModuleInspection_ShouldRejectRepeatedInventoryAndSeparateLoadAddresses(bool sameAddress)
    {
        using var fixture = new SqliteModuleFixture();
        var first = fixture.Native(SqliteProbePlatform.Windows, "e_sqlite3.dll");
        var second = first with { BaseAddress = sameAddress ? first.BaseAddress : "0x2000" };

        var error = Assert.Throws<InvalidOperationException>(() =>
            SqliteProbeModuleInspector.WriteEvidenceAndRequireSingle(SqliteProbePlatform.Windows, [first, second], fixture.Root));

        Assert.Contains(sameAddress ? "Repeated SQLite module inventory entry" : "found 2", error.Message);
        using var report = fixture.ReadReport();
        Assert.Equal(2, report.RootElement.GetProperty("candidates").GetArrayLength());
        Assert.Equal(2, report.RootElement.GetProperty("modules").GetArrayLength());
    }

    [Theory]
    [InlineData("renamed_e_sqlite3.dll")]
    [InlineData("SQLitePCLRaw.provider.e_sqlite3.dll")]
    [InlineData("e_sqlite3.dll.bak")]
    public void ModuleInspection_ShouldRejectUnexpectedNativeNamesInsteadOfIgnoringThem(string name)
    {
        using var fixture = new SqliteModuleFixture();
        var native = fixture.Native(SqliteProbePlatform.Windows, "e_sqlite3.dll");
        var unexpected = fixture.Native(SqliteProbePlatform.Windows, name, "unexpected");

        var error = Assert.Throws<InvalidOperationException>(() =>
            SqliteProbeModuleInspector.WriteEvidenceAndRequireSingle(SqliteProbePlatform.Windows, [native, unexpected], fixture.Root));

        Assert.Contains("Unexpected SQLite-related module identity", error.Message);
        using var report = fixture.ReadReport();
        Assert.Equal("unexpectedNativeImage", report.RootElement.GetProperty("candidates")[1].GetProperty("kind").GetString());
    }

    [Theory]
    [InlineData("SQLitePCLRaw.provider.e_sqlite3.dll", false)]
    [InlineData("e_sqlite3.dll", true)]
    public void ModuleInspection_ShouldRejectManagedIdentityOrNameMismatch(string fileName, bool actualProvider)
    {
        using var fixture = new SqliteModuleFixture();
        var native = fixture.Native(SqliteProbePlatform.Windows, "e_sqlite3.dll");
        var source = actualProvider ? Assembly.Load("SQLitePCLRaw.provider.e_sqlite3").Location : typeof(SqliteConnection).Assembly.Location;
        var managed = fixture.Managed(fileName, source);

        Assert.Throws<InvalidOperationException>(() =>
            SqliteProbeModuleInspector.WriteEvidenceAndRequireSingle(SqliteProbePlatform.Windows, [native, managed], fixture.Root));

        using var report = fixture.ReadReport();
        var candidate = report.RootElement.GetProperty("candidates")[1];
        Assert.Equal("unexpectedManagedImage", candidate.GetProperty("kind").GetString());
        Assert.Equal(actualProvider ? "SQLitePCLRaw.provider.e_sqlite3" : "Microsoft.Data.Sqlite",
            candidate.GetProperty("assemblyName").GetString());
    }

    [Fact]
    public void ModuleInspection_ShouldRejectProviderOnlyAndPreserveTheEvidence()
    {
        using var fixture = new SqliteModuleFixture();
        var provider = fixture.Managed("SQLitePCLRaw.provider.e_sqlite3.dll", Assembly.Load("SQLitePCLRaw.provider.e_sqlite3").Location);
        var error = Assert.Throws<InvalidOperationException>(() =>
            SqliteProbeModuleInspector.WriteEvidenceAndRequireSingle(SqliteProbePlatform.Windows, [provider], fixture.Root));
        Assert.Contains("found 0", error.Message);
        using var report = fixture.ReadReport();
        Assert.Equal("managedProvider", report.RootElement.GetProperty("candidates")[0].GetProperty("kind").GetString());
    }

    [Fact]
    public void ModuleInspection_ShouldRejectEmptyInventory()
    {
        using var fixture = new SqliteModuleFixture();
        var error = Assert.Throws<InvalidOperationException>(() =>
            SqliteProbeModuleInspector.WriteEvidenceAndRequireSingle(SqliteProbePlatform.Windows, [], fixture.Root));
        Assert.Contains("found 0", error.Message);
        using var report = fixture.ReadReport();
        Assert.Equal(0, report.RootElement.GetProperty("modules").GetArrayLength());
    }

    [Theory]
    [InlineData("Windows", "E_SQLITE3.DLL", true)]
    [InlineData("Linux", "LIBE_SQLITE3.SO", false)]
    [InlineData("MacOS", "LIBE_SQLITE3.DYLIB", false)]
    public void ModuleInspection_ShouldUseTheTargetPlatformsNameComparison(string platform, string name, bool accepted)
    {
        using var fixture = new SqliteModuleFixture();
        var target = Enum.Parse<SqliteProbePlatform>(platform);
        var native = fixture.Native(target, name);
        if (accepted)
            Assert.Equal(native, SqliteProbeModuleInspector.WriteEvidenceAndRequireSingle(target, [native], fixture.Root).Module);
        else
            Assert.Throws<InvalidOperationException>(() =>
                SqliteProbeModuleInspector.WriteEvidenceAndRequireSingle(target, [native], fixture.Root));
    }

    [Fact]
    public void ModuleInspection_ShouldNotTreatDirectoryNamesAsModuleNames()
    {
        using var fixture = new SqliteModuleFixture();
        var native = fixture.Native(SqliteProbePlatform.Windows, "e_sqlite3.dll");
        var unrelated = new SqliteProbeLoadedModule("framework.dll", Path.Combine(fixture.Root, "e_sqlite3", "framework.dll"), "0x3000");
        Assert.Equal(native, SqliteProbeModuleInspector.WriteEvidenceAndRequireSingle(
            SqliteProbePlatform.Windows, [native, unrelated], fixture.Root).Module);
        using var report = fixture.ReadReport();
        Assert.Equal(2, report.RootElement.GetProperty("modules").GetArrayLength());
        Assert.Single(report.RootElement.GetProperty("candidates").EnumerateArray());
    }

    [Fact]
    public void ModuleInspection_ShouldRejectANativeExecutableRenamedAsTheExpectedDll()
    {
        using var fixture = new SqliteModuleFixture();
        var native = fixture.Native(SqliteProbePlatform.Windows, "e_sqlite3.dll");
        var bytes = File.ReadAllBytes(native.FileName);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0x96), 0x22);
        File.WriteAllBytes(native.FileName, bytes);
        Assert.Throws<InvalidOperationException>(() =>
            SqliteProbeModuleInspector.WriteEvidenceAndRequireSingle(SqliteProbePlatform.Windows, [native], fixture.Root));
        using var report = fixture.ReadReport();
        Assert.Equal("unexpectedNativeImage", report.RootElement.GetProperty("candidates")[0].GetProperty("kind").GetString());
    }

    [Fact]
    public void ModuleInspection_ShouldRejectTheWrongNativeFormatForTheTargetPlatform()
    {
        using var fixture = new SqliteModuleFixture();
        var native = fixture.Native(SqliteProbePlatform.Linux, "e_sqlite3.dll");
        Assert.Throws<InvalidOperationException>(() =>
            SqliteProbeModuleInspector.WriteEvidenceAndRequireSingle(SqliteProbePlatform.Windows, [native], fixture.Root));
        using var report = fixture.ReadReport();
        Assert.Equal("unexpectedNativeImage", report.RootElement.GetProperty("candidates")[0].GetProperty("kind").GetString());
    }

    [Fact]
    public void ModuleInspection_ShouldKeepTheHashWhenPeHeaderParsingFails()
    {
        using var fixture = new SqliteModuleFixture();
        var native = fixture.Native(SqliteProbePlatform.Windows, "e_sqlite3.dll");
        var bytes = new byte[64]; bytes[0] = 0x4d; bytes[1] = 0x5a;
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(0x3c), int.MaxValue);
        File.WriteAllBytes(native.FileName, bytes);
        Assert.Throws<BadImageFormatException>(() =>
            SqliteProbeModuleInspector.WriteEvidenceAndRequireSingle(SqliteProbePlatform.Windows, [native], fixture.Root));
        using var report = fixture.ReadReport();
        Assert.Equal("failed", report.RootElement.GetProperty("status").GetString());
        Assert.Equal(64, report.RootElement.GetProperty("candidates")[0].GetProperty("sha256").GetString()!.Length);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ModuleInspection_ShouldRetainInventoryAndErrorsWhenTheImageCannotBeInspected(bool missing)
    {
        using var fixture = new SqliteModuleFixture();
        var native = fixture.Native(SqliteProbePlatform.Windows, "e_sqlite3.dll");
        if (missing) File.Delete(native.FileName);
        else File.WriteAllBytes(native.FileName, [0x4d]);
        Assert.ThrowsAny<IOException>(() =>
            SqliteProbeModuleInspector.WriteEvidenceAndRequireSingle(SqliteProbePlatform.Windows, [native], fixture.Root));
        using var report = fixture.ReadReport();
        Assert.Equal("failed", report.RootElement.GetProperty("status").GetString());
        Assert.False(string.IsNullOrWhiteSpace(report.RootElement.GetProperty("error").GetString()));
        var candidate = report.RootElement.GetProperty("candidates")[0];
        Assert.Equal(native.FileName, candidate.GetProperty("module").GetProperty("fileName").GetString());
        if (!missing) Assert.Equal(64, candidate.GetProperty("sha256").GetString()!.Length);
    }

    private sealed class SqliteModuleFixture : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "radish-sqlite-module-contract-" + Guid.NewGuid().ToString("N"));

        public SqliteModuleFixture() => Directory.CreateDirectory(Root);

        public SqliteProbeLoadedModule Native(SqliteProbePlatform platform, string name, string folder = "native")
        {
            var directory = Path.Combine(Root, folder);
            Directory.CreateDirectory(directory);
            var file = Path.Combine(directory, name);
            var bytes = new byte[512];
            if (platform == SqliteProbePlatform.Windows)
            {
                bytes[0] = 0x4d; bytes[1] = 0x5a;
                BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(0x3c), 0x80);
                bytes[0x80] = 0x50; bytes[0x81] = 0x45;
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0x84), 0x8664);
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0x94), 0xf0);
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0x96), 0x2022);
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0x98), 0x20b);
                BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(0x98 + 32), 0x1000);
                BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(0x98 + 36), 0x200);
                BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(0x98 + 56), 0x1000);
                BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(0x98 + 60), 0x200);
                BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(0x98 + 108), 16);
            }
            else if (platform == SqliteProbePlatform.Linux)
                new byte[] { 0x7f, 0x45, 0x4c, 0x46 }.CopyTo(bytes, 0);
            else
                new byte[] { 0xcf, 0xfa, 0xed, 0xfe }.CopyTo(bytes, 0);
            File.WriteAllBytes(file, bytes);
            return new(name, file, "0x1000");
        }

        public SqliteProbeLoadedModule Managed(string name, string source)
        {
            var directory = Path.Combine(Root, "managed");
            Directory.CreateDirectory(directory);
            var file = Path.Combine(directory, name);
            File.Copy(source, file);
            return new(name, file, "0x4000");
        }

        public JsonDocument ReadReport() => JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, "module-inventory.json")));

        public void Dispose() => Directory.Delete(Root, recursive: true);
    }
}
