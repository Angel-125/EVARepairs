using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using KSP.IO;
using KSP.Localization;

namespace EVARepairs
{
    /// <summary>
    /// This is a simple part module that disables other part modules when the Mean Time Between Failures expires.
    /// </summary>
    public class ModuleEVARepairs : PartModule, IModuleInfo
    {
        #region constants
        public const float messageDuration = 5.0f;
        const string kBreakablePartModule = "breakablePartModule";
        #endregion

        #region KSPEvents
        /// <summary>
        /// Signifies that the part has been repaired.
        /// </summary>
        public static EventData<Part, ModuleEVARepairs> onPartWornOut = new EventData<Part, ModuleEVARepairs>("onPartWornOut");
        #endregion

        #region Fields
        /// <summary>
        /// A flag to enable/disable debug mode.
        /// </summary>
        [KSPField]
        public bool debugMode = false;

        /// <summary>
        /// Display string for the status.
        /// </summary>
        [KSPField(guiActive = true, guiName = "#LOC_EVAREPAIRS_status")]
        public string statusDisplay = Localizer.Format("#LOC_EVAREPAIRS_statusOK");

        /// <summary>
        /// Flag to indicate that the part needs maintenance in order to function.
        /// </summary>
        [KSPField(isPersistant = true)]
        public bool needsMaintenance = false;

        /// <summary>
        /// In hours, how long until the part needs maintenance in order to function. Default is 600. Time is counted even when the vessel isn't active!
        /// Note: The part module is smart and if the part has at least one engine or resource converter then the engine/converter needs to be running for the current mtbf to be reduced.
        /// </summary>
        [KSPField]
        public double mtbf = 600;

        /// <summary>
        /// In seconds, the current time remaining until the part needs maintenance in order to function.
        /// </summary>
        [KSPField(isPersistant = true)]
        public double currentMTBF = 600 * 3600;

        /// <summary>
        /// Percent of MTBF lost each time the part is repaired. If a part has no MTBF remaining then it has worn out and is permanently disabled. Default is 0, which means no MTBF is lost.
        /// </summary>
        [KSPField()]
        public float mtbfPercentLostPerCycle = 0;

        /// <summary>
        /// Current MTBF multiplier. It starts at 1, and is reduced by mtbfPercentLostPerCycle each time the part is repaired. When it reaches 0, the part is permanently disabled and cannot be repaired.
        /// </summary>
        [KSPField(isPersistant = true)]
        public float mtbfCurrentMultiplier = 1f;

        /// <summary>
        /// The skill required to perform repairs. Default is "RepairSkill" (Engineers have this).
        /// </summary>
        [KSPField]
        public string repairSkill = "RepairSkill";

        /// <summary>
        /// The minimum skill level required to perform repairs. Default is 1.
        /// </summary>
        [KSPField]
        public int minimumSkillLevel = 1;

        /// <summary>
        /// The part name that is consumed during repairs. It MUST be a part that can be store in an inventory. Default is evaRepairKit (the stock EVA Repair Kit).
        /// </summary>
        [KSPField]
        public string repairKitName = "evaRepairKit";

        /// <summary>
        /// The number of repair kits required to repair the part. Default is 1.
        /// </summary>
        [KSPField]
        public int repairKitsRequired = 1;

        /// <summary>
        /// Flag indicating that the part is worn out and can no longer be repaired.
        /// </summary>
        [KSPField(isPersistant = true)]
        public bool partWornOut = false;

        /// <summary>
        /// Determines when the last time the part module was updated.
        /// </summary>
        [KSPField(isPersistant = true)]
        public double lastUpdated = 0;

        /// <summary>
        /// Flag indicating that the MTBF will be automatically updated. If set to false then an external party will need to call UpdateMTBF.
        /// </summary>
        [KSPField]
        public bool autoUpdatesEnabled = true;

        /// <summary>
        /// If Reliability is enabled, this is the part's reliability that is factored into its activation check.
        /// </summary>
        [KSPField(isPersistant = true)]
        public int reliability = -1;

