﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using KSP.IO;
using KSP.Localization;
using System.Reflection;
using ModuleWheels;

namespace EVARepairs
{
    #region ReactionWheelState
    internal class ReactionWheelState
    {
        public float rollTorque = 0;
        public float pitchTorque = 0;
        public float yawTorque = 0;
        public ModuleReactionWheel reactionWheel = null;

        public ReactionWheelState(ModuleReactionWheel wheel)
        {
            reactionWheel = wheel;
            rollTorque = reactionWheel.RollTorque;
            pitchTorque = reactionWheel.PitchTorque;
            yawTorque = reactionWheel.YawTorque;
        }

        public void DisableModule()
        {
            int roll = UnityEngine.Random.Range(1, 100);

            if (roll >= 75)
            {
                reactionWheel.OnInactive();
                reactionWheel.enabled = false;
                reactionWheel.isEnabled = false;
            }
            else if (roll >= 50)
            {
                reactionWheel.YawTorque = 0;
            }
            else if (roll >= 25)
            {
                reactionWheel.PitchTorque = 0;
            }
            else
            {
                reactionWheel.RollTorque = 0;
            }
        }

        public void EnableModule()
        {
            if (!reactionWheel.enabled)
            {
                reactionWheel.enabled = true;
                reactionWheel.isEnabled = true;
                reactionWheel.RollTorque = rollTorque;
                reactionWheel.PitchTorque = pitchTorque;
                reactionWheel.YawTorque = yawTorque;
                reactionWheel.OnActive();
            }
        }

        public bool isEnabled
        {
            get
            {
                return reactionWheel.wheelState == ModuleReactionWheel.WheelState.Active;
            }
        }
    }
    #endregion

    /// <summary>
    /// This is a simple part module that disables other part modules when the Mean Time Between Failures expires.
    /// </summary>
    public class ModuleEVARepairs : PartModule, IModuleInfo
    {
        #region constants
        public const float messageDuration = 5.0f;
        const string kBreakablePartModule = "breakablePartModule";
        const float kOverhaulThreshold = 0.2f;

        // Bonus to activation roll when throttling engines up
        const int kThrottleActivationBonus = 25;
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

        [KSPField]
        public bool updateReliability = true;

        /// <summary>
        /// Display string for the status.
        /// </summary>
        [KSPField(guiActive = true, guiName = "#LOC_EVAREPAIRS_status", groupName = "#LOC_EVAREPAIRS_groupTitleName", groupDisplayName = "#LOC_EVAREPAIRS_groupTitleName")]
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
        [KSPField(isPersistant = true)]
        public double mtbf = -1;

        /// <summary>
        /// In seconds, the current time remaining until the part needs maintenance in order to function.
        /// </summary>
        [KSPField(isPersistant = true, groupName = "#LOC_EVAREPAIRS_groupTitleName", groupDisplayName = "#LOC_EVAREPAIRS_groupTitleName")]
        public double currentMTBF = -1f;

        /// <summary>
        /// Percent of MTBF lost each time the part is repaired. If a part has no MTBF remaining then it has worn out and is permanently disabled. Default is 0, which means no MTBF is lost.
        /// </summary>
        [KSPField()]
        public float mtbfPercentLostPerCycle = 0;

        /// <summary>
        /// Current MTBF multiplier. It starts at 1, and is reduced by mtbfPercentLostPerCycle each time the part is repaired. When it reaches 0, the part is permanently disabled and cannot be repaired.
        /// </summary>
        [KSPField(isPersistant = true, groupName = "#LOC_EVAREPAIRS_groupTitleName", groupDisplayName = "#LOC_EVAREPAIRS_groupTitleName")]
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
        /// The minimum skill level required to perform overhauls. Default is 3.
        /// </summary>
        [KSPField]
        public int minimumOverhaulSkillLevel = 3;

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
        /// The number of repair kits required to overhaul the part. Default is 4.
        /// </summary>
        [KSPField]
        public int repairKitsRequiredOverhaul = 4;

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
        [KSPField(guiActive = true, guiActiveEditor = true, guiName = "#LOC_EVAREPAIRS_mtbfTitle", groupName = "#LOC_EVAREPAIRS_groupTitleName", groupDisplayName = "#LOC_EVAREPAIRS_groupTitleName")]
        public string mtbfDisplay = string.Empty;

        /// <summary>
        /// Animation position of a stuck wheel.
        /// </summary>
        [KSPField(isPersistant = true)]
        public float wheelStuckPosition = -1f;

        /// <summary>
        /// The resource required to overhaul the part. Default is ore.
        /// </summary>
        [KSPField]
        public string overhaulResource = "Ore";
        #endregion

        #region Housekeeping
        [KSPField(groupName = "#LOC_EVAREPAIRS_groupTitleName", groupDisplayName = "#LOC_EVAREPAIRS_groupTitleName")]
        double mtbfRateMultiplier = 1f;

        [KSPField(groupName = "#LOC_EVAREPAIRS_groupTitleName", groupDisplayName = "#LOC_EVAREPAIRS_groupTitleName")]
        bool partDidFail = false;

        [KSPField(groupName = "#LOC_EVAREPAIRS_groupTitleName", groupDisplayName = "#LOC_EVAREPAIRS_groupTitleName")]
        int activationRoll;

