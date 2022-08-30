using UnityEngine;
using System.Collections.Generic;
using System.Reflection;

namespace Charon.StarValor.Minifix.AggressiveProjectiles {
    partial class AimObjectControl {
        class AimContainer {
            static FieldInfo gunTip = typeof(Weapon).GetField("gunTip", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            static FieldInfo extraBarrels = typeof(Weapon).GetField("extraBarrels", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            static FieldInfo alternateExtraFire = typeof(Weapon).GetField("alternateExtraFire", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            static FieldInfo delayPortion = typeof(Weapon).GetField("delayPortion", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            static (Color start, Color end) infoHitEnemy =     (new Color(0, 1f, 0, 0.3f),         new Color(0, 1f, 0, 0.1f));
            static (Color start, Color end) infoHitNeutral =   (new Color(0.7f, 0.7f, 0, 0.3f),    new Color(0.7f, 0.7f, 0, 0.1f));
            static (Color start, Color end) infoHitFriendly =  (new Color(1f, 0, 0, 1f),         new Color(1f, 0, 0, 0.1f));
            static (Color start, Color end) infoMiss =         (new Color(0, 0, 1f, 0.1f),         new Color(0, 0, 1f, 0.1f));

            public bool Enabled {
                get => _enabled;
                set {
                    if (value != _enabled) {
                        _enabled = value;
                        if (Reticle != null) {
                            Reticle.SetActive(value);
                            Line.enabled = value;
                        }
                        if (children != null)
                            foreach (var o in children)
                                o.Enabled = value;
                    }
                }
            }
            bool _enabled = false;

            
            public Weapon Weapon { get; private set; }
            public GameObject Reticle { get; }
            public LineRenderer Line { get; }
            public Transform Owner { get; private set; }
            int delayIndex;
            
            List<AimContainer> children;

            public void SetPosition(TargetPredictor thisPredictor, float speed, FriendlyFireSystem ownerFFS) {
                const int baseLayerMask = (1 << 8) | (1 << 9) | (1 << 13) | (1 << 14);

                float delay = (bool)alternateExtraFire.GetValue(Weapon) ? (float)delayPortion.GetValue(Weapon) * delayIndex : 0;
                
                Vector3 intersect;
                Vector3 projectileDirection = Owner.rotation * Vector3.forward;
                if (!float.IsInfinity(speed))
                    projectileDirection = (projectileDirection * speed + thisPredictor.State.vel).normalized;
                intersect = projectileDirection * Weapon.range + Owner.position + thisPredictor.State.vel * delay;

                //var targetVel = targetPredictor.State.vel;
                //if (targetVel.sqrMagnitude < 1) targetVel = targetPredictor.Target.forward;

                //var thisPos = Owner.position + delay * thisPredictor.State.vel;
                //var targetPos = targetPredictor.State.pos + delay * targetPredictor.State.vel;

                //var intersect = TargetPredictor.GetInterceptPoint(thisPos, projectileVel, targetPos, targetVel);
                //intersect = (intersect - thisPredictor.State.pos).normalized * Weapon.range + thisPredictor.State.pos;

                int hitLayerMask = Weapon.wRef.canHitProjectiles ? baseLayerMask | (1 << 16) : baseLayerMask;
                var wasHit = Physics.Raycast(Owner.position, projectileDirection, out var hitInfo, Weapon.range, hitLayerMask, QueryTriggerInteraction.Ignore);
                bool isFriendly = false;
                bool isEnemy = false;

                if (wasHit) {
                    var targetTransform = hitInfo.collider.transform;
                    if (targetTransform.CompareTag("Collider"))
                        targetTransform = targetTransform.GetComponent<ColliderControl>().ownerEntity.transform;

                    var ss = targetTransform.GetComponent<SpaceShip>();
                    if (ss != null) {
                        var ffs = ss.ffSys;
                        isFriendly = ownerFFS.TargetIsFriendly(ffs);
                        isEnemy = ownerFFS.TargetIsEnemy(ffs);
                    }
                    intersect = hitInfo.point;
                }
                SetLine(wasHit, isEnemy, isFriendly);

                Reticle.transform.position = intersect;
                Line.SetPositions(new Vector3[] { Owner.position, intersect });

                if (children != null)
                    foreach (var child in children)
                        child.SetPosition(thisPredictor, speed, ownerFFS);
            }            
            AimContainer(GameObject root, Weapon weapon, Transform owner, int delayIndex) {
                Weapon = weapon;
                Owner = owner;
                this.delayIndex = delayIndex;

                Reticle = Object.Instantiate(root, null);
                Line = Reticle.GetComponent<LineRenderer>();
                if (Line == null)
                    Line = Reticle.AddComponent<LineRenderer>();

                InitializeLine();
            }
            public AimContainer(GameObject root, Weapon weapon) : this(root, weapon, (Transform)gunTip.GetValue(weapon), 0) {
                var transforms = (Transform[])extraBarrels.GetValue(weapon);
                int delayIndex = 0;
                if (transforms != null)
                    foreach (var barrel in transforms) {
                        if (children == null)
                            children = new List<AimContainer>();
                        var extraContainer = new AimContainer(Reticle, Weapon, barrel, ++delayIndex);
                        children.Add(extraContainer);
                    }
            }

            void SetLine(bool wasHit, bool isEnemy, bool isFriendly) {
                (Color start, Color end) lineColor;
                
                if (wasHit)
                    if (isEnemy)
                        lineColor = infoHitEnemy;
                    else if (isFriendly)
                        lineColor = infoHitFriendly;
                    else
                        lineColor = infoHitNeutral;
                else
                    lineColor = infoMiss;

                Line.startColor = lineColor.start;
                Line.endColor = lineColor.end;
            }
            void InitializeLine() {
                Line.alignment = LineAlignment.View;
                Line.colorGradient = new Gradient() {
                    mode = GradientMode.Blend,
                    colorKeys = new GradientColorKey[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                    alphaKeys = new GradientAlphaKey[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 1) }
                };
                Line.endColor = Color.red;
                Line.startColor = Color.red;
                Line.positionCount = 2;
                Line.widthCurve = AnimationCurve.Constant(0, 1, 1);
                Line.startWidth = 1f;
                Line.endWidth = 0.2f;
                Line.textureMode = LineTextureMode.RepeatPerSegment;
                Line.material = ObjManager.GetObj("Effects/LineRenderObj").GetComponent<LineRenderer>().material;
            }
            public void Destroy() {
                if (Reticle == null)
                    return;
                Enabled = false;
                Object.Destroy(Reticle);

                if (children != null)
                    foreach (var o in children)
                        o.Destroy();
            }
        }
    }
}
