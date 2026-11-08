using System;
using System.IO;
using System.Security.Cryptography;
using BepInEx;
using UnityEngine;

namespace FlotsamTutor
{
    [BepInPlugin("local.flotsam.tutor", "Flotsam Tutor", "1.3.0")]
    public sealed class Entry : BaseUnityPlugin
    {
        private const string SupportedGameHash = "5B5C9F730ED71D5F66C0267064FC57667D2FF5644A018E9189B140CE025C1CD8";

        private void Awake()
        {
            try
            {
                using (var file = File.OpenRead(Path.Combine(Application.dataPath, "Managed", "Assembly-CSharp.dll")))
                using (var sha = SHA256.Create())
                {
                    var hash = BitConverter.ToString(sha.ComputeHash(file)).Replace("-", "");
                    if (hash != SupportedGameHash)
                    {
                        Logger.LogWarning("Unsupported Flotsam version. The mod was not loaded.");
                        return;
                    }
                }

                if (TutorPanel.Install() != 0)
                    Logger.LogError("Flotsam Tutor could not install its game hooks.");
            }
            catch (Exception error)
            {
                Logger.LogError(error);
            }
        }
    }
}