        List<BaseConverter> converters = null;
        List<ModuleGenerator> generators = null;
        List<ModuleEngines> engines = null;
        List<bool> engineStates = null;
        List<bool> converterStates = null;
        List<ModuleDeployableSolarPanel> solarPanels = null;
        ReactionWheelState reactionWheelState = null;
        ModuleActiveRadiator radiator = null;
        ModuleDeployableRadiator deployableRadiator = null;
        bool sasIsActive = false;
        bool sasWasActive = false;
        public ModuleWheelDeployment wheelDeployment = null;
        float wheelDeployPosition = 0f;
        float wheelRetractPosition = 0f;
        KFSMState previousWheelState = null;

        KSPActionGroup wheelActionGroup;
        [KSPField(isPersistant = true)]
        public string actionGroupId = string.Empty;
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
        /// Repairs the part.
        /// </summary>
        [KSPEvent(guiName = "#LOC_EVAREPAIRS_repairPart", externalToEVAOnly = false, guiActiveUnfocused = true, unfocusedRange = 25, groupName = "#LOC_EVAREPAIRS_groupTitleName", groupDisplayName = "#LOC_EVAREPAIRS_groupTitleName")]
        public virtual void RepairPart()
        {
            if (CanRepairPart(repairSkill, minimumSkillLevel, repairKitName, repairKitsRequired))
            {
                ConsumeRepairKits(FlightGlobals.ActiveVessel, repairKitName, repairKitsRequired);
                RestorePartFunctionality();
            }
        }

        /// <summary>
        /// Repairs the part.
        /// </summary>
        [KSPEvent(guiName = "#LOC_EVAREPAIRS_overhaulPart", externalToEVAOnly = false, guiActiveUnfocused = true, unfocusedRange = 25, groupName = "#LOC_EVAREPAIRS_groupTitleName", groupDisplayName = "#LOC_EVAREPAIRS_groupTitleName")]
        public virtual void OverhaulPart()
        {
            double overhaulResourceAmount = getResourceAmountForOverhaul();
            double amount = 0;
            double maxAmount = 0;

            // Get the vessel's resource totals for the overhaul resource.
            PartResourceDefinition definition = getOverhaulResourceDefinition();
            if (definition != null)
                part.vessel.GetConnectedResourceTotals(definition.id, out amount, out maxAmount);

            // Make sure that we have enough of the overhaul resource
            if (amount < overhaulResourceAmount)
            {
                if (definition != null)
                {
                    string message = Localizer.Format("#LOC_EVAREPAIRS_overhaulInsufficientResources", new string[1] { definition.displayName }) + " " + part.partInfo.title;
                    ScreenMessages.PostScreenMessage(message, messageDuration, ScreenMessageStyle.UPPER_CENTER);
                }
                return;
            }

            // Now check for sufficient repair kits and skill.
            // If we're all good then consume the repair kits and overhaul resource, and then restore max MTBF.
            if (CanRepairPart(repairSkill, minimumOverhaulSkillLevel, repairKitName, repairKitsRequiredOverhaul))
            {
                ConsumeRepairKits(FlightGlobals.ActiveVessel, repairKitName, repairKitsRequired);
                if (definition != null)
                    part.RequestResource(definition.id, overhaulResourceAmount);
                mtbfCurrentMultiplier = 1f;
                RestorePartFunctionality();
                partWornOut = false;
                Events["OverhaulPart"].active = false;
                Events["OverhaulPart"].guiActive = false;
            }
        }

        /// <summary>
        /// Debug event to break the part.
        /// </summary>
        [KSPEvent(guiName = "(Debug) break part", guiActive = true, guiActiveUncommand = true, groupName = "#LOC_EVAREPAIRS_groupTitleName", groupDisplayName = "#LOC_EVAREPAIRS_groupTitleName")]
        public virtual void DebugBreakPart()
        {
            needsMaintenance = false;
            currentMTBF = 0;
            updateMaintenanceStatus();
            updateReliabilityDisplay();
        }

        /// <summary>
        /// Debug event to break the part.
        /// </summary>
        [KSPEvent(guiName = "(Debug) wear out part", guiActive = true, guiActiveUncommand = true, groupName = "#LOC_EVAREPAIRS_groupTitleName", groupDisplayName = "#LOC_EVAREPAIRS_groupTitleName")]
        public virtual void DebugWearOutPart()
        {
            needsMaintenance = false;
            currentMTBF = 0;
            mtbfCurrentMultiplier = 0f;
            partWornOut = true;
            updateMaintenanceStatus();
            updateReliabilityDisplay();
        }

        /// <summary>
        /// Debug event to break the part.
        /// </summary>
        [KSPEvent(guiName = "(Debug) repair part", guiActive = true, guiActiveUncommand = true, groupName = "#LOC_EVAREPAIRS_groupTitleName", groupDisplayName = "#LOC_EVAREPAIRS_groupTitleName")]
        public virtual void DebugRepairPart()
        {
            RestorePartFunctionality();

            // Restore MTBF fully
            mtbfCurrentMultiplier = 1f;
            partWornOut = false;
        }

