MELVOR AUTO 0.1.0
for Melvor Idle 2 (tested on 0.11.6-alpha.1), needs Melvor Mod Loader 0.2.0+

Three conveniences in one mod. Everything uses the game's own bury, open, plant, harvest,
water, compost and mill calls, so XP, Prayer Points, bonuses, statistics and offline progress
stay correct.

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
  those automations on for every purchased plot, so the game's own automation does the work,
  including during offline progress. The flags are re-applied every few seconds so newly
  purchased plots are covered too.

  Some recipes and higher-tier plots have their own automation requirements. The mod checks
  them and writes a clear log line for each plot that cannot be automated yet, naming the plot
  and crop, so you know what is still gated.

  The grain mill is also stocked with every valid storage item and started with the selected
  mill recipe, or the first affordable one. This part can be switched off with AutoMill.

SETTINGS
  settings.json next to the DLL. The 3am Mods hub edits the same file and Melvor Auto reloads
  it while the game runs. Defaults:
    AutoBuryBones=true, AutoOpenItems=true,
    AutoFarm=true, AutoHarvest=true, AutoCompost=true, AutoWater=true, AutoPlant=true,
    AutoMill=true.
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
