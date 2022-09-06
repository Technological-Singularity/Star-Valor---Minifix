using BepInEx;
using HarmonyLib;
using UnityEngine;
using System.Reflection;
using System.Runtime.InteropServices;
using Rewired;
using static UnityEngine.UI.GridLayoutGroup;

namespace Charon.StarValor.Minifix.Rotocamera {
    [BepInPlugin(pluginGuid, pluginName, pluginVersion)]
    [BepInProcess("Star Valor.exe")]
    public class Plugin : BaseUnityPlugin {
        public const string pluginGuid = "starvalor.charon.minifix.rotocamera";
        public const string pluginName = "Charon - Minifix - Rotocamera";
        public const string pluginVersion = "0.0.0.0";
        static BepInEx.Logging.ManualLogSource Log;

        #region Extern
        [StructLayout(LayoutKind.Sequential)]
        struct POINT {
            public int X;
            public int Y;
        }

        [DllImport("user32.dll")]
        static extern bool SetCursorPos(int x, int y);
        [DllImport("user32.dll")]
        static extern bool GetCursorPos(out POINT lpPoint);
        static void SetCursorPos((int x, int y) point) {
            SetCursorPos(point.x, point.y);
        }
        static (int x, int y) GetCursorPos() {
            GetCursorPos(out var p);
            return (p.X, p.Y);
        }
        #endregion

        static GameObject cameraObject;
        static CameraRotator _cameraControl;
        static CameraRotator CameraControl {
            get {
                if (_cameraControl == null) {
                    if (cameraObject != null)
                        Destroy(cameraObject);
                    cameraObject = new GameObject();
                    _cameraControl = cameraObject.AddComponent<CameraRotator>();
                }
                return _cameraControl;
            }
        }

        public void Awake() {
            Log = Logger;
            Harmony.CreateAndPatchAll(typeof(Plugin));
        }
        [HarmonyPatch(typeof(PlayerControl), "FixedUpdate")]
        [HarmonyPrefix]
        static void PlayerControl_FixedUpdate_CameraFix(Vector3 ___mousePosition, ref Vector3 __state) {
            if (CameraControl.SettingRotation) {
                __state = ___mousePosition;
            }
        }
        [HarmonyPatch(typeof(PlayerControl), "FixedUpdate")]
        [HarmonyPostfix]
        static void PlayerControl_FixedUpdate_CameraFix(ref Vector3 ___mousePosition, Vector3 __state) {
            if (CameraControl.SettingRotation) {
                ___mousePosition = __state;
            }
        }
        [HarmonyPatch(typeof(Starfield), nameof(Starfield.RefreshStarSize))]
        [HarmonyPostfix]
        static void Starfield_RefreshStarSize_UpdateCamera(bool big, float ___starSize, ParticleSystem ___ps) {
            CameraControl.RefreshStarSize(___starSize + (big ? 0.2f : 0), ___ps);
        }

