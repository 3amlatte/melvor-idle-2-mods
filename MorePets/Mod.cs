using System.Text;
using System.Text.Json;
using GamesByMalcs.Melvor2;
using GamesByMalcs.Melvor2.Components.Characters;
using GamesByMalcs.Melvor2.Components.CurrencyWallets;
using GamesByMalcs.Melvor2.Components.Pets;
using GamesByMalcs.Melvor2.Components.PetsCompletion;
using GamesByMalcs.Melvor2.Entities;
using GamesByMalcs.Melvor2.Entities.Bank;
using GamesByMalcs.Melvor2.Entities.Combat;
using GamesByMalcs.Melvor2.GameMenus;
using GamesByMalcs.Melvor2.Modifiers;
using Godot;
using MelvorModLoader;

namespace MorePets;

public sealed class MorePetsMod : IMod
{
    ModContext _ctx = null!;
    string _dir = string.Empty;
    string _settingsPath = string.Empty;

    Game? _game;
    string _character = "default";
    CombatEntity? _combat;
    BankEntity? _bank;
    MorePetsSettings? _settings;
    MorePetsState? _state;

    PetsCompletionView? _view;
    PetsCompletionComponent? _petsCompletion;
    VBoxContainer? _panel;
    Texture2D? _missingIcon;

    readonly Dictionary<string, Enemy> _enemyCache = new(StringComparer.OrdinalIgnoreCase);
    readonly HashSet<string> _applied = new(StringComparer.OrdinalIgnoreCase);
    readonly List<MorePetDef> _pendingFound = new();
    readonly object _pendingGate = new();
    readonly object _goldGate = new();
    bool _addingGold;
    volatile bool _uiDirty = true;
    volatile bool _buffsDirty = true;
    int _frame;

    public void OnLoad(ModContext ctx)
    {
        _ctx = ctx;
        _dir = ctx.Directory;
        _settingsPath = Path.Combine(_dir, "settings.json");

        foreach (MorePetDef pet in PetCatalog.All)
        {
            pet.SetDisplayName();
            pet.Icon = LoadIcon(pet);
        }

        _settings = new MorePetsSettings(_settingsPath, Log);

        ctx.Tree.NodeAdded += OnNodeAdded;
        ctx.Frame += OnFrame;
        ctx.Log("Loaded.");
    }

    void OnNodeAdded(Node node)
    {
        if (node is GameView)
        {
            TryBind();
            return;
        }

        if (node is PetsCompletionView view)
        {
            _view = view;
            _uiDirty = true;
        }
    }

    void OnFrame()
    {
        _frame++;

        if (_frame % 30 == 0) TryBind();

        if (_frame % 6 != 0 || _game == null) return;

        PollSettings();

        if (GameAccess.IsComputingOffline) return;

        if (_buffsDirty)
        {
            ApplyBuffs();
            _buffsDirty = false;
        }

        EnsurePanel();
        FlushFound();
    }

    void TryBind()
    {
        Game? game = GameAccess.CurrentGame;
        if (game is not { IsSaveLoaded: true } || ReferenceEquals(game, _game)) return;

        Detach();
        _game = game;
        _character = string.IsNullOrWhiteSpace(game.CharacterName) ? "default" : game.CharacterName;
        _settings = new MorePetsSettings(_settingsPath, Log);
        _state = new MorePetsState(Path.Combine(_dir, "state", SafeFileName(_character) + ".json"), _character, Log);

        if (game.Entities.TryGetObjectById("MelvorBase:Combat", out GameEntity? combatEntity) && combatEntity is CombatEntity combat)
        {
            _combat = combat;
            _combat.Enemy.Killed += OnEnemyKilled;
        }
        else
        {
            _ctx.Log("The Combat entity was not found; More Pets drops are off for this character.");
        }

        if (game.Entities.TryGetObjectById("MelvorBase:Bank", out GameEntity? bankEntity) && bankEntity is BankEntity bank)
        {
            _bank = bank;
            _bank.Wallet.CurrencyQuantityChanged += OnGoldChanged;
        }
        else
        {
            _ctx.Log("The Bank entity was not found; the gold pet is off for this character.");
        }

        BuildModifiers();
        ApplyBuffs();
        _buffsDirty = false;
        _uiDirty = true;
        _ctx.Log($"Ready for {_character}.");
    }

