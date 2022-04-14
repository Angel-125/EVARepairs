using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using KSP.IO;
using FinePrint;
using Upgradeables;
using KSP.UI.Screens;
using KSP.Localization;

namespace EVARepairs
{
    internal struct PartReliability
    {
        public string partName;
        public int reliability;
        public float scienceAdded;
        public float maxScience;
    }

    [KSPScenario(ScenarioCreationOptions.AddToAllGames, GameScenes.EDITOR, GameScenes.FLIGHT)]
    public class EVARepairsScenario: ScenarioModule
    {
        #region Constants
        const string kTechUnlockBonusNode = "TECH_UNLOCK_BONUS";
        const string kNodeName = "name";
        const string kPartReliabilityNode = "PartReliability";
        const string kPartNameValue = "partName";
        const string kReliabilityValue = "reliability";
        const string kTechNodeReliabilityName = "TechNodeReliabilityBonus";
        const string kTechNodeId = "TechId";
        const string kScienceAddedValue = "scienceAdded";
        const string kMaxScience = "maxScience";
        const int kPartFailureMaxReliabilityIncrease = 10;
        const int kPartReliabilityIncrease = 2;
        const float kMessageDuration = 5f;
        const int kMinStartingReliabilityBonus = 1;
        const int kMaxStartingReliabilityBonus = 10;
        #endregion

        #region Static Fields
        public static EVARepairsScenario shared;
        public static bool maintenanceEnabled = false;
        public static bool canFailOnActivation = false;
        public static bool partsCanWearOut = false;
        public static bool reliabilityEnabled = false;
        public static bool reactionWheelsCanFail = false;
        public static bool probeCoresCanFail = false;
        public static bool landingGearCanFail = false;
        public static bool technologicalProgressEnabled = false;
        public static int startingReliability = 30;
        public static int maxReliabilityLvl1 = 90;
        public static int maxReliabilityLvl2 = 95;
        public static int maxReliabilityLvl3 = 99;
        public static double startingMTBF = 600;
        public static bool debugMode = false;
        static float scienceToAdd = 2;
        static float maxScience = 10;
        #endregion

        #region Housekeeping
        Dictionary<string, PartReliability> partReliabilities = new Dictionary<string, PartReliability>();
        Dictionary<string, int> techNodeStartingReliabilities = new Dictionary<string, int>();
        List<string> techUnlockBonusNodes = new List<string>();
        #endregion

        #region Overrides
        public override void OnAwake()
        {
            base.OnAwake();
            shared = this;
            GameEvents.OnGameSettingsApplied.Add(onGameSettingsApplied);
            onGameSettingsApplied();
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            loadPartReliabilities(node);
            loadTechStartingReliabilityBonuses(node);
            loadTechUnlockBonusNodes();
        }

        public override void OnSave(ConfigNode node)
        {
            base.OnSave(node);
            savePartReliabilities(node);
            saveTechStartingReliabilityBonuses(node);
        }

        public void OnDestroy()
        {
            GameEvents.OnGameSettingsApplied.Remove(onGameSettingsApplied);
        }

        #endregion

        #region API
        public void UpdateSettings()
        {
            onGameSettingsApplied();
        }

        public void UpdateReliability(string partName, bool partDidFail)
        {
            // Get the reliability
            PartReliability partReliability;
            if (!partReliabilities.ContainsKey(partName))
            {
                partReliability = new PartReliability();
                partReliability.partName = partName;
                partReliability.reliability = GetStartingReliability();

                partReliabilities.Add(partName, partReliability);
            }

            // Increment the reliability
            partReliability = partReliabilities[partName];
            partReliability.reliability += partDidFail ? UnityEngine.Random.Range(1, kPartFailureMaxReliabilityIncrease) : kPartReliabilityIncrease;
            int maxReliability = GetMaxReliability();
            if (partReliability.reliability > maxReliability)
                partReliability.reliability = maxReliability;

            // Setup max science
            if (partReliability.maxScience <= 0)
                partReliability.maxScience = UnityEngine.Random.Range(1, maxScience);

            // If the check failed then add a bit of Science
            if (partDidFail && partReliability.scienceAdded < partReliability.maxScience && (HighLogic.CurrentGame.Mode == Game.Modes.CAREER || HighLogic.CurrentGame.Mode == Game.Modes.SCIENCE_SANDBOX))
            {
                float scienceGained = partReliability.maxScience / UnityEngine.Random.Range(1f, 10f);
                scienceGained = scienceGained * HighLogic.CurrentGame.Parameters.Career.ScienceGainMultiplier;

                if (partReliability.scienceAdded + scienceGained > partReliability.maxScience)
                    scienceGained = partReliability.maxScience - partReliability.scienceAdded;

                // Just in case we go negative...
                if (scienceGained <= 0)
                {
                    partReliabilities[partName] = partReliability;
                    return;
                }

                partReliability.scienceAdded += scienceGained;
                ResearchAndDevelopment.Instance.AddScience(scienceGained, TransactionReasons.ScienceTransmission);
                
                string message = Localizer.Format("#LOC_EVAREPAIRS_scienceAdded", new string[2] { string.Format("{0:n1}", scienceToAdd), PartLoader.getPartInfoByName(partName).title } );
                ScreenMessages.PostScreenMessage(message, kMessageDuration, ScreenMessageStyle.UPPER_LEFT);
            }

            // Save updated reliability
            partReliabilities[partName] = partReliability;
        }

