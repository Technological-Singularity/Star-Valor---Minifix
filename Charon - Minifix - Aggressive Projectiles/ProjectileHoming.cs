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
        float gunnerLevelEffective = 0;
        float controlCountAddedError = float.NaN;
        float decayCoeff = 0.990f;
        float errorDirection;
        float errorTarget;
        bool trackActive = false;

        float gunnerLevel;
        Tracker gunnerControlTracker;

        public void Initialize(Transform owner, Rigidbody rb, SpaceShip ss, Weapon weapon, Transform target, float speed, float turnRate) {
            this.owner = owner;
            this.rb = rb;
            this.ss = ss;
            this.weapon = weapon;

            if (target != null) {
                targetPredictor = target.GetComponent<TargetPredictor>();
                if (targetPredictor == null) {
                    targetPredictor = target.gameObject.AddComponent<TargetPredictor>();
                    targetPredictor.enabled = true;
                }
            }

            if (transform.position.y != 0)
                transform.position = new Vector3(transform.position.x, 0, transform.position.z);

            maxSpeed = speed;
            maxAccel = 5 * speed;
            maxTurnRate = turnRate;

            var ownerRB = owner.gameObject.GetComponent<Rigidbody>();
            rb.velocity += 0.2f * speed * transform.forward;
            currentVelocity = rb.velocity;
            (errorDirection, errorTarget) = RandomNormal();
            if (Mathf.Sign(errorDirection) != Mathf.Sign(errorTarget))
                errorDirection *= -1;

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

            gunnerControlTracker = Tracker.GetTracker(crewId);
            if (trackActive) {
                controlCountAddedError = 20f / Mathf.Max(1, (20 + gunnerLevel + (gunnerLevel <= 0 ? -10 : 0)));
                gunnerControlTracker.Ref(controlCountAddedError);
            }
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

            gunnerLevelEffective = gunnerLevel - 3 * gunnerControlTracker.Value + (gunnerLevel <= 0 ? -10 : 0);
            var sideways = trackActive ? Vector3.Cross(targetPredictor.State.vel - rb.velocity, (targetPredictor.State.pos - rb.position).normalized).magnitude : 0;
            var angle = trackActive ? Quaternion.Angle(rb.rotation, Quaternion.LookRotation(rb.position - targetPredictor.State.pos)) : Mathf.Clamp(45 - 3 * gunnerLevelEffective, 15, 90);
            var baseMag = sideways + Mathf.Abs(angle) / 10;
            var errorMag = 1f;
            if (gunnerLevelEffective > 20) {
                errorMag -= 0.05f * (1f - 1f / (gunnerLevelEffective - 20));
                gunnerLevelEffective = 20;
            }
            if (gunnerLevelEffective > 10) {
                errorMag -= 0.30f * (gunnerLevelEffective - 10) / 10f;
                gunnerLevelEffective = 10;
            }
            errorMag -= 0.65f * gunnerLevelEffective / 10f;

            //var (rand, _) = RandomNormal();
            //var rand = Random.Range(-1f, 1f);
            //var aimError = rand * errorMag * baseMag;
            //aimError = Mathf.Clamp(aimError, -180, 180);
            //var filterFactor = Mathf.Max(0, gunnerLevelEffective / 3 + 2);
            //cumulativeError = (filterFactor * cumulativeError + aimError) / (filterFactor + 1);

            cumulativeError += errorDirection * Time.deltaTime * 10;
            if (Mathf.Abs(cumulativeError) >= Mathf.Abs(errorTarget)) {
                (errorDirection, errorTarget) = RandomNormal();
                if (Mathf.Sign(cumulativeError) == Mathf.Sign(errorTarget))
                    errorTarget *= -1;
                if (Mathf.Sign(errorDirection) != Mathf.Sign(errorTarget))
                    errorDirection *= -1;
            }

            return Quaternion.Euler(0, cumulativeError * errorMag * baseMag, 0);
        }

        void FixedUpdate() {
            if (rb == null) {
                enabled = false;
                return;
            }

            if (trackActive && decayCoeff < 1) {
                var orig = controlCountAddedError;
                var coeff = decayCoeff;
                if (orig <= gunnerLevelEffective)
                    coeff = 1 - (1 - coeff) / 20;
                controlCountAddedError *= coeff;
                gunnerControlTracker.Value -= orig - controlCountAddedError;
            }

            if (trackActive && (rb == null || targetPredictor == null)) {
                trackActive = false;
                gunnerControlTracker?.Deref(controlCountAddedError);
                controlCountAddedError = 0;
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
            var newFacing = error * wantFacing;

            //var smoothingFactor = Mathf.Clamp(5 * gunnerLevelEffective, 0, 60);
            //errorSmoothed = Vector3.Slerp(errorSmoothed, newFacing, 1 / (smoothingFactor + 1));

            var maxTurn = effectiveTurnRate * Time.deltaTime;
            var newRotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(newFacing), maxTurn);
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
            if (trackActive) {
                trackActive = false;
                gunnerControlTracker?.Deref(controlCountAddedError);
                controlCountAddedError = 0;
            }
        }
    }
}