    void Detach()
    {
        if (_combat != null)
        {
            try { _combat.Enemy.Killed -= OnEnemyKilled; } catch { /* old game */ }
            _combat = null;
        }

        if (_bank != null)
        {
            try { _bank.Wallet.CurrencyQuantityChanged -= OnGoldChanged; } catch { /* old game */ }
            _bank = null;
        }

        if (_game != null)
        {
            try
            {
                ModifierSystem mods = Accessors.ModifiersOf(_game);
                foreach (MorePetDef pet in PetCatalog.All)
                {
                    try { mods.Remove(pet); } catch { /* old game */ }
                }
            }
            catch { /* old game */ }
        }

        if (_view != null && GodotObject.IsInstanceValid(_view)) _view.Visible = true;
        if (_panel != null && GodotObject.IsInstanceValid(_panel)) _panel.QueueFree();
        _panel = null;
        _view = null;
        _petsCompletion = null;
        _applied.Clear();
        _enemyCache.Clear();
        _game = null;
        _settings = null;
        _state = null;
        _buffsDirty = true;
        _uiDirty = true;

        lock (_pendingGate)
        {
            _pendingFound.Clear();
        }
    }

    // ---------------------------------------------------------------- drops

    void OnEnemyKilled(object? sender, EnemyKilledEventArgs e)
    {
        if (_state == null) return;
        if (!PetCatalog.ByEnemy.TryGetValue(e.Enemy.Id, out MorePetDef? pet)) return;

        int rate = RateFor(pet);
        if (Random.Shared.NextDouble() >= 1.0 / rate) return;

        int count = _state.Add(pet.Id);
        _ctx.Log($"Found {pet.Name} ({count} total) from {e.Enemy.DisplayName.Text}.");
        if (count == 1)
        {
            lock (_pendingGate) _pendingFound.Add(pet);
        }

        _uiDirty = true;
        _buffsDirty = true;
    }

    void OnGoldChanged(object? sender, CurrencyQuantityChangedEventArgs e)
    {
        if (_state == null || _settings == null || _bank == null) return;
        if (!string.Equals(e.Currency.Id, "MelvorBase:GoldPieces", StringComparison.Ordinal)) return;

        MorePetDef morgis = PetCatalog.ById["Morgis"];
        if (!_state.Owned(morgis.Id) || !_settings.IsEnabled(morgis.Name)) return;

        long delta = e.NewValue - e.OldValue;
        if (delta <= 0) return;

        long bonus = (long)Math.Floor(delta * 0.20);
        if (bonus <= 0) return;

        lock (_goldGate)
        {
            if (_addingGold) return;
            _addingGold = true;
            try
            {
                _bank.Wallet.AddGoldPieces(bonus);
            }
            catch (Exception ex)
            {
                _ctx.Log("Gold pet bonus failed: " + ex.Message);
            }
            finally
            {
                _addingGold = false;
            }
        }
    }

    int RateFor(MorePetDef pet)
    {
        Enemy? enemy = GetEnemy(pet);
        return PetCatalog.RateForLevel(enemy?.CombatLevel ?? pet.FallbackLevel);
    }

    Enemy? GetEnemy(MorePetDef pet)
    {
        if (_enemyCache.TryGetValue(pet.EnemyId, out Enemy? cached)) return cached;
        if (_game == null) return null;

        if (_game.Data.TryGetObject<Enemy>(pet.EnemyId, out Enemy? enemy))
        {
            _enemyCache[pet.EnemyId] = enemy;
            return enemy;
        }
        return null;
    }

    double GetKills(MorePetDef pet)
    {
        if (_combat == null) return 0;
        Enemy? enemy = GetEnemy(pet);
        return enemy == null ? 0 : _combat.Statistics.EnemyKilled.GetValue(enemy);
    }

    // ------------------------------------------------------------- modifiers

    void BuildModifiers()
    {
        if (_game == null) return;

        foreach (MorePetDef pet in PetCatalog.All)
        {
            try
            {
                pet.Modifiers = pet.Kind switch
                {
                    MorePetKind.Stance => ParseModifiers(StanceBonusJson(pet.StanceId!, pet.StyleId!)),
                    MorePetKind.GlobalInterval => ParseModifiers(GlobalBonusJson("GlobalPercentActionInterval", -0.20)),
                    MorePetKind.GlobalXp => ParseModifiers(GlobalBonusJson("GlobalPercentXpGained", 0.20)),
                    _ => null,
                };
            }
            catch (Exception e)
            {
                pet.Modifiers = null;
                _ctx.Log($"Could not build the {pet.Name} buff: {e.Message}");
            }
        }
    }

