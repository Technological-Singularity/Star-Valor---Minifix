using BepInEx;
using HarmonyLib;
using Rewired;
using System.Linq;
using UnityEngine;

namespace Charon.StarValor.Minifix.WiderTargeting {
    [BepInPlugin(pluginGuid, pluginName, pluginVersion)]
    [BepInProcess("Star Valor.exe")]
    public class Plugin : BaseUnityPlugin {
        public const string pluginGuid = "starvalor.charon.minifix.widertargeting";
        public const string pluginName = "Charon - Minifix - Wider Ship Targeting";
        public const string pluginVersion = "0.0.0.1";
        static BepInEx.Logging.ManualLogSource Log;

        public void Awake() {
            Log = Logger;
            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        const float scanRadius = 12;
        const float requiredOverTime = 0.25f;
        static float playerScanPeriod = 0.05f;
        static float playerScanLast = 0;
        static Entity entityLast;
        static float entityTimeLast;

        //static FieldInfo entity_mouseOverTime = typeof(Entity).GetField("mouseOverTime", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        //static void EntitySetMouseoverTime(Entity entity, float value) => entity_mouseOverTime.SetValue(entity, value);


        [HarmonyPatch(typeof(PlayerControl), "Update")]
        [HarmonyPostfix]
        public static void PlayerControl_NearbyMouseOver(PlayerControl __instance, Vector3 ___mousePosition, SpaceShip ___ss, Player ___player) {
            var tryingAsteroid = ___player.GetButton("Shift") && ___player.GetButtonDown("Target Any");
            var mouseDown = Input.GetMouseButtonDown(0);
            if (tryingAsteroid && (__instance.target == null || !__instance.target.CompareTag("Asteroid"))) {
                const int layerMask = 1024;
                var toTarget = Physics.OverlapSphere(___mousePosition, 160 + ___ss.shipClass * 40, layerMask)
                    .Where(o => o.CompareTag("Asteroid"))
                    .Select(o => (o.transform, Vector3.SqrMagnitude(o.transform.position - ___mousePosition)))
                    .Aggregate(
                        ((Transform)null, (float)float.MaxValue),
                        (closest, candidate) => candidate.Item2 < closest.Item2 ? candidate : closest)
                    .Item1;
                if (toTarget != null)
                    __instance.SetTarget(toTarget);
            }

            //if (__instance.target != null && !mouseDown)
            //    return;

            if (playerScanLast < playerScanPeriod) {
                playerScanLast += Time.deltaTime;
                return;
            }
            playerScanLast = 0;

            //if ((!entityCandidate.CompareTag("NPC") && !entityCandidate.CompareTag("Asteroid") && !entityCandidate.CompareTag("Station") && !entityCandidate.CompareTag("Object")))
            //    continue;
            //if (entityCandidate.CompareTag("Station") && !___ss.ffSys.TargetIsEnemy(entityCandidate.ffSys))
            //    continue;

            var closestEntity = Physics.OverlapSphere(___mousePosition, scanRadius, Physics.AllLayers)
                .Select(o => o.CompareTag("Collider") ? o.GetComponent<ColliderControl>().ownerEntity : o.GetComponent<Entity>())
                .Where(o => o != null && o.CompareTag("NPC"))
                .Select(o => (o, Vector3.SqrMagnitude(o.transform.position - ___mousePosition)))
                .Aggregate(
                    ((Entity)null, (float)float.MaxValue),
                    (closest, candidate) => candidate.Item2 < closest.Item2 ? candidate : closest)
                .Item1;

            if (closestEntity == null) {
                entityLast = null;
                entityTimeLast = 0;
            }
            if (closestEntity != entityLast) {
                entityLast = closestEntity;
                entityTimeLast = mouseDown ? requiredOverTime : 0;
            }
            else {
                entityTimeLast += playerScanPeriod;
            }

            if (entityLast != null && entityTimeLast >= requiredOverTime) {
                entityTimeLast = requiredOverTime;
                __instance.SetTarget(entityLast.transform);
            }
        }
    }
}
