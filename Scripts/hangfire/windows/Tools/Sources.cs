using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using static Radish.WindowsRazor.Program;

namespace Radish.WindowsRazor;

internal static class Sources
{
    internal static string Core(string repository) => Path.Combine(repository, "src/Hangfire.Core");
    internal static IEnumerable<JsonNode> Pairs => Contract["pairs"]!.AsArray().Select(node => node!);
    internal static string At(string repository, string relative) => Path.Combine(repository, relative.Replace('/', Path.DirectorySeparatorChar));
    internal static void Edit(string file, Func<string, string> change)
    {
        var bytes = File.ReadAllBytes(file);
        var bom = bytes.AsSpan().StartsWith(new byte[] { 0xef, 0xbb, 0xbf });
        var text = Encoding.UTF8.GetString(bytes, bom ? 3 : 0, bytes.Length - (bom ? 3 : 0));
        var changed = change(text);
        Require(changed != text, "Source adaptation did not change " + file);
        File.WriteAllText(file, changed, new UTF8Encoding(bom));
    }
    internal static string Once(string text, string before, string after)
    {
        Require(text.Split(before, StringSplitOptions.None).Length == 2, "Expected one source match: " + before);
        return text.Replace(before, after, StringComparison.Ordinal);
    }
    internal static string RemovePackage(string text, string name)
    {
        var expression = new Regex("(?m)^[ \\t]*<PackageReference Include=\"" + Regex.Escape(name) + "\" Version=\"8\\.0\\.0\" />[^\\S\\r\\n]*\\r?\\n");
        Require(expression.Matches(text).Count == 1, "Unexpected build package reference: " + name);
        return expression.Replace(text, "");
    }
    internal static void Adapt(string repository, string name, string evidence)
    {
        Require(name is "hangfire" or "cronos", "Unknown repository");
        var props = name == "hangfire" ? "src/Directory.Build.props" : "src/Directory.build.props";
        Edit(At(repository, props), text => RemovePackage(RemovePackage(text, "Microsoft.CodeAnalysis.NetAnalyzers"), "Microsoft.SourceLink.GitHub"));
        var project = name == "hangfire" ? "src/Hangfire.Core/Hangfire.Core.csproj" : "src/Cronos/Cronos.csproj";
        Edit(At(repository, project), text =>
        {
            var matches = Regex.Matches(text, "<TargetFrameworks>[^<]+</TargetFrameworks>");
            Require(matches.Count == 1, "Unexpected target frameworks");
            text = text.Replace(matches[0].Value, "<TargetFramework>netstandard2.0</TargetFramework>", StringComparison.Ordinal);
            return name == "hangfire" ? Once(text, "Include=\"Newtonsoft.Json\" Version=\"11.0.1\" NoWarn=\"NU1903\"", "Include=\"Newtonsoft.Json\" Version=\"[13.0.4]\"") : text;
        });
        if (name == "hangfire") Edit(At(repository, "src/Hangfire.Core/Dashboard/JsonStats.cs"), text => Once(text, "new StringEnumConverter { CamelCaseText = true }", "new StringEnumConverter { NamingStrategy = new CamelCaseNamingStrategy() }"));
        var locks = Directory.GetFiles(Path.GetDirectoryName(At(repository, project))!, "packages.lock.json");
        Save(Path.Combine(evidence, "original-locks.json"), locks.Select(file => new { path = Path.GetRelativePath(repository, file), sha256 = FileHash(file), content = Read(file) }).ToArray());
        foreach (var file in locks) File.Delete(file);
    }
    internal static void Prepare(string repository, string variant, string evidence)
    {
        Require(Contract["variants"]!.AsArray().Any(node => node!.GetValue<string>() == variant), "Unknown variant");
        foreach (var pair in Pairs)
        {
            var file = At(repository, pair["input"]!.GetValue<string>());
            Require(FileHash(file) == pair["lfSha256"]!.GetValue<string>(), "Template changed before preparation");
            var generated = At(repository, pair["output"]!.GetValue<string>());
            Require(FileHash(generated) == pair["checkedInOutputSha256"]!.GetValue<string>(), "Checked-in generated source changed");
            if (variant.EndsWith("crlf", StringComparison.Ordinal))
            {
                var bytes = File.ReadAllBytes(file);
                var text = Encoding.UTF8.GetString(bytes).Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\n", "\r\n", StringComparison.Ordinal);
                File.WriteAllBytes(file, Encoding.UTF8.GetBytes(text));
                Require(FileHash(file) == pair["windowsCrlfSha256"]!.GetValue<string>(), "Derived CRLF bytes differ");
            }
            if (variant == "marked-crlf" && Path.GetFileName(file) == "_ErrorAlert.cshtml") Edit(file, text => Once(text, "<div id=\"errorAlert\"", "<div" + MarkerAttribute + " id=\"errorAlert\""));
            if (variant != "checked-in") File.Delete(generated);
        }
        AssertSet(repository, variant == "checked-in");
        Save(Path.Combine(evidence, "prepared.json"), new { variant, outputsDeleted = variant != "checked-in", at = DateTimeOffset.UtcNow, inputs = Pairs.Select(pair => new { path = pair["input"]!.GetValue<string>(), sha256 = FileHash(At(repository, pair["input"]!.GetValue<string>())) }).ToArray() });
    }
    internal static void AssertSet(string repository, bool present)
    {
        var actual = Directory.GetFiles(Core(repository), "*.cshtml.cs", SearchOption.AllDirectories).Select(file => Path.GetRelativePath(repository, file).Replace('\\', '/')).Order(StringComparer.Ordinal).ToArray();
        var expected = present ? Pairs.Select(pair => pair["output"]!.GetValue<string>()).Order(StringComparer.Ordinal).ToArray() : [];
        Require(actual.SequenceEqual(expected), "Generated output set is stale, missing or contains extra files");
    }
    internal static void Generated(string repository, string variant, string evidence)
    {
        var prepared = Read(Path.Combine(evidence, "prepared.json"));
        Require(prepared["variant"]!.GetValue<string>() == variant && prepared["outputsDeleted"]!.GetValue<bool>() == (variant != "checked-in"), "Missing generation preparation proof");
        AssertSet(repository, true);
        var inputs = prepared["inputs"]!.AsArray();
        foreach (var input in inputs) Require(FileHash(At(repository, input!["path"]!.GetValue<string>())) == input["sha256"]!.GetValue<string>(), "Template changed during generation");
        Save(Path.Combine(evidence, "generated.json"), Pairs.Select(pair => new { input = pair["input"]!.GetValue<string>(), inputSha256 = FileHash(At(repository, pair["input"]!.GetValue<string>())), output = pair["output"]!.GetValue<string>(), outputSha256 = FileHash(At(repository, pair["output"]!.GetValue<string>())) }).ToArray());
    }
}
