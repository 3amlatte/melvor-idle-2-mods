BACKUP SAVER 0.1.0
==================

A preinstalled mod for Mod Loader for MacOS. It keeps automatic copies of your
Melvor Idle 2 save files so a bad save, a broken mod, or a mistake cannot cost
you a character.

No button and no settings: it runs when the game starts and after each local
save.


WHERE BACKUPS GO
----------------
    ~/Library/Application Support/MelvorModLoader/backups/

Each backup is a dated folder, for example:

    2026-11-28_09-14-03/
      steam/<steam-id>/saves/save_<save-id>.sav

The folder layout mirrors the game's own user-data folder, so restoring is a
straight copy back.

The five newest backups are kept. Older ones are deleted automatically.


RESTORE A BACKUP
----------------
1. Quit Melvor Idle 2.
2. Open the dated backup folder you want.
3. Copy its `saves` folder over the game's `saves` folder, keeping the same
   paths.

The game's user-data folder on macOS is usually:

    ~/Library/Application Support/Games By Malcs/Melvor Idle 2/

For a Steam install the save files are under:

    .../Games By Malcs/Melvor Idle 2/steam/<steam-id>/saves/


GOOD TO KNOW
------------
- A new backup is skipped when the saves have not changed since the last one.
- The game's own `save_backups` folder is not copied; these are separate backups.
- Backups are outside the game .app, so Steam updates do not remove them.
- Deleting this mod's folder stops new backups. Existing backups stay.


UNINSTALL
---------
Disable or remove `mods/BackupSaver` in Mod Viewer.
Backups in `~/Library/Application Support/MelvorModLoader/backups` are kept.
