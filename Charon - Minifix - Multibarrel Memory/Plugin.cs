using BepInEx;
using HarmonyLib;
using UnityEngine;
using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Collections.Generic;
using System.Linq;

namespace Charon.StarValor.Minifix.MultibarrelMemory {
    [BepInPlugin(pluginGuid, pluginName, pluginVersion)]
    [BepInProcess("Star Valor.exe")]
    public class Plugin : BaseUnityPlugin {
        public const string pluginGuid = "starvalor.charon.minifix.multibarrel_memory";
        public const string pluginName = "Charon - Minifix - Multibarrel Memory";
        public const string pluginVersion = "0.0.0.0";
                
        static BepInEx.Logging.ManualLogSource Log;
        static Dictionary<int, Dictionary<int, int>> turretModesByShipModelId = new Dictionary<int, Dictionary<int, int>>();
        static bool canSave = false;

        public void Awake() {
            Log = Logger;
            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        static void SaveTurrets(SpaceShip ss, int modelId) {
            turretModesByShipModelId[modelId] = ss.transform.GetComponentsInChildren<WeaponTurret>().ToDictionary(o => (int)o.turretIndex, o => o.alternateFire ? 1 : 0);
        }
        static void LoadTurrets(SpaceShip ss, int modelId) {
            if (turretModesByShipModelId.TryGetValue(modelId, out var dict)) {
                foreach (var o in ss.transform.GetComponentsInChildren<WeaponTurret>())
                    o.alternateFire = dict[o.turretIndex] != 0;
                foreach (var o in ss.weaponTrans.GetComponents<Weapon>())
                    o.Load(true);
            }
        }

        [HarmonyPatch(typeof(SpaceShip), "CalculateShipStats")]
        [HarmonyPrefix]
        static void SpaceShip_CalculateShipStats_TurretModesSave(ShipModel shipModel, SpaceShip __instance) {
            if (__instance == null || !__instance.CompareTag("Player") || !canSave)
                return;
            Log.LogMessage("SAVE");
            SaveTurrets(__instance, shipModel.data.id);
        }

        [HarmonyPatch(typeof(SpaceShip), "CalculateShipStats")]
        [HarmonyPostfix]
        static void SpaceShip_CalculateShipStats_TurretModesLoad(ShipModel shipModel, SpaceShip __instance) {
            if (__instance == null || !__instance.CompareTag("Player"))
                return;
            LoadTurrets(__instance, shipModel.data.id);
            canSave = true;
        }

        [HarmonyPatch(typeof(GameData), nameof(GameData.SaveGame))]
        [HarmonyPostfix]
        static void GameData_SaveGame_TurretModes(bool __result) {
            if (!__result)
                return;
            var ss = GameObject.FindGameObjectWithTag("Player").GetComponent<SpaceShip>();
            if (ss != null) {
                SaveTurrets(ss, ss.shipData.shipModelID);
                canSave = false;
            }

            string filename = GameData.fullSaveGamePath + "." + pluginGuid;
            var bnf = new BinaryFormatter();
            lock (GameData.threadSaveLock)
                try {
                    //No real consequences of this failing, so if it doesn't save, just let it go
                    using (var fs = File.Open(filename, FileMode.Create))
                        bnf.Serialize(fs, turretModesByShipModelId);
                }
                catch (Exception) { }
        }

        [HarmonyPatch(typeof(GameData), nameof(GameData.LoadGame))]
        [HarmonyPrefix]
        static void GameData_LoadGame_TurretModes() {
            if (!File.Exists(GameData.fullSaveGamePath))
                return;
            string filename = GameData.fullSaveGamePath + "." + pluginGuid;
            if (!File.Exists(filename))
                return;
            var bnf = new BinaryFormatter();
            using (var fs = File.Open(filename, FileMode.Open))
                turretModesByShipModelId = (Dictionary<int, Dictionary<int, int>>)bnf.Deserialize(fs);
            canSave = false;
        }

        [HarmonyPatch(typeof(GameData), nameof(GameData.LoadGameOld))]
        [HarmonyPrefix]
        static void GameData_LoadGameOld_TurretModes() => GameData_LoadGame_TurretModes();

        [HarmonyPatch(typeof(SpaceShip), "Awake")]
        [HarmonyPrefix]
        static void SpaceShip_Start_TurretModesDisable() {
            canSave = false;
        }

        [HarmonyPatch(typeof(SpaceShip), "Awake")]
        [HarmonyPostfix]
        static void SpaceShip_Start_TurretModesEnable(SpaceShip __instance) {
            LoadTurrets(__instance, __instance.shipData.shipModelID);
            canSave = true;
        }
    }
}
