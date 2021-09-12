using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using KSP.IO;
using KSP.Localization;

namespace EVARepairs
{
    [KSPModule("#LOC_EVAREPAIRS_repairBotTitle")]
    public class ModuleEVARepairBot : PartModule, IModuleInfo
    {
        public Callback<Rect> GetDrawModulePanelCallback()
        {
            return null;
        }

        public string GetModuleTitle()
        {
            return Localizer.Format("LOC_EVAREPAIRS_repairBotTitle");
        }

        public string GetPrimaryField()
        {
            return string.Empty;
        }

        public override string GetModuleDisplayName()
        {
            return GetModuleTitle();
        }

        public override string GetInfo()
        {
            StringBuilder info = new StringBuilder();

            info.AppendLine(Localizer.Format("#LOC_EVAREPAIRS_repairBotDesc"));
            return info.ToString();
        }
    }
}