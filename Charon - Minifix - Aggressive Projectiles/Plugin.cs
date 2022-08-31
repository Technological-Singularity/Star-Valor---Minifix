using BepInEx;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

namespace Charon.StarValor.Minifix.AggressiveProjectiles {

    [BepInPlugin(pluginGuid, pluginName, pluginVersion)]
    [BepInProcess("Star Valor.exe")]
    public partial class Plugin : BaseUnityPlugin {
        public const string pluginGuid = "starvalor.charon.minifix.aggressive_projectiles";
        public const string pluginName = "Charon - Minifix - Aggressive Projectiles";
        public const string pluginVersion = "0.0.0.0";

        static MethodInfo weapon_turret_ClearLOF = typeof(WeaponTurret).GetMethod("ClearLOF", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        static bool WeaponTurret_ClearLOF(WeaponTurret instance, bool toTargetOnly) => (bool)weapon_turret_ClearLOF.Invoke(instance, new object[] { toTargetOnly });

        static MethodInfo ai_control_ClearLOF = typeof(AIControl).GetMethod("ClearLOF", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        static bool AIControl_ClearLOF(AIControl instance, bool toTargetOnly) => (bool)ai_control_ClearLOF.Invoke(instance, new object[] { toTargetOnly });
        static MethodInfo weapon_turret_CanFireAgainst = typeof(WeaponTurret).GetMethod("CanFireAgainst", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        static float WeaponTurret_CanFireAgainst(WeaponTurret instance, Transform targetTrans) => (float)weapon_turret_CanFireAgainst.Invoke(instance, new object[] { targetTrans });

        public static BepInEx.Logging.ManualLogSource Log;

        public void Awake() {
            Log = Logger;
            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        [HarmonyPatch(typeof(PlayerControl), "ShowAimObject")]
        [HarmonyPrefix]
        public static bool PlayerControl_ShowAimObject(PlayerControl __instance, SpaceShip ___ss) {
            var control = __instance.GetComponent<AimObjectControl>();
            if (/*__instance.target != null &&*/ ___ss.stats.hasAimObj) {
                if (control == null)
                    control = __instance.gameObject.AddComponent<AimObjectControl>();
                if (!control.enabled || ___ss != control.SpaceShip /*|| control.Target != __instance.target*/) {
                    if (___ss != control.SpaceShip)
                        control.Clear();
                    control.Initialize(__instance, ___ss/*, __instance.target*/);
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
        public static bool AIControl_AimAtTarget(ref bool __result, AIControl __instance, GameObject ___aimTarget, float ___aimErrorX, float ___aimErrorZ, bool ___firingBeamWeapon, Rigidbody ___rb, SpaceShip ___ss, ref Quaternion ___targetRotation) {
            if (__instance.target == null) {
                __result = false;
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
                var targetPredictor = __instance.target.GetComponent<TargetPredictor>();
                if (targetPredictor == null) {
                    targetPredictor = __instance.target.gameObject.AddComponent<TargetPredictor>();
                    targetPredictor.enabled = true;
                }

                var thisPredictor = __instance.GetComponent<TargetPredictor>();
                if (thisPredictor == null) {
                    thisPredictor = __instance.gameObject.AddComponent<TargetPredictor>();
                    thisPredictor.enabled = true;
                }

                var (targetPos, targetVel, _) = targetPredictor.State;
                prediction = thisPredictor.Predict_OneShot(targetPos + error, targetVel, __instance.currWeaponSpeed);
                if (prediction == Vector3.zero) {
                    var d = (thisPredictor.State.pos - targetPos).magnitude / __instance.currWeaponSpeed;
                    prediction = __instance.target.position + d * (__instance.target.GetComponent<Rigidbody>().velocity - targetVel);
                }
                else {
                    prediction = targetPos + Vector3.Dot(targetPos - thisPredictor.State.pos, prediction) * prediction;
                }
            }

            var relP = ___ss.transform.position - prediction;            
            var hits = Physics.RaycastAll(___ss.transform.position, relP, 2 * relP.magnitude, 1 << __instance.target.gameObject.layer, QueryTriggerInteraction.Ignore);
            foreach(var hit in hits) {
                if (__instance.target == hit.transform || (hit.transform.CompareTag("Collider") && __instance.target == hit.transform.GetComponent<ColliderControl>().ownerEntity.transform)) {
                    prediction = hit.point;
                    break;
                }                
            }

            ___aimTarget.transform.position = prediction;
            ___targetRotation = Quaternion.LookRotation(prediction - __instance.transform.position);
            ___ss.Turn(___targetRotation);

            var angleDeltaActual = Quaternion.Angle(__instance.transform.rotation, ___targetRotation);
            __result = angleDeltaActual <= 20f && AIControl_ClearLOF(__instance, angleDeltaActual > 10f);

            return false;
        }

        public static List<(Transform transform, int layer)> SetLayers(Transform transform, int layerMask, int newLayer) {
            var list = new List<(Transform, int)>();
            void _setLayers(Transform curTransform) {
                var thisLayer = curTransform.gameObject.layer;
                if ((layerMask >> thisLayer & 1) == 1) {
                    list.Add((curTransform, thisLayer));
                    curTransform.gameObject.layer = newLayer;
                }
                foreach (Transform child in curTransform)
                    _setLayers(child);
            }
            _setLayers(transform);
            return list;
        }
        public static void ResetLayers(List<(Transform, int)> list) {
            foreach (var (transform, layer) in list)
                transform.gameObject.layer = layer;
        }

        [HarmonyPatch(typeof(WeaponTurret), "FindTarget")]
        [HarmonyPrefix]
        public static void FindTarget(Transform ___parentShipTrans, Transform ___tf, ref List<ScanObject> objs, bool smallObject) {
            //This fix was designed to fix the Taurus laser targeting - it needs to be fixed so it doesn't stop e.g. firing at an asteroid behind another asteroid

            ////filter the initial list so that only objects that are currently in LOF can actualy be targeted
            //const int layerMask = (1 << 8) | (1 << 9) | (1 << 13) | (1 << 14) | (1 << 16); //these are the objects that can occlude a shot

            //var oldLayers = SetLayers(___parentShipTrans, layerMask, 2); //ignore raycast layer

            //var newList = new List<ScanObject>();
            //foreach (var o in objs) {
            //    if (o == null || o.trans == null)
            //        continue;
            //    if (o.trans.CompareTag("Projectile")) {
            //        newList.Add(o);
            //        continue;
            //    }
            //    var relP = o.trans.position - ___tf.position;
            //    var wasHit = Physics.Raycast(___tf.position, relP.normalized, out var hitInfo, 2 * relP.magnitude, layerMask, QueryTriggerInteraction.Ignore);
            //    if (wasHit && hitInfo.transform == o.trans)
            //        newList.Add(o);
            //}

            //ResetLayers(oldLayers);

            //objs = newList;
        }


        [HarmonyPatch(typeof(WeaponTurret), "AimAtTarget")]
        [HarmonyPrefix]
        public static bool WeaponTurret_AimAtTarget(ref bool __result, WeaponTurret __instance, GameObject ___aimTarget, float ___aimErrorX, float ___aimErrorZ, bool ___firingBeamWeapon, Rigidbody ___rb, SpaceShip ___ss) {
            if (__instance.target == null) {
                __result = false;
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
                var targetPredictor = __instance.target.GetComponent<TargetPredictor>();
                if (targetPredictor == null) {
                    targetPredictor = __instance.target.gameObject.AddComponent<TargetPredictor>();
                    targetPredictor.enabled = true;
                }

                var thisPredictor = __instance.transform.parent.parent.GetComponent<TargetPredictor>();
                if (thisPredictor == null) {
                    thisPredictor = __instance.transform.parent.parent.gameObject.AddComponent<TargetPredictor>();
                    thisPredictor.enabled = true;
                }

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

                var (_, parentVel, _) = thisPredictor.State;
                var pos = __instance.transform.position;
                prediction = targetPredictor.Predict_OneShot(pos + error, parentVel, __instance.currWeaponSpeed);
                if (prediction == Vector3.zero) {
                    var d = (targetPredictor.State.pos - pos).magnitude / __instance.currWeaponSpeed;
                    prediction = __instance.target.position + d * (targetPredictor.State.vel - parentVel)  + error;
                }
                else {
                    prediction = pos + Vector3.Dot(targetPredictor.State.pos - pos, prediction) * prediction;
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
            if (true/*targetTrans.CompareTag("Projectile")*/) {
                var component = ___ss.GetComponent<WeaponSlotExtraData>();
                if (component == null) {
                    component = ___ss.gameObject.AddComponent<WeaponSlotExtraData>();
                    component.Initialize(___ss);
                    component.Refresh();
                }
                var newDistance = component[__instance.turretIndex, WeaponSlotExtraData.WeaponStatType.PD].GetEffectiveRange(targetTrans, ___ss.rb.position, ___ss.rb.velocity);
                ___desiredDistance = newDistance;
            }
        }

        [HarmonyPatch(typeof(WeaponTurret), "CanFireAgainst")]
        [HarmonyPostfix]
        public static void WeaponTurret_CanFireAgainst_FixRangeCleanup(Transform targetTrans, ref float ___desiredDistance, ref float __state) {
            ___desiredDistance = __state;
        }

        static void Weapon_Fire_Projectile(Transform target, bool buttonDown,
            Weapon instance, SpaceShip ss, Transform mainParent, bool isDrone, bool loaded,
            TWeapon wRef, float chargeTime, Transform weaponSlot,
            MuzzleFlash muzzleFlash, MuzzleFlash[] extraMuzzleFlash, Color muzzleFlashColor, float flashSize,
            float delayTime, TCritical critical,
            WeaponStatsModifier mods, GameObject projectileRef, Transform gunTip,
            Drone drone, Rigidbody rbShip, float damage,
            AudioSource audioS, float audioMod, AudioClip audioToPlay,
            int explodeBoostChance, float explodeBoost,
            Vector3 size, float sizeMod,
            WeaponImpact impact, int projSpeed, int range, float chargedDamageBoost,
            Transform[] extraBarrels, float delayPortion, bool alternateExtraFire,
            ref sbyte burstCount, ref float chargedFireCount, ref float currCoolDown
        ) {
            if (!isDrone && ss.energyMmt.valueMod(0) == 0f)
                return;
            
            if (!loaded || weaponSlot == null)
                instance.Load(true);
            
            if (delayTime > 0f && currCoolDown <= 0f && buttonDown)
                currCoolDown += wRef.rateOfFire * delayTime;
            
            if (chargeTime > 0f && !ChargedWeaponPass(instance))
                return;

            if (currCoolDown <= 0f && PayCost(instance)) {
                var dmgMod = 1f;
                var tempCritical = critical;
                if (!isDrone) {
                    dmgMod = ss.DamageMod((int)wRef.type) * mods.DamageMod((int)wRef.type);
                    if (chargedFireCount > 0f) {
                        dmgMod *= chargedDamageBoost;
                    }
                    tempCritical.chance += mods.criticalChanceBonus;
                    tempCritical.dmgBonus += mods.criticalDamageBonus;
                    if (ss.fluxChargeSys.charges > 0)
                        tempCritical = ss.stats.ApplyFluxCriticalBonuses(tempCritical);
                }

                void FireBarrel(MuzzleFlash _muzzleFlash, Transform _barrelTip) {
                    if (muzzleFlashColor != Color.black)
                        _muzzleFlash.FlashFire(muzzleFlashColor, flashSize, true);

                    GameObject gameObject = Instantiate(projectileRef, _barrelTip.position, _barrelTip.rotation);
                    var projControl = gameObject.GetComponent<ProjectileControl>();
                    projControl.target = target;
                    if (isDrone || wRef.energyCost == 0f)
                        projControl.damage = damage;
                    else
                        projControl.damage = damage * ss.energyMmt.valueMod(0);

                    if (!isDrone) {
                        projControl.damage *= dmgMod;
                        projControl.SetFFSystem(ss.ffSys);
                    }
                    else {
                        projControl.SetFFSystem(drone.ffSys);
                    }
                    audioS.PlayOneShot(audioToPlay, SoundSys.SFXvolume * (isDrone ? 0.3f : 1f) * audioMod);
                    if (explodeBoostChance > 0 && Random.Range(1, 101) <= explodeBoostChance) {
                        projControl.aoe = wRef.aoe * (1f + explodeBoost);
                        projControl.transform.localScale = size * 2f * sizeMod;
                        SoundSys.PlaySound(22, true);
                    }
                    else {
                        projControl.aoe = wRef.aoe;
                        projControl.transform.localScale = size * sizeMod;
                    }
                    projControl.critical = tempCritical;
                    projControl.impact = impact;
                    projControl.speed = (float)projSpeed;
                    if (wRef.timedFuse) {
                        projControl.timeToDestroy = GetDistanceToAimPoint(instance) / (float)projSpeed;
                        projControl.explodeOnDestroy = true;
                    }
                    else {
                        projControl.timeToDestroy = ((projSpeed != 0) ? ((float)range / (float)projSpeed) : 0f);
                        projControl.explodeOnDestroy = wRef.explodeOnMaxRange;
                    }
                    projControl.damageType = wRef.damageType;
                    projControl.owner = mainParent;
                    projControl.canHitProjectiles = wRef.canHitProjectiles;
                    projControl.piercing = wRef.piercing;
                    var projRB = gameObject.GetComponent<Rigidbody>();
                    if (wRef.compType == WeaponCompType.MineObject) {
                        projControl.timeToDestroy = 240f;
                        projRB.velocity = Vector3.zero;
                    }
                    else if (!isDrone && ss.stats.weaponStabilized) {
                        projRB.velocity = ss.ForwardVelocity();
                    }
                    else {
                        projRB.velocity = rbShip.velocity;
                    }
                    if (projControl.homing) {
                        var control = projControl.gameObject.AddComponent<ProjectileHoming>();
                        control.Initialize(mainParent, projRB, ss, instance, target, projSpeed, projControl.turnSpeed * 15); //15 is from original code
                        control.enabled = true;
                    }
                }

                IEnumerator<object> FireDelayed(MuzzleFlash _muzzleFlash, Transform _barrel, float delay) {
                    if (delay > 0)
                        yield return new WaitForSeconds(delay);
                    FireBarrel(_muzzleFlash, _barrel);
                    yield break;
                };

                if (extraBarrels != null && extraBarrels.Length > 0) {
                    float delay = alternateExtraFire ? delayPortion : 0;
                    for (int i = 0; i < extraBarrels.Length; ++i)
                        instance.StartCoroutine(FireDelayed(extraMuzzleFlash[i], extraBarrels[i], delay * (i + 1)));
                }
                FireBarrel(muzzleFlash, gunTip);

                if (wRef.burst == 0) {
                    currCoolDown = wRef.rateOfFire;
                }
                else {
                    burstCount += 1;
                    if (burstCount == wRef.burst + 1) {
                        currCoolDown = wRef.rateOfFire;
                        burstCount = 0;
                    }
                    else {
                        currCoolDown = wRef.shortCooldown;
                    }
                }
                if (chargeTime > 0f && burstCount == 0 && wRef.rateOfFire > chargedFireCount)
                    chargedFireCount = 0.001f;
            }
        }

        static MethodInfo _ChargedWeaponPass = typeof(Weapon).GetMethod("ChargedWeaponPass", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        static bool ChargedWeaponPass(Weapon w) => (bool)_ChargedWeaponPass.Invoke(w, null);
        static MethodInfo _PayCost = typeof(Weapon).GetMethod("PayCost", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        static bool PayCost(Weapon w) => (bool)_PayCost.Invoke(w, null);
        static MethodInfo _GetDistanceToAimPoint = typeof(Weapon).GetMethod("GetDistanceToAimPoint", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        static float GetDistanceToAimPoint(Weapon w) => (float)_GetDistanceToAimPoint.Invoke(w, null);

        [HarmonyPatch(typeof(Weapon), nameof(Weapon.Fire))]
        [HarmonyPrefix]
        public static bool Weapon_Fire_ProjectileFix(Transform target, bool buttonDown,
            Weapon __instance, SpaceShip ___ss, Transform ___mainParent, bool ___isDrone, bool ___loaded, 
            TWeapon ___wRef, float ___chargeTime, Transform ___weaponSlot,
            MuzzleFlash ___muzzleFlash, MuzzleFlash[] ___extraMuzzleFlash, Color ___muzzleFlashColor, float ___flashSize,
            float ___delayTime, TCritical ___critical,
            WeaponStatsModifier ___mods, GameObject ___projectileRef, Transform ___gunTip,
            Drone ___drone, Rigidbody ___rbShip, float ___damage,
            AudioSource ___audioS, float ___audioMod, AudioClip ___audioToPlay,
            int ___explodeBoostChance, float ___explodeBoost,
            Vector3 ___size, float ___sizeMod,
            WeaponImpact ___impact, int ___projSpeed, int ___range, float ___chargedDamageBoost,
            Transform[] ___extraBarrels, float ___delayPortion, bool ___alternateExtraFire,
            ref sbyte ___burstCount, ref float ___chargedFireCount, ref float ___currCoolDown
        ) {
            Weapon_Fire_Projectile(target, buttonDown,
                __instance, ___ss, ___mainParent, ___isDrone, ___loaded,
                ___wRef, ___chargeTime, ___weaponSlot,
                ___muzzleFlash, ___extraMuzzleFlash, ___muzzleFlashColor, ___flashSize,
                ___delayTime, ___critical,
                ___mods, ___projectileRef, ___gunTip, ___drone,
                ___rbShip, ___damage, ___audioS, ___audioMod, ___audioToPlay,
                ___explodeBoostChance, ___explodeBoost, ___size, ___sizeMod, 
                ___impact, ___projSpeed, ___range, ___chargedDamageBoost,
                ___extraBarrels, ___delayPortion, ___alternateExtraFire,
                ref ___burstCount, ref ___chargedFireCount, ref ___currCoolDown
                );

            return false;
        }
    }
}
