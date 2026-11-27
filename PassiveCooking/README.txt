PASSIVE COOKING 0.1.1
for Melvor Idle 2 (tested on 0.11.7-alpha), needs Melvor Mod Loader 0.2.0+

Runs the cooking recipe selected on the Cooking page as a passive action: its timer keeps
ticking while your character trains another skill and during offline progress. It never
takes the active action slot.

Only these cooking categories are cooked:
  - Recipe Bases
  - Basic Food

Selecting any other cooking category stops the passive action, so meals and other recipes
are left for you to cook normally.

SPEED TIERS (same as Passive Smelting)
  Cooking 1-50:    1 portion at a time (normal speed)
  Cooking 51-85:   2 portions at a time (interval halved)
  Cooking 86-99:   3 portions at a time (interval one third)

The tiers shorten the cooking interval. Ingredients, XP, success chance, preservation and
products stay exactly as the game intends for each portion.

MILESTONES TAB
  Open Cooking -> Milestones. A "Passive Cooking" entry appears below the cooking categories.
  Click it to see the current tier, the selected recipe and the level table. Choosing a
  category returns to the normal milestone list.

INSTALL
  Extract this zip into the Melvor Idle 2 game folder, so the file ends up as
  mods\PassiveCooking\PassiveCooking.dll
  (Steam > Melvor Idle 2 > Manage > Browse local files)
  The zip already contains the mods\PassiveCooking\ folder, so extract it into the game folder,
  not into the mods folder.

NOTES
  - Uses the game's own cooking action, so XP, success chance, preservation, products and
    offline progress stay exactly as the game intends.
  - No extra items: each tier produces more portions per cycle by shortening the interval,
    exactly like Passive Smelting.
  - If the recipe's requirements are not met, or materials/bank space are missing, the mod
    writes a clear line to MelvorModLoader.log and waits.
  - Unofficial, fan-made. Not affiliated with Games by Malcs or Jagex.
