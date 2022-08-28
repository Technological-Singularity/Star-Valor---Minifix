using UnityEngine;

namespace Charon.StarValor.Minifix.AggressiveProjectiles {
    partial class AimObjectControl {
        class AimContainer {
            public bool Enabled {
                get => _enabled;
                set {
                    if (value != _enabled) {
                        _enabled = value;
                        if (Reticle != null) {
                            Reticle.SetActive(value);
                            Line.enabled = value;
                        }
                    }
                }
            }
            bool _enabled = false;

            public Weapon Weapon { get; private set; }

            public GameObject Reticle { get; }
            public LineRenderer Line { get; }
            public Transform Owner { get; private set; }
            public Vector3 position {
                get => Reticle.transform.position;
                set {
                    Reticle.transform.position = value;
                    Line.SetPositions(new Vector3[] { Owner.position, value });
                }
            }
            

            public AimContainer(GameObject copyFrom) {
                Reticle = Instantiate(copyFrom, null);
                Line = Reticle.AddComponent<LineRenderer>();
                Line.alignment = LineAlignment.View;
                Line.colorGradient = new Gradient() {
                    mode = GradientMode.Blend,
                    colorKeys = new GradientColorKey[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                    alphaKeys = new GradientAlphaKey[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 1) }
                };

                var baseRenderer = ObjManager.GetObj("Effects/LineRenderObj").GetComponent<LineRenderer>();

                Line.endColor = Color.red;
                Line.startColor = Color.red;
                Line.positionCount = 2;
                Line.widthCurve = AnimationCurve.Constant(0, 1, 1);
                Line.startWidth = 1;
                Line.endWidth = 1;
                Line.textureMode = LineTextureMode.RepeatPerSegment;
                Line.material = baseRenderer.material;
            }
            public void Initialize(Weapon weapon, Transform owner) {
                Weapon = weapon;
                Owner = owner;
            }
            public void Destroy() {
                if (Reticle == null)
                    return;
                Enabled = false;
                Object.Destroy(Reticle);
            }
        }
    }
}
