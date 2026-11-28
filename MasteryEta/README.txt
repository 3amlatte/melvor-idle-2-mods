MASTERY ETA 0.1.1
for Melvor Idle 2 (tested on 0.11.7-alpha), needs Melvor Mod Loader 0.2.0+

Adds a compact mastery readout inside the game's action footer, next to the item-depletion timer. When the action you are doing is
part of a Mastery Guild task - for example "Cook 400 Beef" or "Create 400 Rune Defenders" -
the row shows:

  - the Mastery icon and the task ("Cook 123 / 400 Beef");
  - the item/action icon;
  - a progress bar;
  - the estimated time left, based on the progress rate you are actually getting. Until
    the first progress sample arrives it shows "Calculating ETA".

Click a cell to open that skill's Mastery Guild on the matching category. The readout sits
in the footer next to the item-depletion timer and disappears when the
current action has no matching incomplete mastery task. If more than one active skill has a
matching mastery task at once, one compact cell is shown per skill along the footer, and up
to two different masteries for the same skill. Tiers of the same mastery share one cell, so
one achievement never fills two. It follows the footer when the window is resized.

The cell uses the footer's own font, size and cream colour, and follows the game's window
dim so it fades with the rest of the bar when a menu page is open.

INSTALL
  Extract this zip into the Melvor Idle 2 game folder, so the file ends up as
  mods\MasteryEta\MasteryEta.dll
  (Steam > Melvor Idle 2 > Manage > Browse local files)
  The zip already contains the mods\MasteryEta\ folder, so extract it into the game folder,
  not into the mods folder.

NOTES
  - Reads the game's own Mastery Guild tasks; nothing is written to the save.
  - Works for Cooking ("Cook ..." tasks), artisan skills such as Smithing, Crafting and
    Fletching ("Create ..." tasks), and gathering skills whose tasks name the tree, rock, fish
    or other action (Woodcutting, Mining, Fishing, Firemaking and similar). Other task types
    are left alone.
  - Supports multiple simultaneous actions: if another mod unlocks parallel actions, each
    active skill gets its own cell (up to four at once).
  - The ETA uses measured progress, so it corrects itself for speed bonuses, multi-item
    actions and passive production.
  - Unofficial, fan-made. Not affiliated with Games by Malcs or Jagex.
