using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using KSP.IO;
using KSP.Localization;

namespace EVARepairs
{
    public class EVARepairsSettings : GameParameters.CustomParameterNode
    {
        [GameParameters.CustomParameterUI("#LOC_EVAREPAIRS_settingsMaintenanceDesc", toolTip = "#LOC_EVAREPAIRS_settingsMaintenanceTip", autoPersistance = true, gameMode = GameParameters.GameMode.ANY)]
        public bool maintenanceEnabled = true;

        [GameParameters.CustomParameterUI("#LOC_EVAREPAIRS_settingsPartsWearOutDesc", toolTip = "#LOC_EVAREPAIRS_settingsPartsWearOutTip", autoPersistance = true, gameMode = GameParameters.GameMode.ANY)]
        public bool partsCanWearOut = false;

        [GameParameters.CustomParameterUI("#LOC_EVAREPAIRS_settingsActivationFailDesc", toolTip = "#LOC_EVAREPAIRS_settingsActivationFailTip", autoPersistance = true, gameMode = GameParameters.GameMode.ANY)]
        public bool canFailOnActivation = false;

        [GameParameters.CustomParameterUI("#LOC_EVAREPAIRS_settingsReliabilityDesc", toolTip = "#LOC_EVAREPAIRS_settingsReliabilityTip", autoPersistance = true, gameMode = GameParameters.GameMode.ANY)]
        public bool reliabilityEnabled = false;

        [GameParameters.CustomIntParameterUI("#LOC_EVAREPAIRS_settingsStartingReliabilityDesc", maxValue = 80, minValue = 30, stepSize = 5, toolTip = "#LOC_EVAREPAIRS_settingsStartingReliabilityTip", autoPersistance = true, gameMode = GameParameters.GameMode.ANY)]
        public int startingReliability = 50;

        [GameParameters.CustomIntParameterUI("#LOC_EVAREPAIRS_settingsStartingMTBFDesc", maxValue = 6000, minValue = 600, stepSize = 100, toolTip = "#LOC_EVAREPAIRS_settingsStartingMTBFTip", autoPersistance = true, gameMode = GameParameters.GameMode.ANY)]
        public int startingMTBF = 600;

        [GameParameters.CustomParameterUI("#LOC_EVAREPAIRS_settingsTechProgressDesc", toolTip = "#LOC_EVAREPAIRS_settingsTechProgressTip", autoPersistance = true, gameMode = GameParameters.GameMode.CAREER | GameParameters.GameMode.SCIENCE)]
        public bool technologicalProgressEnabled = false;

        [GameParameters.CustomParameterUI("#LOC_EVAREPAIRS_settingsWheelsDesc", toolTip = "#LOC_EVAREPAIRS_settingsWheelsTip", autoPersistance = true, gameMode = GameParameters.GameMode.ANY)]
        public bool reactionWheelFailureEnabled = false;

        [GameParameters.CustomParameterUI("#LOC_EVAREPAIRS_settingsLandingGearDesc", toolTip = "#LOC_EVAREPAIRS_settingsLandingGearTip", autoPersistance = true, gameMode = GameParameters.GameMode.ANY)]
        public bool landingGearCanFail = false;

        [GameParameters.CustomParameterUI("#LOC_EVAREPAIRS_settingsEnginesDesc", toolTip = "#LOC_EVAREPAIRS_settingsEnginesTip", autoPersistance = true, gameMode = GameParameters.GameMode.ANY)]
        public bool enginesCanFail = false;

        [GameParameters.CustomParameterUI("#LOC_EVAREPAIRS_settingsConvertersDesc", toolTip = "#LOC_EVAREPAIRS_settingsConvertersTip", autoPersistance = true, gameMode = GameParameters.GameMode.ANY)]
        public bool convertersCanFail = false;

        [GameParameters.CustomParameterUI("#LOC_EVAREPAIRS_settingsGeneratorsDesc", toolTip = "#LOC_EVAREPAIRS_settingsGeneratorsTip", autoPersistance = true, gameMode = GameParameters.GameMode.ANY)]
        public bool generatorsCanFail = false;

        [GameParameters.CustomParameterUI("#LOC_EVAREPAIRS_settingsDebugModeDesc", toolTip = "#LOC_EVAREPAIRS_settingsDebugModeTip", autoPersistance = true, gameMode = GameParameters.GameMode.ANY)]
        public bool debugModeEnabled = false;
        #region CustomParameterNode

