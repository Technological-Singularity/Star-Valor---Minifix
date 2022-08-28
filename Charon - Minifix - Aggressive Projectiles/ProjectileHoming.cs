using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Charon.StarValor.Minifix.AggressiveProjectiles {
    partial class ProjectileHoming : MonoBehaviour {
        static Tracker.Collection controlledProjectiles = new Tracker.Collection();
        Rigidbody rb;
        int crewId;

        Transform owner;
        SpaceShip ss;
        Weapon weapon;

        float maxAccel;
        float maxSpeed;
        float maxTurnRate;
        TargetPredictor targetPredictor;

        Vector3 currentVelocity;

        float deadTime = deadTimeLaunch;
        const float deadTimeLaunch = 0.65f; //time after launch before tracking becomes active

        float lastAngleTime = 0;
        const float lastAngleTimeout = 2f;

        const float maxAngleCutoffRatio = 0.02f;
        const float stutterMaximum = 1f;
        const float stutterMinimum = 0.5f;

        const float maxVelocityDelta = 200;
        float cumulativeError = 0;
        float controlCountAddedError = float.NaN;
        float decayCoeff = 0.999f;
        bool trackActive = false;
        Vector3 facingSmoothed;

        float gunnerLevel;
        Tracker gunnerControlTracker;

        public void Initialize(Transform owner, Rigidbody rb, SpaceShip ss, Weapon weapon, Transform target, float speed, float turnRate) {
            this.owner = owner;
            this.rb = rb;
            this.ss = ss;
            this.weapon = weapon;

            targetPredictor = target.GetComponent<TargetPredictor>();
            if (targetPredictor == null) {
                targetPredictor = target.gameObject.AddComponent<TargetPredictor>();
                targetPredictor.enabled = true;
            }

            if (transform.position.y != 0)
                transform.position = new Vector3(transform.position.x, 0, transform.position.z);

            maxSpeed = speed;
            maxAccel = 5 * speed;
            maxTurnRate = turnRate;

            if (decayCoeff > 0)
                Decayer.AddDecayer(owner.gameObject, controlledProjectiles, decayCoeff);

            var ownerRB = owner.gameObject.GetComponent<Rigidbody>();
            rb.velocity += 0.2f * speed * transform.forward;
            currentVelocity = rb.velocity;
            rb.rotation = Quaternion.LookRotation(rb.velocity);
            facingSmoothed = rb.velocity.normalized;

            if (ss == null)
                return;

            trackActive = targetPredictor != null && targetPredictor.Target != null;

            var gunner = ss.shipData.members.Where(o => o.position == CrewPosition.Gunner && o.slot == weapon.weaponSlotIndex && o.control >= 0).FirstOrDefault();
            if (gunner == null) {
                crewId = -1;
                var pc = ss.GetComponent<PlayerControl>();
                if (pc == null) {
                    var aic = ss.GetComponent<AIControl>();
                    if (aic != null)
                        gunnerLevel = aic.Char.gunnerLevel;
                }
                else {
                    gunnerLevel = PChar.SpacePilot() / 2.5f;
                }
            }
            else {
                crewId = gunner.crewMemberID;
                gunnerLevel = gunner.crewMember.GetSkillLevel(CrewPosition.Gunner, ss);
            }
            controlCountAddedError = 20f / Mathf.Max(1, (20 + gunnerLevel + (gunnerLevel <= 0 ? -10 : 0)));
            gunnerControlTracker = Tracker.GetTracker(crewId);
            gunnerControlTracker.Ref(controlCountAddedError);
        }
        (float n1, float n2) RandomNormal() {
            var (u1, u2) = (Random.Range(0f, 1f), Random.Range(0f, 1f));
            var mag = Mathf.Sqrt(-2 * Mathf.Log(u1));
            var angle = 2 * Mathf.PI * u2;
            return (mag * Mathf.Cos(angle), mag * Mathf.Sin(angle));
        }
        Quaternion CalculateAimError() {
            if (rb == null)
                return Quaternion.identity;

            var gunnerLevelEffective = gunnerLevel - 3 * gunnerControlTracker.Value + (gunnerLevel <= 0 ? -10 : 0);
            var sideways = trackActive ? Vector3.Cross(targetPredictor.State.vel - rb.velocity, (targetPredictor.State.pos - rb.position).normalized).magnitude : 0;
            var angle = trackActive ? Quaternion.Angle(rb.rotation, Quaternion.LookRotation(rb.position - targetPredictor.State.pos)) : 180;
            var baseMag = sideways + Mathf.Abs(angle);
            var error = 1f;
            if (gunnerLevelEffective > 20) {
                error -= 0.10f * (1f - 1f / (gunnerLevelEffective - 20));
                gunnerLevelEffective = 20;
            }
            if (gunnerLevelEffective > 10) {
                error -= 0.35f * (gunnerLevelEffective - 10) / 10f;
                gunnerLevelEffective = 10;
            }
            error -= 0.55f * gunnerLevelEffective / 10f;
            var (rand, _) = RandomNormal();
            //var rand = Random.Range(-1f, 1f);
            var aimError = rand * error * baseMag;
            aimError = Mathf.Clamp(aimError, -270, 270);

            var filterFactor = Mathf.Max(0, gunnerLevelEffective / 5 + 2f);
            cumulativeError = (filterFactor * cumulativeError + aimError + 0) / (filterFactor + 2);

            return Quaternion.Euler(0, cumulativeError, 0);
        }
        void FixedUpdate() {
            if (rb == null) {
                enabled = false;
                return;
            }

            if (decayCoeff > 1)
                controlCountAddedError *= decayCoeff;

            if (trackActive && targetPredictor == null) {
                trackActive = false;
                gunnerControlTracker?.Deref(controlCountAddedError);
            }

            var effectiveGunnerLevel = gunnerLevel + (gunnerLevel <= 0 ? -10 : 0);
            var statFactor = 1f + 0.01f * effectiveGunnerLevel;
            var effectiveMaxAccel = maxAccel * statFactor;
            var effectiveMaxSpeed = maxSpeed * statFactor;
            var effectiveTurnRate = maxTurnRate * statFactor;

            currentVelocity = rb.velocity + effectiveMaxAccel * Time.deltaTime * transform.forward;
            if (currentVelocity.sqrMagnitude > effectiveMaxSpeed * effectiveMaxSpeed) {
                var magRem = currentVelocity.magnitude - effectiveMaxSpeed;
                magRem /= (magRem/maxVelocityDelta + 1);
                rb.velocity = (effectiveMaxSpeed + magRem) * transform.forward;
            }
            else {
                rb.velocity = currentVelocity;
            }

            if (deadTime > 0) {
                deadTime -= Time.deltaTime;
                return;
            }

            var wantFacing = targetPredictor == null ? rb.transform.forward : targetPredictor.Predict_SelfPropelled(rb.position, rb.velocity, maxAccel);
            var error = CalculateAimError();
            var facing = error * wantFacing;
            facingSmoothed = (59 * facingSmoothed + facing) / 60;
            var newFacing = Quaternion.LookRotation(facingSmoothed);

            var maxTurn = effectiveTurnRate * Time.deltaTime;
            var newRotation = Quaternion.RotateTowards(transform.rotation, newFacing, maxTurn);
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
            enabled = false;
            if (trackActive) {
                trackActive = false;
                gunnerControlTracker?.Deref(controlCountAddedError);
            }
            if (crewId < 0)
                return;
        }
    }
}
