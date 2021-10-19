using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using KSP.IO;
using KSP.UI.Screens;
using KSP.Localization;

namespace EVARepairs.AppButton
{

    [KSPAddon(KSPAddon.Startup.EveryScene, false)]
    public class EVARepairsAppButton: MonoBehaviour
    {
        static protected ApplicationLauncherButton appLauncherButton = null;
        static public Texture2D appIconEnabled = null;
        static public Texture2D appIconDisabled = null;
        static bool maintenanceEnabled = false;

        public void Awake()
        {
            if (!HighLogic.LoadedSceneIsFlight && !HighLogic.LoadedSceneIsEditor)
                return;

            appIconEnabled = GameDatabase.Instance.GetTexture("WildBlueIndustries/EVARepairs/Icons/EnabledIcon", false);
            appIconDisabled = GameDatabase.Instance.GetTexture("WildBlueIndustries/EVARepairs/Icons/DisabledIcon", false);

            maintenanceEnabled = EVARepairsSettings.MaintenanceEnabled;

            GameEvents.onGUIApplicationLauncherReady.Add(SetupGUI);
            GameEvents.OnGameSettingsApplied.Add(onGameSettingsApplied);
        }

        public void OnDestroy()
        {
            if (appLauncherButton != null)
            {
                ApplicationLauncher.Instance.RemoveModApplication(appLauncherButton);
                appLauncherButton = null;
            }
            GameEvents.onGUIApplicationLauncherReady.Remove(SetupGUI);
            GameEvents.OnGameSettingsApplied.Remove(onGameSettingsApplied);
        }

        private void SetupGUI()
        {
            Texture2D appIcon = maintenanceEnabled ? appIconEnabled : appIconDisabled;

            // Remove previous button.
            if (appLauncherButton != null)
            {
                ApplicationLauncher.Instance.RemoveModApplication(appLauncherButton);
                appLauncherButton = null;
            }

            if (HighLogic.LoadedSceneIsFlight || HighLogic.LoadedSceneIsEditor)
            {
                appLauncherButton = ApplicationLauncher.Instance.AddModApplication(toggleEVARepairs, toggleEVARepairs, null, null, null, null, ApplicationLauncher.AppScenes.ALWAYS, appIcon);
            }
        }

        private void toggleEVARepairs()
        {
            maintenanceEnabled = !EVARepairsSettings.MaintenanceEnabled;

            EVARepairsSettings.MaintenanceEnabled = maintenanceEnabled;
            EVARepairsScenario.maintenanceEnabled = maintenanceEnabled;

            if (appLauncherButton != null)
            {
                Texture2D appIcon = maintenanceEnabled ? appIconEnabled : appIconDisabled;
                appLauncherButton.SetTexture(appIcon);

                string message = maintenanceEnabled ? Localizer.Format("#LOC_EVAREPAIRS_enabled") : Localizer.Format("#LOC_EVAREPAIRS_disabled");
                ScreenMessages.PostScreenMessage(message, 5f, ScreenMessageStyle.UPPER_CENTER);
            }
        }

        private void onGameSettingsApplied()
        {
            maintenanceEnabled = EVARepairsSettings.MaintenanceEnabled;

            if (appLauncherButton != null)
            {
                Texture2D appIcon = maintenanceEnabled ? appIconEnabled : appIconDisabled;
                appLauncherButton.SetTexture(appIcon);
            }
        }
    }
}
