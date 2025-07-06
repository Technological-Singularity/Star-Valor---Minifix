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

        static BepInEx.Logging.ManualLogSource Log;

        static float[] lut = new float[1024];
        const int mask = (1 << 8) | (1 << 9) | (1 << 10) | (1 << 13) | (1 << 14) | (1 << 16);

        public static float GetLUT(float ratio) {
            ratio = Mathf.Clamp(ratio, 0f, 1f);
            int idx = (int)(ratio * (lut.Length + 1));
            if (idx == lut.Length) --idx;
            return lut[idx];
        }

        public void Awake() {
            Log = Logger;
            Harmony.CreateAndPatchAll(typeof(Plugin));

            const float a = 2;
            const float b = 6;
            const float c = 3;
            float min = float.MaxValue;
            float max = float.MinValue;

            for (int i = 0; i < lut.Length; ++i) {
                float t = (float)i / (lut.Length - 1);
                lut[i] = a / (1 + Mathf.Exp(-b * t)) - a / 2;
                lut[i] *= Mathf.Pow(1 - t, c);
                if (lut[i] < min) min = lut[i];
                if (lut[i] > max) max = lut[i];
            }
            for (int i = 0; i < lut.Length; ++i)
                lut[i] = (lut[i] - min) / (max - min);
        }

        public static void Dump(Transform t, string prefix = "--", bool recurse = true) {
            foreach (var o in t.GetComponents<Component>())
                Log.LogMessage(prefix + t.name + " : " + o.name + " " + o.GetType().FullName);
            if (recurse)
                foreach (Transform child in t)
                    Dump(child, prefix + " ", true);
        }

        [HarmonyPatch(typeof(ProjectileControl), "CreateExplosion")]
        [HarmonyPrefix]
        static bool CreateExplosion(Vector3 pos, ProjectileControl __instance, float ___aoe, float ___damage, DamageType ___damageType, TCritical ___critical, bool ___canHitProjectiles, Transform ___owner) {
            const float explosionSpeed = 50;

            var explosion = Instantiate(ObjManager.GetProj("Projectiles/Explosion"), __instance.transform.position, __instance.transform.rotation).GetComponent<Explosion>();
            if (___aoe < 6)
                ___aoe = 6;
            explosion.aoe = ___aoe;
            explosion.damage = ___damage;
            explosion.damageType = ___damageType;
            explosion.critical = ___critical;
            explosion.canHitProjectiles = ___canHitProjectiles;
            explosion.owner = ___owner;
            explosion.Setup(explosionSpeed);

            return false;
        }

        static MethodInfo __Explosion_OnTriggerEnter = typeof(Explosion).GetMethod("OnTriggerEnter", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        [HarmonyPatch(typeof(Explosion), nameof(Explosion.Setup))]
        [HarmonyPostfix]
        static void Explosion_Setup_Fix(float newTimeToDie, Explosion __instance, float ___aoe, ref float ___count, ref float ___incr, ref float ___maxLight, ref float ___size) {
            //__instance.gameObject.AddComponent<OnTriggerExitDelegator>().owner = __instance;
            //___count = ___aoe / newTimeToDie;
            ___count = 0.6f;
            ___size = 0;
            ___incr = 1;
            ___maxLight = ___aoe / 3;

            __instance.GetComponent<SphereCollider>().enabled = false;
            foreach (var collider in Physics.OverlapSphere(__instance.transform.position, ___aoe, mask, QueryTriggerInteraction.Ignore))
                __Explosion_OnTriggerEnter.Invoke(__instance, new object[] { collider });
            foreach (var collider in Physics.OverlapSphere(__instance.transform.position, ___aoe, mask, QueryTriggerInteraction.Collide))
                __Explosion_OnTriggerEnter.Invoke(__instance, new object[] { collider });
        }

        [HarmonyPatch(typeof(Explosion), "Update")]
        [HarmonyPrefix]
        static bool Explosion_Update_Fix(Explosion __instance, ref float ___size, float ___count, float ___incr, float ___aoe,
            float ___damage, DamageType ___damageType, TCritical ___critical, bool ___canHitProjectiles, Transform ___owner,
            Light ___pointLight, Material ___mat1, Material ___mat2, ref Color ___blastColor, float ___maxLight
            ) {

            //Dump(__instance.transform);
            Log.LogMessage(___size);

            ___size += ___incr * Time.deltaTime;
            var ratio = ___size / ___count;
            if (ratio > 1) {
                Destroy(__instance.gameObject);
                return false;
            }
            var intensity = GetLUT(ratio);
            __instance.transform.localScale = Vector3.one * intensity * ___aoe;
            ___pointLight.intensity = intensity * ___maxLight;
            ___pointLight.color = Color.Lerp(Color.red, Color.white, intensity);
            ___blastColor = ___pointLight.color;
            ___blastColor.a = 1;// intensity;

            if (___mat1 != null)
                ___mat1.SetColor("_TintColor", ___blastColor);
            if (___mat2 != null)
                ___mat2.SetColor("_TintColor", ___blastColor);

            return false;
        }

        //public class OnTriggerExitDelegator : MonoBehaviour {
        //    public Explosion owner;
        //    void OnTriggerExit(Collider collider) {
        //        __Explosion_OnTriggerEnter.Invoke(owner, new object[] { collider });
        //    }
        //}
    }
}
