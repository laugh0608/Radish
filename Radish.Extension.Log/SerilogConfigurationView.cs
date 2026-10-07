using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;

namespace Radish.Extension.Log;

/// <summary>保留实时配置 / reload，将根设置和输出设置分开接线，防止配置 sink 绕过适配。</summary>
internal sealed class SerilogConfigurationView(IConfiguration source, string mode, string path = "") : IConfigurationSection
{
    public string Key => path.Split(':')[^1];
    public string Path => path;
    public string? Value { get => Allowed(path) ? source[path] : null; set => throw new NotSupportedException(); }
    public string? this[string key]
    {
        get => GetSection(key).Value;
        set => throw new NotSupportedException();
    }
    public IConfigurationSection GetSection(string key) => new SerilogConfigurationView(source, mode,
        string.IsNullOrEmpty(path) ? key : path + ":" + key);
    public IEnumerable<IConfigurationSection> GetChildren() =>
        (string.IsNullOrEmpty(path) ? source.GetChildren() : source.GetSection(path).GetChildren())
        .Where(child => Allowed(child.Path))
        .Select(child => new SerilogConfigurationView(source, mode, child.Path));
    public IChangeToken GetReloadToken() => source.GetReloadToken();

    private bool Allowed(string key)
    {
        var parts = key.Split(':');
        if (parts.Length < 2 || !parts[0].Equals("Serilog", StringComparison.OrdinalIgnoreCase)) return true;
        var setting = parts[1];
        if (mode == "root") return !setting.Equals("WriteTo", StringComparison.OrdinalIgnoreCase)
            && !setting.Equals("AuditTo", StringComparison.OrdinalIgnoreCase);
        return setting.Equals(mode, StringComparison.OrdinalIgnoreCase)
            || setting.Equals("Using", StringComparison.OrdinalIgnoreCase)
            || setting.Equals("LevelSwitches", StringComparison.OrdinalIgnoreCase)
            || setting.Equals("FilterSwitches", StringComparison.OrdinalIgnoreCase);
    }
}
