using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using KSP.Localization;
using ModuleWheels;

namespace EVARepairs.SettingsAndScenario
{
    [KSPAddon(KSPAddon.Startup.Instantly, false)]
    sealed class EVARepairsLoader : MonoBehaviour
    {
        class RepairModulesLoader : LoadingSystem
        {
            const string kModuleNameToAdd = "ModuleEVARepairs";
            const string kBotRepairModuleName = "ModuleEVABotRepairs";

            public override bool IsReady()
            {
                return true;
            }

            public override void StartLoad()
            {
                int count = PartLoader.LoadedPartsList.Count;
                AvailablePart availablePart;

                // Find the baseline config node.
                ConfigNode[] nodes = GameDatabase.Instance.GetConfigNodes("EVAREPAIRS_BASELINE_CONFIG");
                ConfigNode baselineConfig;
                if (nodes == null || nodes.Length == 0)
                    return;
                baselineConfig = nodes[0];
                if (!baselineConfig.HasNode("MODULE"))
                    return;
                baselineConfig = baselineConfig.GetNode("MODULE");
                if (!baselineConfig.HasValue("name") || baselineConfig.GetValue("name") != kModuleNameToAdd)
                    return;

                // Create the blacklist. Parts on this list won't be subjected to EVA Repairs
                nodes = GameDatabase.Instance.GetConfigNodes("EVAREPAIRS_BLACKLISTED_PARTS");
                ConfigNode node;
                string[] partNameValues;
                string partNameBlacklist = string.Empty;
                for (int index = 0; index < nodes.Length; index++)
                {
                    node = nodes[index];
                    if (node.HasValue("partName"))
                    {
                        partNameValues = node.GetValues("partName");
                        if (partNameValues.Length > 0)
                        {
                            for (int partNameIndex = 0; partNameIndex < partNameValues.Length; partNameIndex++)
                                partNameBlacklist += partNameValues[partNameIndex] + ";";
                        }
                    }
                }
                Debug.Log("[EVARepairsLoader] - Blacklisted parts: " + partNameBlacklist);

                // Now, go through each part and see if it needs to have a ModuleEVARepairs.
                for (int index = 0; index < count; index++)
                {
                    // Get the available part
                    availablePart = PartLoader.LoadedPartsList[index];

                    // Skip part if it's on the blacklist.
                    if (partNameBlacklist.Contains(availablePart.name))
                    {
                        Debug.Log("[EVARepairsLoader] - Skipping blacklisted part " + availablePart.name);
                        continue;
                    }

                    // Add repair module
                    addRepairModule(availablePart, baselineConfig);

                    // Add bot repairs module
                    addBotRepairModule(availablePart);
                }
            }

            public override string ProgressTitle()
            {
                return Localizer.Format("#LOC_EVAREPAIRS_partLoaderTitle");
            }

            private void addBotRepairModule(AvailablePart availablePart)
            {
                if (availablePart.partPrefab.HasModuleImplementing<ModuleEVABotRepairs>())
                    return;
                if (!availablePart.partPrefab.HasModuleImplementing<ModuleWheelDamage>() && !availablePart.partPrefab.HasModuleImplementing<ModuleDeployablePart>())
                    return;

                availablePart.partPrefab.AddModule(kBotRepairModuleName, true);
            }

