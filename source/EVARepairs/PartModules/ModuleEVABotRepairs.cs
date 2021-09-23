using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using KSP.Localization;
using UnityEngine;
using ModuleWheels;
using System.Reflection;

namespace EVARepairs
{
    public class ModuleEVABotRepairs: PartModule
    {
        #region constants
        public const float messageDuration = 5.0f;
        #endregion

        #region Fields
        [KSPField]
        public bool debugMode = false;

        [KSPField]
        public string repairKitName = "evaRepairKit";

        [KSPField(unfocusedRange = 25, guiName = "#LOC_EVAREPAIRS_kitsRequired")]
        public int repairKitsRequired = 1;
        #endregion

        #region Housekeeping
        ModuleParachute parachute = null;
        ModuleWheelDamage wheelDamage = null;
        ModuleDeployablePart[] deployableParts = new ModuleDeployablePart[0];
        MethodInfo repairMethod = null;
        #endregion

        #region Overrides
        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            if (!HighLogic.LoadedSceneIsFlight)
                return;

            Events["DebugRepairPart"].active = debugMode;
            Events["DebugBreakPart"].active = debugMode;

            // Calculate repair kits needed
            repairKitsRequired = Math.Min(Math.Max((int)(part.mass / (double)GameSettings.PART_REPAIR_MASS_PER_KIT), 1), GameSettings.PART_REPAIR_MAX_KIT_AMOUNT);

            // Get the wheel damage module (if any)
            wheelDamage = part.FindModuleImplementing<ModuleWheelDamage>();

            // Get the parachute module (if any)
            parachute = part.FindModuleImplementing<ModuleParachute>();
            if (parachute == null)
                Events["PackChute"].active = false;

            // Get the list of deployable part modules (if any)
            List<ModuleDeployablePart> moduleDeployableParts = part.FindModulesImplementing<ModuleDeployablePart>();
            if (moduleDeployableParts != null)
                deployableParts = moduleDeployableParts.ToArray();
            else
                return;

            // Find the DoRepair method. It is protected for some stupid reason.
            BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            Type objectType = typeof(ModuleDeployablePart);
            if (objectType == null)
                return;
            MethodInfo[] methods = objectType.GetMethods(flags);
            for (int index = 0; index < methods.Length; index++)
            {
                if (methods[index].Name == "DoRepair")
                {
                    repairMethod = methods[index];
                    break;
                }
            }
        }

        public override void OnUpdate()
        {
            base.OnUpdate();
            if (!HighLogic.LoadedSceneIsFlight)
                return;

            Events["RepairPart"].active = debugMode;
            Fields["repairKitsRequired"].guiActiveUnfocused = debugMode;

            // If the wheel is broken then enable the repair event.
            if (wheelDamage != null && wheelDamage.isDamaged)
            {
                Events["RepairPart"].active = true;
                Fields["repairKitsRequired"].guiActiveUnfocused = true;
                return;
            }

            // Check deployable parts
            ModuleDeployablePart deployablePart;
            for (int index = 0; index < deployableParts.Length; index++)
            {
                deployablePart = deployableParts[index];
                if (deployablePart.isBreakable && deployablePart.deployState == ModuleDeployablePart.DeployState.BROKEN)
                {
                    Events["RepairPart"].active = true;
                    Fields["repairKitsRequired"].guiActiveUnfocused = true;
                    return;
                }
            }

            // Check chutes
            if (parachute != null && parachute.deploymentState == ModuleParachute.deploymentStates.CUT)
            {
                Events["PackChute"].active = true;
            }
            else
            {
                Events["PackChute"].active = false;
            }
        }
        #endregion

        #region Events
        /// <summary>
        /// Performs maintenance on the part.
        /// </summary>
        [KSPEvent(guiName = "#LOC_EVAREPAIRS_botRepairPart", externalToEVAOnly = false, guiActiveUnfocused = true, unfocusedRange = 25)]
        public virtual void RepairPart()
        {
            if (CanRepairPart())
            {
                ConsumeRepairKits(FlightGlobals.ActiveVessel);
                RestorePartFunctionality();
            }
        }