        /// <summary>
        /// Shows the current reliability rating, reflected by currentMTBF/maxMTBF and, if enabled, the part's Reliability rating.
        /// </summary>
        [KSPField(guiActive = true, guiActiveEditor = true, guiUnits = "%", guiName = "#LOC_EVAREPAIRS_reliability")]
        public string reliabilityDisplay = string.Empty;
        #endregion

        #region Housekeeping
        List<BaseConverter> converters = null;
        List<ModuleGenerator> generators = null;
        List<ModuleEngines> engines = null;
        double mtbfRateMultiplier = 1f;
        #endregion

        #region IModuleInfo
        public string GetModuleTitle()
        {
            return Localizer.Format("#LOC_EVAREPAIRS_repairModuleTitle");
        }

        public Callback<Rect> GetDrawModulePanelCallback()
        {
            return null;
        }

        public string GetPrimaryField()
        {
            StringBuilder info = new StringBuilder();
            info.AppendLine(Localizer.Format("#LOC_EVAREPAIRS_infoMTBF", new string[1] { string.Format("{0:n1}", mtbf) }));
            return info.ToString();
        }

        public override string GetModuleDisplayName()
        {
            return GetModuleTitle();
        }

        public override string GetInfo()
        {
            StringBuilder info = new StringBuilder();

            info.AppendLine(Localizer.Format("#LOC_EVAREPAIRS_infoDesc"));
            info.AppendLine(" ");
            info.AppendLine(Localizer.Format("#LOC_EVAREPAIRS_infoMaintenance"));
            info.AppendLine(Localizer.Format("#LOC_EVAREPAIRS_infoMTBF", new string[1] { string.Format("{0:n1}", mtbf) }));
            info.AppendLine(Localizer.Format("#LOC_EVAREPAIRS_infoRepairSkill", new string[1] { repairSkill }));
            info.AppendLine(Localizer.Format("#LOC_EVAREPAIRS_infoRepairRating", new string[1] { minimumSkillLevel.ToString() }));
            info.AppendLine(Localizer.Format("#LOC_EVAREPAIRS_infoKitsRequired", new string[1] { repairKitsRequired.ToString() }));

            return info.ToString();
        }
        #endregion

        #region Events
        /// <summary>
        /// Performs maintenance on the part.
        /// </summary>
        [KSPEvent(guiName = "#LOC_EVAREPAIRS_repairPart", externalToEVAOnly = false, guiActiveUnfocused = true, unfocusedRange = 25)]
        public virtual void RepairPart()
        {
            if (CanRepairPart(repairSkill, minimumSkillLevel, repairKitName, repairKitsRequired))
            {
                ConsumeRepairKits(FlightGlobals.ActiveVessel, repairKitName, repairKitsRequired);
                RestorePartFunctionality();
            }
        }

        /// <summary>
        /// Debug event to break the part.
        /// </summary>
        [KSPEvent(guiName = "(Debug) break part", guiActive = true)]
        public virtual void DebugBreakPart()
        {
            needsMaintenance = false;
            currentMTBF = TimeWarp.fixedDeltaTime;
        }

        /// <summary>
        /// Debug event to break the part.
        /// </summary>
        [KSPEvent(guiName = "(Debug) wear out part", guiActive = true)]
        public virtual void DebugWearOutPart()
        {
            needsMaintenance = false;
            currentMTBF = TimeWarp.fixedDeltaTime;
            mtbfCurrentMultiplier = 0;
        }

        /// <summary>
        /// Debug event to break the part.
        /// </summary>
        [KSPEvent(guiName = "(Debug) repair part", guiActive = true)]
        public virtual void DebugRepairPart()
        {
            RestorePartFunctionality();

            // Restore MTBF fully
            currentMTBF = mtbf * 3600;
            mtbfCurrentMultiplier = 1f;
            partWornOut = false;
        }
        #endregion

        #region GameEvent Handlers
        #endregion

