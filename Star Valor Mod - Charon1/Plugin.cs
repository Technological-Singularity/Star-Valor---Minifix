using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace Charon_NAMESPACE {
    [BepInPlugin(pluginGuid, pluginName, pluginVersion)]
    [BepInProcess("Star Valor.exe")]
    public class Plugin : BaseUnityPlugin {
        public const string pluginGuid = "starvalor.charon.GUID";
        public const string pluginName = "Charon - PLUGINNAME";
        public const string pluginVersion = "0.0.0.0";
        static BepInEx.Logging.ManualLogSource Log;

        public void Awake() {
            Log = Logger;
            Harmony.CreateAndPatchAll(typeof(Plugin));
        }
    }
}
