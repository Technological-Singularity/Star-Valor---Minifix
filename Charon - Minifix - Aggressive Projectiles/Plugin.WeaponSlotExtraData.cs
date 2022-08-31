using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using System;

namespace Charon.StarValor.Minifix.AggressiveProjectiles {
    public partial class Plugin {
        class WeaponSlotExtraData : MonoBehaviour {
            static FieldInfo gunTip = typeof(Weapon).GetField("gunTip", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            static FieldInfo extraBarrels = typeof(Weapon).GetField("extraBarrels", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            public enum WeaponStatType : int {
                Normal = 0,
                PD = 1,
                Repair = 2,
                Beam = 3,
            }
            public class WeaponStat {
                public WeaponStatType Type { get; }
                public float Range { get; private set; }
                public float Speed { get; private set; }
                public WeaponStat(WeaponStatType type) {
                    Type = type;
                    Clear();
                }
                public void Update(Weapon weapon) {
                    if ((weapon.range > Range || float.IsNaN(Range))) {
                        Range = weapon.range;
                        Speed = weapon.wRef.compType == WeaponCompType.BeamWeaponObject ? float.PositiveInfinity : weapon.wRef.speed;
                    }
                }
                public bool IsValid => !float.IsNaN(Range);
                public void Clear() {
                    Range = float.NaN;
                    Speed = float.NaN;
                }
                public float GetEffectiveRange(Transform target, Vector3 sourcePosition, Vector3 sourceVelocity) {
                    if (!IsValid)
                        return -1f;

                    if (float.IsInfinity(Speed))
                        return Range;

                    var targetPredictor = target.GetComponent<TargetPredictor>();
                    if (targetPredictor == null) {
                        targetPredictor = target.gameObject.AddComponent<TargetPredictor>();
                        targetPredictor.enabled = true;
                    }
                    var pos = targetPredictor.Predict_OneShot(sourcePosition, sourceVelocity, Speed);
                    var relPosition = targetPredictor.State.pos - sourcePosition;
                    var relVelocity = targetPredictor.State.vel - sourceVelocity;

                    var speedTowards = Mathf.Max(0, Speed - Vector3.Dot(relPosition.normalized, relVelocity));
                    return speedTowards * Range / Speed;
                }
            }

            SpaceShip ss;
            List<List<WeaponStat>> weaponStats = new List<List<WeaponStat>>();
            List<(Transform[] barrels, Func<int, Transform> gunTipGetter, Func<int, Transform[]> extraBarrelsGetter)> barrelInfo = new List<(Transform[] barrels, Func<int, Transform> gunTipGetter, Func<int, Transform[]> extraBarrelsGetter)>();

            public WeaponStat this[int slotId, WeaponStatType type] => weaponStats[slotId][(int)type];
            public Transform[] GetBarrels(int slotId) {
                var (barrels, getGunTip, getExtra) = barrelInfo[slotId];
                if (barrels[0] != null)
                    return barrels;

                barrels[0] = getGunTip(slotId);

                int bidx = 0;
                foreach (var o in getExtra(slotId))
                    barrels[++bidx] = o;

                return barrels;
            }

            public void Initialize(SpaceShip ss) {
                weaponStats.Clear();
                barrelInfo.Clear();
                this.ss = ss;

                for (int i = 0; i < ss.weaponSlots.childCount; ++i) {
                    var stats = new List<WeaponStat>();
                    foreach (WeaponStatType type in Enum.GetValues(typeof(WeaponStatType)))
                        stats.Add(new WeaponStat(type));
                    weaponStats.Add(stats);

                    var weaponSlot = ss.weaponSlots.GetChild(i);
                    var turret = weaponSlot.GetComponent<WeaponTurret>();

                    var isGunTip = weaponSlot.Find("GunTip") != null;
                    var isTurret = weaponSlot.GetComponent<WeaponTurret>() != null;
                    var allBarrels = new Transform[turret == null ? 1 : 1 + (turret.extraBarrels?.Length ?? 0)];

                    Func<int, Transform> gunTipGetter;
                    if (isGunTip)
                        gunTipGetter = (idx) => ss.weaponSlots.GetChild(idx).Find("GunTip");
                    else
                        gunTipGetter = (idx) => ss.weaponSlots.GetChild(idx);

                    Func<int, Transform[]> extraBarrelsGetter;
                    if (isTurret)
                        extraBarrelsGetter = (idx) => ss.weaponSlots.GetChild(idx).GetComponent<WeaponTurret>().extraBarrels;
                    else
                        extraBarrelsGetter = (idx) => null;


                    barrelInfo.Add((allBarrels, gunTipGetter, extraBarrelsGetter));
                }
            }
            public void Refresh() {
                for (int slotIndex = 0; slotIndex < weaponStats.Count; ++slotIndex) {
                    var weapons = ss.weapons.Where(o => o.weaponSlotIndex == slotIndex);
                    foreach (var w in weapons) {
                        if (w.wRef.canHitProjectiles)
                            this[slotIndex, WeaponStatType.PD].Update(w);
                        if (w.wRef.compType == WeaponCompType.BeamWeaponObject && w.wRef.damageType != DamageType.Repair)
                            this[slotIndex, WeaponStatType.Beam].Update(w);
                        else if (w.wRef.damageType == DamageType.Repair)
                            this[slotIndex, WeaponStatType.Repair].Update(w);
                        else
                            this[slotIndex, WeaponStatType.Normal].Update(w);
                    }
                }
            }

        }
    }
}

//The following functions should be updated to have altered range based on the velocity of the target:
//WeaponTurret.CanFireAt
//AIControl.FireAllWeapons

