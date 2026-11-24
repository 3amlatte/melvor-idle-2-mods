# Mod grouping in Mod Viewer

Mod Viewer groups mods automatically. Nothing needs to be configured by the player.

## For mod authors

Put a file called `group.txt` in the root of your mod folder:

```text
Example Developer
```

The first non-empty line is the group name. Ship it in your zip next to the DLL, for example:

```text
mods\YourMod\YourMod.dll
mods\YourMod\group.txt
```

Mod Viewer reads `group.txt` from every folder in `mods` and `mods-disabled`, so the mod
appears under **Example Developer** as soon as it is installed.

If a mod does not include `group.txt`, it appears under **Other mods** unless the player maps
it manually in `mods\ModViewer-groups.json`.

## For players

You do not need to edit anything for mods that include `group.txt`.

If a third-party mod does not include one, you can optionally create
`mods\ModViewer-groups.json`:

```json
{
  "Some Developer": ["AstrologyKnowledgeMultiplier", "DungeonCompletions"]
}
```

Match by the mod folder name or assembly name. Every group heading in Mod Viewer is
collapsible. 3am Mods is shown first, other groups alphabetically, and Other mods last.

## 3am mods

Every mod built or packaged from this repository automatically gets:

- `group.txt` containing `3am Mods`
- the older `3am.mod` marker, still recognised by Mod Viewer

Future 3am mods therefore land in the **3am Mods** group without any manual step. The build
uses the `ModGroup` property in `Directory.Build.props`; a project can override it or ship its
own `group.txt` if it belongs somewhere else.