        #region API
        /// <summary>
        /// Restores part functionality.
        /// </summary>
        public virtual void RestorePartFunctionality()
        {
            needsMaintenance = false;
            currentMTBF = mtbf * 3600;

            // If parts can wear out, then adjust the current multiplier.
            if (EVARepairsScenario.partsCanWearOut && mtbfPercentLostPerCycle > 0)
            {
                currentMTBF *= mtbfCurrentMultiplier;

                // Reduce the multiplier.
                mtbfCurrentMultiplier -= (mtbfPercentLostPerCycle / 100f);

                // The next time that the part breaks, it won't be repairable.
                if (mtbfCurrentMultiplier <= 0)
                    mtbfCurrentMultiplier = 0;
            }

            string message = Localizer.Format("#LOC_EVAREPAIRS_partRepaired", new string[1] { part.partInfo.title });
            ScreenMessages.PostScreenMessage(message, messageDuration, ScreenMessageStyle.UPPER_CENTER);

            statusDisplay = Localizer.Format("#LOC_EVAREPAIRS_statusOK");

            Events["RepairPart"].active = false;
            GameEvents.onPartRepaired.Fire(part);
            EnablePartModules();
        }

        /// <summary>
        /// Determines whether or not we can update MTBF at this time.
        /// </summary>
        /// <returns>true if we can, false if not.</returns>
        public virtual bool CanUpdateMTBF()
        {
            // If EVA Repairs is disabled, or the part needs maintenance, or the part is worn out, then we can't update.
            if (!EVARepairsScenario.maintenanceEnabled || needsMaintenance || partWornOut)
                return false;

            // If the part has no engine, then we can update.
            if (engines.Count == 0 && generators.Count == 0 && converters.Count == 0)
                return true;

            // If the part has an active engine, generator, or converter, then we can update.
            int count = engines.Count;
            for (int index = 0; index < count; index++)
            {
                if (engines[index].isOperational)
                    return true;
            }

            // If the part has an active converter, then we can update.
            count = converters.Count;
            for (int index = 0; index < count; index++)
            {
                if ((converters[index].moduleIsEnabled && converters[index].IsActivated) || converters[index].AlwaysActive)
                    return true;
            }

            // If the part has an active generator, then we can update.
            count = generators.Count;
            for (int index = 0; index < count; index++)
            {
                if (generators[index].isActiveAndEnabled || generators[index].isAlwaysActive)
                    return true;
            }

            // The part has an engine, generator, and/or a converter, but none are currently active, so we can't update.
            return false;
        }

        /// <summary>
        /// Updates the rate multiplier that is applied when decrementing MTBF.
        /// </summary>
        /// <param name="rateMultiplier">The rate multiplier to multiply the current MTBF by.</param>
        public virtual void SetRateMultiplier(double rateMultiplier)
        {
            mtbfRateMultiplier = rateMultiplier;
        }

        /// <summary>
        /// Updates the mean time between failures.
        /// </summary>
        public virtual void UpdateMTBF(double elapsedTime)
        {
            if (!CanUpdateMTBF())
                return;

            // Account for time spent away...
            currentMTBF -= (elapsedTime * mtbfRateMultiplier);
            lastUpdated = Planetarium.GetUniversalTime();

            if (currentMTBF <= 0)
            {
                // If the part hasn't worn out yet then it just needs maintenance.
                if (mtbfCurrentMultiplier > 0)
                {
                    needsMaintenance = true;

                    statusDisplay = Localizer.Format("#LOC_EVAREPAIRS_needsMaintenance");

                    Events["RepairPart"].active = true;

                    string message = Localizer.Format("#LOC_EVAREPAIRS_partNeedsMaintenance", new string[1] { part.partInfo.title });
                    ScreenMessages.PostScreenMessage(message, messageDuration, ScreenMessageStyle.UPPER_LEFT);
                }

                // Part has worn out and can no longer be repaired.
                else
                {
                    partWornOut = true;
                    statusDisplay = Localizer.Format("#LOC_EVAREPAIRS_statusBroken");

                    string message = Localizer.Format("#LOC_EVAREPAIRS_partWornOut", new string[1] { part.partInfo.title });
                    ScreenMessages.PostScreenMessage(message, messageDuration, ScreenMessageStyle.UPPER_LEFT);

                    onPartWornOut.Fire(part, this);
                }

                GameEvents.onPartFailure.Fire(part);
                DisablePartModules();
            }
        }

