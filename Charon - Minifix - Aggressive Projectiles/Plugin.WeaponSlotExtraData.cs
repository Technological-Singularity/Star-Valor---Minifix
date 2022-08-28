using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Charon.StarValor.Minifix.AggressiveProjectiles {
    public partial class Plugin {
        class WeaponSlotExtraData : MonoBehaviour {
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
                public float GetEffectiveRange(Vector3 relativePosition, Vector3 relativeVelocity) {
                    if (!IsValid)
                        return -1f;

                    if (float.IsInfinity(Speed))
                        return Range;

                    var speed = Mathf.Max(0, Speed - Vector3.Dot(relativePosition.normalized, relativeVelocity));
                    return speed * Range / Speed;
                }
            }

            SpaceShip ss;
            List<List<WeaponStat>> weaponStats = new List<List<WeaponStat>>();
            public WeaponStat this[int slotId, WeaponStatType type] => weaponStats[slotId][(int)type];

            public void Initialize(SpaceShip ss) {
                weaponStats.Clear();
                this.ss = ss;
                for (int i = 0; i < ss.weaponSlots.childCount; ++i) {
                    var stats = new List<WeaponStat>();
                    foreach (WeaponStatType type in System.Enum.GetValues(typeof(WeaponStatType)))
                        stats.Add(new WeaponStat(type));
                    weaponStats.Add(stats);
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

