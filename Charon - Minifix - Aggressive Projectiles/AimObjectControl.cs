using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace Charon_SV_Minifix.AggressiveProjectiles {
    class AimObjectControl : MonoBehaviour {
        class AimContainer {
            public bool Enabled {
                get => _enabled;
                set {
                    if (value != _enabled) {
                        _enabled = value;
                        Reticle.SetActive(value);
                        Predictor.enabled = value;
                    }
                }
            }
            bool _enabled = false;

            public Weapon Weapon { get; private set; }
            
            public GameObject Reticle { get; }
            public TargetPredictor Predictor { get; }
            public Transform Target { get; private set; }

            public AimContainer(GameObject copyFrom) {
                Reticle = Instantiate(copyFrom, null);
                Predictor = Reticle.AddComponent<TargetPredictor>();
            }
            public void Initialize(Weapon w) {
                Weapon = w;
                Target = w.weaponSlot;
                Predictor.Initialize(Target);
            }
            public void Destroy() {
                if (Reticle == null)
                    return;
                Enabled = false;
                Object.Destroy(Reticle);
            }
        }
        Dictionary<(Transform transform, float speed), AimContainer> updateValues = new Dictionary<(Transform transform, float speed), AimContainer>();
        List<KeyValuePair<(Transform transform, float speed), AimContainer>> toRemove = new List<KeyValuePair<(Transform transform, float speed), AimContainer>>();

        public Transform Target => predictor.Target;
        public SpaceShip SpaceShip => ss;

        TargetPredictor predictor;
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
            this.rb = ss.rb;
            turrets = ss.transform.GetComponentsInChildren<WeaponTurret>().ToDictionary(o => (int)o.turretIndex);
            predictor = control.gameObject.GetComponent<TargetPredictor>();
            if (predictor == null) {
                predictor = control.gameObject.AddComponent<TargetPredictor>();
                predictor.enabled = true;
            }
            predictor.Initialize(target);
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
                if (w == null || w.wRef.compType == WeaponCompType.BeamWeaponObject || w.wRef.compType == WeaponCompType.MissleObject)
                    continue;

                var speed = w.projSpeed / w.projectileRef.GetComponent<Rigidbody>().mass;
                var pair = (w.weaponSlot, speed);
                if (!updateValues.TryGetValue(pair, out var container)) {                    
                    container = new AimContainer(control.AimObj);
                    container.Initialize(w);
                    updateValues[pair] = container;
                }           

                float degreeLimit;
                bool enabled = !w.manned;
                if (enabled && w.turretMounted >= 0) {
                    var turret = turrets[w.turretMounted];
                    if (turret.type == WeaponTurretType.limitedArch)
                        degreeLimit = Mathf.Max(15, turret.degreesLimit / 2);
                    else
                        degreeLimit = 360;

                    float y = Quaternion.LookRotation(predictor.Target.position - w.weaponSlot.position).eulerAngles.y;
                    float delta = Mathf.Abs(Mathf.DeltaAngle(rb.rotation.eulerAngles.y + turret.baseDegreeRaw, y));
                    enabled = delta < degreeLimit + 5;
                }
                container.Enabled = enabled;
            }
            foreach (var kvp in updateValues.Where(o => o.Value.Weapon.manned))
                kvp.Value.Enabled = false;
            foreach (var kvp in updateValues.Where(o => o.Value.Weapon == null || o.Value.Reticle == null || o.Value.Target != o.Value.Weapon.weaponSlot))
                toRemove.Add(kvp);

            foreach (var kvp in toRemove) {
                kvp.Value.Destroy();
                updateValues.Remove(kvp.Key);
            }
                
            toRemove.Clear();
        }
        void FixedUpdate() {
            foreach(var kvp in updateValues) {
                var container = kvp.Value;
                if (container.Reticle == null || container.Weapon == null)
                    continue;

                var (_, speed) = kvp.Key;
                var (pos, vel, _) = container.Predictor.State;
                var (_, prediction) = predictor.Predict_OneShot(pos, vel, speed);

                if (prediction != Vector3.zero && container.Enabled)
                    container.Reticle.transform.position = pos + Vector3.Dot(predictor.State.pos - pos, prediction) * prediction; //predictor.FindClosestPoint(pos, prediction);//weapon.range * prediction + transform.position;
            }
        }
        void OnDisable() {
            foreach (var container in updateValues.Values)
                container.Enabled = false;
        }
        void OnDestroy() => Clear();
    }
}
