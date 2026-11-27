# Offline behaviour

Every mod that produces items, advances an action or applies a modifier that the game uses
while the character is away must be active **before** the game runs its load-time offline
ticks. Binding only on `GameView` is too late on the first load: the offline calculation has
already finished by then.

UI-only mods (Mod Viewer, Mastery ETA) do not need this; they can keep binding whenever the
game view appears.

## The pattern

Subscribe to `Game.SaveLoaded` as soon as the `Game` object exists, and re-bind there:

```csharp
Game? _earlyGame;

// In OnLoad:
ctx.Frame += SubscribeSaveLoaded;

void SubscribeSaveLoaded()
{
    Game? game = GameAccess.CurrentGame;
    if (game == null || ReferenceEquals(game, _earlyGame)) return;

    try { if (_earlyGame != null) _earlyGame.SaveLoaded -= OnGameSaveLoaded; } catch { }
    _earlyGame = game;
    try { game.SaveLoaded += OnGameSaveLoaded; } catch { }
}

void OnGameSaveLoaded(object? sender, EventArgs e)
{
    // SaveLoaded fires after the save is read and before offline ticks run.
    _game = null;          // force a clean bind
    TryBind();             // start the passive action / apply the modifier
    Update();
}
```

`TryBind`/`Update` must be safe to call here: the game data and entities are loaded, and the
action components exist. Do not simulate production yourself; the game's offline ticks call
every ticking entity, so a started action or an applied modifier is enough.

## Checklist for a new mod

1. Does it produce, advance or modify anything while the game is closed?
2. If yes, bind on `Game.SaveLoaded` and start/apply there, not just on `GameView`.
3. Do not duplicate offline output: let the game tick the component rather than adding items
   in a separate catch-up loop.
4. Test: start the action or modifier, close the game for at least 10 minutes, reopen, and
   check the items/XP and `MelvorModLoader.log`.
5. UI-only mods can ignore this.

## Current state

- **Passive Smelting** and **Passive Cooking** start their passive action on `SaveLoaded`, so
  the game's offline ticks produce bars/food. (0.1.4 / 0.1.1+)
- **Speedy Mill** applies its mill interval modifier on `SaveLoaded`, so offline mill cycles
  use the faster interval.
- **Melvor Auto** lets the game run offline ticks and defers its repairs/petting to the end of
  offline progress.
- **Mastery ETA** and **Mod Viewer** are UI-only.
