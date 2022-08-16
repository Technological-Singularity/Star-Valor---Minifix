using BepInEx;
using HarmonyLib;
using UnityEngine;
using System.Reflection;
using System.Linq;

namespace Charon_SV_Minifix.MinerGunners {
    [BepInPlugin(pluginGuid, pluginName, pluginVersion)]
    [BepInProcess("Star Valor.exe")]
    public class Plugin : BaseUnityPlugin {
        public const string pluginGuid = "starvalor.charon.minifix.minergunners";
        public const string pluginName = "Charon - Minifix - Miner Gunners";
        public const string pluginVersion = "0.0.0.0";

        static BepInEx.Logging.ManualLogSource Log;
        static MethodInfo weapon_turret_CanFireAgainst;
        static float CanFireAgainst(WeaponTurret instance, Transform targetTrans) => (float)weapon_turret_CanFireAgainst.Invoke(instance, new object[] { targetTrans });

        static Plugin() {
            weapon_turret_CanFireAgainst = typeof(WeaponTurret).GetMethod("CanFireAgainst", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        }

        public void Awake() {
            Log = Logger;
            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        [HarmonyPatch(typeof(WeaponTurret), "SearchForATarget")]
        [HarmonyPostfix]
        public static void SearchForATarget(WeaponTurret __instance, SpaceShip ___ss) {
            if (___ss.transform.GetComponent<PlayerControl>() == null)
                return;

            if (__instance.target != null)
                return;
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

    //ShipInfo.SetGunnerControl
}
