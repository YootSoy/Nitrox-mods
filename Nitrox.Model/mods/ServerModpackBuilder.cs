using System.Security.Cryptography;
using System.Text.Json;
using Nitrox.Model.Mods;

namespace Nitrox.Server.Subnautica.Mods;

public sealed class ServerModpackBuilder
{
    private readonly string modsDirectory;
    private readonly string manifestPath;

    public ServerModpackBuilder(string dataPath)
    {
        modsDirectory = Path.Combine(dataPath, "Mods");
        manifestPath = Path.Combine(dataPath, "mods-manifest.json");
    }

    public ModpackManifest Build(string serverName, string serverId)
    {
        Directory.CreateDirectory(modsDirectory);

        ModpackManifest manifest = new()
        {
            ServerName = serverName,
            ServerId = serverId,
            RequiresBepInEx = true,
            RequiresNautilus = Directory
                .EnumerateFiles(modsDirectory, "*", SearchOption.AllDirectories)
                .Any(path => Path.GetFileName(path).Contains("Nautilus", StringComparison.OrdinalIgnoreCase))
        };

        foreach (string file in Directory.EnumerateFiles(modsDirectory, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(modsDirectory, file).Replace('\\', '/');

            manifest.Files.Add(new ModpackFile
            {
                ModId = GuessModId(file),
                Name = Path.GetFileNameWithoutExtension(file),
                Version = "unknown",
                RelativePath = relative,
                Sha256 = Sha256File(file),
                SizeBytes = new FileInfo(file).Length,
                Required = true
            });
        }

        File.WriteAllText(
            manifestPath,
            JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true })
        );

        return manifest;
    }

    private static string GuessModId(string path)
    {
        string name = Path.GetFileNameWithoutExtension(path);
        return name.ToLowerInvariant().Replace(" ", "-");
    }

    private static string Sha256File(string path)
    {
        using FileStream stream = File.OpenRead(path);
        byte[] hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