        /// <summary>
        /// Determines whether or not the part can be repaired.
        /// </summary>
        /// <param name="maintenanceSkill">A string containing the required repair skill.</param>
        /// <param name="minimumSkillLevel">An int containing the minimum skill level required.</param>
        /// <param name="repairKitName">A string containing the name of the repair kit part.</param>
        /// <param name="repairKitsRequired">An int containing the number of repair kits required.</param>
        /// <returns></returns>
        public virtual bool CanRepairPart(string maintenanceSkill = "RepairSkill", int minimumSkillLevel = 1, string repairKitName = "evaRepairKit", int repairKitsRequired = 1)
        {
            // Make sure that we have sufficient skill
            if (!hasSufficientSkill(FlightGlobals.ActiveVessel, maintenanceSkill, minimumSkillLevel))
            {
                string message = Localizer.Format("#LOC_EVAREPAIRS_insufficientSkill", new string[1] { minimumSkillLevel.ToString() });
                ScreenMessages.PostScreenMessage(message, messageDuration, ScreenMessageStyle.UPPER_CENTER);
                return false;
            }

            // Make sure that we have sufficient repair kits.
            if (!hasEnoughRepairKits(FlightGlobals.ActiveVessel, repairKitsRequired, repairKitName))
            {
                string message = Localizer.Format("#LOC_EVAREPAIRS_insufficientKits", new string[1] { repairKitsRequired.ToString() });
                ScreenMessages.PostScreenMessage(message, messageDuration, ScreenMessageStyle.UPPER_CENTER);
                return false;
            }

            // A-OK
            return true;
        }

        /// <summary>
        /// Consumes repair kits.
        /// </summary>
        /// <param name="vessel">The Vessel to consume the kits from</param>
        /// <param name="repairKitName">A string containing the name of the repair part.</param>
        /// <param name="amount">An int containing the number of kits to consume.</param>
        public virtual void ConsumeRepairKits(Vessel vessel, string repairKitName = "evaRepairKit", int amount = 1)
        {
            List<ModuleInventoryPart> inventories = vessel.FindPartModulesImplementing<ModuleInventoryPart>();
            ModuleInventoryPart inventory;
            int count = inventories.Count;
            int repairPartsFound = 0;
            int repairPartsRemaining = amount;

            for (int index = 0; index < count; index++)
            {
                inventory = inventories[index];

                if (inventory.ContainsPart(repairKitName))
                {
                    repairPartsFound += inventory.TotalAmountOfPartStored(repairKitName);

                    if (repairPartsFound >= repairPartsRemaining)
                    {
                        inventory.RemoveNPartsFromInventory(repairKitName, repairPartsRemaining, true);
                        break;
                    }
                    else
                    {
                        repairPartsRemaining -= repairPartsFound;
                        inventory.RemoveNPartsFromInventory(repairKitName, repairPartsFound, true);
                    }
                }
            }
        }