        [KSPEvent(guiName = "(Debug) Enable Failures", guiActive = true, guiActiveUncommand = true, groupName = "#LOC_EVAREPAIRS_groupTitleName", groupDisplayName = "#LOC_EVAREPAIRS_groupTitleName")]
        public virtual void DebugEnableFailures()
        {
            Debug.Log("[ModuleEVARepairs] - Enabling " + part.partInfo.name);
            EVARepairsScenario.shared.EnableFailuresFor(part);

            int count = EditorLogic.fetch.ship.parts.Count;
            List<Part> shipParts = EditorLogic.fetch.ship.parts;
            Part shipPart;
            for (int index = 0; index < count; index++)
            {
                shipPart = shipParts[index];
                if (shipPart.partInfo.name == part.partInfo.name)
                {
                    FailuresEnabled();
                }
            }
        }

        [KSPEvent(guiName = "(Debug) Disable Failures", guiActive = true, guiActiveUncommand = true, groupName = "#LOC_EVAREPAIRS_groupTitleName", groupDisplayName = "#LOC_EVAREPAIRS_groupTitleName")]
        public virtual void DebugDisableFailures()
        {
            Debug.Log("[ModuleEVARepairs] - Disabling " + part.partInfo.name);
            
            EVARepairsScenario.shared.DisableFailuresFor(part);

            int count = EditorLogic.fetch.ship.parts.Count;
            List<Part> shipParts = EditorLogic.fetch.ship.parts;
            Part shipPart;
            for (int index = 0; index < count; index++)
            {
                shipPart = shipParts[index];
                if (shipPart.partInfo.name == part.partInfo.name)
                {
                    FailuresDisabled();
                }
            }
        }
        #endregion

        #region GameEvent Handlers
        #endregion

        #region API
        public void FailuresEnabled()
        {
            updatePartEnabledDisabledUI();
            Events["RepairPart"].active = true;
            Events["DebugEnableFailures"].guiActiveEditor = false;
            Events["DebugDisableFailures"].guiActiveEditor = true;
            Fields["mtbfDisplay"].guiActive = true;
            Fields["mtbfDisplay"].guiActiveEditor = true;
            Fields["statusDisplay"].guiActiveEditor = true;
            statusDisplay = "Enabled";
        }

        public void FailuresDisabled()
        {
            updatePartEnabledDisabledUI();
            Events["RepairPart"].active = false;
            Events["DebugEnableFailures"].guiActiveEditor = true;
            Events["DebugDisableFailures"].guiActiveEditor = false;
            Fields["mtbfDisplay"].guiActive = false;
            Fields["mtbfDisplay"].guiActiveEditor = false;
            Fields["statusDisplay"].guiActiveEditor = true;
            statusDisplay = "Disabled";
        }

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

                // The next time that the part breaks, it won't be repairable - unless it's overhauled!
                if (mtbfCurrentMultiplier <= 0)
                    mtbfCurrentMultiplier = 0;
            }

            string message = Localizer.Format("#LOC_EVAREPAIRS_partRepaired", new string[1] { part.partInfo.title });
            ScreenMessages.PostScreenMessage(message, messageDuration, ScreenMessageStyle.UPPER_CENTER);

            statusDisplay = Localizer.Format("#LOC_EVAREPAIRS_statusOK");

