EVA Repairs

The stock KSP game now has EVA Repair Kits that are used to fix things like solar panels, wheels, and landing legs. But why not use them to fix engines, drills, and ISRU converters? With EVA Repairs, now you can!

This mini-mod introduces part wear and tear, but only to a select few parts like the aforementioned engines, drills, and ISRU converters. These parts all have a Mean Time Between Failure (MTBF) rating, and when that rating reaches zero, the part is disabled until a kerbal or a repair bot goes outside and fixes it with one or more EVA Repair Kits. Once fixed, the part is re-enabled.

To enable EVA Repairs, pause the game at the Space Center, press the Settings button, and navigate to the EVA Repairs tab. There you'll find an option named "Parts require maintenance." Make sure that the option is selected. When you do, you'll have more configurable options:

You can select "Parts fail when activated." If you select this option, then whenever a part like an engine is activated, then there's a chance that the part will fail and require maintenance. The chance of this happening depends upon the ratio between the part's current MTBF and its maximum possible MTBF. Thus, newer parts won't break as often as older parts.

You can also select "Parts wear out." If selected, then whenever a part is repaired, it can lose some of its maximum possible MTBF. Exactly how much depends upon the part config, and not all parts are guaranteed to wear out. But when a part breaks and runs out of MTBF, it can never be repaired again.

You can also select "Flight experience improves future Reliability." You will need to have "Parts fail when activated" enabled in order to select this option.
Reliability represents flight experience, and FUTURE missions will benefit from Reliability gained on the CURRENT flight. When selected, Reliability becomes a factor when determining if a part fails upon activation. When starting out, parts have 30 Reliability, but it can go up to 100. If the activation check suceeds, then for future missions, all parts of the same type will start with 10 more points of Reliability when they are launched. So, if a Reliant engine had 30 Reliability on the current flight, the next flight with a Reliant engine starts with 40 Reliability. If the check fails, they only gain 5 Reliability, but you also gain 2 Science points as you learn from the failure. Once a part starts with 100 Reliability, the only thing that will cause it to fail is having low MTBF.

As a final configurable option, you can vary the starting Reliability from 30 to 80. You will need "Flight experience improves future Reliability" enabled to vary the starting Reliability. The default is 50.

That's all there is to this mini-mod- no griding out reliability like in BARIS (unless you want to), no vehicle integration times, no random events. Just a simple means for parts to wear out and to be repaired again.

The required part module, ModuleEVARepairs, is automatically added to any part that has a part module based on BaseConverter (resource converters/drills), ModuleGenerator, and/or ModuleEngines.
If you have a part that doesn't have a module based on one of these, then you need to manually add it via a Module Manager patch.
To add the ModuleEVARepairs part module to a part config:

@PART[somePartName]:NEEDS[EVARepairs]
{
	MODULE
	{
		name = ModuleEVARepairs

		// In hours, how long until the part needs maintenance in order to function. Default is 600. Time is counted even when the vessel isn't active!
		// Note: The part module is smart and if the part has at least one engine, generator or resource converter 
		// then the engine/generator/converter needs to be running for the current mtbf to be reduced.
		mtbf = 600

		// Percent of MTBF lost each time the part is repaired. If a part has no MTBF remaining then it has worn out and is permanently disabled. 
		// Default is 0, which means no MTBF is lost.
		// NOTE: This only applies if the "Parts can wear out" game difficulty setting is enabled.
		mtbfPercentLostPerCycle = 10

		// The skill required to perform repairs. Default is RepairSkill (Engineers have this).
		repairSkill = RepairSkill

		// The minimum skill level required to perform repairs. Default is 1.
		minimumSkillLevel = 1

		// The part name that is consumed during repairs. It MUST be a part that can be stored in an inventory. 
		// Default is evaRepairKit (the stock EVA Repair Kit).
		repairKitName = evaRepairKit

		// The number of repair kits required to repair the part. Default is 1.
		repairKitsRequired = 1

		// You can specify one or more part modules that ModuleEVARepairs is responsible for.
		// When the part needs maintenance or wears out, the modules on this list will be disabled.
		// Similarly, when the part is repaired, the modules on the list will be re-enabled.
		// For convenience, engines, generators, and converters are automatically added to the list.
		breakablePartModule = WBIModuleGraviticRCS
		breakablePartModule = WBIPlumeController
	}
}

To make a part a Repair Bot:

@PART[somePartName]:NEEDS[EVARepairs]
{
	MODULE
	{
		name = ModuleEVARepairBot
	}
}

---INSTALLATION---

Simply copy all the files into your GameData folder. When done, it should look like:

GameData
	WildBlueIndustries
		EVARepairs


---LICENSE---
Art Assets, including .mu, .png, and .dds files are copyright 2021 by Michael Billard, All Rights Reserved.

Wild Blue Industries is trademarked by Michael Billard. All rights reserved.
Note that Wild Blue Industries is a ficticious entity 
created for entertainment purposes. It is in no way meant to represent a real entity.
Any similarity to a real entity is purely coincidental.

Source code copyright 2021 by Michael Billard (Angel-125)

    This source code is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.