EVA Repairs

The stock KSP game now has EVA Repair Kits that are used to fix things like solar panels, wheels, and landing legs. But why not use them to fix engines, drills, and ISRU converters? With EVA Repairs, now you can!

This mini-mod introduces part wear and tear, but only to a select few parts like the aforementioned engines, drills, and ISRU converters. These parts all have a Mean Time Between Failure (MTBF) rating, and when that rating reaches zero, the part is disabled until a kerbal or a repair bot goes outside and fixes it with one or more EVA Repair Kits. Once fixed, the part is re-enabled.

---INSTALLATION---

Simply copy all the files into your GameData folder. When done, it should look like:

GameData
	WildBlueIndustries
		EVARepairs

Changes

This update introduces the last type of part that can fail: the Probe core. Together with reaction wheels, engines, drills, generators, and converters, EVA Repairs now has a wide variety of parts that can fail out of the box. You can still make other types of parts fail if desired by adding a Module Manager patch to the part. This update also introduces the ability to account for how technological progress improves part reliability- and reduces the Reliability grind later in the game.

- New option: Probe cores can fail. When enabled, probe cores are subjected to MTBF, can optionally fail during activation checks, and optionally have Reliability ratings. Probe cores don't lose MTBF when they're hibernating, and they can fail when they go into or out of hibernation.

- New option: Tech level affects Reliability. When enabled, the starting and maximum Reliability improves as technology improves. At the R&D building's starting Level 1, the maximum possible Reliability that a part can attain is 90%. At Level 2, the maximum improves to 95%. If parts previously reached the 90% maximum Reliability cap, then further testing can bring them up to 95%. And at Level 3, the maximum caps at 99%. Again, parts can be further tested to 99% Reliability. 

Similarly, starting with General Rocketry, as you unlock the various rocketry tech tree nodes (General Rocketry, Advanced Rocketry, Heavy Rocketry, Heavier Rocketry, Very Heavy Rocketry), the starting Reliability of a part with no flight experience improves by 1 to 10%, potentially allowing parts with no flight experience to start at the maximum possible Reliability. Example: your starting Reliability is 50%, and you unlock General Rocketry. The game rolls up a 5% starting Reliability bonus for General Rocketry. You start testing the LV-T30 "Reliant," which has never flown before. Instead of starting with 50% Reliability, it starts with 55% Reliability. Since your R&D building is currently at Level 1, the maximum Reliability that the LV-T30 can attain is 90%.

- Fixed issue where converter states weren't being recorded properly.
- Fixed NRE that happened when ships were loaded into the editor.

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