        [KSPEvent(guiName = "#LOC_EVAREPAIRS_botPackChute", externalToEVAOnly = false, guiActiveUnfocused = true, unfocusedRange = 25)]
        public void PackChute()
        {
            if (parachute == null)
                return;

            if (!vessel.FindPartModuleImplementing<ModuleEVARepairBot>())
            {
                string message = Localizer.Format("#LOC_EVAREPAIRS_noRepairBot");
                ScreenMessages.PostScreenMessage(message, messageDuration, ScreenMessageStyle.UPPER_CENTER);
                return;
            }

            //Repack the chute.
            bool experienceEnabled = HighLogic.CurrentGame.Parameters.CustomParams<GameParameters.AdvancedParams>().EnableKerbalExperience;
            HighLogic.CurrentGame.Parameters.CustomParams<GameParameters.AdvancedParams>().EnableKerbalExperience = false;
            parachute.Repack();
            HighLogic.CurrentGame.Parameters.CustomParams<GameParameters.AdvancedParams>().EnableKerbalExperience = experienceEnabled;
        }

        /// <summary>
        /// Debug method to repair part.
        /// </summary>
        [KSPEvent(guiActive = true, guiName = "(Debug) Repair Part")]
        public virtual void DebugRepairPart()
        {
            RestorePartFunctionality();
        }

        /// <summary>
        /// Debug method to break the part.
        /// </summary>
        [KSPEvent(guiActive = true, guiName = "(Debug) Break Part")]
        public virtual void DebugBreakPart()
        {
            if (wheelDamage != null)
                wheelDamage.SetDamaged(true);

            ModuleDeployablePart deployablePart;
            for (int index = 0; index < deployableParts.Length; index++)
            {
                deployablePart = deployableParts[index];
                if (!deployablePart.isBreakable || deployablePart.deployState == ModuleDeployablePart.DeployState.BROKEN)
                    continue;

                deployablePart.deployState = ModuleDeployablePart.DeployState.BROKEN;
            }
        }
        #endregion

        #region API
        /// <summary>
        /// Restores part functionality.
        /// </summary>
        public virtual void RestorePartFunctionality()
        {
            // Fix broken wheel (if any)
            if (wheelDamage != null)
                wheelDamage.SetDamaged(false);

            // Fix deployable parts (if any)
            if (repairMethod == null)
                return;
            ModuleDeployablePart deployablePart;
            for (int index = 0; index < deployableParts.Length; index++)
            {
                deployablePart = deployableParts[index];
                if (!deployablePart.isBreakable || deployablePart.deployState != ModuleDeployablePart.DeployState.BROKEN)
                    continue;

                try
                {
                    repairMethod.Invoke(deployablePart, null);
                }
                catch (Exception ex)
                {
                    Debug.Log("[ModuleEVABotRepairs] - " + ex.ToString());
                }
            }
        }

        /// <summary>
        /// Determines whether or not the part can be repaired.
        /// </summary>
        /// <returns></returns>
        public virtual bool CanRepairPart()
        {
            if (!vessel.FindPartModuleImplementing<ModuleEVARepairBot>())
            {
                string message = Localizer.Format("#LOC_EVAREPAIRS_noRepairBot");
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
        /// <param name="amount">An int containing the number of kits to consume.</param>
        public virtual void ConsumeRepairKits(Vessel vessel)
        {
            List<ModuleInventoryPart> inventories = vessel.FindPartModulesImplementing<ModuleInventoryPart>();
            ModuleInventoryPart inventory;
            int count = inventories.Count;
            int repairPartsFound = 0;
            int repairPartsRemaining = repairKitsRequired;

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
        #endregion

        #region Helpers
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
        #endregion
    }
}
