using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace Charon_SV_Minifix.AggressiveProjectiles {
    class AimObjectControl : MonoBehaviour {
        LinkedList<GameObject> aimObjects = new LinkedList<GameObject>();
        HashSet<(Weapon weap, Transform pos, float speed)> updateValues = new HashSet<(Weapon, Transform, float)>();

        public Transform Target => predictor.Target;
        public SpaceShip SpaceShip => ss;

        TargetPredictor predictor;
        PlayerControl control;
        SpaceShip ss;
        Rigidbody rb;
        const float updatePeriod = 0.33f;
        float lastUpdate = 0;
        Dictionary<int, WeaponTurret> turrets;
        Dictionary<Transform, (Vector3 position, Vector3 velocity, Vector3 acceleration)> lastStates = new Dictionary<Transform, (Vector3 position, Vector3 velocity, Vector3 acceleration)>();

        public void Initialize(PlayerControl control, SpaceShip ss, Transform target) {
            this.control = control;
            this.ss = ss;
            this.rb = ss.rb;
            turrets = ss.transform.GetComponentsInChildren<WeaponTurret>().ToDictionary(o => (int)o.turretIndex);
            predictor = control.gameObject.GetComponent<TargetPredictor>();
            if (predictor == null) {
                predictor = control.gameObject.AddComponent<TargetPredictor>();
                predictor.enabled = true;
            }
            predictor.Initialize(target);
            updateValues.Clear();
        }
        public void ClearStates() => lastStates.Clear();
        (Vector3 pos, Vector3 vel, Vector3 accel) UpdateState(Transform tf) {
            if (!lastStates.TryGetValue(tf, out var oldValues)) {
                oldValues = (tf.position, new Vector3(), new Vector3());
                lastStates[tf] = oldValues;
            }
            var newPos = tf.position;
            var newVel = (newPos - oldValues.position) / Time.deltaTime;
            var newAccel = (newVel - oldValues.velocity) / Time.deltaTime;
            var wr = (newPos, newVel, newAccel);
            lastStates[tf] = wr;
            return wr;
        }
        void Update() {
            if (lastUpdate < updatePeriod) {
                lastUpdate += Time.deltaTime;
                return;
            }

            lastUpdate = 0;
            updateValues.Clear();

            foreach (var w in ss.weapons) {
                if (w == null || w.wRef.compType == WeaponCompType.BeamWeaponObject || w.wRef.compType == WeaponCompType.MissleObject)
                    continue;

                float degreeLimit;
                if (w.turretMounted < 0) {
                    updateValues.Add((w, w.weaponSlot, w.projSpeed / w.projectileRef.GetComponent<Rigidbody>().mass));
                }
                else {
                    var turret = turrets[w.turretMounted];
                    if (turret.type == WeaponTurretType.limitedArch)
                        degreeLimit = Mathf.Max(15, turret.degreesLimit / 2);
                    else
                        degreeLimit = 360;

                    float y = Quaternion.LookRotation(predictor.Target.position - w.weaponSlot.position).eulerAngles.y;
                    float delta = Mathf.Abs(Mathf.DeltaAngle(rb.rotation.eulerAngles.y + turret.baseDegreeRaw, y));
                    if (delta < degreeLimit + 5)
                        updateValues.Add((w, w.weaponSlot, w.projSpeed / w.projectileRef.GetComponent<Rigidbody>().mass));
                }
            }

            int newCount = updateValues.Count - aimObjects.Count;
            for (int i = 0; i < newCount; ++i) {
                aimObjects.AddLast(Instantiate(control.AimObj, null));
            }
            for (int i = newCount; i < 0; ++i) {
                var obj = aimObjects.Last.Value;
                obj.SetActive(false);
                aimObjects.RemoveLast();
                Destroy(obj);
            }
        }
        void FixedUpdate() {
            var node = aimObjects.First;
            foreach(var (weapon, transform, speed) in updateValues) {
                var (pos, vel, accel) = UpdateState(transform);

                var (intersect, intercept) = predictor.Predict_OneShot(pos, vel, speed);
                if (intercept != Vector3.zero) {
                    node.Value.transform.position = weapon.range * intercept + transform.position;
                    if (!node.Value.activeSelf)
                        node.Value.SetActive(true);
                }
                node = node.Next;
            }
        }
        void OnDisable() {
            foreach (var go in aimObjects)
                if (go != null && go.activeSelf)
                    go.SetActive(false);
        }

        void OnDestroy() {
            OnDisable();
            foreach (var go in aimObjects)
                if (go != null)
                    Destroy(go);
        }
    }
}
