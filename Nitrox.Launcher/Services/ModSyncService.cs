using System.Security.Cryptography;
using System.Text.Json;
using Nitrox.Model.Mods;

namespace Nitrox.Launcher.Services;

public sealed class ModSyncService
{
    private readonly string gamePath;

    public ModSyncService(string gamePath)
    {
        this.gamePath = gamePath;
    }

    public async Task<ModSyncResult> SyncAsync(Uri manifestUri, Func<ModpackFile, Uri> fileUriFactory)
    {
        using HttpClient http = new();

        string manifestJson = await http.GetStringAsync(manifestUri);
        ModpackManifest manifest = JsonSerializer.Deserialize<ModpackManifest>(
            manifestJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        ) ?? throw new InvalidOperationException("Invalid modpack manifest.");

        EnsureBepInExFolder();

        List<ModpackFile> installed = new();
        List<ModpackFile> downloaded = new();

        foreach (ModpackFile file in manifest.Files)
        {
            string targetPath = GetTargetPath(manifest, file);

            if (File.Exists(targetPath) && Sha256File(targetPath) == file.Sha256)
            {
                installed.Add(file);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

            byte[] data = await http.GetByteArrayAsync(fileUriFactory(file));

            string tempPath = targetPath + ".download";
            await File.WriteAllBytesAsync(tempPath, data);

            string downloadedHash = Sha256File(tempPath);
            if (!downloadedHash.Equals(file.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(tempPath);
                throw new InvalidOperationException($"Hash mismatch for mod file: {file.Name}");
            }

            if (File.Exists(targetPath))
            {
                string backupPath = targetPath + ".backup";
                File.Move(targetPath, backupPath, overwrite: true);
            }

            File.Move(tempPath, targetPath, overwrite: true);
            downloaded.Add(file);
        }

        return new ModSyncResult(manifest, installed, downloaded);
    }

    private void EnsureBepInExFolder()
    {
        string bepInExPath = Path.Combine(gamePath, "BepInEx");
        string pluginsPath = Path.Combine(bepInExPath, "plugins");

        if (!Directory.Exists(pluginsPath))
        {
            throw new InvalidOperationException(
                "BepInEx is not installed. Install BepInEx first before using server mod sync."
            );
        }
    }

    private string GetTargetPath(ModpackManifest manifest, ModpackFile file)
    {
        string safeServerId = MakeSafePathPart(manifest.ServerId);

        return Path.Combine(
            gamePath,
            "BepInEx",
            "plugins",
            "NitroxServerMods",
            safeServerId,
            file.RelativePath.Replace('/', Path.DirectorySeparatorChar)
        );
    }

    private static string MakeSafePathPart(string value)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(c, '_');
        }

        return value;
    }

    private static string Sha256File(string path)
    {
        using FileStream stream = File.OpenRead(path);
        byte[] hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

public sealed record ModSyncResult(
    ModpackManifest Manifest,
    IReadOnlyList<ModpackFile> AlreadyInstalled,
    IReadOnlyList<ModpackFile> Downloaded
);
