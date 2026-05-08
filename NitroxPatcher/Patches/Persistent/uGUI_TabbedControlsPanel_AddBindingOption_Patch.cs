using System.Reflection;

namespace NitroxPatcher.Patches.Persistent;

/// <summary>
/// Intercepts binding options created by Nitrox to remove the "Option" prefix.
/// Avoids recursive AddBindingOption calls which can break rebinding.
/// </summary>
public partial class uGUI_TabbedControlsPanel_AddBindingOption_Patch : NitroxPatch, IPersistentPatch
{
    private static readonly MethodInfo TARGET_METHOD =
        Reflect.Method((uGUI_TabbedControlsPanel t) => t.AddBindingOption(default, default, default, default));

    private const string SUBNAUTICA_OPTION_PREFIX = "Option";
    private const string DETECT_OPTION_PREFIX = "OptionNitrox";

    public static void Prefix(ref string label)
    {
        if (string.IsNullOrEmpty(label))
        {
            return;
        }

        if (label.StartsWith(DETECT_OPTION_PREFIX))
        {
            label = label[SUBNAUTICA_OPTION_PREFIX.Length..];
        }
    }
}