        public double GetStartingMTBF()
        {
            double adjustedMTBF = startingMTBF;

            if (HighLogic.CurrentGame.Mode != Game.Modes.CAREER && HighLogic.CurrentGame.Mode != Game.Modes.SCIENCE_SANDBOX)
                return adjustedMTBF * 3;

            // Max reliability depends upon the level of the R&D building.
            float facilityLevel = ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.ResearchAndDevelopment);

            if (facilityLevel >= 1)
                return adjustedMTBF * 3;
            else if (facilityLevel >= 0.5f)
                return adjustedMTBF * 2;
            else
                return adjustedMTBF;
        }

        public int GetStartingReliability()
        {
            if (HighLogic.CurrentGame.Mode != Game.Modes.CAREER && HighLogic.CurrentGame.Mode != Game.Modes.SCIENCE_SANDBOX)
                return startingReliability;
            int maxReliability = GetMaxReliability();
            int adjustedStartingReliability = startingReliability;

            // Adjust reliability based on unlocked tech nodes.
            int count = techUnlockBonusNodes.Count;
            for (int index = 0; index < count; index++)
            {
                adjustedStartingReliability += getStartingReliabilityBonus(techUnlockBonusNodes[index]);
            }

            // Don't go over maximum reliability allowed
            if (adjustedStartingReliability > maxReliability)
                adjustedStartingReliability = maxReliability;

            return adjustedStartingReliability;
        }

        public int GetReliability(string partName)
        {
            int minimumReliability = GetStartingReliability();
            if (partReliabilities.ContainsKey(partName))
            {
                PartReliability partReliability = partReliabilities[partName];
                if (partReliability.reliability < minimumReliability)
                {
                    partReliability.reliability = minimumReliability;
                    partReliabilities[partName] = partReliability;
                }
                return partReliabilities[partName].reliability;
            }
            else
            {
                PartReliability partReliability = new PartReliability();
                partReliability.partName = partName;
                partReliability.reliability = minimumReliability;

                partReliabilities.Add(partName, partReliability);
                return minimumReliability;
            }
        }

        public int GetMaxReliability()
        {
            if (HighLogic.CurrentGame.Mode != Game.Modes.CAREER && HighLogic.CurrentGame.Mode != Game.Modes.SCIENCE_SANDBOX)
                return maxReliabilityLvl3;

            // Max reliability depends upon the level of the R&D building.
            float facilityLevel = ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.ResearchAndDevelopment);

