MASTERY ETA 0.1.0
for Melvor Idle 2 (tested on 0.11.7-alpha), needs Melvor Mod Loader 0.2.0+

Adds a compact mastery readout inside the game's action footer, next to the item-depletion timer. When the action you are doing is
part of a Mastery Guild task - for example "Cook 400 Beef" or "Create 400 Rune Defenders" -
the row shows:

  - the Mastery icon and the task ("Cook 123 / 400 Beef");
  - the item/action icon;
  - a progress bar;
  - the estimated time left, based on the progress rate you are actually getting, with a
    rough base-interval estimate until the first actions finish;
  - a one-line reminder of what mastery is: long-term skill goals that earn Mastery Guild
    points and ranks.

The readout sits in the footer next to the item-depletion timer and disappears when the
current action has no matching incomplete mastery task. If more than one active skill has a
matching mastery task at once, one compact cell is shown per skill along the footer. It follows the footer when the window
is resized; hover it for the one-line mastery explanation.

INSTALL
  Extract this zip into the Melvor Idle 2 game folder, so the file ends up as
  mods\MasteryEta\MasteryEta.dll
  (Steam > Melvor Idle 2 > Manage > Browse local files)
  The zip already contains the mods\MasteryEta\ folder, so extract it into the game folder,
  not into the mods folder.

NOTES
  - Reads the game's own Mastery Guild tasks; nothing is written to the save.
  - Works for Cooking ("Cook ..." tasks) and artisan skills such as Smithing, Crafting and
    Fletching ("Create ..." tasks). Other task types are left alone.
  - The ETA uses measured progress, so it corrects itself for speed bonuses, multi-item
    actions and passive production.
  - Unofficial, fan-made. Not affiliated with Games by Malcs or Jagex.
