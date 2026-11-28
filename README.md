# Melvor Idle 2 mods

Source code for my **Melvor Idle 2** (Steam; Windows, plus an experimental macOS build) mods, built on
[Melvor Mod Loader](https://github.com/falorfrozen-cmd/MelvorModLoader).

**Downloads and installation: [Nexus Mods](https://www.nexusmods.com/melvoridle2).**
This repository is for viewing the source. Don't install by copying files from here — use the
zips on Nexus, which contain exactly the files the game needs.

| Mod | What it does | Install |
|:--|:--|:--|
| [NoNegativeGear](NoNegativeGear) | Removes negative stats and bonuses from armour (weapons optional). | [Nexus](https://www.nexusmods.com/melvoridle2/mods/25) |
| [SeedReturn](SeedReturn) | Chance to return seeds after harvesting. Three versions: Lite (1–20%), Standard (20–60%), and Max (60–150%). | [Nexus](https://www.nexusmods.com/melvoridle2/mods/26) |
| [UnlimitedOffline](UnlimitedOffline) | Removes the 24-hour offline progress cap. | [Nexus](https://www.nexusmods.com/melvoridle2/mods/27) |
| [TimeSkip](TimeSkip) | Skip the character forward by 1 hour up to 7 days; opened from Mod Viewer. | [Nexus](https://www.nexusmods.com/melvoridle2/mods/28) |
| [PassiveSmelting](PassiveSmelting) | Runs the selected smelting recipe passively while you train another skill; 1–3 bars by level. | [Nexus](https://www.nexusmods.com/melvoridle2/mods/34) |
| [PassiveCooking](PassiveCooking) | Runs the selected recipe base or basic food passively, with the same tiers and a Cooking milestones entry. | [Nexus](https://www.nexusmods.com/melvoridle2/mods/61) |
| [BiggerPastures](BiggerPastures) | Doubles the animal limit of every Ranching enclosure. | [Nexus](https://www.nexusmods.com/melvoridle2/mods/41) |
| [MorePets](MorePets) | Dungeon Bosses pet collection with permanent stance and global buffs; shown in Completion Log → Pets. | [Nexus](https://www.nexusmods.com/melvoridle2/mods/59) |
| [MelvorAuto](MelvorAuto) | Auto bone burial, auto opener, free farming automation, mill stocking, ranch repairs and petting. | [Nexus](https://www.nexusmods.com/melvoridle2/mods/44) |
| [MasteryEta](MasteryEta) | Compact Mastery Guild task readout in the action footer: task icon, progress bar and live ETA; supports multiple simultaneous actions; click to open the category. | [Nexus](https://www.nexusmods.com/melvoridle2/mods/71) |
| [ModViewer](ModViewer) | F2 mod list, developer groups, themes, mods-folder button, enable/disable, settings, mod windows, restart. | [Nexus](https://www.nexusmods.com/melvoridle2/mods/43) |
| [3am Modpack](Modpack) | Every 3am mod in one download; Seed Return Lite/Max are optional alternatives. | [GitHub release](https://github.com/3amlatte/melvor-idle-2-mods/releases/tag/3am-modpack-v1.1.4) |

Tested with game **0.11.7-alpha** and Melvor Mod Loader **0.2.0**.

> These are personal, fan-made mods. Some make the game easier than designed — that is the point.
> Nothing is affiliated with Games by Malcs or Jagex.

## Installing a mod

1. Install [Melvor Mod Loader](https://www.nexusmods.com/melvoridle2/mods/2) first.
2. Download the mod's zip from Nexus.
3. Extract it into the game folder (Steam → Melvor Idle 2 → Manage → Browse local files), so the
   files end up under `mods\<ModName>\`.

On macOS, use [Mod Loader for MacOS](https://github.com/3amlatte/Mod-Loader-for-MacOS)
and extract the same zips into `~/Library/Application Support/MelvorModLoader`, so the files end up under
`.../MelvorModLoader/mods/<ModName>/`. Most mods are platform-neutral; Mod Viewer 0.1.12 added macOS versions
of its folder and restart actions.

There is no installer. On Windows, install [Mod Viewer](ModViewer) for an in-game mod list, settings,
enable/disable and quick restarts. Mod Viewer is already bundled with Mod Loader for MacOS, so macOS
users do not install it separately. Without it, a mod with options reads `settings.json` from
its own folder; delete the mod's folder to uninstall it.

Mod authors can group their mods automatically with a `group.txt` file; see
[GROUPING.md](GROUPING.md).

## Releases

Every mod update publishes a Windows package and a matching macOS package on Nexus and
GitHub. Mod Viewer is the exception: macOS builds include it in the launcher, so it does not
need a separate macOS download.

## Offline behaviour

Production and automation mods must hook `Game.SaveLoaded` so they are active before the
game's load-time offline ticks; binding only on `GameView` is too late on the first load.
See [OFFLINE.md](OFFLINE.md) for the pattern and the checklist every new mod follows.

## License

MIT — free to use, change and share, including commercially. See [LICENSE](LICENSE).
