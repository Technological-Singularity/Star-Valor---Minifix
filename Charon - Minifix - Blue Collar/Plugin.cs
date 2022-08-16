using BepInEx;
using HarmonyLib;

namespace Charon_SV_Minifix.BlueCollar {
    [BepInPlugin(pluginGuid, pluginName, pluginVersion)]
    [BepInProcess("Star Valor.exe")]
    public class Plugin : BaseUnityPlugin {
        public const string pluginGuid = "starvalor.charon.minifix.bluecollar";
        public const string pluginName = "Charon - Minifix - Blue Collar";
        public const string pluginVersion = "0.0.0.0";

        public void Awake() {
            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        [HarmonyPatch(typeof(CharacterScreen), nameof(CharacterScreen.Open))]
        [HarmonyPostfix]
        public static void Open(int mode) {
            if (PChar.HasPerk(3)) { //White Collar
                PChar.RemovePerk(3);
                PerkDB.AcquirePerk(1); //Miner
            }
        }
    }
}
