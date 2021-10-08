EVA Repairs

The stock KSP game now has EVA Repair Kits that are used to fix things like solar panels, wheels, and landing legs. But why not use them to fix engines, drills, and ISRU converters? With EVA Repairs, now you can!

This mini-mod introduces part wear and tear, but only to a select few parts like the aforementioned engines, drills, and ISRU converters. These parts all have a Mean Time Between Failure (MTBF) rating, and when that rating reaches zero, the part is disabled until a kerbal or a repair bot goes outside and fixes it with one or more EVA Repair Kits. Once fixed, the part is re-enabled.

---INSTALLATION---

Simply copy all the files into your GameData folder. When done, it should look like:

GameData
	WildBlueIndustries
		EVARepairs

Changes

- Added new TECH_UNLOCK_BONUS config node that specifies which nodes will provide a starting reliability bonus. This config node makes it possible to support
non-stock tech trees.
- Removed hard-coded stock tech-unlock bonus nodes and moved them to the TECH_UNLOCK_BONUS config nodes found in BaselineConfig.
- Fixed issue where probe cores that hibernate on warp were still losing MTBF while timewarping.

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