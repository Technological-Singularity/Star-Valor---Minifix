using BepInEx;
using HarmonyLib;
using System.Reflection;
using UnityEngine;
using System.Linq;
using System.Collections.Generic;

namespace Charon_SV_Minifix.AggressiveProjectiles {

    [BepInPlugin(pluginGuid, pluginName, pluginVersion)]
    [BepInProcess("Star Valor.exe")]
    public class Plugin : BaseUnityPlugin {
        public const string pluginGuid = "starvalor.charon.minifix.aggressive_projectiles";
        public const string pluginName = "Charon - Minifix - Aggressive Projectiles";
        public const string pluginVersion = "0.0.0.0";

        static MethodInfo weapon_turret_ClearLOF = typeof(WeaponTurret).GetMethod("ClearLOF", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        static bool WeaponTurret_ClearLOF(WeaponTurret instance, bool toTargetOnly) => (bool)weapon_turret_ClearLOF.Invoke(instance, new object[] { toTargetOnly });

        static MethodInfo ai_control_ClearLOF = typeof(AIControl).GetMethod("ClearLOF", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        static bool AIControl_ClearLOF(AIControl instance, bool toTargetOnly) => (bool)ai_control_ClearLOF.Invoke(instance, new object[] { toTargetOnly });
        static MethodInfo weapon_turret_CanFireAgainst = typeof(WeaponTurret).GetMethod("CanFireAgainst", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        static float CanFireAgainst(WeaponTurret instance, Transform targetTrans) => (float)weapon_turret_CanFireAgainst.Invoke(instance, new object[] { targetTrans });

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

            //this only works because the Start() method is called before the instantiation of the next projectile starts (for multi-barrel weapons)
            //this is not a very good way to solve this, but without Transpiler it would be difficult to pass the __instance to the ProjectileHoming object
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
                        control.Clear();
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

        //static System.Collections.Generic.Dictionary<AIControl, GameObject> reticles = new System.Collections.Generic.Dictionary<AIControl, GameObject>();

        [HarmonyPatch(typeof(AIControl), "AimAtTarget")]
        [HarmonyPrefix]
        public static bool AIControl_AimAtTarget(ref bool __result, AIControl __instance, GameObject ___aimTarget, float ___aimErrorX, float ___aimErrorZ, bool ___firingBeamWeapon, Transform ___tf, Rigidbody ___rb, SpaceShip ___ss, ref Quaternion ___targetRotation) {
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

            //var player = GameObject.FindGameObjectWithTag("Player").GetComponent<PlayerControl>();
            //if (!reticles.TryGetValue(__instance, out var reticle)) {
            //    reticle = Instantiate(player.AimObj, ___aimTarget.transform);
            //    reticles[__instance] = reticle;
            //    reticle.SetActive(true);
            //}

            Vector3 prediction;
            var error = new Vector3(___aimErrorX, 0, ___aimErrorZ);

            if (___firingBeamWeapon) {
                prediction = __instance.target.position + error;
            }
            else {
                var components = __instance.transform.GetComponents<TargetPredictor>();
                if (components == null || components.Length == 0) {
                    components = new TargetPredictor[] { null, null };
                    components[0] = __instance.gameObject.AddComponent<TargetPredictor>();
                    components[0].Initialize(__instance.transform);
                    components[0].enabled = true;
                    components[1] = __instance.gameObject.AddComponent<TargetPredictor>();
                }
                if (!components[1].enabled)
                    components[1].enabled = true;
                if (components[1].Target != __instance.target)
                    components[1].Initialize(__instance.target);

                var (pos, vel, _) = components[0].State;
                (_, prediction) = components[1].Predict_OneShot(pos + error, vel, __instance.currWeaponSpeed);
                if (prediction == Vector3.zero) {
                    var d = (__instance.target.position - pos).magnitude / __instance.currWeaponSpeed;
                    prediction = __instance.target.position + d * (__instance.target.GetComponent<Rigidbody>().velocity - vel);
                }
                else {
                    prediction = pos + Vector3.Dot(components[1].State.pos - pos, prediction) * prediction;
                }
            }
            ___aimTarget.transform.position = prediction;
            ___targetRotation = Quaternion.LookRotation(prediction - __instance.transform.position);
            ___ss.Turn(___targetRotation);

            var angleDeltaActual = Quaternion.Angle(__instance.transform.rotation, ___targetRotation);
            __result = angleDeltaActual <= 20f && AIControl_ClearLOF(__instance, angleDeltaActual > 10f);

            return false;
        }

        //[HarmonyPatch(typeof(ScanSystem), "ScanSmallObjects")]
        //[HarmonyPrefix]
        //public static void asdf(ScanSystem __instance, Transform ___owner, float ___scanDistance) {
        //    var player = GameObject.FindGameObjectWithTag("Player");
        //    if (___owner = player.transform) {
        //        Log.LogMessage("Scan distance " + ___scanDistance);
        //    }
        //}

        //[HarmonyPatch(typeof(WeaponTurret), "SearchForATarget")]
        //[HarmonyPostfix]
        //public static void SearchForATarget(WeaponTurret __instance, SpaceShip ___ss) {
        //    if (___ss.transform.GetComponent<PlayerControl>() == null)
        //        return;
        //    if (__instance.notAttackingTime > 0 || (__instance.target != null && __instance.target.GetComponent<Rigidbody>() != null && CanFireAgainst(__instance, __instance.target) >= 0))
        //        return;

        //    //if (__instance.target != null && CanFireAgainst(__instance, __instance.target)
        //    //    return;

        //    if (true) {
        //        //version 2
        //        var control = WeaponTurretExtraControl.GetInstance(___ss);
        //        control.SetTarget(__instance);
        //    }

        //    if (false) {
        //        //Version 1
        //        if (___ss.status.Get(ShipStatusName.Cloaked))
        //            return;

        //        float foundRange = float.MinValue;
        //        var foundWeapons = ___ss.weapons.Where(o => o.turretMounted == __instance.turretIndex && o.wRef.damageType == DamageType.AsteroidBonus);
        //        foreach (var weapon in foundWeapons)
        //            foundRange = weapon.range;
        //        if (foundRange <= 0)
        //            return;

        //        var closestRangeSq = float.MaxValue;
        //        Transform newTarget = null;
        //        var asteroids = Physics.OverlapSphere(__instance.transform.position, foundRange, 1024) //1024 is layer mask for asteroids
        //            .Where(o => o.tag == "Asteroid" && CanFireAgainst(__instance, o.transform) > 0);

        //        foreach (var collider in asteroids) {
        //            var rangeSq = Vector3.SqrMagnitude(__instance.transform.position - collider.transform.position);
        //            if (rangeSq < closestRangeSq) {
        //                closestRangeSq = rangeSq;
        //                newTarget = collider.transform;
        //            }
        //        }
        //        if (newTarget != null)
        //            __instance.SetMiningTarget(newTarget);
        //    }
        //}


        [HarmonyPatch(typeof(WeaponTurret), "AimAtTarget")]
        [HarmonyPrefix]
        public static bool WeaponTurret_AimAtTarget(ref bool __result, WeaponTurret __instance, GameObject ___aimTarget, float ___aimErrorX, float ___aimErrorZ, bool ___firingBeamWeapon, Rigidbody ___rb, SpaceShip ___ss) {
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
                prediction = __instance.target.position + error;
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

                var (pos, vel) = (__instance.transform.position, ___rb.velocity);
                (_, prediction) = component.Predict_OneShot(pos + error, vel, __instance.currWeaponSpeed);
                if (prediction == Vector3.zero) {
                    var d = (__instance.target.position - pos).magnitude / __instance.currWeaponSpeed;
                    prediction = __instance.target.position + d * (__instance.target.GetComponent<Rigidbody>().velocity - vel) + error;
                }
                else {
                    prediction = pos + Vector3.Dot(component.State.pos - pos, prediction) * prediction;
                }
                //}
            }
            ___aimTarget.transform.position = prediction;
            var newRotationTarget = Quaternion.LookRotation(prediction - __instance.transform.position);
            var maxDegreesDelta = Time.deltaTime * 10f * __instance.turnSpeed;

            __instance.transform.rotation = Quaternion.RotateTowards(__instance.transform.rotation, newRotationTarget, maxDegreesDelta);
            var angleDeltaActual = Quaternion.Angle(__instance.transform.rotation, newRotationTarget);
            __result = angleDeltaActual <= 20f && WeaponTurret_ClearLOF(__instance, angleDeltaActual > 10f);

            return false;
        }

        class WeaponSlotExtraData : MonoBehaviour {
            public enum WeaponStatType : int {
                Normal = 0,
                PD = 1,
                Repair = 2,
                Beam = 3,
            }
            public class WeaponStat {
                public WeaponStatType Type { get; }
                public float Range { get; private set; }
                public float Speed { get; private set; }
                public WeaponStat(WeaponStatType type) {
                    Type = type;
                    Clear();
                }
                public void Update(Weapon weapon) {
                    if ((weapon.range > Range || float.IsNaN(Range))) {
                        Range = weapon.range;
                        Speed = weapon.wRef.compType == WeaponCompType.BeamWeaponObject ? float.PositiveInfinity : weapon.wRef.speed;
                    }
                }
                public bool IsValid => !float.IsNaN(Range);
                public void Clear() {
                    Range = float.NaN;
                    Speed = float.NaN;
                }
                public float GetEffectiveRange(Vector3 relativePosition, Vector3 relativeVelocity) {
                    if (!IsValid)
                        return -1f;
                    
                    if (float.IsInfinity(Speed))
                        return Range;

                    var speed = Mathf.Max(0, Speed - Vector3.Dot(relativePosition.normalized, relativeVelocity));
                    return speed * Range / Speed;
                }
            }

            SpaceShip ss;
            List<List<WeaponStat>> weaponStats = new List<List<WeaponStat>>();
            public WeaponStat this[int slotId, WeaponStatType type] => weaponStats[slotId][(int)type];            

            public void Initialize(SpaceShip ss) {
                weaponStats.Clear();
                this.ss = ss;
                for (int i = 0; i < ss.weaponSlots.childCount; ++i) {
                    var stats = new List<WeaponStat>();
                    foreach (WeaponStatType type in System.Enum.GetValues(typeof(WeaponStatType)))
                        stats.Add(new WeaponStat(type));
                    weaponStats.Add(stats);
                }
            }
            public void Refresh() {
                for(int slotIndex = 0; slotIndex < weaponStats.Count; ++slotIndex) {
                    var weapons = ss.weapons.Where(o => o.weaponSlotIndex == slotIndex);
                    foreach (var w in weapons) {
                        if (w.wRef.canHitProjectiles)
                            this[slotIndex, WeaponStatType.PD].Update(w);
                        if (w.wRef.compType == WeaponCompType.BeamWeaponObject && w.wRef.damageType != DamageType.Repair)
                            this[slotIndex, WeaponStatType.Beam].Update(w);
                        else if (w.wRef.damageType == DamageType.Repair)
                            this[slotIndex, WeaponStatType.Repair].Update(w);
                        else
                            this[slotIndex, WeaponStatType.Normal].Update(w);
                    }
                }
            }

        }

        [HarmonyPatch(typeof(SpaceShip), nameof(SpaceShip.UpdateWeaponTurretStats))]
        [HarmonyPrefix]
        public static void SpaceShip_UpdateWeaponTurretStats_AppendModule(SpaceShip __instance) {
            var component = __instance.GetComponent<WeaponSlotExtraData>();
            if (component == null) {
                component = __instance.gameObject.AddComponent<WeaponSlotExtraData>();
                component.Initialize(__instance);
            }
            component.Refresh();
        }

        [HarmonyPatch(typeof(WeaponTurret), "CanFireAgainst")]
        [HarmonyPrefix]
        public static void WeaponTurret_CanFireAgainst_FixRange(WeaponTurret __instance, Transform targetTrans, ref float ___desiredDistance, ref float __state, SpaceShip ___ss) {
            __state = ___desiredDistance;
            if (targetTrans.tag == "Projectile") {
                var component = __instance.GetComponent<WeaponSlotExtraData>();
                if (component == null) {
                    component = __instance.gameObject.AddComponent<WeaponSlotExtraData>();
                    component.Initialize(___ss);
                    component.Refresh();
                }

                var targetRB = targetTrans.GetComponent<Rigidbody>();
                if (targetRB == null)
                    targetRB = ___ss.rb;
                var relPosition = targetTrans.position - __instance.transform.position;
                var relVelocity = targetRB.velocity - ___ss.rb.velocity;
                var newDistance = component[__instance.turretIndex, WeaponSlotExtraData.WeaponStatType.PD].GetEffectiveRange(relPosition, relVelocity);

                ___desiredDistance = newDistance;
            }
            //add more later if appropriate
        }

        [HarmonyPatch(typeof(WeaponTurret), "CanFireAgainst")]
        [HarmonyPostfix]
        public static void WeaponTurret_CanFireAgainst_FixRangeCleanup(Transform targetTrans, ref float ___desiredDistance, ref float __state) {
            ___desiredDistance = __state;
        }
    }
}

//The following functions should be updated to have altered range based on the velocity of the target:
//WeaponTurret.CanFireAt
//AIControl.FireAllWeapons