    ModifierValues ParseModifiers(string json)
    {
        using JsonDocument doc = JsonDocument.Parse(json);
        return new ModifierValues(_game!.Data, doc.RootElement);
    }

    static string GlobalBonusJson(string statGroup, double value) => $$"""
    {
      "Global": [
        {
          "StatGroup": "{{statGroup}}",
          "Value": {{value.ToString(System.Globalization.CultureInfo.InvariantCulture)}},
          "Condition": null,
          "Scaling": null
        }
      ],
      "Component": [],
      "CombatEffect": []
    }
    """;

    static string StanceBonusJson(string stance, string style) => $$"""
    {
      "Global": [],
      "Component": [
        {
          "Stat": { "StatsType": "MelvorBase:Character", "Name": "PercentMaxHit" },
          "FromContext": null,
          "Value": 0.2,
          "Condition": null,
          "LocalContext": {
            "Component": { "Entity": "MelvorBase:Combat", "Component": "Player" },
            "CombatStyle": "{{style}}",
            "AttackStance": "{{stance}}",
            "CombatLevel": null,
            "CombatStat": null,
            "DamageType": null,
            "Effect": null,
            "Element": null
          },
          "Scaling": null
        },
        {
          "Stat": { "StatsType": "MelvorBase:Character", "Name": "PercentAttackInterval" },
          "FromContext": null,
          "Value": -0.2,
          "Condition": null,
          "LocalContext": {
            "Component": { "Entity": "MelvorBase:Combat", "Component": "Player" },
            "CombatStyle": "{{style}}",
            "AttackStance": "{{stance}}",
            "CombatLevel": null,
            "CombatStat": null,
            "DamageType": null,
            "Effect": null,
            "Element": null
          },
          "Scaling": null
        }
      ],
      "CombatEffect": []
    }
    """;

    void ApplyBuffs()
    {
        if (_game == null || _state == null || _settings == null) return;

        ModifierSystem mods;
        try
        {
            mods = Accessors.ModifiersOf(_game);
        }
        catch (Exception e)
        {
            _ctx.Log("Could not access the modifier system: " + e.Message);
            return;
        }

        bool changed = false;
        foreach (MorePetDef pet in PetCatalog.All)
        {
            if (pet.Modifiers == null) continue;

            bool want = _state.Owned(pet.Id) && _settings.IsEnabled(pet.Name);
            bool applied = _applied.Contains(pet.Id);

            try
            {
                if (want && !applied)
                {
                    mods.Add(pet, pet.Modifiers);
                    _applied.Add(pet.Id);
                    _ctx.Log("Buff enabled: " + pet.ShortName);
                    changed = true;
                }
                else if (!want && applied)
                {
                    mods.Remove(pet, pet.Modifiers);
                    _applied.Remove(pet.Id);
                    _ctx.Log("Buff disabled: " + pet.ShortName);
                    changed = true;
                }
            }
            catch (Exception e)
            {
                _ctx.Log($"Could not apply the {pet.Name} buff: {e.Message}");
            }
        }

        if (changed) _uiDirty = true;
    }

    void PollSettings()
    {
        if (_settings?.ReloadIfChanged() == true)
        {
            _buffsDirty = true;
            _uiDirty = true;
            _ctx.Log("Settings reloaded.");
        }
    }

    // ------------------------------------------------------------------- UI

    void EnsurePanel()
    {
        if (_game == null) return;

        if (_view == null || !GodotObject.IsInstanceValid(_view))
        {
            _view = GameAccess.FindNode<PetsCompletionView>();
            if (_view == null) return;
            _uiDirty = true;
        }

        _petsCompletion = Accessors.PetsCompletionOf(_view);
        if (_petsCompletion == null) return;

        if (_panel == null || !GodotObject.IsInstanceValid(_panel) || _panel.GetParent() == null)
        {
            Node? parent = _view.GetParent();
            if (parent == null) return;

            _panel = new VBoxContainer
            {
                Name = "MorePetsPanel",
                CustomMinimumSize = new Vector2(900, 0),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            };
            _panel.AddThemeConstantOverride("separation", 10);
            if (_view.Theme != null) _panel.Theme = _view.Theme;
            parent.AddChild(_panel);
            _uiDirty = true;
            _ctx.Log("Pets panel injected.");
        }

        _view.Visible = false;

        if (_uiDirty) RebuildPanel();
    }

