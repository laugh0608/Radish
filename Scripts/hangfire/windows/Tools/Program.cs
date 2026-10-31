using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Radish.WindowsRazor;

internal static class Program
{
    internal static readonly JsonNode Contract = Read(Path.Combine(AppContext.BaseDirectory, "inputs.json"));
    internal static string Marker => Contract["marker"]!.GetValue<string>();
    internal static string MarkerAttribute => " data-radish-source=\"" + Marker + "\"";
    internal static JsonNode Read(string file) => JsonNode.Parse(File.ReadAllText(file)) ?? throw new InvalidDataException(file);
    internal static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
    internal static string Hash(byte[] data) => Convert.ToHexStringLower(SHA256.HashData(data));
    internal static string FileHash(string file) => Hash(File.ReadAllBytes(file));
    internal static string TextHash(string text) => Hash(Encoding.UTF8.GetBytes(text));
    internal static void Save(string file, object data)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(file))!);
        File.WriteAllText(file, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }) + "\n", new UTF8Encoding(false));
    }
    public static int Main(string[] args)
    {
        try
        {
            switch (args[0])
            {
                case "assembly": case "packages": return HangfireMetadata.Program.Main(args);
                case "adapt": Sources.Adapt(args[1], args[2], args[3]); break;
                case "prepare": Sources.Prepare(args[1], args[2], args[3]); break;
                case "generated": Sources.Generated(args[1], args[2], args[3]); break;
                case "compare": Tokens.Compare(args[1], args[2], args[3], args[4], args[5]); break;
                case "contract": AssemblyProof.CheckContract(Read(args[1]), Read(Path.Combine(AppContext.BaseDirectory, "core-contract.json"))); Save(args[2], new { publicContract = true, resources = true }); break;
                case "proof": AssemblyProof.Verify(args[1], args[2], args[3], args[4], args[5]); break;
                case "self-test": SelfTests.Run(args[1]); break;
                default: throw new ArgumentException("Unknown tool mode");
            }
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