            private void addRepairModule(AvailablePart availablePart, ConfigNode baselineConfig)
            {
                PartModule partModule;
                ConfigNode[] mtbfNodes = GameDatabase.Instance.GetConfigNodes("PART_MODULE_MTBFS");
                ConfigNode mtbfNode = null;
                if (mtbfNodes.Length > 0)
                    mtbfNode = mtbfNodes[0];

                // If the part already has ModuleEVARepairs then we're done.
                if (availablePart.partPrefab.HasModuleImplementing<ModuleEVARepairs>())
                    return;

                // If the part doesn't have one of the supported modules, then we're done.
                else if (!availablePart.partPrefab.HasModuleImplementing<ModuleGenerator>() &&
                    !availablePart.partPrefab.HasModuleImplementing<ModuleEngines>() &&
                    !availablePart.partPrefab.HasModuleImplementing<BaseConverter>() &&
                    !availablePart.partPrefab.HasModuleImplementing<ModuleReactionWheel>() &&
                    !availablePart.partPrefab.HasModuleImplementing<ModuleWheelDeployment>() &&
                    !availablePart.partPrefab.HasModuleImplementing<ModuleDeployableSolarPanel>() &&
                    !availablePart.partPrefab.HasModuleImplementing<ModuleActiveRadiator>() &&
                    !availablePart.partPrefab.HasModuleImplementing<ModuleDeployableRadiator>()
                    )
                    return;

                // Add the module and load the config.
                partModule = availablePart.partPrefab.AddModule(kModuleNameToAdd, true);
                if (partModule != null)
                {
                    partModule.Load(baselineConfig);

                    // Set MTBF
                    if (partModule is ModuleEVARepairs)
                    {
                        ModuleEVARepairs reparsModule = partModule as ModuleEVARepairs;
                        reparsModule.mtbf = getMTBF(mtbfNode, availablePart.partPrefab);
                    }
                }

                // Add module info to the prefab.
                if (partModule is IModuleInfo)
                {
                    IModuleInfo info = partModule as IModuleInfo;
                    AvailablePart.ModuleInfo moduleInfo = new AvailablePart.ModuleInfo();

                    moduleInfo.onDrawWidget = info.GetDrawModulePanelCallback();
                    moduleInfo.moduleName = info.GetModuleTitle();
                    moduleInfo.moduleDisplayName = partModule.GetModuleDisplayName();
                    moduleInfo.info = info.GetInfo().Trim();

                    availablePart.moduleInfos.Add(moduleInfo);
                    availablePart.moduleInfos.Sort(((ap1, ap2) => ap1.moduleName.CompareTo(ap2.moduleName)));
                }
            }
        }

        #region Overrides
        public void Awake()
        {
            List<LoadingSystem> loaders = LoadingScreen.Instance.loaders;
            if (loaders != null)
            {
                int count = loaders.Count;
                for (int index = 0; index < count; index++)
                {
                    if (loaders[index] is PartLoader)
                    {
                        GameObject gameObject = new GameObject();
                        RepairModulesLoader modulesLoader = gameObject.AddComponent<RepairModulesLoader>();
                        loaders.Insert(index + 1, modulesLoader);
                        break;
                    }
                }
            }
        }

        #endregion

        #region Helpers
        static double getMTBF(ConfigNode mtbfNode, Part partPrefab)
        {
            if (mtbfNode == null)
                return EVARepairsScenario.startingMTBF;

            double value = EVARepairsScenario.startingMTBF;
            if (mtbfNode.HasValue("default"))
            {
                double.TryParse(mtbfNode.GetValue("default"), out value);
            }

            if (mtbfNode.HasValue("ModuleEngines") && partPrefab.HasModuleImplementing<ModuleEngines>())
            {
                double.TryParse(mtbfNode.GetValue("ModuleEngines"), out value);
                return value;
            }
            if (mtbfNode.HasValue("ModuleGenerator") && partPrefab.HasModuleImplementing<ModuleGenerator>())
            {
                double.TryParse(mtbfNode.GetValue("ModuleGenerator"), out value);
                return value;
            }
            if (mtbfNode.HasValue("BaseConverter") && partPrefab.HasModuleImplementing<BaseConverter>())
            {
                double.TryParse(mtbfNode.GetValue("BaseConverter"), out value);
                return value;
            }
            if (mtbfNode.HasValue("ModuleReactionWheel") && partPrefab.HasModuleImplementing<ModuleReactionWheel>())
            {
                double.TryParse(mtbfNode.GetValue("ModuleReactionWheel"), out value);
                return value;
            }
            if (mtbfNode.HasValue("ModuleWheelDeployment") && partPrefab.HasModuleImplementing<ModuleWheelDeployment>())
            {
                double.TryParse(mtbfNode.GetValue("ModuleWheelDeployment"), out value);
                return value;
            }
            if (mtbfNode.HasValue("ModuleDeployableSolarPanel") && partPrefab.HasModuleImplementing<ModuleDeployableSolarPanel>())
            {
                double.TryParse(mtbfNode.GetValue("ModuleDeployableSolarPanel"), out value);
                return value;
            }
            if (mtbfNode.HasValue("ModuleActiveRadiator") && partPrefab.HasModuleImplementing<ModuleActiveRadiator>())
            {
                double.TryParse(mtbfNode.GetValue("ModuleActiveRadiator"), out value);
                return value;
            }
            if (mtbfNode.HasValue("ModuleDeployableRadiator") && partPrefab.HasModuleImplementing<ModuleDeployableRadiator>())
            {
                double.TryParse(mtbfNode.GetValue("ModuleDeployableRadiator"), out value);
                return value;
            }

            return value;
        }
        #endregion
    }
}