        public override string DisplaySection
        {
            get
            {
                return Section;
            }
        }

        public override string Section
        {
            get
            {
                return "EVA Repairs";
            }
        }

        public override string Title
        {
            get
            {
                return "EVA Repairs";
            }
        }

        public override int SectionOrder
        {
            get
            {
                return 1;
            }
        }

        public override void SetDifficultyPreset(GameParameters.Preset preset)
        {
            base.SetDifficultyPreset(preset);
        }

        public override GameParameters.GameMode GameMode
        {
            get
            {
                return GameParameters.GameMode.ANY;
            }
        }

        public override bool HasPresets
        {
            get
            {
                return false;
            }
        }

        public override bool Enabled(System.Reflection.MemberInfo member, GameParameters parameters)
        {
            if (member.Name == "startingReliability" || member.Name == "technologicalProgressEnabled")
                return maintenanceEnabled && reliabilityEnabled;

            else if (member.Name == "reliabilityEnabled")
                return maintenanceEnabled && canFailOnActivation;

            else
                return member.Name == "maintenanceEnabled" ? true : maintenanceEnabled;
        }
        #endregion

        public static bool MaintenanceEnabled
        {
            get
            {
                EVARepairsSettings settings = HighLogic.CurrentGame.Parameters.CustomParams<EVARepairsSettings>();
                return settings.maintenanceEnabled;
            }

            set
            {
                EVARepairsSettings settings = HighLogic.CurrentGame.Parameters.CustomParams<EVARepairsSettings>();
                settings.maintenanceEnabled = value;
            }
        }

        public static bool FailOnActivation
        {
            get
            {
                EVARepairsSettings settings = HighLogic.CurrentGame.Parameters.CustomParams<EVARepairsSettings>();
                return settings.canFailOnActivation;
            }
        }

        public static bool PartsCanWearOut
        {
            get
            {
                EVARepairsSettings settings = HighLogic.CurrentGame.Parameters.CustomParams<EVARepairsSettings>();
                return settings.partsCanWearOut;
            }
        }

        public static bool ReliabilityEnabled
        {
            get
            {
                EVARepairsSettings settings = HighLogic.CurrentGame.Parameters.CustomParams<EVARepairsSettings>();
                return settings.reliabilityEnabled;
            }
        }

        public static int StartingReliability
        {
            get
            {
                EVARepairsSettings settings = HighLogic.CurrentGame.Parameters.CustomParams<EVARepairsSettings>();
                return settings.startingReliability;
            }
        }

        public static double StartingMTBF
        {
            get
            {
                EVARepairsSettings settings = HighLogic.CurrentGame.Parameters.CustomParams<EVARepairsSettings>();
                return settings.startingMTBF;
            }
        }

        public static bool ReactionWheelsCanFail
        {
            get
            {
                EVARepairsSettings settings = HighLogic.CurrentGame.Parameters.CustomParams<EVARepairsSettings>();
                return settings.reactionWheelFailureEnabled;
            }
        }

        public static bool LandingGearCanFail
        {
            get
            {
                EVARepairsSettings settings = HighLogic.CurrentGame.Parameters.CustomParams<EVARepairsSettings>();
                return settings.landingGearCanFail;
            }
        }

        public static bool TechnologicalProgressEnabled
        {
            get
            {
                EVARepairsSettings settings = HighLogic.CurrentGame.Parameters.CustomParams<EVARepairsSettings>();
                return settings.technologicalProgressEnabled;
            }
        }

        public static bool EnginesCanFail
        {
            get
            {
                EVARepairsSettings settings = HighLogic.CurrentGame.Parameters.CustomParams<EVARepairsSettings>();
                return settings.enginesCanFail;
            }
        }

        public static bool ConvertersCanFail
        {
            get
            {
                EVARepairsSettings settings = HighLogic.CurrentGame.Parameters.CustomParams<EVARepairsSettings>();
                return settings.convertersCanFail;
            }
        }

        public static bool GeneratorsCanFail
        {
            get
            {
                EVARepairsSettings settings = HighLogic.CurrentGame.Parameters.CustomParams<EVARepairsSettings>();
                return settings.generatorsCanFail;
            }
        }

        public static bool DebugModeEnabled
        {
            get
            {
                EVARepairsSettings settings = HighLogic.CurrentGame.Parameters.CustomParams<EVARepairsSettings>();
                return settings.debugModeEnabled;
            }
        }
    }
}