    void RebuildPanel()
    {
        if (_panel == null || _petsCompletion == null || _state == null || _settings == null) return;

        foreach (Node child in _panel.GetChildren().ToList())
        {
            _panel.RemoveChild(child);
            child.QueueFree();
        }

        int owned = PetCatalog.All.Count(p => _state.Owned(p.Id));
        int total = PetCatalog.All.Count;

        const string petHint = "Hover a pet for its drop rate and buff. Buffs can be toggled in Mod Viewer settings.";
        VBoxContainer moreBody = AddCollapsible(_panel, "More Pets", $"({owned}/{total})", petHint);
        moreBody.AddThemeConstantOverride("separation", 8);

        VBoxContainer bossBody = AddStyledSection(
            moreBody,
            PetCatalog.Subcategory,
            PetCatalog.ById["Kraken"].Icon ?? MissingIcon,
            owned,
            total,
            petHint);
        HFlowContainer bossGrid = NewGrid();
        foreach (MorePetDef pet in PetCatalog.All)
        {
            bossGrid.AddChild(BuildMorePetCell(pet));
        }
        bossBody.AddChild(bossGrid);

        (int defaultOwned, int defaultTotal) = DefaultPetCounts();
        VBoxContainer defaultBody = AddCollapsible(_panel, "Default Pets", $"({defaultOwned}/{defaultTotal})");
        defaultBody.AddThemeConstantOverride("separation", 8);

        foreach (IPetsEntity entity in _petsCompletion.PetEntities)
        {
            if (entity.Pets.ActivePets.Count == 0) continue;

            VBoxContainer skillBody = AddStyledSection(
                defaultBody,
                entity.DisplayName.Text,
                entity.Icon,
                entity.Pets.UnlockedPetCount,
                entity.Pets.ActivePets.Count);

            HFlowContainer defaultGrid = NewGrid();
            foreach (ActivePet activePet in entity.Pets.ActivePets.Values)
            {
                defaultGrid.AddChild(BuildDefaultPetCell(entity, activePet));
            }
            skillBody.AddChild(defaultGrid);
        }

        _uiDirty = false;
    }

    /// <summary>A collapsible panel styled like the default PetsCompletionBreakdownView.</summary>
    VBoxContainer AddStyledSection(
        Control parent,
        string title,
        Texture2D? icon,
        int owned,
        int total,
        string? tooltip = null)
    {
        PanelContainer panel = new()
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            TooltipText = tooltip ?? string.Empty,
        };
        if (_view?.Theme != null) panel.Theme = _view.Theme;

        MarginContainer margin = new();
        margin.AddThemeConstantOverride("margin_left", 10);
        margin.AddThemeConstantOverride("margin_right", 10);
        margin.AddThemeConstantOverride("margin_top", 8);
        margin.AddThemeConstantOverride("margin_bottom", 10);

        VBoxContainer column = new();
        column.AddThemeConstantOverride("separation", 6);

        HBoxContainer header = new();
        header.AddThemeConstantOverride("separation", 6);

        Button toggle = new()
        {
            Text = "▼",
            Flat = true,
            ToggleMode = true,
            ButtonPressed = true,
            CustomMinimumSize = new Vector2(26, 0),
        };
        toggle.AddThemeFontSizeOverride("font_size", 13);

        TextureRect categoryIcon = new()
        {
            Texture = icon ?? MissingIcon,
            CustomMinimumSize = new Vector2(34, 34),
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        };

        Label titleLabel = new()
        {
            Text = title,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            VerticalAlignment = VerticalAlignment.Center,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
            TooltipText = tooltip ?? string.Empty,
        };
        titleLabel.AddThemeFontSizeOverride("font_size", 18);

        Label countLabel = new()
        {
            Text = $"{owned}/{total}",
            VerticalAlignment = VerticalAlignment.Center,
        };
        countLabel.AddThemeFontSizeOverride("font_size", 13);
        countLabel.Modulate = new Color(0.78f, 0.8f, 0.84f);

        header.AddChild(toggle);
        header.AddChild(categoryIcon);
        header.AddChild(titleLabel);
        header.AddChild(countLabel);

        ProgressBar progress = new()
        {
            MinValue = 0,
            MaxValue = Math.Max(1, total),
            Value = owned,
            ShowPercentage = false,
            CustomMinimumSize = new Vector2(0, 6),
            Step = 0.01,
        };

        VBoxContainer body = new();
        body.AddThemeConstantOverride("separation", 6);

        toggle.Toggled += pressed =>
        {
            toggle.Text = pressed ? "▼" : "▶";
            body.Visible = pressed;
        };

