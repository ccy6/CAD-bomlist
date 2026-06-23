using System.Text.Encodings.Web;
using System.Text.Json;
using BomCadPlugin.Core.Models;

namespace BomCadPlugin.Core.Services;

public sealed class ConfigService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public BomConfig Load(string configPath)
    {
        if (!File.Exists(configPath))
        {
            return new BomConfig();
        }

        var json = File.ReadAllText(configPath);
        return JsonSerializer.Deserialize<BomConfig>(json, JsonOptions) ?? new BomConfig();
    }

    public void Save(string configPath, BomConfig config)
    {
        var directory = Path.GetDirectoryName(configPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        config.Project.UpdatedAt = DateTime.Now;
        var json = JsonSerializer.Serialize(config, JsonOptions);
        File.WriteAllText(configPath, json, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }
}
