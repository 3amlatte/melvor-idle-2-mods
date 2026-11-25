TIMESKIP 0.2.2
for Melvor Idle 2 (tested on 0.11.7-alpha), needs Melvor Mod Loader 0.2.0+

WHAT'S NEW IN 0.2.1
  - Mod Viewer integration: click TimeSkip's name to open the window; when Mod Viewer is
    installed, TimeSkip no longer binds F4 or appears in the loader's Mods menu.
  - If Mod Viewer is not installed (or is disabled), F4 opens TimeSkip as before.
  - The window follows the Mod Viewer theme stored in mods\ModViewer-settings.json.

If Mod Viewer is installed, open Mod Viewer (F2) and click TimeSkip. Without Mod Viewer, press
F4. Pick how far to skip, then click Skip Forward. The game pauses, simulates that much time,
and shows its normal offline progress summary.

OPTIONS
  1, 2, 4, 6 and 12 hours
  24 hours
  2, 4 and 7 days

THEME
  When Mod Viewer is installed, TimeSkip uses the same theme (background, accent and text
  colours). Without Mod Viewer it uses the default AI palette: dark panel and gold accent.

HOW IT WORKS
  The skip uses the game's own offline progress calculation, so farming, combat, actions,
  timers and offline bonuses all advance exactly as if you had been away that long.

  The offline cap is temporarily raised just enough for the chosen duration (plus one hour
  of headroom). It is removed again after the skip finishes, so normal offline progress keeps
  its usual cap unless you also use Unlimited Offline.

THINGS TO KNOW
  - The character really advances: crops grow, actions complete and combat is simulated.
    The game saves normally when you exit.
  - Long skips take a few seconds to simulate, because the game runs every missed tick.
  - The offline summary appears after the skip, so you can see what happened.
  - This is a cheat: it gives you time without waiting for it.

INSTALL
  Put this folder under the game's mods folder, so the file ends up as
  mods\TimeSkip\TimeSkip.dll
  (Steam > Melvor Idle 2 > Manage > Browse local files)

UNINSTALL
  Delete the folder mods\TimeSkip
  Nothing is written to your save.

With Mod Viewer installed, TimeSkip has no hotkey and opens by clicking its name there; it does
not add an entry to the loader's Mods menu. Without Mod Viewer, F4 opens it (and the loader's
Mods menu lists it). The key can be changed in the loader's Mods menu.

Unofficial, fan-made. Not affiliated with Games by Malcs or Jagex.

LICENSE
  MIT: free to use, change and share. See the repository's LICENSE file.
