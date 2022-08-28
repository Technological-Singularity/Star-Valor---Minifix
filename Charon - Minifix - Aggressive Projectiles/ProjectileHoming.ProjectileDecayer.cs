using System.Collections.Generic;
using UnityEngine;

namespace Charon.StarValor.Minifix.AggressiveProjectiles {
    partial class ProjectileHoming {
        class Decayer : MonoBehaviour {
            public float Coefficient { get; set; }
            Tracker.Collection trackedValues;

            void Initialize(Tracker.Collection trackedValues, float coeff) => (this.trackedValues, this.Coefficient) = (trackedValues, coeff);
            public static Decayer AddDecayer(GameObject obj, Tracker.Collection trackedValues, float coeff = 0.995f) {
                var decayer = obj.GetComponent<Decayer>();
                if (decayer == null)
                    decayer = obj.AddComponent<Decayer>();
                decayer.Initialize(trackedValues, coeff);
                decayer.enabled = true;
                return decayer;
            }
            static int lastFrame = -1;
            void FixedUpdate() {
                if (Coefficient <= 0 || lastFrame == Time.frameCount)
                    return;
                lastFrame = Time.frameCount;
                foreach (var kvp in trackedValues)
                    kvp.Value.Value *= Coefficient;
                if (trackedValues.Count == 0)
                    return;
            }
        }
    }
}
