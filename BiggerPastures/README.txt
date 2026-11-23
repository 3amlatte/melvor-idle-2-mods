BIGGER PASTURES 0.1.0
for Melvor Idle 2 (tested on 0.11.6-alpha.1), needs Melvor Mod Loader 0.2.0+

Doubles the number of animals every Ranching enclosure can hold: each pen type, current and
upgraded, gets twice its normal limit. A 6-animal pasture becomes 12, and so on.

HOW IT WORKS
  The animal limit is part of the enclosure's game data and is read every time the game checks
  whether an animal fits or whether breeding is allowed. The mod doubles that value for every
  enclosure type when a character is opened, so the Ranching screen, the build/upgrade panel
  and the breeding logic all use the new limit.

  Nothing is written to the save. Uninstalling and reopening the game restores the original
  limits; animals above the old limit stay until you move them or the pen changes.

INSTALL
  Put this folder under the game's mods folder, so the file ends up as
  mods\BiggerPastures\BiggerPastures.dll
  (Steam > Melvor Idle 2 > Manage > Browse local files)

UNINSTALL
  Delete the folder mods\BiggerPastures

This is a cheat: it lets each pasture hold more animals than designed.
Unofficial, fan-made. Not affiliated with Games by Malcs or Jagex.

LICENSE
  MIT: free to use, change and share. See the repository's LICENSE file.