            if (facilityLevel >= 1)
                return maxReliabilityLvl3;
            else if (facilityLevel >= 0.5f)
                return maxReliabilityLvl2;
            else
                return maxReliabilityLvl1;
        }

        #endregion

        #region Helpers
        private int getStartingReliabilityBonus(string nodeName)
        {
            ProtoTechNode techNode = ResearchAndDevelopment.Instance.GetTechState(nodeName);

            if (techNode == null || techNode.state == RDTech.State.Unavailable)
                return 0;

            else if (!techNodeStartingReliabilities.ContainsKey(techNode.techID))
            {
                techNodeStartingReliabilities.Add(techNode.techID, UnityEngine.Random.Range(kMinStartingReliabilityBonus, kMaxStartingReliabilityBonus));
            }
            else if (techNodeStartingReliabilities[techNode.techID] <= 0)
            {
                techNodeStartingReliabilities.Add(techNode.techID, UnityEngine.Random.Range(kMinStartingReliabilityBonus, kMaxStartingReliabilityBonus));
            }

            return techNodeStartingReliabilities[techNode.techID];
        }

        private void loadTechUnlockBonusNodes()
        {
            ConfigNode[] nodes = GameDatabase.Instance.GetConfigNodes(kTechUnlockBonusNode);
            ConfigNode node;
            string nodeName;

            for (int index = 0; index < nodes.Length; index++)
            {
                node = nodes[index];
                if (!node.HasValue(kNodeName))
                    continue;

                nodeName = node.GetValue(kNodeName);

                if (!techUnlockBonusNodes.Contains(nodeName))
                    techUnlockBonusNodes.Add(nodeName);
            }
        }

        private void loadTechStartingReliabilityBonuses(ConfigNode node)
        {
            if (!node.HasNode(kTechNodeReliabilityName))
                return;

            ConfigNode[] nodes = node.GetNodes(kTechNodeReliabilityName);
            ConfigNode reliabilityNode;
            int bonusReliability = 0;
            for (int index = 0; index < nodes.Length; index++)
            {
                reliabilityNode = nodes[index];
                if (!reliabilityNode.HasValue(kTechNodeId) || !reliabilityNode.HasValue(kReliabilityValue))
                    continue;

                if (int.TryParse(reliabilityNode.GetValue(kReliabilityValue), out bonusReliability))
                {
                    techNodeStartingReliabilities.Add(reliabilityNode.GetValue(kTechNodeId), bonusReliability);
                }
            }
        }

        private void loadPartReliabilities(ConfigNode node)
        {
            if (!node.HasNode(kPartReliabilityNode))
                return;

            ConfigNode[] nodes = node.GetNodes(kPartReliabilityNode);
            ConfigNode reliabilityNode;
            PartReliability reliability;
            for (int index = 0; index < nodes.Length; index++)
            {
                reliabilityNode = nodes[index];
                if (!reliabilityNode.HasValue(kPartNameValue) || !reliabilityNode.HasValue(kReliabilityValue))
                    continue;

                reliability = new PartReliability();
                reliability.partName = reliabilityNode.GetValue(kPartNameValue);
                int.TryParse(reliabilityNode.GetValue(kReliabilityValue), out reliability.reliability);
                float.TryParse(reliabilityNode.GetValue(kScienceAddedValue), out reliability.scienceAdded);
                if (reliabilityNode.HasValue(kMaxScience))
                    float.TryParse(reliabilityNode.GetValue(kMaxScience), out reliability.maxScience);

                partReliabilities.Add(reliability.partName, reliability);
            }
        }

        private void savePartReliabilities(ConfigNode node)
        {
            if (partReliabilities.Values.Count <= 0)
                return;

            PartReliability[] reliabilities = partReliabilities.Values.ToArray();
            ConfigNode reliabilityNode;
            for (int index = 0; index < reliabilities.Length; index++)
            {
                reliabilityNode = new ConfigNode(kPartReliabilityNode);
                reliabilityNode.AddValue(kPartNameValue, reliabilities[index].partName);
                reliabilityNode.AddValue(kReliabilityValue, reliabilities[index].reliability.ToString());
                reliabilityNode.AddValue(kScienceAddedValue, reliabilities[index].scienceAdded.ToString());
                reliabilityNode.AddValue(kMaxScience, reliabilities[index].maxScience.ToString());

                node.AddNode(reliabilityNode);
            }
        }

        private void saveTechStartingReliabilityBonuses(ConfigNode node)
        {
            if (techNodeStartingReliabilities.Keys.Count <= 0)
                return;

            string[] keys = techNodeStartingReliabilities.Keys.ToArray();
            ConfigNode reliabilityNode;
            for (int index = 0; index < keys.Length; index++)
            {
                reliabilityNode = new ConfigNode(kTechNodeReliabilityName);
                reliabilityNode.AddValue(kTechNodeId, keys[index]);
                reliabilityNode.AddValue(kReliabilityValue, techNodeStartingReliabilities[keys[index]].ToString());

                node.AddNode(reliabilityNode);
            }
        }

        private void onGameSettingsApplied()
        {
            maintenanceEnabled = EVARepairsSettings.MaintenanceEnabled;
            canFailOnActivation = EVARepairsSettings.FailOnActivation;
            partsCanWearOut = EVARepairsSettings.PartsCanWearOut;
            reliabilityEnabled = EVARepairsSettings.ReliabilityEnabled;
            startingReliability = EVARepairsSettings.StartingReliability;
            reactionWheelsCanFail = EVARepairsSettings.ReactionWheelsCanFail;
            landingGearCanFail = EVARepairsSettings.LandingGearCanFail;
            technologicalProgressEnabled = EVARepairsSettings.TechnologicalProgressEnabled;
            startingMTBF = EVARepairsSettings.StartingMTBF;
            debugMode = EVARepairsSettings.DebugModeEnabled;
        }

        /*
        private void onCrewTransferred(GameEvents.HostedFromToAction<ProtoCrewMember, Part> data)
        {
            if (expendedKits.Count <= 0)
                return;

            // Hack to remove expended repair kits
            try
            {
                ProtoCrewMember astronaut = data.host;
                Part fromPart = data.from;
                Part toPart = data.to;
                int count = expendedKits.Count;

                List<UsedRepairKits> doomed = new List<UsedRepairKits>();
                UsedRepairKits expendedKit;
                for (int index = 0; index < count; index++)
                {
                    if (expendedKits[index].crewMember == astronaut)
                    {
                        expendedKit = expendedKits[index];
                        expendedKit.inventory.RemoveNPartsFromInventory(expendedKit.repairKitName, expendedKit.count);
                        doomed.Add(expendedKit);
                    }
                }

                count = doomed.Count;
                for (int index = 0; index < count; index++)
                {
                    expendedKits.Remove(doomed[index]);
                }
                doomed.Clear();
            }
            catch (Exception ex)
            {
                Debug.Log("[EVARepairs] - Error while trying to remove expended kits:" + ex);
            }
        }
        */
        #endregion
    }
}
