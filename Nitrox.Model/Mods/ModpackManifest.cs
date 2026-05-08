using System;
using System.Collections.Generic;

namespace Nitrox.Model.Mods;

[Serializable]
public sealed class ModpackManifest
{
    public int SchemaVersion { get; set; } = 1;

    public string ServerName { get; set; } = "";
    public string ServerId { get; set; } = "";

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public List<ModpackPlugin> Plugins { get; set; } = new();
    public List<ModpackFile> Files { get; set; } = new();
}

[Serializable]
public sealed class ModpackPlugin
{
    public string Guid { get; set; } = "";
    public string Name { get; set; } = "";
    public string Version { get; set; } = "";
    public string MainDll { get; set; } = "";
    public bool Required { get; set; } = true;
}

[Serializable]
public sealed class ModpackFile
{
    public string PluginGuid { get; set; } = "";
    public string RelativeSourcePath { get; set; } = "";
    public string InstallPath { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public long SizeBytes { get; set; }
    public bool Required { get; set; } = true;
}
