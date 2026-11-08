using System;
using System.Linq;
using UnityEngine;

namespace FlotsamTutor
{
    internal static class RecipeShortcut
    {
        private const string Setting = "FlotsamTutor.RecipeShortcut";
        private static readonly KeyCode[] Keys = Enumerable.Range((int)KeyCode.A, 26)
            .Concat(Enumerable.Range((int)KeyCode.Alpha0, 10))
            .Concat(Enumerable.Range((int)KeyCode.F1, 12))
            .Concat(Enumerable.Range((int)KeyCode.Mouse1, 6)).Select(value => (KeyCode)value).ToArray();

        internal static KeyCode Key { get; private set; } = KeyCode.Mouse2;
        private static int modifiers;
        internal static bool Capturing { get; set; }

        internal static int CurrentModifiers =>
            ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) ? 1 : 0) |
            ((Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) ? 2 : 0) |
            ((Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)) ? 4 : 0);

        internal static bool Pressed => !Capturing && Input.GetKeyDown(Key) && CurrentModifiers == modifiers;

        internal static string Caption =>
            ((modifiers & 1) != 0 ? "Ctrl＋" : "") +
            ((modifiers & 2) != 0 ? "Shift＋" : "") +
            ((modifiers & 4) != 0 ? "Alt＋" : "") +
            (Key == KeyCode.Mouse1 ? "鼠标右键" : Key == KeyCode.Mouse2 ? "鼠标中键" :
                Key >= KeyCode.Mouse3 ? $"鼠标键 {(int)Key - (int)KeyCode.Mouse0 + 1}" :
                Key >= KeyCode.Alpha0 && Key <= KeyCode.Alpha9 ? Key.ToString().Substring(5) : Key.ToString());

        internal static void Load()
        {
            var parts = PlayerPrefs.GetString(Setting, "").Split(':');
            if (parts.Length == 2 && int.TryParse(parts[0], out int key) && int.TryParse(parts[1], out int flags) &&
                flags >= 0 && flags <= 7 && Keys.Contains((KeyCode)key) && (key != (int)KeyCode.Mouse1 || flags != 0))
            {
                Key = (KeyCode)key;
                modifiers = flags;
            }
        }

        internal static bool TryCapture()
        {
            int flags = CurrentModifiers;
            foreach (var key in Keys)
            {
                if (!Input.GetKeyDown(key) || (key == KeyCode.Mouse1 && flags == 0))
                    continue;
                Key = key;
                modifiers = flags;
                PlayerPrefs.SetString(Setting, $"{(int)key}:{flags}");
                PlayerPrefs.Save();
                return true;
            }
            return false;
        }
    }
}
