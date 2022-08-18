using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Charon_SV_Minifix.MinerGunners {
    public enum ControlMode : int {
        Player = 0,
        AI = 1,
    }
    public enum Stance : int {
        Helpful,
        Harmful,
        Betrayal,
    }
    public enum TargetMode : int {
        None = 0,
        //Hostile = 1 << 0,
        Neutral = 1 << 1,
        Friendly = 1 << 2,
        //Owned = 1 << 3,
        Asteroid = 1 << 4,
    }
    public struct WeaponTargetingContext {
        public SpaceShip SpaceShip;
        public WeaponTurret Turret;
        public Weapon Weapon;
        public CrewMember Gunner;
        public Transform Target;
        public Stance Stance;
        public int TargetModes;
    }
    public class WeaponTurretExtraControl : MonoBehaviour {

        public static WeaponTurretExtraControl GetInstance(SpaceShip ss) {
            WeaponTurretExtraControl control = ss.GetComponent<WeaponTurretExtraControl>();
            if (control == null) {
                control = ss.gameObject.AddComponent<WeaponTurretExtraControl>();
                control.Initialize(ss);
            }
            return control;
        }
        
        static readonly List<(ControlMode control, Stance stance, int targeting)> controlCycle = new List<(ControlMode control, Stance stance, int targeting)>() {
            (ControlMode.Player, Stance.Helpful, (int)TargetMode.None),
            (ControlMode.AI, Stance.Helpful, (int)TargetMode.Neutral | (int)TargetMode.Friendly),
            (ControlMode.AI, Stance.Helpful, (int)TargetMode.Neutral | (int)TargetMode.Friendly | (int)TargetMode.Asteroid),
        };

        readonly static MethodInfo weapon_turret_CanFireAgainst = typeof(WeaponTurret).GetMethod("CanFireAgainst", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        static float CanFireAgainst(WeaponTurret instance, Transform targetTrans) => (float)weapon_turret_CanFireAgainst.Invoke(instance, new object[] { targetTrans });

        Dictionary<int, int> turretControlMap = new Dictionary<int, int>();

        //List<WeaponTurret> turrets;
        SpaceShip ss;
        public delegate void TargetValidationFunc(ref WeaponTargetingContext context, ref Stance result);
        List<TargetValidationFunc> onTargetFuncs = null;
        public void Initialize(SpaceShip ss) {
            this.ss = ss;
            //turrets = new List<WeaponTurret>();
            var foundTurrets = this.transform.GetComponentsInChildren<WeaponTurret>();
            foreach (var turret in foundTurrets) {
                //while (turrets.Count <= turret.turretIndex)
                //    turrets.Add(null);
                //turrets[turret.turretIndex] = turret;
                foreach (var crew in ss.crew.GetAssignedMembers(CrewPosition.Gunner).Where(o => o.slot == turret.turretIndex))
                    turretControlMap[turret.turretIndex] = crew.control + 1;
            }
        }


        public void AddTurretStanceModifier(TargetValidationFunc func) {
            if (onTargetFuncs == null)
                new List<TargetValidationFunc>();
            onTargetFuncs.Add(func);
        }
        //Stance GetTurretStance(SpaceShip spaceShip, WeaponTurret turret, Weapon weapon, CrewMember gunner, Transform target, Stance stance, int targetModes) {
        //    //Not yet implemented - designed to allow crew, skills, effects to force targeting in certain situations
        //    throw new NotImplementedException();

        //    if (onTargetFuncs == null || onTargetFuncs.Count == 0)
        //        return stance;

        //    Stance wr = stance;
        //    WeaponTargetingContext context = new WeaponTargetingContext() {
        //        SpaceShip = spaceShip,
        //        Turret = turret,
        //        Weapon = weapon,
        //        Gunner = gunner,
        //        Target = target,
        //        Stance = stance,
        //        TargetModes = targetModes
        //    };
        //    foreach (var func in onTargetFuncs)
        //        func(ref context, ref wr);
        //    return wr;
        //}
        //public void SetControlMode(int turretIndex, ControlMode controlMode, Stance stance, params TargetMode[] targetModes) {
        //    if (turretInfo == null)
        //        turretInfo = new Dictionary<int, (ControlMode, Stance, int)>();
        //    int targets = targetModes == null ? 0 : 1;
        //    if (targetModes != null)
        //        for (int i = 0; i < targetModes.Length; ++i)
        //            targets *= (int)targetModes[1];
        //    turretInfo[turretIndex] = (controlMode, stance, targets);
        //}
        public string GetControlString(int turretIndex) {
            var gunner = ss.crew.GetGunner(turretIndex);
            if (!turretControlMap.TryGetValue(turretIndex, out var code)) {
                code = gunner.control + 1;
                Preload_SetControlCode(gunner, code);
            }

            (_, var stance, var targets) = controlCycle[code];
            bool isMining = (targets & (int)TargetMode.Asteroid) != 0;
            string text_stance, text_targets;
            switch (stance) {
                case Stance.Helpful:  //blue/cyan?
                    if (isMining) text_stance = ColorSys.infoText2;
                    else text_stance = ColorSys.infoText3; 
                    break;
                default:  //red?
                    text_stance = ColorSys.damageText; 
                    break;
            }

            if (isMining)
                text_targets = "Mining";
            else if (stance == Stance.Helpful)
                text_targets = "Defense";
            else
                text_targets = "Offense";

            return text_stance + text_targets + "</color>";
        }
        public void Preload_CycleControlCode(AssignedCrewMember crew) {
            var turretIndex = crew.slot;
            var gunner = ss.crew.GetGunner(turretIndex);
            if (!turretControlMap.TryGetValue(turretIndex, out var code))
                code = gunner.control + 1;
            
            if (++code >= controlCycle.Count)
                code = 0;

            Preload_SetControlCode(gunner, code);
        }
        public bool IsEqual(AssignedCrewMember first, AssignedCrewMember second) {
            if (!turretControlMap.TryGetValue(first.slot, out var codeFirst)) {
                codeFirst = first.control + 1;
                turretControlMap[first.slot] = codeFirst;
            }
            if (!turretControlMap.TryGetValue(second.slot, out var codeSecond)) {
                codeSecond = second.control + 1;
                turretControlMap[second.slot] = codeSecond;
            }
            return codeFirst == codeSecond;
        }
        public void Preload_CopyControlCode(AssignedCrewMember dst, AssignedCrewMember src) {
            Preload_SetControlCode(dst, turretControlMap[src.slot]);
        }
        int Preload_SetControlCode(AssignedCrewMember gunner, int code) {
            turretControlMap[gunner.slot] = code;
            gunner.control = code == 0 ? 0 : -1; //prefix, set to -1 so it will be incremented to 0 after preload
            return code;
        }
        public void Pretext_Save() {
            foreach (var kvp in turretControlMap) {
                var gunner = ss.crew.GetGunner(kvp.Key);
                if (gunner != null)
                    gunner.control = kvp.Value == 0 ? -1 : 0; //prefix, set to -1 or 0 to redirect to appropriate place for text lookup
            }
        }
        public void Postload_Restore() {
            foreach(var kvp in turretControlMap) {
                var gunner = ss.crew.GetGunner(kvp.Key);
                if (gunner != null)
                    gunner.control = kvp.Value - 1; //restore value so it can be inferred between scenes
            }
        }
        void OnDestroy() {
            Postload_Restore();
        }
        public void SetTarget(WeaponTurret turret) {          
            if (!turretControlMap.TryGetValue(turret.turretIndex, out var code)) {
                var gunner = ss.crew.GetGunner(turret.turretIndex);
                code = gunner.control + 1;
                turretControlMap[gunner.slot] = code;
            }                
            
            (var control, var stance, var targets) = controlCycle[code];

            //These modes are currently handled by the default targeting, so implement them later
            //var pdWeapons = ss.weapons.Where(o => o.turretMounted == turret.turretIndex && o.wRef.canHitProjectiles).Count() > 0;
            //bool hostile = (targets & (int)TargetMode.Hostile) != 0;

            bool neutral = (targets & (int)TargetMode.Neutral) != 0;
            bool friendly = (targets & (int)TargetMode.Friendly) != 0;
            //bool owned = (targets & (int)TargetMode.Owned) != 0;
            bool asteroid = (targets & (int)TargetMode.Asteroid) != 0;

            var repairRange = float.MinValue;
            var harmRange = float.MinValue;
            foreach (var o in ss.weapons) {
                if (o.wRef.damageType == DamageType.Repair && o.range > repairRange)
                    repairRange = o.range;
                else if (o.range > harmRange)
                    harmRange = o.range;
            }

            List<Transform> harmTargets = new List<Transform>();
            List<SpaceShip> repairTargets = new List<SpaceShip>();
            if (asteroid && turret.canDealDmg) {
                var asteroids = Physics.OverlapSphere(turret.transform.position, harmRange, 1024) //1024 is layer mask for asteroids
                    .Where(o => o.tag == "Asteroid" && CanFireAgainst(turret, o.transform) > 0).Select(o => o.transform);
                harmTargets.AddRange(asteroids);
            }

            turret.SetMiningTarget(null);
            if (neutral || friendly/* || owned*/) {
                var allShips = Physics.OverlapSphere(turret.transform.position, harmRange, 8704) //8704 is layer mask for ships
                    .Where(o => CanFireAgainst(turret, o.transform) > 0)
                    .Select(o => o.tag == "Collider" ? o.GetComponent<ColliderControl>().ownerEntity.transform.GetComponent<SpaceShip>() : o.transform.GetComponent<SpaceShip>());
                
                if (neutral && stance != Stance.Helpful && turret.canDealDmg) {
                    var allNeutral = allShips.Where(o => !ss.ffSys.TargetIsEnemy(o.ffSys) && !ss.ffSys.TargetIsFriendly(o.ffSys) && Vector3.Distance(o.transform.position, ss.transform.position) <= harmRange && !o.status.Get(ShipStatusName.Cloaked));
                    harmTargets.AddRange(allNeutral.Select(o => o.transform));
                }
                if (friendly && turret.canRepair) {
                    var allFriendly = allShips.Where(o => ss.ffSys.TargetIsFriendly(o.ffSys) && Vector3.Distance(o.transform.position, ss.transform.position) <= repairRange && o.currHP < o.baseHP);
                    repairTargets.AddRange(allFriendly);
                }
                if (repairTargets.Count > 0) {
                    Transform mostDamaged = null;
                    float curPct = float.MaxValue;
                    foreach(var o in repairTargets) {
                        var pct = o.currHP / o.baseHP;
                        if (pct < curPct) {
                            curPct = pct;
                            mostDamaged = o.transform;
                        }
                    }
                    turret.SetMiningTarget(mostDamaged); //used to force attack target
                    turret.repairingTarget = true; //override repair behavior
                }
            }
            if (turret.target == null) {
                Transform closest = null;
                float curRange = float.MaxValue;
                foreach(var o in harmTargets) {
                    var range = Vector3.SqrMagnitude(ss.transform.position - o.position);
                    if (range < curRange) {
                        curRange = range;
                        closest = o;
                    }
                }
                turret.SetMiningTarget(closest);
            }
        }
    }
}