        /// <summary>
        /// Disables the part modules that EVA Repairs is responsible for.
        /// </summary>
        public virtual void DisablePartModules()
        {
            ConfigNode node = getPartConfigNode();
            if (node == null || !node.HasValue(kBreakablePartModule))
                return;

            string[] breakbleModuleNames = node.GetValues(kBreakablePartModule);
            int moduleCount = part.Modules.Count;
            PartModule module;
            bool disableModule = false;
            for (int index = 0; index < moduleCount; index++)
            {
                module = part.Modules[index];

                // Shutdown the engine
                if (module is ModuleEngines)
                {
                    ModuleEngines engine = (ModuleEngines)module;
                    if (engine.isOperational)
                    {
                        // If the engine can be shut down, then shut it down. Otherwise, decouple the part.
                        if (engine.allowShutdown)
                        {
                            engine.Shutdown();
                            disableModule = true;
                        }
                        else
                        {
                            part.decouple();
                        }
                    }
                }

                // Shutdown the generator
                if (module is ModuleGenerator)
                {
                    ModuleGenerator generator = (ModuleGenerator)module;
                    if (!generator.isAlwaysActive)
                        generator.Shutdown();
                    disableModule = true;
                }

                // Shutdown the converter
                if (module is BaseConverter)
                {
                    BaseConverter converter = (BaseConverter)module;
                    if (!converter.AlwaysActive && converter.IsActivated)
                        converter.StopResourceConverter();
                    disableModule = true;
                }

                // Now handle modules on our disable list.
                if (!disableModule && (module == this || !breakbleModuleNames.Contains(module.moduleName)))
                    continue;

                module.OnInactive();
                module.enabled = false;
                module.isEnabled = false;
                disableModule = false;
            }
        }

        /// <summary>
        /// Enables the part modules that EVA Repairs is responsible for.
        /// </summary>
        public virtual void EnablePartModules()
        {
            ConfigNode node = getPartConfigNode();
            if (node == null || !node.HasValue(kBreakablePartModule))
                return;

            string[] breakbleModuleNames = node.GetValues(kBreakablePartModule);
            int moduleCount = part.Modules.Count;
            PartModule module;
            bool enableModule = false;
            for (int index = 0; index < moduleCount; index++)
            {
                module = part.Modules[index];
                if (module is ModuleEngines || module is ModuleGenerator || module is BaseConverter)
                    enableModule = true;

                if (!enableModule && (module == this || !breakbleModuleNames.Contains(module.moduleName)))
                    continue;

                module.enabled = true;
                module.isEnabled = true;
                // You'll need to manually restart engines...
                if (!(module is ModuleEngines))
                    module.OnActive();
                enableModule = false;
            }

        }
        #endregion

        #region Overrides
        public override void OnActive()
        {
            base.OnActive();
            if (!EVARepairsScenario.maintenanceEnabled || !EVARepairsScenario.canFailOnActivation || needsMaintenance || partWornOut)
                return;

            // Get target number
            int targetNumber = calculateReliabilityTarget();

            // Make the check
            int dieRoll = UnityEngine.Random.Range(1, 100);
            bool checkFailed = false;
            if (dieRoll <= targetNumber)
            {
                // Immediately run out of MTBF. This will trigger part failure.
                currentMTBF = TimeWarp.fixedDeltaTime;
                checkFailed = true;
            }

            // Update reliability
            if (EVARepairsScenario.reliabilityEnabled)
                EVARepairsScenario.shared.UpdateReliability(part.partName, checkFailed);
        }

        public override void OnFixedUpdate()
        {
            base.OnFixedUpdate();
            if (!HighLogic.LoadedSceneIsFlight || !autoUpdatesEnabled)
                return;

            // Account for time spent away...
            double elapsedTime = Planetarium.GetUniversalTime() - lastUpdated;
            UpdateMTBF(elapsedTime);

            reliabilityDisplay = calculateReliabilityTarget().ToString();
        }

