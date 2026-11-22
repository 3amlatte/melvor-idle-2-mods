# Melvor Idle 2 mods

Source code for my **Melvor Idle 2** (Steam, Windows) mods, built on
[Melvor Mod Loader](https://github.com/falorfrozen-cmd/MelvorModLoader).

**Downloads and installation: [Nexus Mods](https://www.nexusmods.com/melvoridle2).**
This repository is for viewing the source. Don't install by copying files from here — use the
zips on Nexus, which contain exactly the files the game needs.

| Mod | What it does | Install |
|:--|:--|:--|
| [NoNegativeGear](NoNegativeGear) | Removes negative stats and bonuses from armour (weapons optional). | Nexus (link when published) |

Tested with game **0.11.6-alpha.1** and Melvor Mod Loader **0.2.0**.

> These are personal, fan-made mods. Some make the game easier than designed — that is the point.
> Nothing is affiliated with Games by Malcs or Jagex.

## Installing a mod

1. Install [Melvor Mod Loader](https://www.nexusmods.com/melvoridle2/mods/2) first.
2. Download the mod's zip from Nexus.
3. Extract it into the game folder (Steam → Melvor Idle 2 → Manage → Browse local files), so the
   files end up under `mods\<ModName>\`.

There is no installer and no in-game settings window. A mod with options reads
`settings.json` from its own folder; delete the mod's folder to uninstall it.

## Building

Only needed to work on the source.

- Melvor Idle 2 installed through Steam (Windows).
- .NET SDK 10 or newer. A newer SDK can still target `net10.0`; the targeting pack comes from
  NuGet. Players don't need .NET at all — the game ships its own runtime.

`Directory.Build.props` defaults `GameDir` to `G:\SteamLibrary\steamapps\common\Melvor Idle 2`.
Override it if the game lives elsewhere:

```powershell
dotnet build -c Release -p:GameDir="D:\SteamLibrary\steamapps\common\Melvor Idle 2"
```

## Layout

```
melvor-idle-2-mods\
  Directory.Build.props          shared GameDir and compiler settings
  scripts\package.ps1            builds the Nexus zip into dist\
  NoNegativeGear\
    NoNegativeGear.csproj        build target: mods\NoNegativeGear\NoNegativeGear.dll
    Mod.cs                       IMod entry point, settings, log
    Clamp.cs                     the item-data edits
    Accessors.cs                 private game members via UnsafeAccessor
    README.txt                   ships inside the zip
    settings.json                ships inside the zip
```

## Build and test

Close the game first: it keeps the mod DLL open while running.

```powershell
dotnet build -c Release .\NoNegativeGear\NoNegativeGear.csproj
```

The build lands in `mods\NoNegativeGear\` inside the game folder. Start the game, open a
character, then read `MelvorModLoader.log` in the game folder.

## Package a mod for Nexus

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\package.ps1
# or a single mod:
powershell -ExecutionPolicy Bypass -File .\scripts\package.ps1 -Mods NoNegativeGear
```

Output:

```
dist\NoNegativeGear-0.1.0.zip          upload this to Nexus
dist\NoNegativeGear-0.1.0.zip.sha256   optional, for the file description
```

The script builds into a staging folder rather than the live game folder, so it is safe to run
while the game is open. The version in the zip name comes from `<Version>` in the mod's csproj.
Zips are deliberately not committed to this repository.

## Adding a mod

1. Create `<Name>\<Name>.csproj` with `<OutputPath>$(GameDir)\mods\$(AssemblyName)\</OutputPath>`
   and the three references (`MelvorModLoader`, `GodotSharp`, `GamesByMalcs.Melvor2`, all
   `Private=false`).
2. Add a public class implementing `MelvorModLoader.IMod`.
3. Add `README.txt` and, if the mod has settings, a default `settings.json`.
4. Add the mod to the table above and create its Nexus page.
