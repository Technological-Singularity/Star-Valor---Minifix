using BepInEx;
using HarmonyLib;
using UnityEngine;
using System.Reflection;

namespace Charon.StarValor.Minifix.LessFragileCollectibles {
    [BepInPlugin(pluginGuid, pluginName, pluginVersion)]
    [BepInProcess("Star Valor.exe")]
    public class Plugin : BaseUnityPlugin {
        public const string pluginGuid = "starvalor.charon.less_fragile_collectibles";
        public const string pluginName = "Charon - Less Fragile Collectibles";
        public const string pluginVersion = "0.0.0.0";

        public static BepInEx.Logging.ManualLogSource Log;

        static FieldInfo __CargoSystem_playerFleetMember = typeof(Entity).GetField("playerFleetMember", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        public void Awake() {
            Log = Logger;
            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        //[HarmonyPatch(typeof(Asteroid), nameof(Asteroid.Apply_Damage))]
        //[HarmonyPrefix]
        //static bool Asteroid_Apply_Damage_Wait(float dmg, TCritical crit, DamageType dmgType, Vector3 point, Transform dmgDealer, WeaponImpact impact, Asteroid __instance) {
        //    if (Delegator.Active)
        //        return true;
        //    Delegator.DelegateChecked(__instance, () => __instance.Apply_Damage(dmg, crit, dmgType, point, dmgDealer, impact));
        //    return false;
        //}
        [HarmonyPatch(typeof(Entity), nameof(Entity.ApplyImpactDamage))]
        [HarmonyPrefix]
        static bool Entity_ApplyImpactDamage_DontHurtCollectible(float dmg, TCritical crit, DamageType dmgType, Vector3 point, Transform dmgDealer, WeaponImpact impact, Vector3 lookPosition, Entity __instance) {
            if (dmgDealer == null)
                return true;

            //Plugin.Log.LogMessage(__instance.name + " was dealt " + dmg + " damage by " + (dmgDealer == null ? " NULL" : dmgDealer.name));
            
            var cs = dmgDealer.GetComponent<CargoSystem>();
            if (cs == null)
                return true;

            var collectible = __instance.GetComponent<Collectible>();
            if (collectible == null)
                return true;

            var pc = __instance.GetComponent<PlayerControl>();
            var pfm = (PlayerFleetMember)__CargoSystem_playerFleetMember.GetValue(cs);
            if (pc != null || (pfm != null && pfm.behavior.collectLoot))
                return false;

            return true;
        }
        //[HarmonyPatch(typeof(SpaceShip), nameof(SpaceShip.Apply_Damage))]
        //[HarmonyPrefix]
        //static bool SpaceShip_Apply_Damage_Wait(float dmg, TCritical crit, DamageType dmgType, Vector3 point, Transform dmgDealer, WeaponImpact impact, SpaceShip __instance) {
        //    if (Delegator.Active)
        //        return true;
        //    Delegator.DelegateChecked(__instance, () => __instance.Apply_Damage(dmg, crit, dmgType, point, dmgDealer, impact));
        //    return false;
        //}
    }
}
