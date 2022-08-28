using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System;

namespace Charon.StarValor.Minifix.AggressiveProjectiles {
    partial class ProjectileHoming {
        public class Tracker {
            public class Collection : Dictionary<int, Tracker> {
                int lastFrame = -1;
                public void FixedUpdate(Func<float, float> updateFunc) {
                    if (Count == 0 || lastFrame == Time.frameCount)
                        return;
                    lastFrame = Time.frameCount;
                    foreach (var tracker in this.Values)
                        tracker.Value = updateFunc(tracker.Value);
                }
            }

            public int Key { get; }
            public float Value;
            public int Refs { get; private set; }
            public void Ref(float addedValue) {
                if (!float.IsNaN(addedValue))
                    Value += addedValue;
                Ref();
            }
            public void Ref() => ++Refs;
            public void Deref(float addedValue) {
                if (!float.IsNaN(addedValue))
                    Value -= addedValue;
                Deref();
            }
            public void Deref() {
                if (--Refs == 0)
                    controlledProjectiles.Remove(Key);
            }
            Tracker(int key) => Key = key;
            public static Tracker GetTracker(int key) {
                if (controlledProjectiles.TryGetValue(key, out var wr))
                    return wr;

                wr = new Tracker(key);
                controlledProjectiles[key] = wr;
                return wr;
            }
        }
    }
}
