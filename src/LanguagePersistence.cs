using HarmonyLib;
using M4.Session;

namespace FlotsamTutor
{
    [HarmonyPatch(typeof(EpicUser), nameof(EpicUser.LoadFile))]
    internal static class EpicSettingsPath
    {
        private static void Prefix(ref string path)
        {
            if (path == Settings.FILENAME)
                path = PlatformEpic.SETTINGS_PATH;
        }
    }

    [HarmonyPatch(typeof(Session), "OnSettingsLoaded")]
    internal static class ApplyLoadedSettings
    {
        private static void Prefix()
        {
            // Apply the saved language before the session opens the menu.
            // The game retries automatically if audio initialization is still pending.
            Settings.Apply();
        }
    }
}
