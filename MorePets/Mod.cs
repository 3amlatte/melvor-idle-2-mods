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
using GamesByMalcs.Melvor2.Entities.CompletionLog;
using GamesByMalcs.Melvor2.GameMenus;
using GamesByMalcs.Melvor2.Localization;
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
    HBoxContainer? _panel;
    PanelContainer? _detailPane;
    TextureRect? _detailImage;
    Label? _detailName;
    Label? _detailMeta;
    Label? _detailDrop;
    Label? _detailBuff;
    Label? _detailDescription;
    MorePetDef? _selectedPet;
    Texture2D? _missingIcon;
    StyleBox? _cardStyle;
    Font? _cardFont;
    int _cardFontSize = 18;
    Color _cardFontColor = Colors.White;
    Font? _cardCountFont;
    int _cardCountFontSize = 13;
    Color _cardCountFontColor = new(0.78f, 0.8f, 0.84f);
    TextureProgressBar? _cardProgressTemplate;
    Label? _cardTitlePrototype;

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

        CompletionLogActiveView? logView = GameAccess.FindNode<CompletionLogActiveView>();
        if (logView != null)
        {
            Label? categoryLabel = Accessors.CurrentCategoryLabelOf(logView);
            if (categoryLabel != null) categoryLabel.Text = "More Pets";
        }

        _petsCompletion = Accessors.PetsCompletionOf(_view);
        if (_petsCompletion == null) return;

        if (_panel == null || !GodotObject.IsInstanceValid(_panel) || _panel.GetParent() == null)
        {
            Node? parent = _view.GetParent();
            if (parent == null) return;

            _panel = new HBoxContainer
            {
                Name = "MorePetsPanel",
                CustomMinimumSize = new Vector2(900, 0),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            };
            _panel.AddThemeConstantOverride("separation", 12);
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

        PackedScene? breakdownScene = GD.Load<PackedScene>("res://Components/PetsCompletion/pets_completion_breakdown_view.tscn");
        _cardStyle = null;
        if (breakdownScene != null)
        {
            PetsCompletionBreakdownView template = breakdownScene.Instantiate<PetsCompletionBreakdownView>();
            _cardStyle = template.GetThemeStylebox("panel");

            Label? templateName = Accessors.SkillNameLabelOf(template);
            if (templateName != null)
            {
                _cardFont = templateName.GetThemeFont("font");
                _cardFontSize = templateName.GetThemeFontSize("font_size");
                _cardFontColor = templateName.GetThemeColor("font_color");
                _cardTitlePrototype = templateName.Duplicate() as Label;
            }
            else
            {
                _cardTitlePrototype = null;
            }

            Int32OutOfLabel? templateCount = Accessors.PetCountLabelOf(template);
            if (templateCount != null)
            {
                _cardCountFont = templateCount.GetThemeFont("font");
                _cardCountFontSize = templateCount.GetThemeFontSize("font_size");
                _cardCountFontColor = templateCount.GetThemeColor("font_color");
            }

            TextureProgressBar? templateProgress = Accessors.PetProgressOf(template);
            _cardProgressTemplate = templateProgress == null
                ? null
                : templateProgress.Duplicate() as TextureProgressBar;

            template.QueueFree();
        }

        VBoxContainer left = new()
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        left.AddThemeConstantOverride("separation", 10);
        _panel.AddChild(left);

        PanelContainer details = BuildDetailPane();
        _panel.AddChild(details);

        const string petHint = "Hover a pet for its drop rate and buff. Buffs can be toggled in Mod Viewer settings.";
        VBoxContainer bossBody = AddCollapsibleBar(
            left,
            PetCatalog.Subcategory,
            $"({owned}/{total})",
            PetCatalog.ById["Kraken"].Icon ?? MissingIcon,
            petHint);
        bossBody.AddThemeConstantOverride("separation", 8);

        GridContainer petGrid = new()
        {
            Columns = 3,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        petGrid.AddThemeConstantOverride("h_separation", 14);
        petGrid.AddThemeConstantOverride("v_separation", 14);
        bossBody.AddChild(petGrid);

        foreach (MorePetDef pet in PetCatalog.All)
        {
            petGrid.AddChild(BuildPetCard(pet));
        }

        (int defaultOwned, int defaultTotal) = DefaultPetCounts();
        VBoxContainer defaultBody = AddCollapsibleBar(
            left,
            "Default Pets",
            $"({defaultOwned}/{defaultTotal})",
            DefaultPetIcon(),
            null);
        defaultBody.AddThemeConstantOverride("separation", 8);

        GridContainer skillGrid = new()
        {
            Columns = 3,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        skillGrid.AddThemeConstantOverride("h_separation", 14);
        skillGrid.AddThemeConstantOverride("v_separation", 14);
        defaultBody.AddChild(skillGrid);

        foreach (IPetsEntity entity in _petsCompletion.PetEntities)
        {
            if (entity.Pets.ActivePets.Count == 0) continue;

            if (breakdownScene != null)
            {
                PetsCompletionBreakdownView defaultPanel = breakdownScene.Instantiate<PetsCompletionBreakdownView>();
                defaultPanel.CustomMinimumSize = new Vector2(340, 0);
                defaultPanel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                defaultPanel.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
                defaultPanel.Bind(entity.Pets, _petsCompletion);
                Label? defaultName = Accessors.SkillNameLabelOf(defaultPanel);
                if (defaultName != null) defaultName.HorizontalAlignment = HorizontalAlignment.Center;
                skillGrid.AddChild(defaultPanel);
            }
            else
            {
                VBoxContainer skillBody = AddStyledSection(
                    skillGrid,
                    entity.DisplayName.Text,
                    entity.Icon,
                    entity.Pets.UnlockedPetCount,
                    entity.Pets.ActivePets.Count,
                    collapsible: false);

                HFlowContainer defaultGrid = NewGrid(center: true);
                foreach (ActivePet activePet in entity.Pets.ActivePets.Values)
                {
                    defaultGrid.AddChild(BuildDefaultPetCell(entity, activePet));
                }
                skillBody.AddChild(defaultGrid);
            }
        }

        if (_selectedPet != null)
        {
            UpdateDetailPane(_selectedPet);
            details.Visible = true;
        }
        else
        {
            details.Visible = false;
        }

        _uiDirty = false;
    }

    void ApplyCardStyle(Control control)
    {
        if (_cardStyle != null) control.AddThemeStyleboxOverride("panel", _cardStyle);
    }

    PanelContainer BuildDetailPane()
    {
        PanelContainer pane = new()
        {
            CustomMinimumSize = new Vector2(330, 0),
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        if (_view?.Theme != null) pane.Theme = _view.Theme;
        ApplyCardStyle(pane);
        _detailPane = pane;

        MarginContainer margin = new();
        margin.AddThemeConstantOverride("margin_left", 12);
        margin.AddThemeConstantOverride("margin_right", 12);
        margin.AddThemeConstantOverride("margin_top", 10);
        margin.AddThemeConstantOverride("margin_bottom", 12);

        VBoxContainer box = new();
        box.AddThemeConstantOverride("separation", 8);

        box.AddChild(ThemeBar("More Pets"));
        box.AddChild(ThemeBar(PetCatalog.Subcategory));

        HBoxContainer header = new();
        header.AddThemeConstantOverride("separation", 4);

        Control titleSpacer = new() { CustomMinimumSize = new Vector2(28, 0) };
        Label heading = new()
        {
            Text = "Pet Details",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        heading.AddThemeFontSizeOverride("font_size", 18);

        Button close = new()
        {
            Text = "✕",
            Flat = true,
            CustomMinimumSize = new Vector2(28, 28),
            TooltipText = "Close",
        };
        close.AddThemeFontSizeOverride("font_size", 14);
        close.Pressed += HideDetail;
        header.AddChild(titleSpacer);
        header.AddChild(heading);
        header.AddChild(close);

        _detailImage = new TextureRect
        {
            CustomMinimumSize = new Vector2(180, 180),
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
        };

        _detailName = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        _detailName.AddThemeFontSizeOverride("font_size", 19);

        _detailMeta = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };

        _detailBuff = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        _detailBuff.Modulate = new Color(0.82f, 0.9f, 1f);

        _detailDescription = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        _detailDescription.Modulate = new Color(0.75f, 0.78f, 0.82f);

        _detailDrop = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };

        Label bonusTitle = new() { Text = "Bonus" };
        bonusTitle.AddThemeFontSizeOverride("font_size", 16);
        bonusTitle.Modulate = new Color(0.96f, 0.82f, 0.48f);

        box.AddChild(header);
        box.AddChild(_detailImage);
        box.AddChild(_detailName);
        box.AddChild(_detailMeta);
        box.AddChild(_detailDescription);
        box.AddChild(_detailDrop);
        box.AddChild(bonusTitle);
        box.AddChild(_detailBuff);
        margin.AddChild(box);
        pane.AddChild(margin);
        pane.Visible = false;
        return pane;
    }

    PanelContainer ThemeBar(string text)
    {
        PanelContainer bar = new()
        {
            CustomMinimumSize = new Vector2(0, 30),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        if (_view?.Theme != null) bar.Theme = _view.Theme;
        ApplyCardStyle(bar);

        CenterContainer center = new()
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };

        Label label = new()
        {
            Text = text,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        label.AddThemeFontSizeOverride("font_size", 15);
        label.Modulate = new Color(0.96f, 0.82f, 0.48f);

        MarginContainer shift = new();
        shift.AddThemeConstantOverride("margin_top", 6);
        shift.AddChild(label);
        center.AddChild(shift);
        bar.AddChild(center);
        return bar;
    }

    void SelectPet(MorePetDef pet)
    {
        _selectedPet = pet;
        if (_detailPane == null || !GodotObject.IsInstanceValid(_detailPane)) return;
        _detailPane.Visible = true;
        UpdateDetailPane(pet);
    }

    void HideDetail()
    {
        _selectedPet = null;
        if (_detailPane != null && GodotObject.IsInstanceValid(_detailPane))
        {
            _detailPane.Visible = false;
        }
    }

    void UpdateDetailPane(MorePetDef pet)
    {
        if (_detailImage == null || _detailName == null || _detailMeta == null ||
            _detailDrop == null || _detailBuff == null || _detailDescription == null)
        {
            return;
        }

        _detailImage.Texture = pet.Icon ?? MissingIcon;
        _detailName.Text = pet.ShortName;

        string enemyName = pet.Name.StartsWith("Lil ", StringComparison.Ordinal) ? pet.Name[4..] : pet.Name;
        StringBuilder meta = new();
        meta.AppendLine($"Enemy: {enemyName}");
        meta.AppendLine($"Obtained: {_state?.Count(pet.Id) ?? 0:N0}");
        meta.AppendLine($"Enemy kills: {GetKills(pet):N0}");
        meta.Append($"Location: {pet.Location}");
        _detailMeta.Text = meta.ToString();
        _detailDrop.Text = $"Drop rate: 1 / {RateFor(pet):N0}";

        _detailBuff.Text = pet.BuffText;
        _detailDescription.Text = pet.Description;
    }

    /// <summary>A collapsible panel styled like the default PetsCompletionBreakdownView.</summary>
    VBoxContainer AddStyledSection(
        Control parent,
        string title,
        Texture2D? icon,
        int owned,
        int total,
        string? tooltip = null,
        bool collapsible = true)
    {
        PanelContainer panel = new()
        {
            CustomMinimumSize = new Vector2(340, 0),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            TooltipText = tooltip ?? string.Empty,
        };
        if (_view?.Theme != null) panel.Theme = _view.Theme;
        ApplyCardStyle(panel);

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
            CustomMinimumSize = new Vector2(28, 28),
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

        if (collapsible) header.AddChild(toggle);
        header.AddChild(categoryIcon);
        header.AddChild(titleLabel);
        header.AddChild(countLabel);

        ProgressBar progress = new()
        {
            MinValue = 0,
            MaxValue = Math.Max(1, total),
            Value = owned,
            ShowPercentage = false,
            CustomMinimumSize = new Vector2(0, 8),
            Step = 0.01,
        };

        VBoxContainer body = new();
        body.AddThemeConstantOverride("separation", 6);

        if (collapsible)
        {
            toggle.Toggled += pressed =>
            {
                toggle.Text = pressed ? "▼" : "▶";
                body.Visible = pressed;
            };
        }

        column.AddChild(header);
        column.AddChild(progress);
        column.AddChild(body);
        margin.AddChild(column);
        panel.AddChild(margin);
        parent.AddChild(panel);
        return body;
    }

    VBoxContainer AddCollapsibleBar(
        Control parent,
        string title,
        string count,
        Texture2D? icon,
        string? tooltip)
    {
        PanelContainer bar = new()
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            TooltipText = tooltip ?? string.Empty,
        };
        if (_view?.Theme != null) bar.Theme = _view.Theme;
        ApplyCardStyle(bar);

        MarginContainer margin = new();
        margin.AddThemeConstantOverride("margin_left", 10);
        margin.AddThemeConstantOverride("margin_right", 10);
        margin.AddThemeConstantOverride("margin_top", 6);
        margin.AddThemeConstantOverride("margin_bottom", 0);

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

        Label titleLabel = new()
        {
            Text = title,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            VerticalAlignment = VerticalAlignment.Center,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
            TooltipText = tooltip ?? string.Empty,
        };
        titleLabel.AddThemeFontSizeOverride("font_size", 18);
        titleLabel.Modulate = new Color(0.96f, 0.82f, 0.48f);

        Label countLabel = new()
        {
            Text = count,
            VerticalAlignment = VerticalAlignment.Center,
        };
        countLabel.AddThemeFontSizeOverride("font_size", 13);
        countLabel.Modulate = new Color(0.78f, 0.8f, 0.84f);

        header.AddChild(toggle);
        header.AddChild(titleLabel);
        header.AddChild(countLabel);

        VBoxContainer body = new();
        body.AddThemeConstantOverride("separation", 8);

        toggle.Toggled += pressed =>
        {
            toggle.Text = pressed ? "▼" : "▶";
            body.Visible = pressed;
        };

        margin.AddChild(header);
        bar.AddChild(margin);
        parent.AddChild(bar);
        parent.AddChild(body);
        return body;
    }

    static HFlowContainer NewGrid(bool expand = false, bool center = false)
    {
        HFlowContainer grid = new()
        {
            SizeFlagsHorizontal = expand ? Control.SizeFlags.ExpandFill : Control.SizeFlags.ShrinkCenter,
            Alignment = center ? FlowContainer.AlignmentMode.Center : FlowContainer.AlignmentMode.Begin,
        };
        grid.AddThemeConstantOverride("h_separation", 4);
        grid.AddThemeConstantOverride("v_separation", 8);
        return grid;
    }

    static Label Label(string text) => new()
    {
        Text = text,
        AutowrapMode = TextServer.AutowrapMode.WordSmart,
    };

    Control BuildPetCard(MorePetDef pet)
    {
        int count = _state?.Count(pet.Id) ?? 0;
        bool owned = count > 0;

        PanelContainer card = new()
        {
            CustomMinimumSize = new Vector2(340, 0),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        if (_view?.Theme != null) card.Theme = _view.Theme;
        ApplyCardStyle(card);

        MarginContainer margin = new();
        margin.AddThemeConstantOverride("margin_left", 10);
        margin.AddThemeConstantOverride("margin_right", 10);
        margin.AddThemeConstantOverride("margin_top", 8);
        margin.AddThemeConstantOverride("margin_bottom", 10);

        VBoxContainer column = new();
        column.AddThemeConstantOverride("separation", 6);

        HBoxContainer header = new();
        header.AddThemeConstantOverride("separation", 6);

        Label title;
        if (_cardTitlePrototype != null)
        {
            title = (Label)_cardTitlePrototype.Duplicate();
        }
        else
        {
            title = new Label();
            if (_cardFont != null) title.AddThemeFontOverride("font", _cardFont);
            title.AddThemeFontSizeOverride("font_size", _cardFontSize);
            title.AddThemeColorOverride("font_color", _cardFontColor);
        }
        title.Text = pet.ShortName;
        title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.VerticalAlignment = VerticalAlignment.Center;
        title.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;

        Label countLabel = new()
        {
            Text = $"x{count}",
            VerticalAlignment = VerticalAlignment.Center,
        };
        if (_cardCountFont != null) countLabel.AddThemeFontOverride("font", _cardCountFont);
        countLabel.AddThemeFontSizeOverride("font_size", _cardCountFontSize);
        countLabel.AddThemeColorOverride("font_color", _cardCountFontColor);

        header.AddChild(title);
        header.AddChild(countLabel);

        Control progress;
        if (_cardProgressTemplate != null)
        {
            TextureProgressBar source = _cardProgressTemplate;
            TextureProgressBar textureProgress = new()
            {
                TextureUnder = source.TextureUnder,
                TextureProgress = source.TextureProgress,
                TextureOver = source.TextureOver,
                NinePatchStretch = source.NinePatchStretch,
                StretchMarginLeft = source.StretchMarginLeft,
                StretchMarginTop = source.StretchMarginTop,
                StretchMarginRight = source.StretchMarginRight,
                StretchMarginBottom = source.StretchMarginBottom,
                FillMode = source.FillMode,
                RadialInitialAngle = source.RadialInitialAngle,
                RadialFillDegrees = source.RadialFillDegrees,
                TintUnder = source.TintUnder,
                TintProgress = source.TintProgress,
                TintOver = source.TintOver,
                CustomMinimumSize = new Vector2(0, Math.Max(10, source.CustomMinimumSize.Y)),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                MinValue = 0,
                MaxValue = 1,
                Value = owned ? 1 : 0,
                Step = 1,
            };
            progress = textureProgress;
        }
        else
        {
            progress = new ProgressBar
            {
                MinValue = 0,
                MaxValue = 1,
                Value = owned ? 1 : 0,
                ShowPercentage = false,
                CustomMinimumSize = new Vector2(0, 8),
                Step = 1,
            };
        }

        HFlowContainer petRow = NewGrid(center: true);
        petRow.AddChild(BuildMorePetCell(pet));

        column.AddChild(header);
        column.AddChild(progress);
        column.AddChild(petRow);
        margin.AddChild(column);
        card.AddChild(margin);
        return card;
    }

    Control BuildMorePetCell(MorePetDef pet)
    {
        int count = _state?.Count(pet.Id) ?? 0;
        TextureButton button = new()
        {
            TextureNormal = pet.Icon ?? MissingIcon,
            CustomMinimumSize = new Vector2(96, 96),
            IgnoreTextureSize = true,
            StretchMode = TextureButton.StretchModeEnum.Scale,
            Modulate = count > 0 ? Colors.White : new Color(0.22f, 0.22f, 0.22f),
            MouseDefaultCursorShape = Control.CursorShape.PointingHand,
        };
        button.Pressed += () => SelectPet(pet);
        return button;
    }

    Control BuildDefaultPetCell(IPetsEntity entity, ActivePet activePet)
    {
        string status = activePet.IsUnlocked ? "Obtained" : "Not obtained";
        TextureButton button = new()
        {
            TextureNormal = activePet.Pet.Icon ?? MissingIcon,
            CustomMinimumSize = new Vector2(96, 96),
            IgnoreTextureSize = true,
            StretchMode = TextureButton.StretchModeEnum.Scale,
            Modulate = activePet.IsUnlocked ? Colors.White : new Color(0.22f, 0.22f, 0.22f),
            MouseDefaultCursorShape = Control.CursorShape.PointingHand,
            TooltipText = $"{activePet.Pet.DisplayName.Text}\n{entity.DisplayName.Text}\n{status}",
        };
        button.Pressed += () => _petsCompletion?.RequestPetModal(activePet);
        return button;
    }

    Texture2D? DefaultPetIcon()
    {
        if (_petsCompletion != null)
        {
            foreach (IPetsEntity entity in _petsCompletion.PetEntities)
            {
                foreach (ActivePet activePet in entity.Pets.ActivePets.Values)
                {
                    if (activePet.Pet.Icon != null) return activePet.Pet.Icon;
                }
            }
        }
        return MissingIcon;
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
            Text = title ?? pet.ShortName,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        heading.AddThemeFontSizeOverride("font_size", 22);

        Label name = new()
        {
            Text = pet.ShortName,
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
        details.AppendLine("Bonus");
        details.Append(pet.BuffText);

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
                Text = $"{pet.ShortName}  -  {pet.Location}",
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
