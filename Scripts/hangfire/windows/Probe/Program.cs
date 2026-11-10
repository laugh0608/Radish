using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Hangfire.Dashboard;
using JsonConvert = Newtonsoft.Json.JsonConvert;

namespace Radish.WindowsRazorProbe;

internal static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            if (args.Length != 5 || args[2] is not ("plain" or "marked")) throw new ArgumentException("Expected output, Core hash, variant, marker and JSON hash");
            var assembly = typeof(RazorPage).Assembly;
            var hash = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(assembly.Location)));
            if (Path.GetDirectoryName(assembly.Location) != AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar) || hash != args[1]) throw new InvalidDataException("Wrong Core DLL loaded");
            var json = typeof(JsonConvert).Assembly;
            var jsonHash = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(json.Location)));
            if (Path.GetDirectoryName(json.Location) != AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar) || jsonHash != args[4]) throw new InvalidDataException("Wrong Newtonsoft DLL loaded");
            var type = assembly.GetType("Hangfire.Dashboard.Pages.ErrorAlert", throwOnError: true)!;
            var page = (RazorPage)Activator.CreateInstance(type, nonPublic: true)!;
            var html = page.ToString();
            if (!html.Contains("id=\"errorAlert\"", StringComparison.Ordinal) || !html.Contains("role=\"alert\"", StringComparison.Ordinal) || !html.Contains("alert-danger", StringComparison.Ordinal)) throw new InvalidDataException("Generated fragment contract missing");
            var attribute = " data-radish-source=\"" + args[3] + "\"";
            if (html.Contains(args[3], StringComparison.Ordinal) != (args[2] == "marked") || args[2] == "marked" && html.Split(attribute, StringSplitOptions.None).Length != 2) throw new InvalidDataException("Template marker missing or unexpected");
            var normalized = html.Replace(attribute, "", StringComparison.Ordinal).Replace("\r\n", "\n", StringComparison.Ordinal);
            var report = new { assembly = assembly.Location, sha256 = hash, mvid = assembly.ManifestModule.ModuleVersionId, jsonAssembly = json.Location, jsonSha256 = jsonHash, type = type.FullName, marked = args[2] == "marked", htmlSha256 = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(html))), normalizedHtmlSha256 = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(normalized))), html };
            File.WriteAllText(args[0], JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }) + "\n", new UTF8Encoding(false));
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error);return 1; }
    }
}
