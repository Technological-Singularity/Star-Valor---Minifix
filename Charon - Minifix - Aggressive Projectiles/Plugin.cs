using BepInEx;
using HarmonyLib;
using System.Reflection;
using UnityEngine;
using System.Linq;

namespace Charon_SV_Minifix.AggressiveProjectiles {

    [BepInPlugin(pluginGuid, pluginName, pluginVersion)]
    [BepInProcess("Star Valor.exe")]
    public class Plugin : BaseUnityPlugin {
        public const string pluginGuid = "starvalor.charon.minifix.aggressive_projectiles";
        public const string pluginName = "Charon - Minifix - Aggressive Projectiles";
        public const string pluginVersion = "0.0.0.0";

        static MethodInfo weapon_turret_ClearLOF = typeof(WeaponTurret).GetMethod("ClearLOF", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        static bool ClearLOF(WeaponTurret instance, bool toTargetOnly) => (bool)weapon_turret_ClearLOF.Invoke(instance, new object[] { toTargetOnly });

        public static BepInEx.Logging.ManualLogSource Log;

        public void Awake() {
            Log = Logger;
            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        static FieldInfo weaponProjControl = typeof(Weapon).GetField("projControl", BindingFlags.NonPublic | BindingFlags.Instance);

        [HarmonyPatch(typeof(ProjectileControl), "Start")]
        [HarmonyPostfix]
        public static void Projectile_Start(ProjectileControl __instance, Transform ___owner, Rigidbody ___rb, float ___speed, float ___turnSpeed) {
            if (!__instance.homing)
                return;           

            var control = __instance.transform.gameObject.AddComponent<ProjectileHoming>();
            var ss = ___owner.GetComponent<SpaceShip>();
            var weapon = ss.weapons.Where(o => o != null && (ProjectileControl)weaponProjControl.GetValue(o) == __instance).FirstOrDefault();

            control.Initialize(___owner, ___rb, ss, weapon, __instance.target, ___speed, ___turnSpeed * 15); //15 is from original code



            control.enabled = true;
        }

        [HarmonyPatch(typeof(PlayerControl), "ShowAimObject")]
        [HarmonyPrefix]
        public static bool PlayerControl_ShowAimObject(PlayerControl __instance, SpaceShip ___ss) {
            var control = __instance.GetComponent<AimObjectControl>();
            if (__instance.target != null && ___ss.stats.hasAimObj) {
                if (control == null)
                    control = __instance.gameObject.AddComponent<AimObjectControl>();
                if (!control.enabled || ___ss != control.SpaceShip || control.Target != __instance.target) {
                    if (___ss != control.SpaceShip)
                        control.ClearStates();
                    control.Initialize(__instance, ___ss, __instance.target);
                    control.enabled = true;
                }
            }
            else if (control != null) {
                control.enabled = false;
            }
            return false;
        }

        [HarmonyPatch(typeof(ProjectileControl), "FixedUpdate")]
        [HarmonyPrefix]
        public static bool Projectile_FixedUpdate_Tracking(ProjectileControl __instance) {
            if (__instance.homing)
                return false;
            return true;
        }

        [HarmonyPatch(typeof(WeaponTurret), "AimAtTarget")]
        [HarmonyPrefix]
        public static bool WeaponTurret_AimAtTarget(ref bool __result, WeaponTurret __instance, GameObject ___aimTarget, float ___aimErrorX, float ___aimErrorZ, bool ___firingBeamWeapon, Transform ___tf, Rigidbody ___rb, SpaceShip ___ss) {
            if (__instance.target == null) {
                __result = false;
                var component = __instance.transform.GetComponent<TargetPredictor>();
                if (component != null && component.Target != null) {
                    component.Initialize(null);
                    component.enabled = false;
                }
                return false;
            }
            if (__instance.target != null && __instance.target.position == __instance.transform.position) {
                __result = true;
                return false;
            }
            
            Vector3 prediction;
            var error = new Vector3(___aimErrorX, 0, ___aimErrorZ);

            if (___firingBeamWeapon) {
                prediction = __instance.target.position - ___tf.position + error;
            }
            else {
                var component = __instance.transform.GetComponent<TargetPredictor>();
                if (component == null)
                    component = __instance.gameObject.AddComponent<TargetPredictor>();
                if (!component.enabled)
                    component.enabled = true;
                if (component.Target != __instance.target)
                    component.Initialize(__instance.target);

                //int count = 0;
                //float avgSpeed = 0;
                //foreach(var w in ___ss.weapons.Where(o => o.wRef.compType == WeaponCompType.WeaponObject && o.turretMounted == __instance.turretIndex)) {
                //    ++count;
                //    avgSpeed += w.projSpeed / w.projectileRef.GetComponent<Rigidbody>().mass;
                //}
                //if (count == 0) {
                //    prediction = (__instance.target.position - ___tf.position);
                //}
                //else {
                //avgSpeed /= count;

                (_, prediction) = component.Predict_OneShot(___tf.position + error, ___rb.velocity, __instance.currWeaponSpeed);
                if (prediction == Vector3.zero)
                    prediction = (__instance.target.position - ___tf.position);
                
                //}
            }
            ___aimTarget.transform.position = prediction;
            var newRotationTarget = Quaternion.LookRotation(prediction);
            var maxDegreesDelta = Time.deltaTime * 10f * __instance.turnSpeed;

            __instance.transform.rotation = Quaternion.RotateTowards(__instance.transform.rotation, newRotationTarget, maxDegreesDelta);
            var angleDeltaActual = Quaternion.Angle(__instance.transform.rotation, newRotationTarget);
            __result = angleDeltaActual <= 20f && ClearLOF(__instance, angleDeltaActual > 10f);

            return false;
        }
    }
}
