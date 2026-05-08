using System;
using System.Collections.Generic;

namespace Nitrox.Model.Mods;

[Serializable]
public sealed class ModpackManifest
{
    public int ProtocolVersion { get; set; } = 1;

    public string ServerName { get; set; } = "";
    public string ServerId { get; set; } = "";

    public bool RequiresNautilus { get; set; } = true;
    public bool RequiresBepInEx { get; set; } = true;

    public List<ModpackFile> Files { get; set; } = new();
}

[Serializable]
public sealed class ModpackFile
{
    public string ModId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Version { get; set; } = "";
    public string RelativePath { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public long SizeBytes { get; set; }
    public bool Required { get; set; } = true;
}
