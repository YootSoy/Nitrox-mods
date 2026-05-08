using System;
using System.Linq;
using System.Reflection;
using NitroxClient.MonoBehaviours.Gui.Input;

namespace NitroxPatcher.Patches.Persistent;

/// <summary>
/// Specific patch for GameInput.Button enum to return Nitrox's custom values.
/// Noe safer for Nautilus compatibility.
/// </summary>
public partial class Enum_GetValues_Patch : NitroxPatch, IPersistentPatch
{
    private static readonly MethodInfo TARGET_METHOD =
        Reflect.Method(() => Enum.GetValues(default!));

    public static void Postfix(Type enumType, ref Array __result)
    {
        if (enumType != typeof(GameInput.Button))
        {
            return;
        }

        GameInput.Button[] existingButtons = __result
            .Cast<GameInput.Button>()
            .ToArray();

        GameInput.Button[] nitroxButtons = Enumerable
            .Range(KeyBindingManager.NITROX_BASE_ID, KeyBindingManager.KeyBindings.Count)
            .Cast<GameInput.Button>()
            .Where(button => !existingButtons.Contains(button))
            .ToArray();

        if (nitroxButtons.Length == 0)
        {
            return;
        }

        __result = existingButtons
            .Concat(nitroxButtons)
            .ToArray();
    }
}
