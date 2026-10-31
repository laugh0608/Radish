using System.Text.Json.Nodes;
using System.Security.Cryptography;
using static Radish.WindowsRazor.Program;

namespace Radish.WindowsRazor;

internal static class AssemblyProof
{
    internal static string ApiHash(JsonNode metadata) => TextHash(string.Join("\n", metadata["api"]!.AsArray().Select(node => node!.GetValue<string>())) + "\n");
    internal static string ResourcesHash(JsonNode metadata) => TextHash(string.Concat(metadata["resources"]!.AsObject().OrderBy(item => item.Key, StringComparer.Ordinal).Select(item => $"{item.Key}\t{item.Value!["size"]}\t{item.Value["sha256"]}\n")));
    internal static void CheckContract(JsonNode metadata, JsonNode expected)
    {
        foreach (var field in new[] { "name", "version", "publicKey" }) Require(metadata[field]!.GetValue<string>() == expected[field]!.GetValue<string>(), "Core identity differs: " + field);
        Require(metadata["api"]!.AsArray().Count == expected["apiEntries"]!.GetValue<int>() && ApiHash(metadata) == expected["apiSha256"]!.GetValue<string>(), "Core public contract differs");
        Require(metadata["resources"]!.AsObject().Count == expected["resourceEntries"]!.GetValue<int>() && ResourcesHash(metadata) == expected["resourcesSha256"]!.GetValue<string>(), "Core resources differ");
    }
    internal static string Contained(string root, string suffix)
    {
        var full = Path.GetFullPath(Path.Combine(root, suffix.Replace('/', Path.DirectorySeparatorChar)));
        Require(full.StartsWith(Path.GetFullPath(root) + Path.DirectorySeparatorChar, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal), "PDB source escaped its owned root");
        return full;
    }
    internal static void Verify(string metadataFile, string hangfire, string cronos, string packages, string report)
    {
        var metadata = Read(metadataFile);
        CheckContract(metadata, Read(Path.Combine(AppContext.BaseDirectory, "core-contract.json")));
        var links = metadata["sourceLinks"]!.AsArray().Select(node => ReadLinks(node!.GetValue<string>())).ToArray();
        Require(links.Length > 0, "Missing PDB SourceLink");
        var checkedPaths = new Dictionary<string, bool>(StringComparer.Ordinal);
        var absent = new List<string>();
        foreach (var item in metadata["documents"]!.AsArray())
        {
            var document = item!;var name = document["path"]!.GetValue<string>();
            var normalized = name.Replace('\\', '/');
            string root;string suffix;string? repo;
            if (normalized.StartsWith("/_/sources/hangfire/", StringComparison.Ordinal)) { root = hangfire;suffix = normalized[20..];repo = "hangfire"; }
            else if (normalized.StartsWith("/_/sources/cronos/", StringComparison.Ordinal)) { root = cronos;suffix = normalized[18..];repo = "cronos"; }
            else if (normalized.StartsWith("/_/packages/", StringComparison.Ordinal)) { root = packages;suffix = normalized[12..];repo = null; }
            else throw new InvalidDataException("PDB document outside mapped roots: " + name);
            var actual = Contained(root, suffix);
            Require(File.Exists(actual), "PDB source not found: " + actual);
            var checksum = document["checksum"]!.GetValue<string>();
            if (checksum.Length == 0)
            {
                Require(repo == "hangfire" && actual.EndsWith(".cshtml", StringComparison.Ordinal) && Sources.Pairs.Any(pair => Sources.At(hangfire, pair["input"]!.GetValue<string>()) == actual), "Unknown PDB document without checksum");
                absent.Add(name);continue;
            }
            var bytes = File.ReadAllBytes(actual);
            var digest = document["algorithm"]!.GetValue<string>() switch
            {
                "8829d00f-11b8-4213-878b-770e8597ac16" => Convert.ToHexStringLower(SHA256.HashData(bytes)),
                "ff1816ec-aa5e-4d10-87f7-6f4963833460" => Convert.ToHexStringLower(SHA1.HashData(bytes)),
                _ => throw new InvalidDataException("Unknown PDB checksum algorithm")
            };
            Require(digest == checksum, "PDB source checksum differs: " + name);
            var embedded = document["embeddedSha256"]?.GetValue<string>();
            if (embedded != null) Require(embedded == Hash(bytes), "Embedded source differs");
            else
            {
                if (repo == null) throw new InvalidDataException("Unembedded generated package source");
                var source = Contract["repositories"]![repo]!;
                var prefix = $"https://raw.githubusercontent.com/{source["repository"]}/{source["commit"]}/";
                Require(links.Any(link => link.Any(mapping => mapping.Key.EndsWith('*') && normalized.StartsWith(mapping.Key[..^1], StringComparison.Ordinal) && mapping.Value == prefix + "*" && normalized[(mapping.Key.Length - 1)..] == suffix)), "SourceLink does not bind this document to its exact repository/path");
            }
            checkedPaths[Path.GetFullPath(actual)] = embedded != null;
        }
        foreach (var pair in Sources.Pairs) Require(checkedPaths.GetValueOrDefault(Path.GetFullPath(Sources.At(hangfire, pair["output"]!.GetValue<string>()))), "Generated C# was not embedded in actual PDB");
        Require(checkedPaths.GetValueOrDefault(Path.Combine(hangfire, "src/Hangfire.Core/Dashboard/JsonStats.cs")), "Modified JsonStats not embedded");
        Require(absent.Count == 22, "Known Razor PDB checksum boundary changed; review required");
        Save(report, new { publicContract = true, resources = true, generatedSourcesEmbedded = 23, checkedDocuments = checkedPaths.Count, razorDocumentsWithoutChecksum = absent, sourceLinks = links });
    }
    private static Dictionary<string, string> ReadLinks(string json) => JsonNode.Parse(json)!["documents"]!.AsObject().ToDictionary(item => item.Key, item => item.Value!.GetValue<string>());
}
