using BepInEx;
using HarmonyLib;
using UnityEngine;
using System;
using System.Collections.Generic;

namespace Charon.StarValor.Minifix.ReorderedDamage {
    [BepInPlugin(pluginGuid, pluginName, pluginVersion)]
    [BepInProcess("Star Valor.exe")]
    public class Plugin : BaseUnityPlugin {
        public const string pluginGuid = "starvalor.charon.reordered_damage";
        public const string pluginName = "Charon - Reordered Damage";
        public const string pluginVersion = "0.0.0.0";

        static GameObject damageDelegatorObject;
        static DamageDelegator _delegator;
        static DamageDelegator Delegator {
            get {
                if (_delegator == null) {
                    if (damageDelegatorObject != null)
                        Destroy(damageDelegatorObject);
                    damageDelegatorObject = new GameObject();
                    _delegator = damageDelegatorObject.AddComponent<DamageDelegator>();
                }
                return _delegator;
            }
        }

        public static BepInEx.Logging.ManualLogSource Log;

        public void Awake() {
            Log = Logger;
            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        [HarmonyPatch(typeof(Asteroid), nameof(Asteroid.Apply_Damage))]
        [HarmonyPrefix]
        static bool Asteroid_Apply_Damage_Wait(float dmg, TCritical crit, DamageType dmgType, Vector3 point, Transform dmgDealer, WeaponImpact impact, Asteroid __instance) {
            if (Delegator.Active)
                return true;
            Delegator.DelegateChecked(__instance, () => __instance.Apply_Damage(dmg, crit, dmgType, point, dmgDealer, impact));
            return false;
        }
        [HarmonyPatch(typeof(Entity), nameof(Entity.Apply_Damage))]
        [HarmonyPrefix]
        static bool Entity_Apply_Damage_Wait(float dmg, TCritical crit, DamageType dmgType, Vector3 point, Transform dmgDealer, WeaponImpact impact, Entity __instance) {
            if (Delegator.Active)
                return true;
            Delegator.DelegateChecked(__instance, () => __instance.Apply_Damage(dmg, crit, dmgType, point, dmgDealer, impact));
            return false;
        }
        [HarmonyPatch(typeof(SpaceShip), nameof(SpaceShip.Apply_Damage))]
        [HarmonyPrefix]
        static bool SpaceShip_Apply_Damage_Wait(float dmg, TCritical crit, DamageType dmgType, Vector3 point, Transform dmgDealer, WeaponImpact impact, SpaceShip __instance) {
            if (Delegator.Active)
                return true;
            Delegator.DelegateChecked(__instance, () => __instance.Apply_Damage(dmg, crit, dmgType, point, dmgDealer, impact));
            return false;
        }

        /// <summary>
        /// Component that delays execution until the LateUpdate phase. 
        /// Delegates added in the LateUpdate phase might get delayed until the next LateUpdate phase, depending on execution order.
        /// For best results, this should only be used for functions that run during other update phases.
        /// </summary>
        class DamageDelegator : MonoBehaviour {
            List<Action> delegates = new List<Action>();
            public bool Active { get; private set; }
            /// <summary>
            /// Add delegate <paramref name="action"/> to the queue, checking <paramref name="instance"/> against null references at execution time.
            /// See also: <seealso cref="Delegate(Action)"/>
            /// </summary>
            /// <param name="instance"></param>
            /// <param name="action"></param>
            /// <returns></returns>
            public bool DelegateChecked(object instance, Action action) {
                if (Active) {//already in correct phase, just run
                    if (instance != null)
                        action.Invoke();
                }
                else {
                    delegates.Add(() => {
                        if (instance != null)
                            action.Invoke();
                    });
                }
                return Active;
            }
            /// <summary>
            /// Add delegate <paramref name="action"/> to the queue, with no null checking.
            /// See also: <seealso cref="DelegateChecked(object, Action)"/>
            /// </summary>
            /// <param name="action"></param>
            /// <returns></returns>
            public bool Delegate(Action action) {
                if (Active) //already in correct phase, just run
                    action.Invoke();
                else
                    delegates.Add(action);
                return Active;
            }
            void LateUpdate() {
                Active = true; //flag whether or not functions will get routed to this delegator
                foreach (var a in delegates)
                    a.Invoke();
                delegates.Clear();
                Active = false;
            }
        }
    }
}
