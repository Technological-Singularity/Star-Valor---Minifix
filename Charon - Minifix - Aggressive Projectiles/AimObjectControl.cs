using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static UnityEngine.UI.GridLayoutGroup;

namespace Charon.StarValor.Minifix.AggressiveProjectiles {
    partial class AimObjectControl : MonoBehaviour {
        Dictionary<(Transform transform, float speed), AimContainer> updateValues = new Dictionary<(Transform transform, float speed), AimContainer>();
        List<KeyValuePair<(Transform transform, float speed), AimContainer>> toRemove = new List<KeyValuePair<(Transform transform, float speed), AimContainer>>();

        public Transform Target => targetPredictor.Target;
        public SpaceShip SpaceShip => ss;

        TargetPredictor targetPredictor;
        TargetPredictor thisPredictor;
        PlayerControl control;
        SpaceShip ss;
        Rigidbody rb;
        const float updatePeriod = 0.33f;
        float lastUpdate = 0;
        Dictionary<int, WeaponTurret> turrets;

        public void Initialize(PlayerControl control, SpaceShip ss, Transform target) {
            this.control = control;
            if (ss != this.ss)
                Clear();
            this.ss = ss;
            rb = ss.rb;
            turrets = ss.transform.GetComponentsInChildren<WeaponTurret>().ToDictionary(o => (int)o.turretIndex);

            targetPredictor = target.gameObject.GetComponent<TargetPredictor>();
            if (targetPredictor == null) {
                targetPredictor = target.gameObject.AddComponent<TargetPredictor>();
                targetPredictor.enabled = true;
            }

            thisPredictor = control.GetComponent<TargetPredictor>();
            if (thisPredictor == null) {
                targetPredictor = control.gameObject.AddComponent<TargetPredictor>();
                targetPredictor.enabled = true;
            }
        }
        public void Clear() {
            foreach (var container in updateValues.Values)
                container.Destroy();
            updateValues.Clear();
        }
        void Update() {
            if (lastUpdate < updatePeriod) {
                lastUpdate += Time.deltaTime;
                return;
            }
            lastUpdate = 0;

            foreach (var w in ss.weapons) {
                if (w == null || w.wRef.compType == WeaponCompType.BeamWeaponObject || w.wRef.compType == WeaponCompType.MissileObject)
                    continue;

                var speed = w.projSpeed / w.projectileRef.GetComponent<Rigidbody>().mass;
                var pair = (w.weaponSlot, speed);
                if (!updateValues.TryGetValue(pair, out var container)) {
                    container = new AimContainer(control.AimObj);
                    container.Initialize(w, w.weaponSlot);
                    //container.Initialize(w, ss.transform);
                    updateValues[pair] = container;
                }

                bool enabled = !w.manned;
                if (enabled && w.turretMounted >= 0) {
                    var turret = turrets[w.turretMounted];
                    float degreeLimit;
                    if (turret.type == WeaponTurretType.limitedArch)
                        degreeLimit = turret.degreesLimit / 2;
                    else
                        degreeLimit = 360;

                    float delta = Quaternion.Angle(w.weaponSlot.rotation, Quaternion.LookRotation(targetPredictor.State.pos - thisPredictor.State.pos));
                    //enabled = Mathf.Abs(delta) <= degreeLimit;
                }
                container.Enabled = enabled;
            }
            foreach (var kvp in updateValues.Where(o => o.Value.Weapon.manned))
                kvp.Value.Enabled = false;
            foreach (var kvp in updateValues.Where(o => o.Value.Weapon == null || o.Value.Reticle == null || o.Value.Owner != o.Value.Weapon.weaponSlot))
                toRemove.Add(kvp);

            foreach (var kvp in toRemove) {
                kvp.Value.Destroy();
                updateValues.Remove(kvp.Key);
            }

            toRemove.Clear();
        }
        void FixedUpdate() {
            foreach (var kvp in updateValues) {
                var container = kvp.Value;
                if (container.Reticle == null || container.Weapon == null)
                    continue;

                var (_, speed) = kvp.Key;
                var (pos, vel, _) = thisPredictor.State;
                var (_, prediction) = targetPredictor.Predict_OneShot(pos, vel, speed);

                if (prediction != Vector3.zero && container.Enabled)
                    container.position = pos + Vector3.Dot(targetPredictor.State.pos - pos, prediction) * prediction; //predictor.FindClosestPoint(pos, prediction);//weapon.range * prediction + transform.position;
            }
        }
        void OnDisable() {
            foreach (var container in updateValues.Values)
                container.Enabled = false;
        }
        void OnDestroy() => Clear();
    }
}
