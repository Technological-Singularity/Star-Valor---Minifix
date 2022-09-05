using BepInEx;
using HarmonyLib;
using UnityEngine;
using System.Reflection;

namespace Charon.StarValor.Minifix.ReliableExplosions {
    [BepInPlugin(pluginGuid, pluginName, pluginVersion)]
    [BepInProcess("Star Valor.exe")]
    public class Plugin : BaseUnityPlugin {
        public const string pluginGuid = "starvalor.charon.reliable_explosions";
        public const string pluginName = "Charon - Reliable Explosions";
        public const string pluginVersion = "0.0.0.0";

        public void Awake() {
            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        static MethodInfo __Explosion_OnTriggerEnter = typeof(Explosion).GetMethod("OnTriggerEnter", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        [HarmonyPatch(typeof(Explosion), nameof(Explosion.Setup))]
        [HarmonyPostfix]
        static void Explosion_Setup_SpeedFix(float newTimeToDie, Explosion __instance, float ___aoe, float ___damage, DamageType ___damageType, TCritical ___critical, bool ___canHitProjectiles, Transform ___owner) {
            __instance.gameObject.AddComponent<MineOnTriggerExit>();

            const int mask = (1 << 8) | (1 << 9) | (1 << 10) | (1 << 13) | (1 << 14) | (1 << 16);
            foreach (var collider in Physics.OverlapSphere(__instance.transform.position, ___aoe / 6, mask, QueryTriggerInteraction.Ignore))
                __Explosion_OnTriggerEnter.Invoke(__instance, new object[] { collider });
        }

        class MineOnTriggerExit : MonoBehaviour {
            void OnTriggerExit(Collider collider) {
                __Explosion_OnTriggerEnter.Invoke(transform.GetComponent<Explosion>(), new object[] { collider });
            }
        }
    }
}
