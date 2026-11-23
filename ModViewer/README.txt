MOD VIEWER 0.1.0
for Melvor Idle 2 (tested on 0.11.6-alpha.1), needs Melvor Mod Loader 0.2.0+

Press F2 (or pick it in the loader's Mods menu) to open a list of every installed mod, both
enabled and disabled.

GROUPS
  Mods whose folder contains a "3am.mod" marker file are listed under "3am Mods". Everything
  else is under "Other mods". The marker is included in every 3am mod package, so no name
  list needs maintaining.

ENABLED / DISABLED
  Unchecking Enabled moves the mod's folder (or loose DLL) from mods\ to mods-disabled\.
  Checking it moves it back. The loader only scans mods\, so a disabled mod does not load on
  the next start. A running mod keeps running until then, so the window asks you to restart.
  The "Restart Game" button quits and launches Melvor Idle 2 again through Steam.

SETTINGS
  Mods with a settings.json get a Settings button.
    - Generic files show checkboxes for true/false and text boxes for numbers/strings.
    - Melvor Auto has a dedicated page: its toggles, a checkbox per bone bigger than Big Bone,
      and a checkbox per openable item. Those lists come from Melvor Auto's available.json.
  Settings changes are written immediately. Melvor Auto reloads them while the game runs;
  other mods may need the restart.

INSTALL
  Put this folder under the game's mods folder, so the file ends up as
  mods\ModViewer\ModViewer.dll
  (Steam > Melvor Idle 2 > Manage > Browse local files)

UNINSTALL
  Delete the folder mods\ModViewer
  Nothing is written to your save.

Unofficial, fan-made. Not affiliated with Games by Malcs or Jagex.

LICENSE
  MIT: free to use, change and share. See the repository's LICENSE file.
