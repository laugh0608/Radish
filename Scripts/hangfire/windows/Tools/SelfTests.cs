using System.Text.Json.Nodes;
using static Radish.WindowsRazor.Program;

namespace Radish.WindowsRazor;

internal static class SelfTests
{
    internal static void Run(string report)
    {
        var cases = new List<string>();
        void Pass(string name, Action action) { action();cases.Add(name); }
        void Reject(Action action)
        {
            try { action(); } catch (InvalidDataException) { return; }
            throw new InvalidDataException("Negative fixture was accepted");
        }
        Pass("token trivia does not mask values", () => { Tokens.Equal("class C{string x=\"a\";}", "// comment\nclass C { string x = \"a\"; }");Reject(() => Tokens.Equal("class C{string x=\"a\";}", "class C{string x=\"b\";}")); });
        Pass("only literal line endings can be classified", () => { Tokens.Equal("class C{string x=\"a\\r\\nb\";}", "class C{string x=\"a\\nb\";}", true);Reject(() => Tokens.Equal("class C{int x=1;}", "class C{int x=2;}", true)); });
        Pass("invalid generated syntax fails", () => Reject(() => Tokens.Values("class {")));
        Pass("source edit requires one exact match", () => { Require(Sources.Once("abc", "b", "z") == "azc", "edit");Reject(() => Sources.Once("bb", "b", "x"));Reject(() => Sources.Once("a", "b", "x")); });
        Pass("only approved build references removed", () => { var before = "    <PackageReference Include=\"Microsoft.SourceLink.GitHub\" Version=\"8.0.0\" />\r\n";Require(Sources.RemovePackage(before, "Microsoft.SourceLink.GitHub") == "", "ref");Reject(() => Sources.RemovePackage(before.Replace("8.0.0", "9.0.0"), "Microsoft.SourceLink.GitHub")); });
        Pass("CodeDom literal wrapping preserves values", () => { Tokens.Equal("class C{string x=\"a\"+\"b\";}", "class C{string x=\"ab\";}");Reject(() => Tokens.Equal("class C{int x=1+2;}", "class C{int x=3;}")); });
        Pass("PDB paths cannot escape their owned root", () => Reject(() => AssemblyProof.Contained(Path.Combine(Path.GetTempPath(), "owned"), "../foreign.cs")));
        var directory = Path.Combine(Path.GetTempPath(), "radish-razor-self-" + Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        try
        {
            Pass("BOM and CRLF preserved by adaptation", () => { var file = Path.Combine(directory, "file.cs");File.WriteAllBytes(file, new byte[] { 0xef, 0xbb, 0xbf }.Concat(System.Text.Encoding.UTF8.GetBytes("abc\r\n")).ToArray());Sources.Edit(file, text => Sources.Once(text, "b", "z"));Require(File.ReadAllBytes(file).SequenceEqual(new byte[] { 0xef, 0xbb, 0xbf }.Concat(System.Text.Encoding.UTF8.GetBytes("azc\r\n"))), "source format"); });
            Pass("missing generated output rejected", () => { Directory.CreateDirectory(Sources.Core(directory));Reject(() => Sources.AssertSet(directory, true)); });
            Pass("extra generated output rejected", () => { File.WriteAllText(Path.Combine(Sources.Core(directory), "unowned.cshtml.cs"), "");Reject(() => Sources.AssertSet(directory, false)); });
        }
        finally { Directory.Delete(directory, true); }
        Pass("public contract fingerprint rejects drift", () => { var data = JsonNode.Parse("{\"name\":\"X\",\"version\":\"1\",\"publicKey\":\"\",\"api\":[\"one\"],\"resources\":{}}")!;var expected = new JsonObject { ["name"] = "X", ["version"] = "1", ["publicKey"] = "", ["apiEntries"] = 1, ["resourceEntries"] = 0, ["apiSha256"] = AssemblyProof.ApiHash(data), ["resourcesSha256"] = AssemblyProof.ResourcesHash(data) };AssemblyProof.CheckContract(data, expected);data["api"] = new JsonArray("two");Reject(() => AssemblyProof.CheckContract(data, expected)); });
        Save(report, new { count = cases.Count, passed = cases });Console.WriteLine($"{cases.Count} C# contract groups passed");
    }
}
