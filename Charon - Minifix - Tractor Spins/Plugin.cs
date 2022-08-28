using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace Charon.StarValor.Minifix.TractorSpins {
    [BepInPlugin(pluginGuid, pluginName, pluginVersion)]
    [BepInProcess("Star Valor.exe")]
    public class Plugin : BaseUnityPlugin {
        public const string pluginGuid = "starvalor.charon.minifix.tractorspins";
        public const string pluginName = "Charon - Minifix - Tractor Spins";
        public const string pluginVersion = "0.0.0.0";
        static BepInEx.Logging.ManualLogSource Log;

        public void Awake() {
            Log = Logger;
            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        [HarmonyPatch(typeof(BuffTowing), "FixedUpdate")]
        [HarmonyPrefix]
        public static bool BuffTowing_FixedUpdate(BuffTowing __instance, Transform ___ownerTrans, Entity ___targetEntity, float ___desiredDistance, Rigidbody ___targetRb) {
            if (!__instance.active || ___ownerTrans == null)
                return false;

            var collider = ___targetRb.GetComponent<Collider>();
            if (collider == null)
                return false;
            var closest = collider.ClosestPoint(___ownerTrans.position);

            var normalized = (closest - ___ownerTrans.position).normalized;
            var dist = Vector3.Distance(___ownerTrans.position, ___targetEntity.transform.position);
            float d = Mathf.Clamp(___desiredDistance - dist, -__instance.towingForce, __instance.towingForce);
            ___targetRb.AddForceAtPosition(normalized * d, closest, ForceMode.Force);
            return false;
        }
    }
}
