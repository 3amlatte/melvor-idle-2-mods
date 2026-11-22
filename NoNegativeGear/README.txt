NO NEGATIVE GEAR 0.1.0
for Melvor Idle 2 (tested on 0.11.6-alpha.1), needs Melvor Mod Loader 0.2.0+

Removes the negative stats and bonuses from equipment. A Rune Platebody normally gives
-100 Accurate Attack Bonus; with this mod it gives no penalty. Positive values are untouched.

By default only armour is changed (anything without a Weapon capability). Weapons are
left alone; set "IncludeWeapons": true in settings.json to include them too.

INSTALL
  Put this folder under the game's mods folder, so the file ends up as
  mods\NoNegativeGear\NoNegativeGear.dll
  (Steam > Melvor Idle 2 > Manage > Browse local files)

UNINSTALL
  Delete the folder mods\NoNegativeGear
  Nothing in your save was changed: uninstalling restores the original stats.

SETTINGS
  settings.json next to the DLL, created on first run:
    {
      "IncludeWeapons": false
    }

WHAT IT DOES
  The mod edits the game's item data once per character load:
  - combat stats (Attack Bonus, Defence Bonus, resistances, ...) below 0 become 0;
  - equipment bonuses the game itself marks as negative become 0.
  Item tooltips show the changed values, so what you see matches what you get.

  Values whose game meaning is "lower is better" (for example a negative action
  interval) are left alone.

  last-run.txt next to the DLL lists everything that was changed on the last run.

This is a cheat: it makes gear stronger than designed. Nothing is written into the save.
Unofficial, fan-made. Not affiliated with Games by Malcs or Jagex.

LICENSE
  MIT: free to use, change and share. See the repository's LICENSE file.
