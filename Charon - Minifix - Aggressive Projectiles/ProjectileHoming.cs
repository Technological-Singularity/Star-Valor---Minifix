using UnityEngine;
using System.Linq;
using System.Collections.Generic;

namespace Charon_SV_Minifix.AggressiveProjectiles {
    class ProjectileHoming : TargetPredictor {
        class ProjectileDecayer : MonoBehaviour {
            public static Tracker GetTracker(int key) {
                if (numControlledProjectiles.TryGetValue(key, out var wr))
                    return wr;
                wr = new Tracker(key);
                numControlledProjectiles[key] = wr;
                return wr;
            }
            public class Tracker {
                public int Key { get; }
                public float Value;
                public int Refs { get; private set; }
                public void Ref() => ++Refs;
                public void Deref() {
                    if (--Refs == 0)
                        numControlledProjectiles.Remove(Key);
                }
                public Tracker(int key) => Key = key;
            }
            public const float Coefficient = 0.990f;
            public static ProjectileDecayer AddDecayer(GameObject obj) {
                var decayer = obj.GetComponent<ProjectileDecayer>();
                if (decayer == null) {
                    decayer = obj.AddComponent<ProjectileDecayer>();
                    decayer.enabled = true;
                }
                return decayer;
            }
            static Dictionary<int, Tracker> numControlledProjectiles = new Dictionary<int, Tracker>();
            static int lastFrame = -1;
            void FixedUpdate() {
                if (lastFrame == Time.frameCount)
                    return;
                lastFrame = Time.frameCount;
                foreach (var kvp in numControlledProjectiles)
                    kvp.Value.Value *= Coefficient;
            }
        }
        
        Transform owner;
        Rigidbody rb;
        int crewId;
        SpaceShip ss;
        Weapon weapon;

        float maxAccel;
        float maxSpeed;
        float maxTurnRate;

        Vector3 currentVelocity;

        float deadTime = deadTimeLaunch;
        const float deadTimeLaunch = 0.65f; //time after launch before tracking becomes active

        float lastAngleTime = 0;
        const float lastAngleTimeout = 2f;

        const float maxAngleCutoffRatio = 0.02f;
        const float stutterMaximum = 1f;
        const float stutterMinimum = 0.5f;

        const float maxVelocityDelta = 100;

        int gunnerLevel;
        ProjectileDecayer.Tracker gunnerControlTracker;

        public void Initialize(Transform owner, Rigidbody rb, SpaceShip ss, Weapon weapon, Transform target, float speed, float turnRate) {
            base.Initialize(target);
            this.owner = owner;
            this.rb = rb;
            this.ss = ss;
            this.weapon = weapon;

            if (transform.position.y != 0)
                transform.position = new Vector3(transform.position.x, 0, transform.position.z);

            this.maxSpeed = speed;
            this.maxAccel = 4 * speed;
            this.maxTurnRate = turnRate;

            ProjectileDecayer.AddDecayer(owner.gameObject);
            var ownerRB = owner.gameObject.GetComponent<Rigidbody>();
            rb.velocity = 0.2f * speed * transform.forward;
            if (ownerRB != null)
                rb.velocity += ownerRB.velocity;
            currentVelocity = rb.velocity;

            if (ss == null)
                return;

            var gunner = ss.shipData.members.Where(o => o.position == CrewPosition.Gunner && o.slot == weapon.weaponSlotIndex && o.control >= 0).FirstOrDefault();
            if (gunner == null) {
                crewId = -1;
            }
            else {
                gunnerLevel = gunner.crewMember.GetSkillLevel(CrewPosition.Gunner, ss);
                crewId = gunner.crewMemberID;

                gunnerControlTracker = ProjectileDecayer.GetTracker(crewId);
                gunnerControlTracker.Ref();
                gunnerControlTracker.Value += 1;
            }
        }
        (float n1, float n2) RandomNormal(float variance) {
            var (u1, u2) = (Random.Range(0f, 1f), Random.Range(0f, 1f));
            var mag = variance * Mathf.Sqrt(-2 * Mathf.Log(u1));
            var angle = 2 * Mathf.PI * u2;
            return (mag * Mathf.Cos(angle), mag * Mathf.Sin(angle));
        }
        Vector3 CalculateAimError() {
            if (crewId < 0 || rb == null || targetRB == null)
                return Vector3.zero;

            var sideways = targetRB.velocity - rb.velocity;
            var toward = (targetRB.position - rb.position).normalized;
            sideways -= Vector3.Dot(sideways, toward) * toward;
            var gunnerLevelEffective = Mathf.Max(0, gunnerLevel - Mathf.Max(0, gunnerControlTracker.Value - 0.5f));
            float deviation = sideways.magnitude / (70 + gunnerLevelEffective * 5) * 20f + 3f - gunnerLevelEffective;
            deviation = Mathf.Clamp(deviation, 0.1f, 20f);
            var (aimErrorX, aimErrorZ) = RandomNormal(deviation);

            return new Vector3(aimErrorX, 0, aimErrorZ);
        }
        protected override void FixedUpdate() {
            base.FixedUpdate();
            if (rb == null) {
                enabled = false;
                return;
            }              

            currentVelocity = rb.velocity + maxAccel * Time.deltaTime * transform.forward;
            if (currentVelocity.sqrMagnitude > maxSpeed * maxSpeed) {
                var magRem = currentVelocity.magnitude - maxSpeed;
                magRem = magRem * maxVelocityDelta / (magRem + maxVelocityDelta);
                rb.velocity = (maxSpeed + magRem) * transform.forward;
            }
            else {
                rb.velocity = currentVelocity;
            }

            if (deadTime > 0) {
                deadTime -= Time.deltaTime;
                return;
            }

            var wantFacing = targetRB == null ? rb.transform.forward : Predict_SelfPropelled(rb.position, rb.velocity, maxAccel);
            var error = CalculateAimError();
            var facing = Quaternion.LookRotation(wantFacing + error);
            var maxTurn = maxTurnRate * Time.deltaTime;
            var newRotation = Quaternion.RotateTowards(transform.rotation, facing, maxTurn);
            var newAngle = Quaternion.Angle(newRotation, transform.rotation);

            if (Mathf.Abs(maxTurn - newAngle) / maxTurn < maxAngleCutoffRatio) {
                lastAngleTime += Time.deltaTime;
                if (lastAngleTime > lastAngleTimeout) {
                    deadTime = Random.Range(stutterMinimum, stutterMaximum);
                    lastAngleTime = 0;
                }
            }
            else {
                lastAngleTime = 0;
            }

            transform.rotation = newRotation;
        }
        void OnDestroy() {
            this.enabled = false;
            if (crewId < 0)
                return;
            gunnerControlTracker.Deref();
        }
    }
}
