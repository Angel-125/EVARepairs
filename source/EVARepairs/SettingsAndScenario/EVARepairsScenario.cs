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
    }

    [KSPScenario(ScenarioCreationOptions.AddToAllGames, GameScenes.EDITOR, GameScenes.FLIGHT)]
    public class EVARepairsScenario: ScenarioModule
    {
        #region Constants
        const string kPartReliabilityNode = "PartReliability";
        const string kPartNameValue = "partName";
        const string kReliabilityValue = "reliability";
        const string kScienceAddedValue = "scienceAdded";
        const int kPartFailureReliabilityIncrease = 5;
        const int kPartReliabilityIncrease = 10;
        const float kMessageDuration = 5f;
        #endregion

        #region Static Fields
        public static EVARepairsScenario shared;
        public static bool maintenanceEnabled = false;
        public static bool canFailOnActivation = false;
        public static bool partsCanWearOut = false;
        public static bool reliabilityEnabled = false;
        public static int startingReliability = 30;
        public static int maxReliability = 99;
        static float scienceToAdd = 2;
        static float maxScience = 10;
        #endregion

        #region Housekeeping
        Dictionary<string, PartReliability> partReliabilities = new Dictionary<string, PartReliability>();
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
        }

        public override void OnSave(ConfigNode node)
        {
            base.OnSave(node);
            savePartReliabilities(node);
        }

        public void OnDestroy()
        {
            GameEvents.OnGameSettingsApplied.Remove(onGameSettingsApplied);
        }
        #endregion

        #region API
        public void UpdateReliability(string partName, bool partDidFail)
        {
            // Get the reliability
            PartReliability partReliability;
            if (!partReliabilities.ContainsKey(partName))
            {
                partReliability = new PartReliability();
                partReliability.partName = partName;
                partReliability.reliability = startingReliability;

                partReliabilities.Add(partName, partReliability);
            }

            // Increment the reliability
            partReliability = partReliabilities[partName];
            partReliability.reliability += partDidFail ? kPartFailureReliabilityIncrease : kPartReliabilityIncrease;
            if (partReliability.reliability > maxReliability)
                partReliability.reliability = maxReliability;

            // If the check failed then add a bit of Science
            if (partReliability.scienceAdded < maxScience && (HighLogic.CurrentGame.Mode == Game.Modes.CAREER || HighLogic.CurrentGame.Mode == Game.Modes.SCIENCE_SANDBOX))
            {
                ResearchAndDevelopment.Instance.AddScience(scienceToAdd, TransactionReasons.ScienceTransmission);
                
                string message = Localizer.Format("#LOC_EVAREPAIRS_scienceAdded", new string[2] { string.Format("{0:n1}", scienceToAdd), PartLoader.getPartInfoByName(partName).title } );
                ScreenMessages.PostScreenMessage(message, kMessageDuration, ScreenMessageStyle.UPPER_CENTER);
            }

            // Save updated reliability
            partReliabilities[partName] = partReliability;
        }

        public int GetReliability(string partName)
        {
            if (partReliabilities.ContainsKey(partName))
            {
                return partReliabilities[partName].reliability;
            }
            else
            {
                PartReliability partReliability = new PartReliability();
                partReliability.partName = partName;
                partReliability.reliability = startingReliability;

                partReliabilities.Add(partName, partReliability);
                return startingReliability;
            }
        }
        #endregion

        #region Helpers
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
        }
        #endregion
    }
}
