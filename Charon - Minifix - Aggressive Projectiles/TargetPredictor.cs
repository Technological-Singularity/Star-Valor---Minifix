﻿using UnityEngine;

namespace Charon_SV_Minifix.AggressiveProjectiles {
    public class TargetPredictor : MonoBehaviour {
        public Transform Target { get; private set; } = null;
        protected Rigidbody targetRB;
        (Vector3 pos, Vector3 vel, Vector3 accel) targetState;
        (Vector3 pos, Vector3 vel, Vector3 accel) targetStateRaw;

        public void Initialize(Transform target) {
            if (target == null) {
                Target = null;
                targetRB = null;
                targetState = (Vector3.zero, Vector3.zero, Vector3.zero);
                return;
            }
            var rb = target.GetComponent<Rigidbody>();
            this.Target = target;
            this.targetRB = rb;
            if (rb == null)
                targetState = (Vector3.zero, Vector3.zero, Vector3.zero);
            else
                targetState = (rb.position, rb.velocity, Vector3.zero);
            targetStateRaw = targetState;
        }

        static float rms(Vector3 values) {
            var inner = 0f;
            inner += values.x * values.x;
            inner += values.y * values.y;
            inner += values.z * values.z;
            return Mathf.Sqrt(inner / 3);
        }

        (float lower, float higher) quadratic(float a, float b, float c) {
            var interior = b * b - 4 * a * c;
            if (interior < 0)
                return (float.NaN, float.NaN);

            interior = Mathf.Sqrt(interior);
            var negative = (-b - interior) / (2 * a);
            var positive = (-b + interior) / (2 * a);
            return negative < positive ? (negative, positive) : (positive, negative);
        }
        float quadraticLowestPositive(float a, float b, float c) {
            var (lower, higher) = quadratic(a, b, c);
            if (float.IsNaN(higher) || higher < 0)
                return float.NaN;
            return lower < 0 ? higher : lower;
        }
        protected virtual void FixedUpdate() {
            UpdateTargetData();
        }
        const int iir_factor = 5;
        const float missile_time_estimate = 4f;
        private void UpdateTargetData() {
            if (targetRB == null)
                return;
            var newPos = targetRB.position;
            var newVel = (newPos - targetStateRaw.pos) / Time.deltaTime;
            var newAccel = (newVel - targetStateRaw.vel) / Time.deltaTime;

            targetStateRaw = (newPos, newVel, newAccel);

            newPos = (newPos + (iir_factor - 1) * targetState.pos) / iir_factor;
            newVel = (newVel + (iir_factor - 1) * targetState.vel) / iir_factor;
            newAccel = (newAccel + (iir_factor - 1) * targetState.accel) / iir_factor;

            targetState = (newPos, newVel, newAccel);
        }
        Vector3 GetInterceptPoint(Vector3 firstPos, Vector3 firstDir, Vector3 secondPos, Vector3 secondDir) {
            var a = ((secondPos.x - firstPos.x) * secondDir.z - secondDir.x * (secondPos.z - firstPos.z)) / (firstDir.x * secondDir.z - secondDir.x * firstDir.z);
            return firstPos + a * firstDir;        }

        public (Vector3 position, Vector3 intercept) Predict_OneShot(Vector3 sourcePosition, Vector3 sourceVelocity, float projectileSpeed) {
            var relPosition = targetState.pos - sourcePosition;
            var relVelocity = targetState.vel - sourceVelocity;

            var projectileSpeedSq = projectileSpeed * projectileSpeed;
            var targetSpeedSq = relVelocity.sqrMagnitude;

            float towardMag = Vector3.Dot(relPosition, relVelocity);
            float relDistSq = relPosition.sqrMagnitude;
            float inner = (towardMag * towardMag) + relDistSq * (projectileSpeedSq - targetSpeedSq);

            if (inner <= 0) { //projectile too slow, no solution
                //Plugin.Log.LogWarning("NS " + towardMag + " " + relDistSq + " " + targetSpeedSq + " " + projectileSpeedSq + " " + relVelocity + " " + sourceVelocity);
                return (Vector3.zero, Vector3.zero);
            }

            float sqrt = Mathf.Sqrt(inner);
            float invTimeN = Mathf.Abs((-towardMag - sqrt) / relDistSq); //check to make sure Abs is okay here
            float invTimeP = Mathf.Abs((-towardMag + sqrt) / relDistSq);

            if (invTimeP < invTimeN)
                invTimeP = invTimeN;
            //if (invTimeP < 0)
            //    invTimeP *= -1;

            Vector3 position = targetState.pos + targetState.vel / invTimeP;
            Vector3 intercept = (invTimeP * relPosition + relVelocity).normalized;
            Vector3 velVector = targetState.vel;
            if (velVector.magnitude < 1)
                velVector = Target.forward;
            velVector.Normalize();
            Vector3 intersectPos;

            if (Mathf.Abs(Vector3.Dot(velVector, intercept)) > 0.95f)
                intersectPos = position;
            else
                intersectPos = GetInterceptPoint(sourcePosition, intercept, targetState.pos, velVector);
            //intersectPos = (intersectPos + sourcePosition) / 2;

            return (intersectPos, intercept);


            //var relPosition = targetPosition - sourcePosition;
            //var relDistance = relPosition.magnitude;
            //var relTowardDirection = relPosition.normalized;

            //var relVelocity = targetState.vel - sourceVelocity;
            //var relAcceleration = Vector3.zero;// targetState.accel - sourceAcceleration;

            //var projectileVelocity = relVelocity + projectileSpeed * relTowardDirection;
            //var relTowardSpeed = Vector3.Dot(projectileVelocity, relTowardDirection);

            //float expectedTime;
            //if (relTowardSpeed <= 0) //projectile will never hit                
            //    expectedTime = Time.deltaTime;
            //else
            //    expectedTime = relDistance / relTowardSpeed;

            //Plugin.Log.LogWarning(expectedTime);

            //return targetPosition + relVelocity * expectedTime + relAcceleration / 2 * expectedTime * expectedTime;
        }
        public Vector3 Predict_SelfPropelled(Vector3 sourcePosition, Vector3 sourceVelocity, float sourceAccel) {
            var relSpeed = sourceAccel * missile_time_estimate; //this is wrong, but assume updates will catch errors
            var (intersect, intercept) = Predict_OneShot(sourcePosition, sourceVelocity, sourceAccel * missile_time_estimate);
            if (intercept == Vector3.zero) {
                intercept = (targetState.pos - sourcePosition) + (targetState.vel - sourceVelocity) * missile_time_estimate;
                intercept.Normalize();
            }
            return intercept;

            //var relPosition = targetState.pos - sourcePosition;
            //var relDistance = relPosition.magnitude;
            //var relTowardDirection = relPosition.normalized;

            //var relVelocity = targetRB.velocity - sourceVelocity;
            //var relTowardSpeed = Vector3.Dot(relVelocity, relTowardDirection);
            //var relCrossSpeed = (relVelocity - relTowardSpeed * relTowardDirection).magnitude;

            //var timeToward = quadraticLowestPositive(-sourceAccel, relTowardSpeed, relDistance);
            //var timeCross = relCrossSpeed / Mathf.Abs(sourceAccel);
            //expectedTime = Mathf.Sqrt(timeToward * timeToward + timeCross * timeCross);
            //return targetRB.position + targetState.vel * expectedTime + targetState.accel / 2 * expectedTime * expectedTime;
        }
    }
}