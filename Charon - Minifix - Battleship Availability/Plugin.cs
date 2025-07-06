using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace Charon.StarValor {
    [BepInPlugin(pluginGuid, pluginName, pluginVersion)]
    [BepInProcess("Star Valor.exe")]
    public class Plugin : BaseUnityPlugin {
        public const string pluginGuid = "starvalor.charon.minifix.battleshipavailability";
        public const string pluginName = "Charon - Minifix - Battleship Availability";
        public const string pluginVersion = "0.0.0.0";

        public void Awake() {
            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        [HarmonyPatch(typeof(ShipDB), nameof(ShipDB.GetList))]
        [HarmonyPrefix]
        static void ShipDB_GetList_OverrideSpinal(int minShipClass, int maxShipClass, int faction, int maxPower, bool allowUnarmed, ref bool allowSpinalMount, bool allowNonCombatRole, Random rand) {
            allowSpinalMount = true;
        }
    }
}