        public void OnDestroy()
        {
            if (HighLogic.LoadedSceneIsFlight)
            {
                GameEvents.OnGameSettingsApplied.Remove(onGameSettingsApplied);
            }
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            findEnginesAndConverters();

            if (HighLogic.LoadedSceneIsFlight)
            {
                GameEvents.OnGameSettingsApplied.Add(onGameSettingsApplied);
            }

            // Check to see if EVA Repairs is enabled.
            if (!EVARepairsScenario.maintenanceEnabled)
            {
                needsMaintenance = false;
                Events["RepairPart"].active = false;
                Fields["statusDisplay"].guiActive = false;
                Fields["statusDisplay"].guiActiveEditor = false;
                Fields["reliabilityDisplay"].guiActive = false;
                Fields["reliabilityDisplay"].guiActiveEditor = false;
                return;
            }

            // Make sure we have a starting value for last updated.
            if (lastUpdated <= 0 && HighLogic.LoadedSceneIsFlight)
                lastUpdated = Planetarium.GetUniversalTime();

            // Set the part reliability if needed.
            // In the editor, make sure that the part reflects the current flight experience.
            if (HighLogic.LoadedSceneIsEditor)
            {
                int baseReliability = EVARepairsScenario.shared.GetReliability(part.partName);
                if (reliability < baseReliability)
                    reliability = baseReliability;
            }

            // Account for in-field parts that haven't been initialized with reliability.
            if (reliability <= 0)
                reliability = EVARepairsScenario.shared.GetReliability(part.partName);

            // Setup GUI
            Events["DebugBreakPart"].active = debugMode;
            Events["DebugRepairPart"].active = debugMode;
            Events["DebugWearOutPart"].active = debugMode;
            Fields["mtbfCurrentMultiplier"].guiActive = debugMode;
            Fields["currentMTBF"].guiActive = debugMode;
            Fields["reliabilityDisplay"].guiActive = EVARepairsScenario.reliabilityEnabled;
            Fields["reliabilityDisplay"].guiActiveEditor = EVARepairsScenario.reliabilityEnabled;
            if (!partWornOut)
            {
                statusDisplay = needsMaintenance ? Localizer.Format("#LOC_EVAREPAIRS_needsMaintenance") : Localizer.Format("#LOC_EVAREPAIRS_statusOK");
                Events["RepairPart"].active = needsMaintenance;
                Events["RepairPart"].guiName = Localizer.Format("#LOC_EVAREPAIRS_repairPart", new string[1] { part.partInfo.title });
            }
            else
            {
                statusDisplay = Localizer.Format("#LOC_EVAREPAIRS_statusBroken");
                Events["RepairPart"].active = false;
            }

            // Disable the part if it needs maintenance or is worn out.
            if (needsMaintenance || partWornOut)
            {
                DisablePartModules();
            }

            reliabilityDisplay = calculateReliabilityTarget().ToString();
        }
        #endregion

        #region Helpers
        private int calculateReliabilityTarget()
        {
            // Get target number
            int targetNumber = (int)(100 * (currentMTBF / 3600) / mtbf);

            // Account for reliability
            if (EVARepairsScenario.reliabilityEnabled && reliability < EVARepairsScenario.maxReliability)
            {
                if (reliability <= 0)
                    reliability = EVARepairsScenario.shared.GetReliability(part.partName);

                targetNumber = (int)(targetNumber * (reliability / 100f));
            }

            return targetNumber;
        }

        private void onGameSettingsApplied()
        {
            bool maintenanceEnabled = EVARepairsSettings.MaintenanceEnabled;
            bool reliabilityEnabled = EVARepairsSettings.ReliabilityEnabled;
            if (!maintenanceEnabled)
            {
                needsMaintenance = false;
            }

            Events["RepairPart"].active = needsMaintenance;
            Fields["statusDisplay"].guiActive = maintenanceEnabled;
            Fields["statusDisplay"].guiActiveEditor = maintenanceEnabled;
            Fields["reliabilityDisplay"].guiActive = reliabilityEnabled;
            Fields["reliabilityDisplay"].guiActiveEditor = reliabilityEnabled;

            // Make sure that Reliability is at the minimum starting value.
            int startingReliability = EVARepairsSettings.StartingReliability;
            if (reliability < startingReliability)
                reliability = startingReliability;

            // If parts no longer wear out and the part is worn out, then make it repairable.
            bool partsWearOut = EVARepairsSettings.PartsCanWearOut;
            if (!partsWearOut && partWornOut)
            {
                partWornOut = false;
                needsMaintenance = false;
                currentMTBF = mtbf * 3600f;
            }
        }

