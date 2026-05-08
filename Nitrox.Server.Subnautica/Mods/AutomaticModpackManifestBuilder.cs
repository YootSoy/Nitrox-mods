using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Mono.Cecil;
using Nitrox.Model.Mods;

namespace Nitrox.Server.Subnautica.Mods;

public sealed class AutomaticModpackManifestBuilder
{
    private readonly string dataPath;
    private readonly string modsPath;
    private readonly string workPath;
    private readonly string manifestPath;

    public AutomaticModpackManifestBuilder(string dataPath)
    {
        this.dataPath = dataPath;
        modsPath = Path.Combine(dataPath, "Mods");
        workPath = Path.Combine(dataPath, "ModpackWork");
        manifestPath = Path.Combine(dataPath, "mods-manifest.json");
    }

    public ModpackManifest Build(string serverName, string serverId)
    {
        Directory.CreateDirectory(modsPath);

        if (Directory.Exists(workPath))
        {
            Directory.Delete(workPath, recursive: true);
        }

        Directory.CreateDirectory(workPath);

        ModpackManifest manifest = new()
        {
            ServerName = serverName,
            ServerId = serverId,
            CreatedAtUtc = DateTime.UtcNow
        };

        foreach (string item in Directory.EnumerateFileSystemEntries(modsPath))
        {
            if (Directory.Exists(item))
            {
                AddFolderMod(manifest, serverId, item);
                continue;
            }

            string ext = Path.GetExtension(item).ToLowerInvariant();

            if (ext == ".dll")
            {
                AddSingleDllMod(manifest, serverId, item);
                continue;
            }

            if (ext == ".zip")
            {
                AddZipMod(manifest, serverId, item);
                continue;
            }
        }

        File.WriteAllText(
            manifestPath,
            JsonSerializer.Serialize(manifest, new JsonSerializerOptions
            {
                WriteIndented = true
            })
        );

        return manifest;
    }

    private void AddSingleDllMod(ModpackManifest manifest, string serverId, string dllPath)
    {
        PluginMetadata meta = ReadPluginMetadata(dllPath)
            ?? PluginMetadata.FallbackFromFile(dllPath);

        string packageName = MakeSafePathPart(meta.Guid);
        string installPath = $"BepInEx/plugins/NitroxServerMods/{MakeSafePathPart(serverId)}/{packageName}/{Path.GetFileName(dllPath)}";

        AddPluginIfMissing(manifest, meta, installPath);

        manifest.Files.Add(new ModpackFile
        {
            PluginGuid = meta.Guid,
            RelativeSourcePath = Path.GetFileName(dllPath),
            InstallPath = installPath,
            Sha256 = Sha256File(dllPath),
            SizeBytes = new FileInfo(dllPath).Length,
            Required = true
        });
    }

    private void AddFolderMod(ModpackManifest manifest, string serverId, string folderPath)
    {
        string folderName = Path.GetFileName(folderPath);
        List<string> files = Directory
            .EnumerateFiles(folderPath, "*", SearchOption.AllDirectories)
            .ToList();

        string? mainDll = files.FirstOrDefault(IsDllWithBepInPlugin)
            ?? files.FirstOrDefault(file => Path.GetExtension(file).Equals(".dll", StringComparison.OrdinalIgnoreCase));

        PluginMetadata meta = mainDll != null
            ? ReadPluginMetadata(mainDll) ?? PluginMetadata.FallbackFromFile(mainDll)
            : new PluginMetadata(MakeSafePathPart(folderName), folderName, "unknown");

        foreach (string file in files)
        {
            string relative = Path.GetRelativePath(folderPath, file).Replace('\\', '/');

            string installPath = LooksLikeFullBepInExPath(relative)
                ? relative
                : $"BepInEx/plugins/NitroxServerMods/{MakeSafePathPart(serverId)}/{MakeSafePathPart(folderName)}/{relative}";

            if (file == mainDll)
            {
                AddPluginIfMissing(manifest, meta, installPath);
            }

            manifest.Files.Add(new ModpackFile
            {
                PluginGuid = meta.Guid,
                RelativeSourcePath = $"{folderName}/{relative}",
                InstallPath = installPath,
                Sha256 = Sha256File(file),
                SizeBytes = new FileInfo(file).Length,
                Required = true
            });
        }
    }

    private void AddZipMod(ModpackManifest manifest, string serverId, string zipPath)
    {
        string zipName = Path.GetFileNameWithoutExtension(zipPath);
        string extractPath = Path.Combine(workPath, MakeSafePathPart(zipName));

        ZipFile.ExtractToDirectory(zipPath, extractPath);

        AddFolderMod(manifest, serverId, extractPath);
    }

    private static void AddPluginIfMissing(ModpackManifest manifest, PluginMetadata meta, string mainDllInstallPath)
    {
        if (manifest.Plugins.Any(plugin => plugin.Guid == meta.Guid))
        {
            return;
        }

        manifest.Plugins.Add(new ModpackPlugin
        {
            Guid = meta.Guid,
            Name = meta.Name,
            Version = meta.Version,
            MainDll = mainDllInstallPath,
            Required = true
        });
    }

    private static bool IsDllWithBepInPlugin(string file)
    {
        if (!Path.GetExtension(file).Equals(".dll", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return ReadPluginMetadata(file) != null;
    }

    private static PluginMetadata? ReadPluginMetadata(string dllPath)
    {
        try
        {
            using AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(dllPath);

            foreach (TypeDefinition type in assembly.MainModule.Types)
            {
                PluginMetadata? meta = ReadPluginMetadataFromType(type);
                if (meta != null)
                {
                    return meta;
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private static PluginMetadata? ReadPluginMetadataFromType(TypeDefinition type)
    {
        foreach (CustomAttribute attr in type.CustomAttributes)
        {
            if (attr.AttributeType.FullName != "BepInEx.BepInPlugin")
            {
                continue;
            }

            if (attr.ConstructorArguments.Count < 3)
            {
                continue;
            }

            string guid = attr.ConstructorArguments[0].Value?.ToString() ?? "";
            string name = attr.ConstructorArguments[1].Value?.ToString() ?? "";
            string version = attr.ConstructorArguments[2].Value?.ToString() ?? "unknown";

            if (string.IsNullOrWhiteSpace(guid))
            {
                return null;
            }

            return new PluginMetadata(guid, name, version);
        }

        foreach (TypeDefinition nested in type.NestedTypes)
        {
            PluginMetadata? meta = ReadPluginMetadataFromType(nested);
            if (meta != null)
            {
                return meta;
            }
        }

        return null;
    }

    private static bool LooksLikeFullBepInExPath(string relative)
    {
        string normalized = relative.Replace('\\', '/');

        return normalized.StartsWith("BepInEx/plugins/", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("BepInEx/patchers/", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("BepInEx/config/", StringComparison.OrdinalIgnoreCase);
    }

    private static string Sha256File(string path)
    {
        using FileStream stream = File.OpenRead(path);
        byte[] hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string MakeSafePathPart(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "unknown";
        }

        foreach (char c in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(c, '_');
        }

        return value.Replace("..", "_");
    }

    private sealed record PluginMetadata(string Guid, string Name, string Version)
    {
        public static PluginMetadata FallbackFromFile(string file)
        {
            string name = Path.GetFileNameWithoutExtension(file);
            string guid = MakeSafePathPart(name).ToLowerInvariant();

            return new PluginMetadata(guid, name, "unknown");
        }
    }
}