        class CameraRotator : MonoBehaviour {
            static FieldInfo __Starfield_isNebula = typeof(Starfield).GetField("isNebula", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            const float turnRate = 180f;
            const float smoothing = 0f;

            public bool SettingRotation { get; private set; }
            public bool ResettingCamera { get; private set; }


            bool oldVisible;
            Vector3 cumulative;
            const int numStarsMax = 7800;
            const int numStarsNebula = 7800;
            const int numStarsNormal = 3900;
            ParticleSystem.Particle[] particles = new ParticleSystem.Particle[0];

            float _x, _y;
            (float x, float y) GetMouse() {
                var (oX, oY) = (Screen.width >> 1, Screen.height >> 1);
                var (x, y) = GetCursorPos();
                SetCursorPos(oX, oY);

                x = oX - x;
                y = oY - y;

                _x = (smoothing * _x + x) / (smoothing + 1);
                _y = (smoothing * _y + y) / (smoothing + 1);

                return (_x, _y);
            }
            public void RefreshStarSize(float starSize, ParticleSystem ps) {
                if (particles.Length == 0)
                    return;

                for (int i = 0; i < particles.Length; ++i)
                    particles[i].startSize = starSize;
                ps.SetParticles(particles);
            }
            void RefreshStars(Starfield stars, ParticleSystem ps, Vector3 centerPos) {
                var starDistance = stars.starDistance * 4;
                var isNebula = (bool)__Starfield_isNebula.GetValue(stars);
                var maxDistance = starDistance + 100;

                bool force = false;
                if (particles.Length == 0) {
                    force = true;
                    particles = new ParticleSystem.Particle[numStarsMax];
                    ps.GetParticles(particles, 1);
                    for (int i = 1; i < numStarsMax; ++i)
                        particles[i] = particles[0];
                }

                int refreshed = 0;
                for (int i = 0; i < numStarsMax; ++i) {
                    bool recalculate = force;
                    if (!recalculate) {
                        var px = particles[i].position.x - centerPos.x;
                        var py = particles[i].position.z - centerPos.z;
                        recalculate = px * px + py * py > maxDistance * maxDistance;
                    }
                    if (recalculate) {
                        ++refreshed;
                        var pos = Random.insideUnitSphere * starDistance;
                        particles[i].position = new Vector3(pos.x + centerPos.x, pos.y + centerPos.y, pos.z + centerPos.z);
                    }
                }
                ps.SetParticles(particles, isNebula ? numStarsNebula : numStarsNormal);
            }

            void LateUpdate() {
                if (!Application.isFocused)
                    return;

                var player = ReInput.players.GetPlayer(0);
                bool setRotationState = player.GetButton("Shift") && player.GetButtonDown("Order Hold Fire");
                bool resetCameraState = !setRotationState && !SettingRotation && Input.GetKey(KeyCode.LeftAlt) && player.GetButtonDown("Order Hold Fire");

                if (Input.GetKeyDown(KeyCode.Escape) && SettingRotation)
                    setRotationState = true;

                if (setRotationState && !SettingRotation) {
                    oldVisible = Cursor.visible;
                    SettingRotation = true;
                    ResettingCamera = false;
                    GetMouse();
                }
                else if (setRotationState && SettingRotation) {
                    Cursor.visible = oldVisible;
                    SettingRotation = false;
                }

                if (SettingRotation) {
                    Cursor.visible = false;
                }

                if (resetCameraState)
                    ResettingCamera = !ResettingCamera;

                if (!SettingRotation && cumulative == Vector3.zero)
                    return;

                if (Camera.main == null || Camera.main.transform.parent == null || PlayerControl.inst == null)
                    return;

                var camera = Camera.main.transform;
                var pc = PlayerControl.inst.transform;
                var stars = camera.parent.GetComponentInChildren<Starfield>();
                var ps = stars.GetComponent<ParticleSystem>();

                if (stars == null || pc == null)
                    return;

                var radius = Vector3.Distance(camera.position, pc.position);
                if (SettingRotation) {
                    var (x, y) = GetMouse();

                    var angles = new Vector3(x, y, 0);
                    var mag = Mathf.Min(angles.magnitude * 10, turnRate);
                    angles = angles.normalized * mag * Time.deltaTime;

                    cumulative += angles;
                    cumulative.x %= 360;
                    cumulative.y = Mathf.Clamp(cumulative.y, -179.999f, -0.001f);
                }
                else if (ResettingCamera) {
                    var inv = cumulative.normalized * turnRate * Time.deltaTime;

                    cumulative -= inv;
                    cumulative.x = Mathf.Max(cumulative.x, 0);
                    cumulative.y = Mathf.Min(cumulative.y, 0);
                }
                
                var cumulativeQ = Quaternion.Euler(cumulative.y, cumulative.x, 0);

                var pos = radius * (cumulativeQ * Vector3.up) + pc.position;
                pos = pc.rotation * (pos - pc.position) + pc.position;

                camera.position = pos;
                camera.LookAt(pc.position);
                RefreshStars(stars, ps, pc.position);
            }
        }
    }
}
