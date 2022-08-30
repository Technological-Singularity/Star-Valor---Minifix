using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Charon.StarValor.Minifix.AggressiveProjectiles {
    partial class AimObjectControl : MonoBehaviour {


        Dictionary<(Transform transform, float speed), AimContainer> updateValues = new Dictionary<(Transform transform, float speed), AimContainer>();
        List<KeyValuePair<(Transform transform, float speed), AimContainer>> toRemove = new List<KeyValuePair<(Transform transform, float speed), AimContainer>>();

        //public Transform Target => targetPredictor.Target;
        public SpaceShip SpaceShip => ss;

        //TargetPredictor targetPredictor;
        TargetPredictor thisPredictor;
        PlayerControl control;
        SpaceShip ss;
        Rigidbody rb;
        const float updatePeriod = 0.33f;
        float lastUpdate = 0;
        Dictionary<int, WeaponTurret> turrets;

        public void Initialize(PlayerControl control, SpaceShip ss/*, Transform target*/) {
            this.control = control;
            if (ss != this.ss)
                Clear();
            this.ss = ss;
            rb = ss.rb;
            turrets = ss.transform.GetComponentsInChildren<WeaponTurret>().ToDictionary(o => (int)o.turretIndex);

            //targetPredictor = target.gameObject.GetComponent<TargetPredictor>();
            //if (targetPredictor == null) {
            //    targetPredictor = target.gameObject.AddComponent<TargetPredictor>();
            //    targetPredictor.enabled = true;
            //}

            thisPredictor = control.GetComponent<TargetPredictor>();
            if (thisPredictor == null) {
                thisPredictor = control.gameObject.AddComponent<TargetPredictor>();
                thisPredictor.enabled = true;
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

            foreach (var weapon in ss.weapons) {                
                if (weapon == null || weapon.wRef.compType == WeaponCompType.MissileObject)
                    continue;

                var speed = weapon.wRef.compType == WeaponCompType.BeamWeaponObject ? float.PositiveInfinity : weapon.projSpeed / weapon.projectileRef.GetComponent<Rigidbody>().mass;
                var pair = (weapon.weaponSlot, speed);
                if (!updateValues.TryGetValue(pair, out var container) && weapon.turretMounted >= 0) {
                    container = new AimContainer(control.AimObj, weapon);
                    updateValues[pair] = container;
                    container.Enabled = true;
                }

                //bool enabled = !w.manned;
                //if (enabled && w.turretMounted >= 0) {
                //    var turret = turrets[w.turretMounted];
                //    float degreeLimit;
                //    if (turret.type == WeaponTurretType.limitedArch)
                //        degreeLimit = turret.degreesLimit / 2;
                //    else
                //        degreeLimit = 360;

                //    float delta = Quaternion.Angle(w.weaponSlot.rotation, Quaternion.LookRotation(targetPredictor.State.pos - thisPredictor.State.pos));
                //    enabled = Mathf.Abs(delta) <= degreeLimit;
                //}
                //container.Enabled = enabled;
            }
            foreach (var kvp in updateValues.Where(o => o.Value.Weapon != null && o.Value.Weapon.manned))
                kvp.Value.Enabled = false;
            foreach (var kvp in updateValues.Where(o => o.Value.Weapon == null || o.Value.Reticle == null))
                toRemove.Add(kvp);

            foreach (var kvp in toRemove) {
                kvp.Value.Destroy();
                updateValues.Remove(kvp.Key);
            }

            toRemove.Clear();
        }
        void FixedUpdate() {
            if (updateValues.Count == 0)
                return;

            //var colliders = ss.GetComponents<Collider>().Select(o => (o, o.enabled));
            //foreach (var (collider, enabled) in colliders)
            //    collider.enabled = false;

            void setLayers(Transform transform, int layer) {
                transform.gameObject.layer = layer;
                foreach (Transform child in transform)
                    setLayers(child, layer);
            }

            const int layerMask = (1 << 8) | (1 << 9) | (1 << 13) | (1 << 14) | (1 << 16);
            var oldLayers = Plugin.SetLayers(ss.transform, layerMask, 2); //ignore raycast layer

            foreach (var kvp in updateValues) {
                var container = kvp.Value;
                if (container.Reticle == null || container.Weapon == null)
                    continue;
                container.SetPosition(thisPredictor, kvp.Key.speed, ss.ffSys);

                //var (weaponSlot, speed) = kvp.Key;
                //var weaponVel = weaponSlot.rotation * Vector3.forward * speed + thisPredictor.State.vel;
                //var targetVel = targetPredictor.State.vel;
                //if (targetVel.sqrMagnitude < 1) targetVel = Target.forward;
                //var intersect = TargetPredictor.GetInterceptPoint(weaponSlot.position, weaponVel, targetPredictor.State.pos, targetVel);
                //intersect = (intersect - thisPredictor.State.pos).normalized * kvp.Value.Weapon.range + thisPredictor.State.pos;
                //container.Position = intersect;

                //var (pos, vel, _) = thisPredictor.State;
                //var (_, prediction) = targetPredictor.Predict_OneShot(pos, vel, speed);
                //if (prediction != Vector3.zero && container.Enabled)
                //    container.position = pos + Vector3.Dot(targetPredictor.State.pos - pos, prediction) * prediction; //predictor.FindClosestPoint(pos, prediction);//weapon.range * prediction + transform.position;
            }

            Plugin.ResetLayers(oldLayers);

            //foreach (var (collider, enabled) in colliders)
            //    collider.enabled = enabled;
        }
        void OnDisable() {
            foreach (var container in updateValues.Values)
                container.Enabled = false;
        }
        void OnDestroy() => Clear();
    }
}
