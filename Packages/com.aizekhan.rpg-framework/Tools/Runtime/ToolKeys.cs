using System.Collections.Generic;
using UnityEngine;
#if RPG_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace RPGFramework.Tools
{
    /// <summary>
    /// Key presses for the debug tools, read through the Input System when it is installed and enabled, else the
    /// legacy input manager, so the tools work (and do not throw every frame) in a game that uses either.
    /// </summary>
    internal static class ToolKeys
    {
#if RPG_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
        private static readonly Dictionary<KeyCode, Key> Keys = new Dictionary<KeyCode, Key>();
#endif

        public static bool WasPressed(KeyCode keyCode)
        {
            if (keyCode == KeyCode.None)
                return false;
#if RPG_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            var key = ToKey(keyCode);
            return keyboard != null && key != Key.None && keyboard[key].wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(keyCode);
#else
            return false;
#endif
        }

#if RPG_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
        private static Key ToKey(KeyCode keyCode)
        {
            if (Keys.TryGetValue(keyCode, out var key))
                return key;

            var name = keyCode.ToString();
            if (keyCode >= KeyCode.Alpha0 && keyCode <= KeyCode.Alpha9)
                name = "Digit" + (keyCode - KeyCode.Alpha0);
            else if (keyCode >= KeyCode.Keypad0 && keyCode <= KeyCode.Keypad9)
                name = "Numpad" + (keyCode - KeyCode.Keypad0);
            else if (keyCode == KeyCode.Return)
                name = nameof(Key.Enter);

            if (!System.Enum.TryParse(name, true, out key))
                key = Key.None;
            Keys[keyCode] = key;
            return key;
        }
#endif
    }
}
