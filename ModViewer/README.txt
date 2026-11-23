MOD VIEWER 0.1.1
for Melvor Idle 2 (tested on 0.11.6-alpha.1), needs Melvor Mod Loader 0.2.0+

Press F2 (or pick it in the loader's Mods menu) to open a list of every installed mod, both
enabled and disabled.

GROUPS
  Mods whose folder contains a "3am.mod" marker file are listed under "3am Mods". Everything
  else is under "Other mods". The marker is included in every 3am mod package, so no name
  list needs maintaining. Click either group heading to collapse or expand it.

THEMES
  The Theme drop-down in the header switches the whole viewer palette:
    AI        the original dark navy + gold look (default)
    Dark      black + grey
    Light     warm paper + dark text
    Midnight  deep blue + periwinkle
    Forest    dark green + mint
    Sakura    plum + pink
    Contrast  black + yellow
  The choice is saved in ModViewer\settings.json. When TimeSkip is installed, its window
  uses the same theme. Buttons, checkboxes and text fields are themed too, so the green
  default controls no longer stand out.

FOLDER
  The "Mods Folder" button in the footer opens the game's mods folder in Explorer.

ENABLED / DISABLED
  Unchecking Enabled moves the mod's folder (or loose DLL) from mods\ to mods-disabled\.
  Checking it moves it back. The loader only scans mods\, so a disabled mod does not load on
  the next start. A running mod keeps running until then, and a banner reminds you to restart
  to see the updated list. The "Restart Game" button quits and launches Melvor Idle 2 again
  through Steam.

SETTINGS AND MOD WINDOWS
  Click a mod's name to open its settings, or, if it has no settings, its own window.
  Hover over an option for a short explanation.
    - Mods with a settings.json show an editor: checkboxes for true/false, text boxes for
      numbers/strings.
    - Melvor Auto has a dedicated settings page: its toggles, collapsible Auto Farmer and
      Auto Ranching groups, and icon-card checklists for bones and openables. Those lists
      come from available.json.
    - TimeSkip has no settings file, so clicking its name opens its skip window.
    - Astrology Knowledge Multiplier does not register a loader window; Mod Viewer opens its
      panel through the loaded mod instance.
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