        private void findEnginesAndConverters()
        {
            engines = part.FindModulesImplementing<ModuleEngines>();
            generators = part.FindModulesImplementing<ModuleGenerator>();
            converters = part.FindModulesImplementing<BaseConverter>();
        }

        private ConfigNode getPartConfigNode()
        {
            if (!HighLogic.LoadedSceneIsEditor && !HighLogic.LoadedSceneIsFlight)
                return null;
            if (this.part.partInfo.partConfig == null)
                return null;
            ConfigNode[] nodes = this.part.partInfo.partConfig.GetNodes("MODULE");
            ConfigNode partConfigNode = null;
            ConfigNode node = null;
            string moduleName;

            //Get the switcher config node.
            for (int index = 0; index < nodes.Length; index++)
            {
                node = nodes[index];
                if (node.HasValue("name"))
                {
                    moduleName = node.GetValue("name");
                    if (moduleName == this.ClassName)
                    {
                        partConfigNode = node;
                        break;
                    }
                }
            }

            return partConfigNode;
        }

        private bool hasEnoughRepairKits(Vessel vessel, int repairKitsRequired, string repairKitName = "evaRepairKit")
        {
            List<ModuleInventoryPart> inventories = vessel.FindPartModulesImplementing<ModuleInventoryPart>();
            int count = inventories.Count;
            int repairPartsFound = 0;

            for (int index = 0; index < count; index++)
            {
                if (inventories[index].ContainsPart(repairKitName))
                {
                    repairPartsFound += inventories[index].TotalAmountOfPartStored(repairKitName);
                    if (repairPartsFound >= repairKitsRequired)
                        return true;
                }
            }

            return false;
        }

        private bool hasSufficientSkill(Vessel vessel, string maintenanceSkill, int minimumSkillLevel)
        {
            ProtoCrewMember astronaut;
            int highestSkill = 0;

            // Make sure that we have sufficient skill.
            if (vessel.FindPartModuleImplementing<ModuleEVARepairBot>())
                return true;
            else if (vessel.isEVA)
                highestSkill = getHighestRank(vessel, maintenanceSkill, out astronaut);
            else
                highestSkill = getHighestRank(vessel, maintenanceSkill, out astronaut);

            if (highestSkill < minimumSkillLevel)
                return false;

            return true;
        }

        public int getHighestRank(Vessel vessel, string skillName, out ProtoCrewMember astronaut)
        {
            astronaut = null;
            if (string.IsNullOrEmpty(skillName))
                return 0;
            try
            {
                if (vessel.GetCrewCount() == 0)
                    return 0;
            }
            catch
            {
                return 0;
            }

            string[] skillsToCheck = skillName.Split(new char[] { ';' });
            string checkSkill;
            int highestRank = 0;
            int crewRank = 0;
            bool hasABadass = false;
            bool hasAVeteran = false;
            bool hasAHero = false;
            for (int skillIndex = 0; skillIndex < skillsToCheck.Length; skillIndex++)
            {
                checkSkill = skillsToCheck[skillIndex];

                //Find the highest racking kerbal with the desired skill (if any)
                ProtoCrewMember[] vesselCrew = vessel.GetVesselCrew().ToArray();
                for (int index = 0; index < vesselCrew.Length; index++)
                {
                    if (vesselCrew[index].HasEffect(checkSkill))
                    {
                        if (vesselCrew[index].isBadass)
                            hasABadass = true;
                        if (vesselCrew[index].veteran)
                            hasAVeteran = true;
                        if (vesselCrew[index].isHero)
                            hasAHero = true;
                        crewRank = vesselCrew[index].experienceTrait.CrewMemberExperienceLevel();
                        if (crewRank > highestRank)
                        {
                            highestRank = crewRank;
                            astronaut = vesselCrew[index];
                        }
                    }
                }
            }

            if (hasABadass)
                highestRank += 1;
            if (hasAVeteran)
                highestRank += 1;
            if (hasAHero)
                highestRank += 1;

            return highestRank;
        }
        #endregion
    }
}
