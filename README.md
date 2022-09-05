Star Valor Plugins
===

Installing Plugins
---
These mods require the BepInEx mod framework.
Install the latest 5.x (x86) release, see here for instructions: [BepinEx](https://docs.bepinex.dev/articles/user_guide/installation/index.html)

**Important**: Star Valor is a 32-bit application. Make sure you choose the 32-bit version of BepinEx.

Recommended version: https://github.com/BepInEx/BepInEx/releases/download/v5.4.21/BepInEx_x86_5.4.21.0.zip

After installing BepInEx, download the latest release of the plugin (the .dll file) from the link below and put it in the BepInEx/plugins folder inside of your Star Valor game directory.

* As with all mods, make sure to **back up your save game** before trying a new mod.
* Only download mods from reliable sources: as with everything on the internet, be careful.

After installing, if your mods don't work, try the following:
> Navigate to the where you installed BepinEx, and open doorstop_config.ini with a text editor

> Change "ignoreDisableSwitch=false" to "ignoreDisableSwitch=true"

Downloads
---
Plugin links: [All plugins](https://www.dropbox.com/sh/bn4kfjyousemti0/AAAQMEH73Icp3-Yvi-WtwREZa?dl=0)
* **'Latest'**: Most recently compiled versions; 
* **'Stable'**: Versions known to be stable

Source links: [Minifix](https://github.com/Technological-Singularity/Star-Valor---Minifix)

---

Plugin Descriptions
===

Category: Minifix
===

These plugins are small, standalone fixes that that do not require any other plugins. Plugins that get too complex will be moved out of this category.

- [ ] Modifies save games: **No**
- [ ] Requires additional mods: **No**

Reliable Explosions
---
* Explosions check for detection more often, which should prevent particularly speedy ships from avoiding damage

Reordered Damage
---
* Damage is applied at the end of a frame, rather than during the physics or main update phases
* Consequences: Bumping into collectible objects will no longer deal damage before collecting them. Instead, they will be collected, and the damage that would be dealt will be ignored.

Junk Diver
---
* Junk destruction calculations tweaked - destroying one junk many times is now just as effective as destroying a big pile all at once
    * Destroying one piece of junk can grant up to one of the items below
    * Equipment has highest priority, then rare metal, then scrap metal
* Destroying junk can give weapons and equipment (as before, new calculations)
    * Chance is affected by Expert Scavenging (minor) and Master Scavenging (major, and does not require perk)
    * Higher average rarity with Master Scavenging (minor)
* Destroying junk can give scrap metal
    * Chance is affected by Expert Scavenging (major)
    * Quantity is affected by Scavenging and bonuses from ships or equipment
* Destroying junk can give rare metal
    * Chance is affected by Expert Scavenging (minor)
    * Quantity is affected by bonuses from ships or equipment


Multibarrel Memory
---
* Alternate fire mode for turrets (i.e., simultaneous multibarrel shots - available by right-clicking a weapon turret slot) will persist after swapping equipment
* Alternate fire mode for turrets will persist through warps
* Alternate fire mode for turrets will persist through save/load games - **note: this adds an additional file to your save directory**

Wider Targeting
---
* Mouseover targeting for ships is more generous, which should make targeting easier for small ships
* Allows targeting the nearest asteroid to the mouse using "Shift" + "Target Any" hotkey (same hotkeys that the base game uses; configurable as per normal)

Aggressive Projectiles (WIP)
---
* Missiles have more accurate targeting and can compensate for "circle of death" (i.e., where missiles just fly in a circle until they run out of fuel)
* Missiles use Gunner skill; more pronounced if the same gunner fires multiple missiles in quick succession
* Projectiles have more accurate targeting
* Battle computer has more accurate lead reticle and will show separate reticles for different valid weapons
* Point defense weapons will start firing earlier, allowing more reliable interception
* NPC enemies will chase more accurately (using the same method as projectile aiming)

Miner Gunners
---
* Allows your gunners to mine asteroids for you. To target an asteroid, the gunner must be set to "Miner" mode. 
* Access "Miner" mode using the Weapon or Crew screens - the "set AI control" button now cycles between different firing modes.
* Includes "Defensive", which is the standard AI control, and "Miner". More firing modes will be added in the future.

Blue Collar
---
* If your character has the 'White Collar' perk, opening the Character panel will replace it with the 'Miner' perk.
* You should remove this mod once you have removed 'White Collar', and save your game.