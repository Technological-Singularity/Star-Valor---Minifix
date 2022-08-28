using BepInEx;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;

namespace Charon.Test {
    [BepInPlugin(pluginGuid, pluginName, pluginVersion)]
    [BepInProcess("Star Valor.exe")]
    public class Plugin : BaseUnityPlugin {
        public const string pluginGuid = "starvalor.charon.GUID";
        public const string pluginName = "Charon - PLUGINNAME";
        public const string pluginVersion = "0.0.0.0";
        static BepInEx.Logging.ManualLogSource Log;

        public void Awake() {
            Log = Logger;
            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        bool alreadyRun = false;
        void Update() {
            if (alreadyRun)
                return;
            FieldInfo f = typeof(GenData).GetField("generalData", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            GeneralData data = (GeneralData)f.GetValue(null);
            if (data == null)
                return;
            alreadyRun = true;

            GenData.ResetTempStuff();

            void remove(List<int> list) {
                List<int> removable = new List<int>() {
                    0,
                    1,
                    2,
                    11,
                    13,
                    21,
                };
                foreach (var i in removable)
                    if (list.Contains(i))
                        list.Remove(i);
            };

            foreach (var i in data.crewmembers) {
                var crew = CrewDB.GetCrewMember(i);
                if (crew == null)
                    Log.LogMessage("tempCrewmemberUnlocks " + i + " NULL");
                else
                    Log.LogMessage("generalData.crewmembers " + crew.id + " " + crew.aiChar.name);
            }
            remove(data.crewmembers);

            FieldInfo ff = typeof(GenData).GetField("tempCrewmemberUnlocks", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            List<int> crews = (List<int>)ff.GetValue(null);
            foreach (var i in crews) {
                var crew = CrewDB.GetCrewMember(i);
                if (crew == null)
                    Log.LogMessage("tempCrewmemberUnlocks " + i + " NULL");
                else
                    Log.LogMessage("tempCrewmemberUnlocks " + crew.id + " " + crew.aiChar.name);
            }
            remove(crews);
        }

        [HarmonyPatch(typeof(PerkControl), nameof(PerkControl.SetupCrewman))]
        [HarmonyPrefix]
        static bool FixCrew(CrewMember newCrewMember, PerksPanel pPanel) {
            if (newCrewMember == null || pPanel == null)
                return false;



            return true;
        }
    }
}
