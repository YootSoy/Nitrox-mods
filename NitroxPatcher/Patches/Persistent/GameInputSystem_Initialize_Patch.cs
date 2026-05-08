using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using NitroxClient.MonoBehaviours.Gui.Input;
using NitroxClient.MonoBehaviours.Gui.Input.KeyBindings;
using UnityEngine.InputSystem;

namespace NitroxPatcher.Patches.Persistent;

/// <summary>
/// Inserts Nitrox's keybinds in the new Subnautica input system.
/// Also makes Nitrox keybinds safer when Nautilus/BepInEx is loaded.
/// </summary>
public partial class GameInputSystem_Initialize_Patch : NitroxPatch, IPersistentPatch
{
    private static readonly MethodInfo TARGET_METHOD =
        Reflect.Method((GameInputSystem t) => t.Initialize());

    private static readonly MethodInfo DEINITIALIZE_METHOD =
        Reflect.Method((GameInputSystem t) => t.Deinitialize());

    private static GameInput.Button[]? oldAllActions;

    public override void Patch(Harmony harmony)
    {
        PatchPrefix(harmony, TARGET_METHOD, ((Action<GameInputSystem>)Prefix).Method);
        PatchTranspiler(harmony, TARGET_METHOD, ((Func<IEnumerable<CodeInstruction>, IEnumerable<CodeInstruction>>)Transpiler).Method);
        PatchPrefix(harmony, DEINITIALIZE_METHOD, ((Action)DeinitializePrefix).Method);
    }

    public static void Prefix(GameInputSystem __instance)
    {
        CachedEnumString<GameInput.Button> actionNames = GameInput.ActionNames;

        int buttonId = KeyBindingManager.NITROX_BASE_ID;

        oldAllActions ??= GameInput.AllActions;

        FieldInfo? allActionsField = typeof(GameInput).GetField(
            nameof(GameInput.AllActions),
            BindingFlags.Public | BindingFlags.Static
        );

        if (allActionsField != null && GameInput.AllActions != null)
        {
            GameInput.Button[] nitroxButtons = Enumerable
                .Range(KeyBindingManager.NITROX_BASE_ID, KeyBindingManager.KeyBindings.Count)
                .Cast<GameInput.Button>()
                .ToArray();

            GameInput.Button[] newAllActions = GameInput.AllActions
                .Concat(nitroxButtons)
                .Distinct()
                .ToArray();

            allActionsField.SetValue(null, newAllActions);
        }

        foreach (KeyBinding keyBinding in KeyBindingManager.KeyBindings)
        {
            GameInput.Button button = (GameInput.Button)buttonId++;

            actionNames.valueToString[button] = keyBinding.ButtonLabel;

            RegisterLanguageOption(button, keyBinding.ButtonLabel);

            if (!string.IsNullOrEmpty(keyBinding.DefaultKeyboardKey) &&
                !GameInputSystem.bindingsKeyboard.ContainsKey(button))
            {
                GameInputSystem.bindingsKeyboard.Add(button, $"/{keyBinding.DefaultKeyboardKey}");
            }

            if (!string.IsNullOrEmpty(keyBinding.DefaultControllerKey) &&
                !GameInputSystem.bindingsController.ContainsKey(button))
            {
                GameInputSystem.bindingsController.Add(button, $"/{keyBinding.DefaultControllerKey}");
            }
        }
    }

    public static void DeinitializePrefix()
    {
        if (oldAllActions == null)
        {
            return;
        }

        FieldInfo? allActionsField = typeof(GameInput).GetField(
            nameof(GameInput.AllActions),
            BindingFlags.Public | BindingFlags.Static
        );

        allActionsField?.SetValue(null, oldAllActions);
        oldAllActions = null;
    }


    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return new CodeMatcher(instructions)
            .MatchStartForward(
            [
                new(OpCodes.Ldarg_0),
                new(OpCodes.Ldfld),
                new(OpCodes.Callvirt, Reflect.Method((InputActionMap t) => t.Enable()))
            ])
            .Insert(
            [
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Call, Reflect.Method(() => RegisterKeybindsActions(default!)))
            ])
            .InstructionEnumeration();
    }

    /// <summary>
    /// Sets callbacks for Nitrox keybinds.
    /// If Nautilus or another input patch changes initialization order, Nitrox creates missing InputAction entries itself.
    /// </summary>
    public static void RegisterKeybindsActions(GameInputSystem gameInputSystem)
    {
        int buttonId = KeyBindingManager.NITROX_BASE_ID;

        foreach (KeyBinding keyBinding in KeyBindingManager.KeyBindings)
        {
            GameInput.Button button = (GameInput.Button)buttonId++;

            if (!gameInputSystem.actions.TryGetValue(button, out InputAction action))
            {
                string buttonName = GameInput.ActionNames.valueToString.TryGetValue(button, out string name)
                    ? name
                    : button.ToString();

                action = new InputAction(buttonName, InputActionType.Button);

                if (!string.IsNullOrEmpty(keyBinding.DefaultKeyboardKey))
                {
                    action.AddBinding($"<Keyboard>/{keyBinding.DefaultKeyboardKey}");
                }

                if (!string.IsNullOrEmpty(keyBinding.DefaultControllerKey))
                {
                    action.AddBinding($"<Gamepad>/{keyBinding.DefaultControllerKey}");
                }

                gameInputSystem.actions[button] = action;
                action.started += gameInputSystem.OnActionStarted;
                action.Enable();
            }

            action.started -= keyBinding.Execute;
            action.started += keyBinding.Execute;
        }
    }

    private static void RegisterLanguageOption(GameInput.Button button, string label)
    {
        if (Language.main == null || string.IsNullOrEmpty(label))
        {
            return;
        }

        string key = $"Option{(int)button}";

        FieldInfo? stringsField = typeof(Language).GetField(
            "strings",
            BindingFlags.Instance | BindingFlags.NonPublic
        );

        if (stringsField?.GetValue(Language.main) is Dictionary<string, string> strings)
        {
            strings[key] = label;
        }
    }
}
