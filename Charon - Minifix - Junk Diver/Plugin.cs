using BepInEx;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace Charon.StarValor.Minifix.JunkDiver {
    [BepInPlugin(pluginGuid, pluginName, pluginVersion)]
    [BepInProcess("Star Valor.exe")]
    public class Plugin : BaseUnityPlugin {
        public const string pluginGuid = "starvalor.charon.minifix.junkdiver";
        public const string pluginName = "Charon - Minifix - Junk Diver";
        public const string pluginVersion = "0.0.0.0";
        static BepInEx.Logging.ManualLogSource Log;

        public void Awake() {
            Log = Logger;
            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.DestroyCargoItem))]
        [HarmonyPrefix]
        static bool DestroyCargoItem(bool destroyAll, Inventory __instance, CargoSystem ___cs, int ___selectedItem, int ___selectedSlot, SpaceShip ___ss, GameObject ___sellPanel) {
            var cargoItem = ___cs.cargo[___selectedItem];
            if (cargoItem.itemType == 3 && cargoItem.itemID == 26) {//item, junk
                int qnt = destroyAll ? cargoItem.qnt : 1;
                GameData.data.AddDeed("DestroyedJunk", qnt);

                var scrapChance = 0.05f * (1f + 0.15f * PChar.Char.SK[3] / 10);
                var scrapQuantity = (1f + 0.2f * PChar.Char.SK[1]) * (1f + PChar.scavengeBonus);
                
                var rareMetalChance = 0.005f + 0.02f / 10 * PChar.Char.SK[3]; //on average, 10 junk is ~1 debris field
                var rareMetalQuantity = 1f + PChar.scavengeBonus;

                var specialChance = (1f + 0.15f * PChar.Char.SK[3] / 10) * (0.02f * Mathf.Max(0, ___ss.stats.junkTreasureMod) + (PChar.Char.SK[40] == 1 ? 0.01f : 0f));
                var playerRarityBoost = PChar.Char.SK[40] == 1 ? 40 : 0;

                //SK[1] => scrap metal bonus (20% scrap metal)
                //SK[3] => rarity bonus (2% rare metal, 15% rarity chance)
                //SK[40] => rarity bonus to scavenged goods

                ScrappedLoot(___ss, scrapChance, scrapQuantity, rareMetalChance, rareMetalQuantity, specialChance, playerRarityBoost, qnt);

                if (___cs.RemoveItem(___selectedItem, qnt) == 0) {
                    __instance.SelectItem(___selectedItem, ___selectedSlot, false);
                    __instance.LoadItems();
                }
                else {
                    ___sellPanel.transform.GetChild(0).GetComponent<Button>().interactable = false;
                    __instance.DeselectItems();
                    __instance.LoadItems();
                }
                ___cs.UpdateAmmoBuffers();
                if (!__instance.TryToSelectItem(cargoItem.itemType, cargoItem.itemID, cargoItem.rarity, cargoItem.stockStationID))
                    __instance.TryToSelectNextItem(___selectedSlot);

                return false;
            }
            return true;
        }

        //Box-Muller transform
        static (float n1, float n2) RandomNormal(System.Random random) {
            var (u1, u2) = ((float)random.NextDouble(), (float)random.NextDouble());
            var mag = Mathf.Sqrt(-2 * Mathf.Log(u1));
            var angle = 2 * Mathf.PI * u2;
            return (mag * Mathf.Cos(angle), mag * Mathf.Sin(angle));
        }

        static void ScrappedLoot(SpaceShip ss, float scrapChance, float scrapQntPerSuccess, float rareMetalChance, float rareMetalQntPerSuccess, float specialChance, int raritySkillBonus, int qnt) {
            var lootRandom = new System.Random(GameData.data.junkTreasureSeed);
            //Log.LogMessage($"Generating {qnt} junk loot. Scrap chance: {scrapChance}, qnt: {scrapQntPerSuccess}, Rare metal chance: {rareMetalChance}, qnt: {rareMetalQntPerSuccess}, Special chance: {specialChance}, Rarity bonus: {raritySkillBonus}");

            float? queuedRandom = null;
            float scrapCreated = 0;
            float rareMetalCreated = 0;
            for(int i = 0; i < qnt; ++i) {
                if (lootRandom.NextDouble() < specialChance) {
                    float normRandom;
                    if (queuedRandom == null) {
                        (normRandom, queuedRandom) = RandomNormal(lootRandom);
                    }
                    else {
                        normRandom = queuedRandom.Value;
                        queuedRandom = null;
                    }

                    int rarityBoost = 65 + raritySkillBonus + (int)(specialChance * 500 * normRandom);

                    var lootStruct = LootSystem.GenerateLootItem(PChar.Char.level + 1, rarityBoost, false, -1, DropLevel.Normal, 0, lootRandom);

                    if (ss.cs != null) {
                        ss.cs.StoreItem(lootStruct.itemType, lootStruct.itemID, lootStruct.rarity, 1, 0f, -1, -1, 0);
                        SoundSys.PlaySound(20, true);
                        GenericCargoItem genericCargoItem = new GenericCargoItem(lootStruct.itemType, lootStruct.itemID, lootStruct.rarity, null, null, ss, null);
                        SideInfo.AddMsg(Lang.Get(5, 325, "<b>" + genericCargoItem.name + "</b>"));
                    }
                }
                else if (lootRandom.NextDouble() < rareMetalChance) {
                    rareMetalCreated += rareMetalQntPerSuccess;
                }
                else if (lootRandom.NextDouble() < scrapChance) {
                    scrapCreated += scrapQntPerSuccess;
                }
            }

            Vector3 getLootPos() => ss.transform.position + Quaternion.Euler(0, Random.Range(0, 360), 0) * Vector3.forward * 5;

            var loot = GameManager.instance.GetComponent<LootSystem>();
            if (rareMetalCreated > 0)
                loot.InstantiateDrop(3, 49, 1, getLootPos(), Mathf.CeilToInt(rareMetalCreated), 0, -1, 20f, -1);
            if (scrapCreated > 0)
                loot.InstantiateDrop(3, 42, 1, getLootPos(), Mathf.CeilToInt(scrapCreated), 0, -1, 20f, -1);

            GameData.data.junkTreasureSeed = lootRandom.Next();
        }
    }
}