            Events["RepairPart"].active = false;
            Events["RepairPart"].guiActive = false;
            GameEvents.onPartRepaired.Fire(part);
            EnablePartModules();
        }

        /// <summary>
        /// Determines whether or not we can update MTBF at this time.
        /// </summary>
        /// <returns>true if we can, false if not.</returns>
        public virtual bool CanUpdateMTBF()
        {
            int count = 0;

            if (EVARepairsScenario.debugMode)
                Debug.Log("[ModuleEVARepairs] - CanUpdateMTBF: Checking part " + part.partInfo.name);

            // If EVA Repairs is disabled, or the part needs maintenance, or the part is worn out, then we can't update.
            if (!EVARepairsScenario.maintenanceEnabled || needsMaintenance || partWornOut)
                return false;

            // If the part has deployable landing gear or legs, then we can update.
            if (EVARepairsScenario.landingGearCanFail && wheelDeployment != null)
            {
                if (EVARepairsScenario.debugMode)
                    Debug.Log("[ModuleEVARepairs] - CanUpdateMTBF: Has breakable wheels");
                if (wheelDeployment.fsm.CurrentState == wheelDeployment.st_deploying || wheelDeployment.fsm.CurrentState == wheelDeployment.st_retracting)
                    return true;
            }

            // If the part has an active reaction wheel, then we can update.
            if (EVARepairsScenario.reactionWheelsCanFail && reactionWheelState != null)
            {
                if (EVARepairsScenario.debugMode)
                    Debug.Log("[ModuleEVARepairs] - CanUpdateMTBF: Has breakable reaction wheel");
                if (sasIsActive && reactionWheelState.isEnabled)
                    return true;
            }

            // Solar panels
            count = solarPanels.Count;
            if (EVARepairsScenario.solarPanelsCanFail && count > 0)
            {
                if (EVARepairsScenario.debugMode)
                    Debug.Log("[ModuleEVARepairs] - CanUpdateMTBF: Has breakable solar panel");

                for (int index = 0; index < count; index++)
                {
                    if (solarPanels[index].deployState == ModuleDeployablePart.DeployState.EXTENDED)
                    {
                        return true;
                    }
                }
            }

            // Radiators
            if (EVARepairsScenario.radiatorsCanFail)
            {
                if (deployableRadiator != null && deployableRadiator.deployState == ModuleDeployablePart.DeployState.EXTENDED && radiator != null && radiator.IsCooling)
                {
                    if (EVARepairsScenario.debugMode)
                        Debug.Log("[ModuleEVARepairs] - CanUpdateMTBF: Has breakable deployable radiator");
                    return true;
                }

                else if (radiator != null && radiator.IsCooling)
                {
                    if (EVARepairsScenario.debugMode)
                        Debug.Log("[ModuleEVARepairs] - CanUpdateMTBF: Has breakable fixed radiator");
                    return true;
                }
            }

            // If the part has an active engine, and we're throttled up, then we can update.
            if (EVARepairsScenario.enginesCanFail)
            {
                count = engines.Count;
                if (count > 0)
                {
                    for (int index = 0; index < count; index++)
                    {
                        if (engines[index].isOperational && FlightInputHandler.state.mainThrottle > 0)
                        {
                            if (EVARepairsScenario.debugMode)
                                Debug.Log("[ModuleEVARepairs] - CanUpdateMTBF: Has running engine");
                            return true;
                        }
                    }
                }
            }

            // If the part has an active converter, then we can update.
            count = converters.Count;
            if (count > 0)
            {
                for (int index = 0; index < count; index++)
                {
                    if (converters[index].moduleIsEnabled && converters[index].IsActivated && !converters[index].AlwaysActive)
                    {
                        if (EVARepairsScenario.debugMode)
                            Debug.Log("[ModuleEVARepairs] - CanUpdateMTBF: Has running converter");
                        return true;
                    }
                }
            }

            // If the part has an active generator, then we can update.
            count = generators.Count;
            if (count > 0)
            {
                for (int index = 0; index < count; index++)
                {
                    if (generators[index].isActiveAndEnabled && !generators[index].isAlwaysActive)
                    {
                        if (EVARepairsScenario.debugMode)
                            Debug.Log("[ModuleEVARepairs] - CanUpdateMTBF: Has running generator");
                        return true;
                    }
                }
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
        public virtual void UpdateMTBF(double elapsedTime, bool canUpdateMTBF)
        {
            if (!canUpdateMTBF)
                return;

            // Account for time spent away...
            currentMTBF -= (elapsedTime * mtbfRateMultiplier);
            lastUpdated = Planetarium.GetUniversalTime();

            updateMaintenanceStatus();
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

            updateMaintenanceStatus();
        }

        protected virtual void updateMaintenanceStatus()
        {
            // Allow maintenance
            double maintenancePercent = (currentMTBF / (mtbf * 3600f));
            if (maintenancePercent <= 0.2f && maintenancePercent > 0.001f)
            {
                Events["RepairPart"].guiName = Localizer.Format("#LOC_EVAREPAIRS_servicePart", new string[1] { part.partInfo.title });
                Events["RepairPart"].active = true;
            }

            if (currentMTBF <= 0)
            {
                // If the part hasn't worn out yet then it just needs maintenance.
                if (mtbfCurrentMultiplier > 0)
                {
                    needsMaintenance = true;

                    statusDisplay = Localizer.Format("#LOC_EVAREPAIRS_needsMaintenance");

                    Events["RepairPart"].guiName = Localizer.Format("#LOC_EVAREPAIRS_repairPart", new string[1] { part.partInfo.title });
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

                    enableOverhauling();

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

            // Hack to remove expended kits when the kerbal returns from EVA...
            ProtoCrewMember astronaut = null;
            if (vessel.isEVA)
            {
                astronaut = vessel.GetVesselCrew()[0];
            }

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
            string[] breakbleModuleNames = new string[0];

            ConfigNode node = getPartConfigNode();
            if (node != null)
                breakbleModuleNames = node.GetValues(kBreakablePartModule);

            int moduleCount = part.Modules.Count;
            PartModule module;
            bool disableModule = false;
            for (int index = 0; index < moduleCount; index++)
            {
                module = part.Modules[index];

                // Shutdown the engine
                if (module is ModuleEngines)
                    shutdownEngine(module);

                // Shutdown the generator
                else if (module is ModuleGenerator)
                {
                    shutdownGenerator(module);
                    disableModule = true;
                }

                // Shutdown the converter
                else if (module is BaseConverter)
                {
                    shutdownConverter(module);
                    disableModule = true;
                }

                // Shutdown the reaction wheel
                else if (EVARepairsScenario.reactionWheelsCanFail && reactionWheelState != null && module == reactionWheelState.reactionWheel)
                {
                    reactionWheelState.DisableModule();
                }

                // Shutdown the probe core
                else if (EVARepairsScenario.probeCoresCanFail && module is ModuleCommand && part.CrewCapacity == 0)
                {
                    disableProbeCore(module);
                    disableModule = true;
                }

                // Stick the landing gear/leg
                else if (EVARepairsScenario.landingGearCanFail && module == wheelDeployment)
                {
                    disableWheelDeployment();
                }

                else if (module is ModuleDeployableSolarPanel || module is ModuleDeployableRadiator || module is ModuleActiveRadiator)
                {
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
            string[] breakbleModuleNames = new string[0];
            if (node != null)
                breakbleModuleNames = node.GetValues(kBreakablePartModule);

            int moduleCount = part.Modules.Count;
            PartModule module;
            bool enableModule = false;
            for (int index = 0; index < moduleCount; index++)
            {
                module = part.Modules[index];

                // Handle reaction wheels
                if (EVARepairsScenario.reactionWheelsCanFail && reactionWheelState != null && module == reactionWheelState.reactionWheel)
                {
                    reactionWheelState.EnableModule();
                    enableModule = false;
                    continue;
                }

                // Handle deployable wheels/legs
                else if (EVARepairsScenario.landingGearCanFail && module == wheelDeployment)
                {
                    wheelDeployment.Events["EventToggle"].active = true;
                    wheelDeployment.deployedPosition = wheelDeployPosition;
                    wheelDeployment.retractedPosition = wheelRetractPosition;
                    if (wheelActionGroup != KSPActionGroup.None)
                        wheelDeployment.Actions["ActionToggle"].actionGroup = wheelActionGroup;
                    else
                        wheelDeployment.Actions["ActionToggle"].actionGroup = KSPActionGroup.Gear;
                    wheelStuckPosition = -1f;
                    continue;
                }

                // Handle other built-in modules
                else if (module is ModuleEngines || module is ModuleGenerator || module is BaseConverter || module is ModuleDeployableSolarPanel || module is ModuleActiveRadiator || module is ModuleDeployableRadiator)
                {
                    enableModule = true;
                }

                // If the module isn't on our list, or the enable flag isn't set, or we're trying to renable this module, then skip it.
                if (!enableModule && (module == this || !breakbleModuleNames.Contains(module.moduleName)))
                    continue;

                // Re-enable the module.
                module.enabled = true;
                module.isEnabled = true;

                // For most modules just call OnActive.
                if (!(module is ModuleEngines))
                {
                    module.OnActive();
                }

                // You'll need to manually restart engines...
                else
                {
                    ModuleEngines engine = (ModuleEngines)module;
                    engine.allowRestart = true;
                    engine.manuallyOverridden = false;
                }

                enableModule = false;
            }

        }

        /// <summary>
        /// Performs the activation check to see if the part should fail.
        /// </summary>
        public virtual void PerformActivationCheck()
        {
            // Get target number
            EVARepairsScenario.shared.UpdateSettings();
            int targetNumber = calculateReliabilityTarget();

            // Make the check
            int dieRoll = UnityEngine.Random.Range(1, 100);
            activationRoll = dieRoll;
            partDidFail = false;
            if (dieRoll > targetNumber)
            {
                // Reduce current MTBF to 1-10 seconds to allow some time before the failure occurs.
                if (wheelDeployment == null)
                    currentMTBF = UnityEngine.Random.Range(1, 10);

                // Immediately run out of MTBF. This will trigger part failure, and the gear will stop somewhere during its animation process.
                else
                    currentMTBF = TimeWarp.fixedDeltaTime;

                partDidFail = true;
            }

            // Update reliability
            if (EVARepairsScenario.reliabilityEnabled && updateReliability)
                EVARepairsScenario.shared.UpdateReliability(part.partInfo.name, partDidFail);
        }
        #endregion

        #region Overrides
        public override void OnActive()
        {
            base.OnActive();
            if (!EVARepairsScenario.maintenanceEnabled || !EVARepairsScenario.canFailOnActivation || needsMaintenance || partWornOut)
                return;

            PerformActivationCheck();
        }

        public void FixedUpdate()
        {
            base.OnFixedUpdate();
            if (!HighLogic.LoadedSceneIsFlight || !autoUpdatesEnabled)
                return;

            // Account for time spent away...
            bool canUpdateMTBF = CanUpdateMTBF();
            double elapsedTime = Planetarium.GetUniversalTime() - lastUpdated;
            if (canUpdateMTBF)
                UpdateMTBF(elapsedTime, canUpdateMTBF);
            else
                lastUpdated = Planetarium.GetUniversalTime();

            // Perform activation check if needed
            if (shouldCheckActivation())
                PerformActivationCheck();

            // Update reliability display
            updateReliabilityDisplay();

            // Make sure player can't retract/extend the wheel/leg
            if (EVARepairsScenario.landingGearCanFail && wheelDeployment != null && (needsMaintenance || partWornOut))
            {
                wheelDeployment.Events["EventToggle"].active = false;
                wheelDeployment.Actions["ActionToggle"].actionGroup = KSPActionGroup.None;
            }
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
            if (!HighLogic.LoadedSceneIsFlight && !HighLogic.LoadedSceneIsEditor)
                return;
            debugMode = EVARepairsScenario.debugMode;
            findModulesThatFail();

            if (HighLogic.LoadedSceneIsFlight)
            {
                GameEvents.OnGameSettingsApplied.Add(onGameSettingsApplied);
            }

            // Check to see if EVA Repairs is enabled.
            if (!EVARepairsScenario.maintenanceEnabled || EVARepairsScenario.shared.IsPartDisabled(part))
            {
                needsMaintenance = false;
                Events["RepairPart"].active = false;
                Fields["statusDisplay"].guiActive = false;
                Fields["statusDisplay"].guiActiveEditor = false;
                Fields["mtbfDisplay"].guiActive = false;
                Fields["mtbfDisplay"].guiActiveEditor = false;

                if (!EVARepairsScenario.maintenanceEnabled)
                    return;
            }

            // Debug mode
            Events["DebugBreakPart"].active = debugMode;
            Events["DebugRepairPart"].active = debugMode;
            Events["DebugWearOutPart"].active = debugMode;
            Fields["mtbfCurrentMultiplier"].guiActive = debugMode;
            Fields["currentMTBF"].guiActive = debugMode;
            Fields["mtbfRateMultiplier"].guiActive = debugMode;
            Fields["partDidFail"].guiActive = debugMode;
            Fields["activationRoll"].guiActive = debugMode;
            Events["DebugEnableFailures"].active = debugMode;
            Events["DebugDisableFailures"].active = debugMode;

            Events["DebugEnableFailures"].guiName = "Failures ON: ALL " + part.partInfo.title;
            Events["DebugDisableFailures"].guiName = "Failures OFF: ALL " + part.partInfo.title;
            updatePartEnabledDisabledUI();

            // Make sure we have a starting value for last updated.
            if (lastUpdated <= 0 && HighLogic.LoadedSceneIsFlight)
                lastUpdated = Planetarium.GetUniversalTime();

            // Set the part reliability if needed.
            // In the editor, make sure that the part reflects the current flight experience.
            if (HighLogic.LoadedSceneIsEditor)
            {
                int baseReliability = EVARepairsScenario.shared.GetReliability(part.partInfo.name);
                if (reliability < baseReliability)
                    reliability = baseReliability;
            }

            // Account for in-field parts that haven't been initialized with reliability.
            if (reliability <= 0)
                reliability = EVARepairsScenario.shared.GetReliability(part.partInfo.name);

            // Account for MTBF as well
            if (mtbf <= 0)
            {
                setStartingMTBF();
            }
            else if (currentMTBF < 0)
            {
                currentMTBF = mtbf * 3600;
                mtbfCurrentMultiplier = 1;
            }

            // Setup GUI
            Events["RepairPart"].guiName = Localizer.Format("#LOC_EVAREPAIRS_repairPart", new string[1] { part.partInfo.title });
            Events["OverhaulPart"].guiName = Localizer.Format("#LOC_EVAREPAIRS_overhaulPart", new string[1] { part.partInfo.title });
            if (!partWornOut)
            {
                statusDisplay = needsMaintenance ? Localizer.Format("#LOC_EVAREPAIRS_needsMaintenance") : Localizer.Format("#LOC_EVAREPAIRS_statusOK");
                Events["RepairPart"].active = needsMaintenance;
                Events["OverhaulPart"].active = false;
            }
            else
            {
                statusDisplay = Localizer.Format("#LOC_EVAREPAIRS_statusBroken");
                Events["RepairPart"].active = false;
            }

            if (EVARepairsScenario.internalRepairsAllowed && part.CrewCapacity > 0)
            {
                Events["RepairPart"].guiActive = true;
            }
            else
            {
                Events["RepairPart"].guiActive = false;
            }

            // Disable the part if it needs maintenance or is worn out.
            if (needsMaintenance || partWornOut)
            {
                DisablePartModules();

                if (EVARepairsScenario.partsCanWearOut && mtbfCurrentMultiplier < kOverhaulThreshold)
                {
                    // Enable overhauling
                    Events["OverhaulPart"].active = true;
                    if (EVARepairsScenario.internalRepairsAllowed)
                        Events["OverhaulPart"].guiActive = true;
                }
            }

            updateReliabilityDisplay();
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            if (!string.IsNullOrEmpty(actionGroupId))
                wheelActionGroup = (KSPActionGroup)Enum.Parse(typeof(KSPActionGroup), actionGroupId);
        }

        public override void OnSave(ConfigNode node)
        {
            actionGroupId = wheelActionGroup.ToString();
            base.OnSave(node);
        }
        #endregion

        #region Helpers

        private void updatePartEnabledDisabledUI()
        {
            bool isPartDisabled = EVARepairsScenario.shared.IsPartDisabled(part);
            Events["DebugEnableFailures"].guiActiveEditor = isPartDisabled;
            Events["DebugDisableFailures"].guiActiveEditor = !isPartDisabled;
        }

        private void enableOverhauling()
        {
            if (EVARepairsScenario.partsCanWearOut && mtbfCurrentMultiplier < kOverhaulThreshold)
            {
                Events["OverhaulPart"].active = true;
                if (EVARepairsScenario.internalRepairsAllowed)
                    Events["OverhaulPart"].guiActive = true;
            }
        }

        private double getResourceAmountForOverhaul()
        {
            PartResourceDefinition definition = getOverhaulResourceDefinition();
            if (definition == null)
                return 0;

            double dryMass = part.mass;

            return dryMass / definition.density;
        }

        private PartResourceDefinition getOverhaulResourceDefinition()
        {
            PartResourceDefinitionList definitions = PartResourceLibrary.Instance.resourceDefinitions;

            if (definitions.Contains(overhaulResource))
                return definitions[overhaulResource];
            else
                return null;
        }

        private void disableWheelDeployment()
        {
            if (wheelStuckPosition <= 0)
                wheelStuckPosition = UnityEngine.Random.Range(0.1f, 1);

            if (wheelDeployment.fsm.CurrentState == wheelDeployment.st_deploying)
            {
                if (wheelDeployment.position > wheelStuckPosition)
                {
                    wheelStuckPosition = wheelDeployment.position;
                    wheelDeployment.deployedPosition = wheelStuckPosition;
                }
                else
                {
                    wheelDeployment.deployedPosition = wheelStuckPosition;
                }
            }
            else if (wheelDeployment.fsm.CurrentState == wheelDeployment.st_retracting)
            {
                if (wheelDeployment.position < wheelStuckPosition)
                {
                    wheelStuckPosition = wheelDeployment.position;
                    wheelDeployment.retractedPosition = wheelStuckPosition;
                }
                else
                {
                    wheelDeployment.retractedPosition = wheelStuckPosition;
                }
            }

            wheelActionGroup = wheelDeployment.Actions["ActionToggle"].actionGroup;
        }

        private void disableProbeCore(PartModule module)
        {
            BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            Type objectType = typeof(ModuleCommand);
            ModuleCommand probeCore = (ModuleCommand)module;
            if (objectType == null)
                return;

            FieldInfo moduleState = objectType.GetField("moduleState", flags);
            if (moduleState != null)
            {
                moduleState.SetValue(probeCore, ModuleCommand.ModuleControlState.NoControlPoint);
            }

            FieldInfo localVesselControlState = objectType.GetField("localVesselControlState", flags);
            if (localVesselControlState != null)
            {
                localVesselControlState.SetValue(probeCore, CommNet.VesselControlState.Probe);
            }

            part.isControlSource = Vessel.ControlLevel.NONE;
        }

        private void shutdownConverter(PartModule module)
        {
            BaseConverter converter = (BaseConverter)module;
            if (!converter.AlwaysActive && converter.IsActivated)
                converter.StopResourceConverter();
        }

        private void shutdownGenerator(PartModule module)
        {
            ModuleGenerator generator = (ModuleGenerator)module;
            if (!generator.isAlwaysActive)
                generator.Shutdown();
        }

        private void shutdownEngine(PartModule module)
        {
            if (module is ModuleEngines)
            {
                ModuleEngines engine = (ModuleEngines)module;
                if (engine.isOperational)
                {
                    // If the engine can be shut down, then shut it down. Otherwise, make it go boom.
                    if (engine.allowShutdown)
                    {
                        engine.allowRestart = false;
                        engine.manuallyOverridden = true;
                        engine.Flameout(Localizer.Format("#LOC_EVAREPAIRS_needsMaintenance"));
                        engine.Shutdown();
                    }
                    else
                    {
                        part.explode();
                    }
                }
            }
        }

        private bool shouldCheckActivation()
        {
            bool checkReliability = false;
            if (!EVARepairsScenario.canFailOnActivation)
                return false;

            // Check engines
            if (engines != null)
            {
                int count = engines.Count;
                for (int index = 0; index < count; index++)
                {
                    // Change in operation state
                    if (engines[index].isOperational != engineStates[index])
                    {
                        engineStates[index] = engines[index].isOperational;
                        checkReliability = true;
                    }
                }

                if (checkReliability)
                    return true;
            }

            // Check converters
            if (converters != null)
            {
                int count = converters.Count;
                for (int index = 0; index < count; index++)
                {
                    if (converters[index].IsActivated != converterStates[index])
                    {
                        converterStates[index] = converters[index].IsActivated;
                        checkReliability = true;
                    }
                }

                if (checkReliability)
                    return true;
            }

            // Check reaction wheel
            if (EVARepairsScenario.reactionWheelsCanFail && reactionWheelState != null)
            {
                sasIsActive = FlightGlobals.ActiveVessel.ActionGroups[KSPActionGroup.SAS];
                if (sasIsActive != sasWasActive)
                {
                    sasWasActive = sasIsActive;
                    return true;
                }
            }

            // Check wheels
            if (EVARepairsScenario.landingGearCanFail && wheelDeployment != null && wheelDeployment.fsm.CurrentState != previousWheelState)
            {
                previousWheelState = wheelDeployment.fsm.CurrentState;

                // Only check for transition between extending and retracting.
                if (wheelDeployment.fsm.CurrentState == wheelDeployment.st_retracting || wheelDeployment.fsm.CurrentState == wheelDeployment.st_deploying)
                {
                    return true;
                }
            }

            return checkReliability;
        }

        private void updateReliabilityDisplay()
        {
            int mtbfValue = (int)(100 * (currentMTBF / 3600) / mtbf);
            mtbfDisplay = Localizer.Format("#LOC_EVAREPAIRS_mtbfValue", new string[1] { mtbfValue.ToString() });
            if (EVARepairsScenario.reliabilityEnabled)
                mtbfDisplay = mtbfDisplay + " " + Localizer.Format("#LOC_EVAREPAIRS_reliability", new string[1] { reliability.ToString() });
        }

        private int calculateReliabilityTarget()
        {
            // Get target number
            int targetNumber = (int)(100 * (currentMTBF / 3600) / mtbf);

            // Account for reliability
            if (EVARepairsScenario.reliabilityEnabled && reliability < EVARepairsScenario.shared.GetMaxReliability())
            {
                if (reliability <= 0)
                    reliability = EVARepairsScenario.shared.GetReliability(part.partName);

                targetNumber = (int)(targetNumber * (reliability / 100f));
            }

            return targetNumber;
        }

        private void onGameSettingsApplied()
        {
            debugMode = EVARepairsScenario.debugMode;

            bool maintenanceEnabled = EVARepairsSettings.MaintenanceEnabled;
            bool reliabilityEnabled = EVARepairsSettings.ReliabilityEnabled;
            if (!maintenanceEnabled)
            {
                needsMaintenance = false;
            }

            Events["RepairPart"].active = needsMaintenance;
            if (EVARepairsScenario.internalRepairsAllowed && part.CrewCapacity > 0)
            {
                Events["RepairPart"].guiActive = true;
            }
            else
            {
                Events["RepairPart"].guiActive = false;
            }

            Fields["statusDisplay"].guiActive = maintenanceEnabled;
            Fields["statusDisplay"].guiActiveEditor = maintenanceEnabled;
            Events["DebugBreakPart"].active = debugMode;
            Events["DebugRepairPart"].active = debugMode;
            Events["DebugWearOutPart"].active = debugMode;
            Fields["mtbfCurrentMultiplier"].guiActive = debugMode;
            Fields["currentMTBF"].guiActive = debugMode;

            // Make sure that Reliability is at the minimum starting value.
            int startingReliability = EVARepairsSettings.StartingReliability;
            if (reliability < startingReliability)
                reliability = startingReliability;

            // Make sure that MTBF is at the minimum starting value.
            setStartingMTBF();

            // If parts no longer wear out and the part is worn out, then make it repairable.
            bool partsWearOut = EVARepairsSettings.PartsCanWearOut;
            if (!partsWearOut && partWornOut)
            {
                partWornOut = false;
                needsMaintenance = false;
                currentMTBF = mtbf * 3600f;
            }
            updateReliabilityDisplay();
        }

        private void setStartingMTBF()
        {
            double startingMTBF = EVARepairsScenario.shared.GetStartingMTBF();

            if (mtbf < startingMTBF)
            {
                mtbf = startingMTBF;
                currentMTBF = mtbf * 3600;
                mtbfCurrentMultiplier = 1;
            }
        }

        private void findModulesThatFail()
        {
            engines = part.FindModulesImplementing<ModuleEngines>();
            if (engines != null)
            {
                engineStates = new List<bool>();
                int count = engines.Count;
                for (int index = 0; index < count; index++)
                {
                    engineStates.Add(engines[index].isOperational);
                }
            }

            generators = part.FindModulesImplementing<ModuleGenerator>();

            converters = part.FindModulesImplementing<BaseConverter>();
            if (converters != null)
            {
                converterStates = new List<bool>();
                int count = converters.Count;
                for (int index = 0; index < count; index++)
                {
                    converterStates.Add(converters[index].IsActivated);
                }
            }

            solarPanels = part.FindModulesImplementing<ModuleDeployableSolarPanel>();

            radiator = part.FindModuleImplementing<ModuleActiveRadiator>();
            deployableRadiator = part.FindModuleImplementing<ModuleDeployableRadiator>();

            if (HighLogic.LoadedSceneIsFlight)
            {
                ModuleReactionWheel reactionWheel = part.FindModuleImplementing<ModuleReactionWheel>();
                if (reactionWheel != null)
                {
                    reactionWheelState = new ReactionWheelState(reactionWheel);
                    sasIsActive = FlightGlobals.ActiveVessel.ActionGroups[KSPActionGroup.SAS];
                    sasWasActive = sasIsActive;
                }
            }

            wheelDeployment = part.FindModuleImplementing<ModuleWheelDeployment>();
            if (wheelDeployment != null)
            {
                wheelDeployPosition = wheelDeployment.deployedPosition;
                wheelRetractPosition = wheelDeployment.retractedPosition;
                previousWheelState = wheelDeployment.fsm.CurrentState;

                if (wheelStuckPosition > 0 && (needsMaintenance || partWornOut))
                {
                    wheelDeployment.deployedPosition = wheelStuckPosition;
                    wheelDeployment.retractedPosition = wheelStuckPosition;
                    wheelDeployment.position = wheelStuckPosition;
                    if (wheelDeployment.fsm.CurrentState == wheelDeployment.st_deployed)
                        wheelDeployment.fsm.RunEvent(wheelDeployment.on_retract);
                    else if (wheelDeployment.fsm.CurrentState == wheelDeployment.st_retracted)
                        wheelDeployment.fsm.RunEvent(wheelDeployment.on_deploy);
                    wheelActionGroup = wheelDeployment.Actions["ActionToggle"].actionGroup;
                }
            }
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
