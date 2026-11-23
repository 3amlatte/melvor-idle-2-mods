MELVOR AUTO 0.1.0
for Melvor Idle 2 (tested on 0.11.6-alpha.1), needs Melvor Mod Loader 0.2.0+

Several conveniences in one mod. Everything uses the game's own bury, open, plant, harvest,
water, compost, mill, repair and pet actions, so XP, Prayer Points, bonuses, statistics and
offline progress stay correct.

AUTO BONE BURIAL
  Any bone received in the bank is buried immediately. Bones worth more than Big Bone are
  excluded by default, because they are used by Herblore and Beastcrafting:
    Large Bone (60), Small Dragon Bone (80), Dragon Bone (100), Large Dragon Bone (175)
  Each of those can be switched back on individually in settings.json (ExcludedBones).

AUTO OPENER
  Any openable item received in the bank is opened immediately. Opened loot that is itself
  openable or buryable is handled too. Items can be excluded in settings.json
  (ExcludedOpenables), so a chest type you want to keep is left alone.

AUTO FARMER
  Makes every Farming automation free: the wine (and any other item/currency) normally charged
  for auto-plant, auto-harvest, auto-water and auto-compost is reduced to zero. It then switches
  those automations on for every purchased plot when a character opens, so the game's own
  automation does the work, including during offline progress. A plot bought during the session
  is picked up the next time you open the game.

  Some recipes and higher-tier plots have their own automation requirements. The mod checks
  them and writes a clear log line for each plot that cannot be automated yet, naming the plot
  and crop, so you know what is still gated.

  The grain mill is stocked with every valid storage item every 2 real hours (the game clock
  runs 1:1, so that is also 2 in-game hours). The time is saved per character, so quitting and
  reopening the game does not restart the timer. On the first run for a character it stocks
  immediately. If the mill is already running it is only topped up, never stopped or toggled
  off; if it is stopped, the selected recipe (or the first affordable one) is started. This
  part can be switched off with AutoMill.

AUTO RANCHING
  Two independent toggles:
    AutoRanchMaintenance (default on)
      Runs whenever the game lowers enclosure maintenance (about once an hour) and repairs
      every enclosure that needs it through the game's own Repair all. It also repairs on
      character load. If a repair cannot be afforded, the log names the enclosure and lists
      exactly what is missing, for example:
        Auto Ranching: could not repair Meadow (Basic Enclosure, 82%). Missing 3x Maple Logs
        (have 1), 1,200 GP (have 300).
      Repairs are done after offline progress finishes, not repeatedly during it.

    AutoRanchPets (default on)
      Whenever an animal wants petting, all animals that accept pets are petted through the
      game's own Pet all animals action. Petting waits until offline progress has finished.

SETTINGS
  settings.json next to the DLL. The Mod Viewer edits the same file. Bone/open changes apply
  while the game runs; the farming automation flags are read when a character opens. Defaults:
    AutoBuryBones=true, AutoOpenItems=true,
    AutoFarm=true, AutoHarvest=true, AutoCompost=true, AutoWater=true, AutoPlant=true,
    AutoMill=true, AutoRanchMaintenance=true, AutoRanchPets=true.
  available.json lists the bones and openable items the hub shows in its checklists.

INSTALL
  Put this folder under the game's mods folder, so the file ends up as
  mods\MelvorAuto\MelvorAuto.dll
  (Steam > Melvor Idle 2 > Manage > Browse local files)

UNINSTALL
  Delete the folder mods\MelvorAuto
  Nothing is written to the save.

This is a cheat: it automates actions that normally take time or clicks.
Unofficial, fan-made. Not affiliated with Games by Malcs or Jagex.

LICENSE
  MIT: free to use, change and share. See the repository's LICENSE file.
