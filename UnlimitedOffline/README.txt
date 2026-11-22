UNLIMITED OFFLINE 0.1.0
for Melvor Idle 2 (tested on 0.11.6-alpha.1), needs Melvor Mod Loader 0.2.0+

Melvor Idle 2 caps offline progress at 24 hours. This mod removes that cap: come back after
a week, a month or longer and the game simulates the whole absence.

HOW IT WORKS
  The game computes its offline limit from a modifier stat (Offline Progress / FlatOfflineTime),
  the same one shop upgrades use. The mod adds a 100-year bonus to that stat for every character,
  which is effectively unlimited. It is applied before the load-time offline calculation, and it
  also covers sleep/wake offline progress and the debug menu's offline time.

  Nothing is patched and nothing is written to the save. The bonus is applied again whenever a
  character is opened; uninstalling restores the normal 24-hour cap.

THINGS TO KNOW
  - The game simulates every missed tick. A very long absence (months) can make the first load
    take noticeably longer, and the offline summary will list the full time.
  - This is a cheat in the broad sense: it increases offline gains beyond the designed limit.
  - The 100-year value is a practical "unlimited"; no real absence comes close.

INSTALL
  Put this folder under the game's mods folder, so the file ends up as
  mods\UnlimitedOffline\UnlimitedOffline.dll
  (Steam > Melvor Idle 2 > Manage > Browse local files)

UNINSTALL
  Delete the folder mods\UnlimitedOffline

Unofficial, fan-made. Not affiliated with Games by Malcs or Jagex.

LICENSE
  MIT: free to use, change and share. See the repository's LICENSE file.
