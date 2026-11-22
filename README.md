# Melvor Idle 2 mods

C# mods for **Melvor Idle 2** (Steam, Windows), built on
[Melvor Mod Loader](https://github.com/falorfrozen-cmd/MelvorModLoader). One folder per mod;
each builds straight into the game's `mods` folder.

| Mod | What it does |
|:--|:--|
| [NoNegativeGear](NoNegativeGear) | Removes negative stats and bonuses from armour (weapons optional). |

Tested with game **0.11.6-alpha.1** and Melvor Mod Loader **0.2.0**.

> These are personal, fan-made mods. Several of them make the game easier than designed — that is
> the point. Nothing is affiliated with Games by Malcs or Jagex.

## Requirements

- Melvor Idle 2 installed through Steam (Windows).
- .NET SDK 10 or newer. A newer SDK can target `net10.0`; the targeting pack comes from NuGet.
- .NET 10 runtime ships inside the game, so players do not need it to run a mod.

## Layout

```
MelvorMods\
  Directory.Build.props          shared GameDir and compiler settings
  scripts\package.ps1            builds a release zip into dist\
  NoNegativeGear\
    NoNegativeGear.csproj        build target: mods\NoNegativeGear\NoNegativeGear.dll
    Mod.cs                       IMod entry point, settings, log
    Clamp.cs                     the item-data edits
    Accessors.cs                 private game members via UnsafeAccessor
    README.txt                   ships inside the zip
    settings.json                ships inside the zip
```

`Directory.Build.props` defaults `GameDir` to
`G:\SteamLibrary\steamapps\common\Melvor Idle 2`. Override it if the game lives elsewhere:

```powershell
dotnet build -c Release -p:GameDir="D:\SteamLibrary\steamapps\common\Melvor Idle 2"
```

## Build and test

Close the game first: it keeps the mod DLL open while running.

```powershell
dotnet build -c Release .\NoNegativeGear\NoNegativeGear.csproj
```

The build lands in `mods\NoNegativeGear\` inside the game folder. Start the game, open a
character, then read `MelvorModLoader.log` in the game folder.

## Package

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\package.ps1
# or a single mod:
powershell -ExecutionPolicy Bypass -File .\scripts\package.ps1 -Mods NoNegativeGear
```

Output:

```
dist\NoNegativeGear-0.1.0.zip          extract into the game folder
dist\NoNegativeGear-0.1.0.zip.sha256   publish next to the download
```

The script builds into a staging folder rather than the live game folder, so it is safe to run
while the game is open. The version in the zip name comes from `<Version>` in the mod's csproj.

## Adding a mod

1. Create `<Name>\<Name>.csproj` with `<OutputPath>$(GameDir)\mods\$(AssemblyName)\</OutputPath>`
   and the three references (`MelvorModLoader`, `GodotSharp`, `GamesByMalcs.Melvor2`, all
   `Private=false`).
2. Add a public class implementing `MelvorModLoader.IMod`.
3. Add `README.txt` and, if the mod has settings, a default `settings.json`.
4. Add the mod to the table above.
