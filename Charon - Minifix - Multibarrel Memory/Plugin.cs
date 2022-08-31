using BepInEx;
using HarmonyLib;
using UnityEngine;
using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Collections.Generic;

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
            var dict = new Dictionary<int, int>();
            turretModesByShipModelId[modelId] = dict;

            for (int i = 0; i < ss.weaponSlots.childCount; ++i) {
                var turret = ss.weaponSlots.GetChild(i).GetComponent<WeaponTurret>();
                if (turret == null)
                    continue;
                dict[i] = turret.alternateFire ? 1 : 0;
            }
        }
        static void SaveTurretSingle(int modelId, WeaponTurret turret) {
            if (!turretModesByShipModelId.TryGetValue(modelId, out var dict)) {
                dict = new Dictionary<int, int>();
                turretModesByShipModelId[modelId] = dict;
            }
            dict[turret.turretIndex] = turret.alternateFire ? 1 : 0;
        }
        static void LoadTurrets(SpaceShip ss, int modelId) {
            if (!turretModesByShipModelId.TryGetValue(modelId, out var dict))
                return;

            for (int i = 0; i < ss.weaponSlots.childCount; ++i) {
                var turret = ss.weaponSlots.GetChild(i).GetComponent<WeaponTurret>();
                if (turret == null)
                    continue;
                turret.alternateFire = dict[i] != 0;
            }
            foreach (var o in ss.weaponTrans.GetComponents<Weapon>())
                o.Load(true);
        }

        [HarmonyPatch(typeof(WeaponPlaceSlot), nameof(WeaponPlaceSlot.MouseDown))]
        [HarmonyPostfix]
        static void WeaponPlaceSlot_MouseDown_SaveAltFire(WeaponTurret ___wt) {
            var ss = ___wt.transform.parent.GetComponentInParent<SpaceShip>();
            SaveTurretSingle(ss.shipData.shipModelID, ___wt);
        }


        [HarmonyPatch(typeof(SpaceShip), "CalculateShipStats")]
        [HarmonyPrefix]
        static void SpaceShip_CalculateShipStats_TurretModesSave(ShipModel shipModel, SpaceShip __instance) {
            if (__instance == null || !__instance.CompareTag("Player") || !canSave)
                return;
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
