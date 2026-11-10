using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Xml.Linq;
using Microsoft.Build.Framework;

// CLR4-compatible task; its source stays outside the Hangfire Compile glob.
public sealed class CaptureRazorAssemblies : Microsoft.Build.Utilities.Task
{
    [Required]
    public string OutputPath { get; set; } = String.Empty;

    public override bool Execute()
    {
        var module = Process.GetCurrentProcess().MainModule;
        if (module == null) throw new InvalidDataException("MSBuild module missing");
        var root = new XElement("snapshot",
            new XAttribute("clr", Environment.Version.ToString()),
            new XAttribute("pointerSize", IntPtr.Size),
            new XAttribute("pid", Process.GetCurrentProcess().Id),
            new XAttribute("executable", module.FileName));
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies().OrderBy(item => item.FullName, StringComparer.Ordinal))
        {
            var name = assembly.GetName().Name;
            if (String.IsNullOrEmpty(name)) throw new InvalidDataException("Assembly name missing");
            if (!name.StartsWith("RazorGenerator", StringComparison.Ordinal) &&
                !name.StartsWith("System.Web.Mvc", StringComparison.Ordinal) &&
                !name.StartsWith("System.Web.Razor", StringComparison.Ordinal) &&
                !name.StartsWith("System.Web.WebPages", StringComparison.Ordinal) && name != "Microsoft.Web.Infrastructure") continue;
            if (assembly.IsDynamic || String.IsNullOrEmpty(assembly.Location)) throw new InvalidDataException("Unlocated Razor assembly");
            string hash;
            using (var algorithm = SHA256.Create()) hash = BitConverter.ToString(algorithm.ComputeHash(File.ReadAllBytes(assembly.Location))).Replace("-", "").ToLowerInvariant();
            root.Add(new XElement("assembly", new XAttribute("name", name), new XAttribute("version", (assembly.GetName().Version ?? throw new InvalidDataException("Assembly version missing")).ToString()), new XAttribute("path", assembly.Location), new XAttribute("sha256", hash)));
        }
        var directory = Path.GetDirectoryName(OutputPath);
        if (String.IsNullOrEmpty(directory)) throw new InvalidDataException("Output directory missing");
        Directory.CreateDirectory(directory);
        new XDocument(root).Save(OutputPath);
        return true;
    }
}
