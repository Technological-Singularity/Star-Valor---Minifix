using BepInEx;
using HarmonyLib;
using UnityEngine;
using System;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;

namespace Charon_SV_Minifix.MinerGunners {
    [BepInPlugin(pluginGuid, pluginName, pluginVersion)]
    [BepInProcess("Star Valor.exe")]
    public class Plugin : BaseUnityPlugin {
        public const string pluginGuid = "starvalor.charon.minifix.minergunners";
        public const string pluginName = "Charon - Minifix - Miner Gunners";
        public const string pluginVersion = "0.0.0.1";

        public static BepInEx.Logging.ManualLogSource Log;
        static MethodInfo weapon_turret_CanFireAgainst;
        static float CanFireAgainst(WeaponTurret instance, Transform targetTrans) => (float)weapon_turret_CanFireAgainst.Invoke(instance, new object[] { targetTrans });

        static Plugin() {
            weapon_turret_CanFireAgainst = typeof(WeaponTurret).GetMethod("CanFireAgainst", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        }

        public void Awake() {
            Log = Logger;
            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        [HarmonyPatch(typeof(ShipInfo), nameof(ShipInfo.LoadData))]
        [HarmonyPrefix]
        public static void LoadData_Push(SpaceShip ___ss, Transform ___weaponTrans, int ___gearMode, bool loadSlots) {
            if (___gearMode != 0 && ___gearMode != 2)
                return;

            var control = WeaponTurretExtraControl.GetInstance(___ss);

            if (___gearMode == 0) {
                control.Pretext_Save();
                for (int i = 0; i < ___ss.shipData.weapons.Count; ++i) {
                    var comp = ___weaponTrans.GetChild(i).GetComponent<Weapon>();
                    if (comp != null && comp.manned)
                        gunnerControlQueue.Enqueue((control, comp.weaponSlotIndex));
                }
            }
            else if (___gearMode == 2) {
                control.Pretext_Save();
                var assignedMembers = ___ss.crew.GetAssignedMembers(CrewPosition.Gunner).Where(o => o.control == 0);
                foreach (AssignedCrewMember gunner in assignedMembers)
                    gunnerControlQueue.Enqueue((control, gunner.slot));
            }
        }

        static Queue<(WeaponTurretExtraControl control, int turretIndex)> gunnerControlQueue = new Queue<(WeaponTurretExtraControl control, int turretIndex)>();
        [HarmonyPatch(typeof(Lang), nameof(Lang.Get), new Type[] { typeof(int), typeof(int) })]
        [HarmonyPostfix]
        public static void LangGet_Pop(ref string __result, int sectionIndex, int code) {
            if (sectionIndex == 23 && (code == 75 || code == 85) && gunnerControlQueue.Count > 0) {//see ShipInfo.LoadData; "if(assignedCrewMemember.control == 0)
                (var control, var index) = gunnerControlQueue.Dequeue();
                __result = control.GetControlString(index);
                if (gunnerControlQueue.Count == 0)
                    control.Postload_Restore();
            }
        }

        [HarmonyPatch(typeof(ShipInfo), nameof(ShipInfo.LoadData))]
        [HarmonyPostfix]
        public static void LoadData_Cleanup(SpaceShip ___ss, bool loadSlots) {
            gunnerControlQueue.Clear();
        }

        [HarmonyPatch(typeof(ShipInfo), nameof(ShipInfo.SetGunnerControl))]
        [HarmonyPrefix]
        public static void SetGunnerControl_Preload(ShipInfo __instance, SpaceShip ___ss, int ___selSlotIndex, int ___selItemType, int ___selItemIndex, Transform ___equipGO, bool all) {
            if (___selSlotIndex < 0)
                return;
            AssignedCrewMember gunner = null;
            if (___selItemType == 1) {
                var weapon = ___ss.shipData.weapons[___selItemIndex];
                gunner = ___ss.crew.GetGunner((int)weapon.slotIndex);
            }
            else if (___selItemType == 5) {
                gunner = ___ss.shipData.members[___selItemIndex];
            }
            if (gunner == null)
                return;

            var control = WeaponTurretExtraControl.GetInstance(___ss);

            if (all) {
                bool allSame = true;
                foreach (var crew in ___ss.crew.GetAssignedMembers(CrewPosition.Gunner)) {
                    if (!control.IsEqual(gunner, crew))
                        allSame = false;
                    control.Preload_CopyControlCode(crew, gunner);
                }
                if (allSame) {
                    control.Preload_CycleControlCode(gunner);
                    foreach (var crew in ___ss.crew.GetAssignedMembers(CrewPosition.Gunner))
                        control.Preload_CopyControlCode(crew, gunner);
                }
            }
            else {
                control.Preload_CycleControlCode(gunner);
            }

            //selItemType => 1 for weapon or 5 for crew
            //selItemIndex => crew index/ss.members or weapon index/ss.weapons
            //selSlotIndex => ??
            //var button = ___equipGO.Find("BtnSetControl");
            //button.gameObject.SetActive(true); //true if you can disable control?           
        }
        [HarmonyPatch(typeof(ShipInfo), nameof(ShipInfo.SetGunnerControl))]
        [HarmonyPostfix]
        public static void SetGunnerControl_Postload(ShipInfo __instance, SpaceShip ___ss, int ___selSlotIndex, int ___selItemType, int ___selItemIndex, Transform ___equipGO, bool all) {
            if (___selSlotIndex < 0)
                return;
            AssignedCrewMember gunner = null;
            if (___selItemType == 1) {
                var weapon = ___ss.shipData.weapons[___selItemIndex];
                gunner = ___ss.crew.GetGunner((int)weapon.slotIndex);
            }
            else if (___selItemType == 5) {
                gunner = ___ss.shipData.members[___selItemIndex];
            }
            if (gunner == null)
                return;

            var control = WeaponTurretExtraControl.GetInstance(___ss);
            control.Postload_Restore();

        }

        [HarmonyPatch(typeof(WeaponTurret), "SearchForATarget")]
        [HarmonyPostfix]
        public static void SearchForATarget(WeaponTurret __instance, SpaceShip ___ss) {
            if (___ss.transform.GetComponent<PlayerControl>() == null)
                return;
            if (__instance.notAttackingTime > 0 || (__instance.target != null && CanFireAgainst(__instance, __instance.target) >= 0))
                return;

            //if (__instance.target != null && CanFireAgainst(__instance, __instance.target)
            //    return;

            if (true) {
                //version 2
                var control = WeaponTurretExtraControl.GetInstance(___ss);
                control.SetTarget(__instance);
            }

            if (false) {
                //Version 1
                if (___ss.status.Get(ShipStatusName.Cloaked))
                    return;

                float foundRange = float.MinValue;
                var foundWeapons = ___ss.weapons.Where(o => o.turretMounted == __instance.turretIndex && o.wRef.damageType == DamageType.AsteroidBonus);
                foreach (var weapon in foundWeapons)
                    foundRange = weapon.range;
                if (foundRange <= 0)
                    return;

                var closestRangeSq = float.MaxValue;
                Transform newTarget = null;
                var asteroids = Physics.OverlapSphere(__instance.transform.position, foundRange, 1024) //1024 is layer mask for asteroids
                    .Where(o => o.tag == "Asteroid" && CanFireAgainst(__instance, o.transform) > 0);

                foreach (var collider in asteroids) {
                    var rangeSq = Vector3.SqrMagnitude(__instance.transform.position - collider.transform.position);
                    if (rangeSq < closestRangeSq) {
                        closestRangeSq = rangeSq;
                        newTarget = collider.transform;
                    }
                }
                if (newTarget != null)
                    __instance.SetMiningTarget(newTarget);
            }
        }
    }

    //ShipInfo.SetGunnerControl
}
