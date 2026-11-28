# Publishing a mod

Every Nexus publish must be reflected on GitHub in the same pass. A mod is not
considered published until both sides match.

## Checklist

1. **Build both packages**
   - `.\scripts\package.ps1 -Mods <Mod>`
   - `.\scripts\package-macos.ps1 -Mods <Mod>`
   - Bump the `.csproj` version first. Every update ships a Windows and a macOS
     zip. Mod Viewer is the only exception (macOS loader includes it).

2. **Nexus**
   - Upload the Windows zip as the `main` file and the macOS zip as the
     `<Mod> Mac` optional file.
   - Set the page version to the new version.
   - Update the description (and add a "New in x.x" section).
   - Make sure every `[size=4]` section header has a blank line above it.
   - Wait for both file scans to show `cleared`.

3. **GitHub (never skip this)**
   - Create a release tagged `<mod>-v<version>` (lowercase, no spaces).
   - Attach `dist\<Mod>-<version>.zip`, its `.sha256`, the macOS zip and its
     `.sha256`.
   - Release notes: what changed, Windows + macOS included, game/loader versions.

4. **README**
   - Every published mod has a row in the mods table.
   - The install column links to **Nexus**, not to a GitHub release.
   - Add new published mods, update renamed ones, remove mods that are not
     standalone (for example BackupSaver, which ships with the macOS loader).
   - Work-in-progress mods stay out of the table until their Nexus page exists.

5. **Modpack (when the mod is bundled)**
   - Rebuild the modpack, bump `Modpack/version.txt`, upload Windows + macOS.
   - Update the modpack description's Included list and the GitHub release.
   - Keep the README modpack link pointing at its Nexus page.

6. **Final check**
   - Nexus page version == release tag version.
   - Both Nexus files are scan-cleared.
   - README row links to the Nexus page.
