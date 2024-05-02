EVA Repairs

The stock KSP game now has EVA Repair Kits that are used to fix things like solar panels, wheels, and landing legs. But why not use them to fix engines, drills, and ISRU converters? With EVA Repairs, now you can!

This mini-mod introduces part wear and tear, but only to a select few parts like the aforementioned engines, drills, and ISRU converters. These parts all have a Mean Time Between Failure (MTBF) rating, and when that rating reaches zero, the part is disabled until a kerbal or a repair bot goes outside and fixes it with one or more EVA Repair Kits. Once fixed, the part is re-enabled.

---INSTALLATION---

Simply copy all the files into your GameData folder. When done, it should look like:

GameData
	WildBlueIndustries
		EVARepairs

Changes

- EVA repairs-related events and displays have been moved to the PAW's EVA Repairs group.
- Solar panels and radiators can now optionally fail.
- Engines will reduce MTBF only when throttled up and running.
- If the option is enabled (it's off by default), then when a part with crew capacity fails, it can be repaired from the inside.
- Parts with less than 20% MTBF now have the option to Service the part to restore their MTBF before they fail.
- Parts that have worn out now have the option to Overhaul the part to restore its maximum possible MTBF. See below for details.
- You can now specify the default MTBF by part module. E.G. Engines have an MTBF of 1 (hour). See EVARepairs/BaselineConfig.cfg for details.
- Removed default MTBF from settings menu.
- In Settings, moved breakable part options to the new Breakable Things section.

Bug Fixes

- Fixed issue where MTBF would drain even when various modules weren't deployed, active, etc.
- Fixed issue where MTBF would show "-1" in the part info view.

Overhaul Game Mechanic

When the "Parts can wear out" option is enabled, parts will lose 10% of their maximum possible MTBF each time they're repaired. The intent was to have stations and ships wear out over time and need replacement. This release introduces a new game mechanic: Overhaul. Overhauling a part requires a Level 3 engineer or above, 4 Repair Kits, and a mass of Ore equal to 20% of the part's dry mass. An Overhaul will restore the part's maximum possible MTBF.

---LICENSE---
Art Assets, including .mu, .png, and .dds files are copyright 2021-2024 by Michael Billard, All Rights Reserved.

Wild Blue Industries is trademarked by Michael Billard. All rights reserved.
Note that Wild Blue Industries is a ficticious entity 
created for entertainment purposes. It is in no way meant to represent a real entity.
Any similarity to a real entity is purely coincidental.

Source code copyright 2021-2024 by Michael Billard (Angel-125)

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