        column.AddChild(header);
        column.AddChild(progress);
        column.AddChild(body);
        margin.AddChild(column);
        panel.AddChild(margin);
        parent.AddChild(panel);
        return body;
    }

    static VBoxContainer AddCollapsible(Control parent, string title, string? count = null, string? tooltip = null)
    {
        Button header = new()
        {
            Flat = true,
            ToggleMode = true,
            ButtonPressed = true,
            Alignment = HorizontalAlignment.Left,
            Text = "▼ " + title + (count == null ? "" : " " + count),
            TooltipText = tooltip ?? string.Empty,
        };
        header.AddThemeFontSizeOverride("font_size", 18);

        VBoxContainer body = new();
        body.AddThemeConstantOverride("separation", 6);

        header.Toggled += pressed =>
        {
            body.Visible = pressed;
            header.Text = (pressed ? "▼ " : "▶ ") + title + (count == null ? "" : " " + count);
        };

        parent.AddChild(header);
        parent.AddChild(body);
        return body;
    }

    static HFlowContainer NewGrid()
    {
        HFlowContainer grid = new();
        grid.AddThemeConstantOverride("h_separation", 4);
        grid.AddThemeConstantOverride("v_separation", 8);
        return grid;
    }

    static Label Label(string text) => new()
    {
        Text = text,
        AutowrapMode = TextServer.AutowrapMode.WordSmart,
    };

    Control BuildMorePetCell(MorePetDef pet)
    {
        int count = _state?.Count(pet.Id) ?? 0;
        TextureButton button = new()
        {
            TextureNormal = pet.Icon ?? MissingIcon,
            CustomMinimumSize = new Vector2(48, 48),
            IgnoreTextureSize = true,
            StretchMode = TextureButton.StretchModeEnum.Scale,
            Modulate = count > 0 ? Colors.White : new Color(0.22f, 0.22f, 0.22f),
            MouseDefaultCursorShape = Control.CursorShape.PointingHand,
            TooltipText = TooltipFor(pet),
        };
        button.Pressed += () => ShowPetDetails(pet);
        return button;
    }

    Control BuildDefaultPetCell(IPetsEntity entity, ActivePet activePet)
    {
        string status = activePet.IsUnlocked ? "Obtained" : "Not obtained";
        TextureButton button = new()
        {
            TextureNormal = activePet.Pet.Icon ?? MissingIcon,
            CustomMinimumSize = new Vector2(48, 48),
            IgnoreTextureSize = true,
            StretchMode = TextureButton.StretchModeEnum.Scale,
            Modulate = activePet.IsUnlocked ? Colors.White : new Color(0.22f, 0.22f, 0.22f),
            MouseDefaultCursorShape = Control.CursorShape.PointingHand,
            TooltipText = $"{activePet.Pet.DisplayName.Text}\n{entity.DisplayName.Text}\n{status}",
        };
        button.Pressed += () => _petsCompletion?.RequestPetModal(activePet);
        return button;
    }

    (int Owned, int Total) DefaultPetCounts()
    {
        if (_petsCompletion == null) return (0, 0);
        int total = 0;
        int owned = 0;
        foreach (IPetsEntity entity in _petsCompletion.PetEntities)
        {
            total += entity.Pets.ActivePets.Count;
            owned += entity.Pets.UnlockedPetCount;
        }
        return (owned, total);
    }

    string TooltipFor(MorePetDef pet)
    {
        StringBuilder text = new();
        text.AppendLine(pet.Name);
        text.AppendLine($"Obtained: {_state?.Count(pet.Id) ?? 0:N0}");
        text.AppendLine($"Enemy kills: {GetKills(pet):N0}");
        text.AppendLine($"Location: {pet.Location}");
        text.AppendLine($"Drop rate: 1 / {RateFor(pet):N0}");
        text.AppendLine(pet.BuffText);
        text.Append(_settings?.IsEnabled(pet.Name) == false ? "Buff: disabled" : "Buff: enabled");
        return text.ToString();
    }

    void ShowPetDetails(MorePetDef pet, string? title = null)
    {
        if (_view == null || !GodotObject.IsInstanceValid(_view)) return;
        Node root = _view.GetTree().Root;

        PopupPanel popup = new() { Name = "MorePetsDetails" };
        if (_view.Theme != null) popup.Theme = _view.Theme;
        MarginContainer margin = new();
        margin.AddThemeConstantOverride("margin_left", 16);
        margin.AddThemeConstantOverride("margin_right", 16);
        margin.AddThemeConstantOverride("margin_top", 14);
        margin.AddThemeConstantOverride("margin_bottom", 14);

        VBoxContainer box = new();
        box.AddThemeConstantOverride("separation", 8);

        TextureRect image = new()
        {
            Texture = pet.Icon ?? MissingIcon,
            CustomMinimumSize = new Vector2(128, 128),
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
        };

        Label heading = new()
        {
            Text = title ?? pet.Name,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        heading.AddThemeFontSizeOverride("font_size", 22);

        Label name = new()
        {
            Text = pet.Name,
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        name.AddThemeFontSizeOverride("font_size", 15);

        Label description = new()
        {
            Text = pet.Description,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(420, 0),
        };
        description.Modulate = new Color(0.78f, 0.8f, 0.84f);

        StringBuilder details = new();
        details.AppendLine($"Obtained: {_state?.Count(pet.Id) ?? 0:N0}");
        details.AppendLine($"Enemy kills: {GetKills(pet):N0}");
        details.AppendLine($"Location: {pet.Location}");
        details.AppendLine($"Drop rate: 1 / {RateFor(pet):N0}");
        details.AppendLine(pet.BuffText);
        details.Append(_settings?.IsEnabled(pet.Name) == false ? "Buff: disabled in settings" : "Buff: enabled");

        Label detailLabel = new()
        {
            Text = details.ToString().TrimEnd(),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };

        Button close = new() { Text = "Close" };
        close.Pressed += popup.Hide;

        box.AddChild(image);
        box.AddChild(heading);
        if (!string.Equals(title, null, StringComparison.Ordinal)) box.AddChild(name);
        box.AddChild(description);
        box.AddChild(detailLabel);
        box.AddChild(close);

        margin.AddChild(box);
        popup.AddChild(margin);
        popup.PopupHide += popup.QueueFree;
        root.AddChild(popup);
        popup.PopupCentered(new Vector2I(500, 520));
    }

    void FlushFound()
    {
        if (_view == null || !GodotObject.IsInstanceValid(_view)) return;

        List<MorePetDef> found;
        lock (_pendingGate)
        {
            if (_pendingFound.Count == 0) return;
            found = new List<MorePetDef>(_pendingFound);
            _pendingFound.Clear();
        }

        if (found.Count == 1)
        {
            ShowPetDetails(found[0], "Pet found!");
            return;
        }

        Node root = _view.GetTree().Root;
        PopupPanel popup = new() { Name = "MorePetsFound" };
        if (_view.Theme != null) popup.Theme = _view.Theme;
        MarginContainer margin = new();
        margin.AddThemeConstantOverride("margin_left", 16);
        margin.AddThemeConstantOverride("margin_right", 16);
        margin.AddThemeConstantOverride("margin_top", 14);
        margin.AddThemeConstantOverride("margin_bottom", 14);
        VBoxContainer box = new();
        box.AddThemeConstantOverride("separation", 8);

        Label heading = new() { Text = "Pets found!" };
        heading.AddThemeFontSizeOverride("font_size", 22);
        box.AddChild(heading);

        foreach (MorePetDef pet in found)
        {
            Label line = new()
            {
                Text = $"{pet.Name}  —  {pet.Location}",
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
            };
            box.AddChild(line);
        }

        Button close = new() { Text = "Close" };
        close.Pressed += popup.Hide;
        box.AddChild(close);

        margin.AddChild(box);
        popup.AddChild(margin);
        popup.PopupHide += popup.QueueFree;
        root.AddChild(popup);
        popup.PopupCentered(new Vector2I(520, 360));
    }

    // -------------------------------------------------------------- helpers

    Texture2D? LoadIcon(MorePetDef pet)
    {
        try
        {
            string path = Path.Combine(_dir, "icons", pet.IconFile);
            if (!File.Exists(path))
            {
                _ctx.Log("Missing icon: " + path);
                return null;
            }
            Image? image = Image.LoadFromFile(path);
            return image == null ? null : ImageTexture.CreateFromImage(image);
        }
        catch (Exception e)
        {
            _ctx.Log("Could not load " + pet.IconFile + ": " + e.Message);
            return null;
        }
    }

    Texture2D? MissingIcon => _missingIcon ??= ResourceLoader.Load<Texture2D>("res://Assets/Icons/missing_artwork.png");

    static string SafeFileName(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
        name = name.Trim();
        return string.IsNullOrWhiteSpace(name) ? "default" : name;
    }

    void Log(string message) => _ctx.Log(message);
}
