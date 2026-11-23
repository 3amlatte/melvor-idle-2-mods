using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using GamesByMalcs.Melvor2;
using GamesByMalcs.Melvor2.Components.CombatEffects;
using GamesByMalcs.Melvor2.Components.Inventories;
using GamesByMalcs.Melvor2.Components.ItemBurying;
using GamesByMalcs.Melvor2.Data;
using GamesByMalcs.Melvor2.Data.Items;
using GamesByMalcs.Melvor2.Entities;
using GamesByMalcs.Melvor2.Entities.Bank;
using GamesByMalcs.Melvor2.Entities.Farming;
using GamesByMalcs.Melvor2.Localization;
using GamesByMalcs.Melvor2.Modifiers;
using Godot;
using MelvorModLoader;

namespace MelvorAuto;

/// <summary>
/// Three small conveniences in one mod:
///   - bones received in the bank are buried automatically (with per-bone exclusions);
///   - openable items received in the bank are opened automatically (with an exclusion list);
///   - Farming plots are harvested, composted, planted and watered automatically, and the
///     grain mill is stocked and started.
/// It reacts to the game's farming "needs attention" flag, which is the red sidebar badge.
/// </summary>
public sealed class MelvorAutoMod : IMod
{
    const int MaxNesting = 8;
    const long MillIntervalSeconds = 2 * 60 * 60;   // 2 real hours = 2 in-game hours at 1:1 speed

    sealed class Source(string name) : INamedObject
    {
        public ILocalizedString DisplayName { get; } = new LocalizedString(name);
    }

    static readonly Source FreeSource = new("MelvorAuto: free farming automation");
    static ModifierValues? s_freeValues;
    static Game? s_freeFor;

    ModContext _ctx = null!;
    Settings _settings = null!;
    string _settingsPath = "";
    string _availablePath = "";
    DateTime _settingsStamp;
    HashSet<string> _excludedBones = new(StringComparer.OrdinalIgnoreCase);
    HashSet<string> _excludedOpenables = new(StringComparer.OrdinalIgnoreCase);

    Game? _game;
    BankEntity? _bank;
    FarmingEntity? _farming;
    FarmingPassiveComponent? _passive;
    FarmingGrainMillComponent? _mill;
    ModifierSystem? _mods;

    int _frame;
    int _depth;
    readonly HashSet<string> _logged = new();
    readonly HashSet<string> _loggedAutomation = new();

    public void OnLoad(ModContext ctx)
    {
        _ctx = ctx;
        _settingsPath = Path.Combine(ctx.Directory, "settings.json");
        _availablePath = Path.Combine(ctx.Directory, "available.json");
        _settings = Settings.Load(_settingsPath, ctx.Log);
        _settingsStamp = File.Exists(_settingsPath) ? File.GetLastWriteTimeUtc(_settingsPath) : DateTime.MinValue;
        RebuildSets();

        ctx.Tree.NodeAdded += OnNodeAdded;
        ctx.Frame += OnFrame;
        ctx.Log("Loaded.");
    }

    void OnNodeAdded(Node node)
    {
        // Before load-time offline progress, so bones/chests from offline drops are handled too.
        if (node is GamesByMalcs.Melvor2.GameMenus.GameView) TryBind();
    }

    void OnFrame()
    {
        if (++_frame % 30 != 0) return;   // twice a second
        TryBind();
        ReloadSettingsIfChanged();

        // The farming flags are applied once when a character opens. Plots bought later are
        // picked up on the next launch, as requested. The mill is checked against a real
        // two-hour timer while the game is open.
        TryStockMill();
    }

    void TryBind()
    {
        Game? game = GameAccess.CurrentGame;
        if (game is not { IsSaveLoaded: true }) return;
        if (ReferenceEquals(game, _game)) return;

        Detach();
        _game = game;

        if (!game.Entities.TryGetObjectById("MelvorBase:Bank", out GameEntity? bankEntity) || bankEntity is not BankEntity bank)
        {
            LogOnce("The Bank entity was not found; auto-bury/open are off for this character.");
            return;
        }
        if (!game.Entities.TryGetObjectById("MelvorBase:Farming", out GameEntity? farmEntity) || farmEntity is not FarmingEntity farming)
        {
            LogOnce("The Farming entity was not found; auto-farm is off for this character.");
            return;
        }

        _bank = bank;
        _farming = farming;
        _passive = farming.Passive;
        _mill = farming.GrainMill;
        _mods = Accessors.ModifiersOf(game);

        _bank.Inventory.ItemQuantityChanged += OnItemQuantityChanged;

        AddFreeAutomation(game);
        ApplyAutomation();
        TryStockMill();

        Available available = BuildAvailable(game);
        available.Save(_availablePath);
        ApplyBoneDefaults(available.Bones);

        _ctx.Log($"Ready. Auto-bury {_settings.AutoBuryBones}, auto-open {_settings.AutoOpenItems}, free auto-farm {_settings.AutoFarm}.");
    }

    void Detach()
    {
        if (_bank != null)
        {
            try { _bank.Inventory.ItemQuantityChanged -= OnItemQuantityChanged; } catch { }
        }
        _bank = null;
        _farming = null;
        _passive = null;
        _mill = null;
        _mods = null;
        _game = null;
    }

    void ReloadSettingsIfChanged()
    {
        try
        {
            if (!File.Exists(_settingsPath)) return;
            DateTime stamp = File.GetLastWriteTimeUtc(_settingsPath);
            if (stamp == _settingsStamp) return;
            _settings = Settings.Load(_settingsPath, _ctx.Log);
            _settingsStamp = File.GetLastWriteTimeUtc(_settingsPath);
            RebuildSets();
            _ctx.Log("settings.json reloaded.");
        }
        catch (Exception e)
        {
            LogOnce("settings reload failed: " + e.Message);
        }
    }

    void RebuildSets()
    {
        _excludedBones = new HashSet<string>(_settings.ExcludedBones ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
        _excludedOpenables = new HashSet<string>(_settings.ExcludedOpenables ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
    }

    // ------------------------------------------------------------------ bank additions

    void OnItemQuantityChanged(object? sender, ItemQuantityChangedEventArgs e)
    {
        if (e.QuantityChange <= 0) return;
        if (_depth > MaxNesting) return;

        Item item = e.Item;

        if (_settings.AutoBuryBones && IsBone(item) && !_excludedBones.Contains(item.Id))
        {
            Bury(item, e.QuantityChange);
            return;
        }

        if (_settings.AutoOpenItems && item.Capabilities.Contains<OpenableCapability>() && !_excludedOpenables.Contains(item.Id))
        {
            Open(item, e.QuantityChange);
        }
    }

    void Bury(Item item, int quantity)
    {
        if (_bank == null) return;
        _depth++;
        try
        {
            ItemBuryingStats stats = _bank.ItemBurying.GetItemStats(item);
            try { _bank.ItemBurying.BuryItem(item, stats, quantity); }
            finally { stats.Destroy(); }
        }
        catch (Exception e)
        {
            LogOnce($"Bury failed for {item.Id}: {e.Message}");
        }
        finally { _depth--; }
    }

    void Open(Item item, int quantity)
    {
        if (_bank == null) return;
        _depth++;
        try { _bank.ItemOpening.OpenItem(item, quantity); }
        catch (Exception e) { LogOnce($"Open failed for {item.Id}: {e.Message}"); }
        finally { _depth--; }
    }

    static bool IsBone(Item item)
    {
        if (!item.Capabilities.TryGet<BuryCapability>(out _)) return false;
        string text = item.Id + " " + item.DisplayName.GetPluralText(1);
        return text.Contains("bone", StringComparison.OrdinalIgnoreCase);
    }

    // ------------------------------------------------------------------ available lists

    Available BuildAvailable(Game game)
    {
        var available = new Available();

        foreach (Item item in game.Data.GetDataRegistryOutput<Item>().GetAllObjects())
        {
            if (item.Capabilities.TryGet<BuryCapability>(out BuryCapability? bury) && IsBone(item))
            {
                available.Bones.Add(new BoneInfo
                {
                    Id = item.Id,
                    Name = item.DisplayName.GetPluralText(1),
                    Points = (int)bury.CurrencyGain.Quantity
                });
            }

            if (item.Capabilities.Contains<OpenableCapability>())
            {
                available.Openables.Add(new OpenableInfo
                {
                    Id = item.Id,
                    Name = item.DisplayName.GetPluralText(1)
                });
            }
        }

        available.Bones.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        available.Openables.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return available;
    }

    void ApplyBoneDefaults(List<BoneInfo> bones)
    {
        if (_settings.BoneDefaultsApplied) return;

        _settings.ExcludedBones = bones
            .Where(b => b.Points > 6)   // anything better than Big Bone
            .Select(b => b.Id)
            .ToList();
        _settings.BoneDefaultsApplied = true;
        _settings.Save(_settingsPath, _ctx.Log);
        _settingsStamp = File.GetLastWriteTimeUtc(_settingsPath);
        RebuildSets();

        _ctx.Log("Default bone exclusions: " +
                 string.Join(", ", bones.Where(b => b.Points > 6).Select(b => b.Name)));
    }

    // ------------------------------------------------------------------ farming automation

    void AddFreeAutomation(Game game)
    {
        if (_mods == null) return;

        try
        {
            _mods.AddOrUpdate(FreeSource, FreeValues(game), 1.0);
            _ctx.Log("Farming automation costs are now free (wine and any other automation cost).");
        }
        catch (Exception e)
        {
            LogOnce("Could not make farming automation free: " + e.Message);
        }
    }

    void ApplyAutomation()
    {
        if (_farming == null || _passive == null) return;

        bool master = _settings.AutoFarm;
        int purchased = 0;

        foreach (ActiveFarmingPlot plot in _passive.ActivePlots.Values)
        {
            if (!plot.IsPurchased) continue;
            purchased++;

            plot.AutoPlant = master && _settings.AutoPlant;
            plot.AutoHarvest = master && _settings.AutoHarvest;
            plot.AutoWater = master && _settings.AutoWater;
            plot.AutoCompost = master && _settings.AutoCompost;

            ActiveFarmingRecipe? recipe = plot.SelectedRecipe ?? plot.PlantedRecipe;
            if (recipe?.AutomationRequirements is { } requirements && !requirements.AreMet)
            {
                string key = plot.Plot.Id + "|" + recipe.Recipe.Id;
                if (_loggedAutomation.Add(key))
                {
                    _ctx.Log($"{plot.Plot.DisplayName.Text} ({recipe.Recipe.DisplayName.Text}): " +
                             "automation requirements are not met; this plot will not be automated yet.");
                }
            }
        }

    }

    void TryStockMill()
    {
        if (!_settings.AutoMill || _mill?.Data == null) return;

        string character = string.IsNullOrWhiteSpace(_game?.CharacterName) ? "default" : _game!.CharacterName!;
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        _settings.MillStockedAtUnix ??= new Dictionary<string, long>();
        if (_settings.MillStockedAtUnix.TryGetValue(character, out long last) &&
            now - last < MillIntervalSeconds)
        {
            return;
        }

        StockMill(character, now);
    }

    void StockMill(string character, long now)
    {
        if (_mill?.Data == null) return;

        try
        {
            _mill.DepositAllToGrainMill();

            // Only ever start the mill. If it is already running, leave it running: calling
            // ToggleGrainMillTimer(true) while active would restart its timer, and passing false
            // would stop it.
            bool started = false;
            if (!_mill.IsActive)
            {
                if (_mill.SelectedRecipe == null || !_mill.CanCreateRecipe(_mill.SelectedRecipe))
                {
                    ActiveGrainMillRecipe? pick = _mill.ActiveRecipes.Values
                        .FirstOrDefault(r => _mill.CanCreateRecipe(r));
                    if (pick != null) _mill.SelectRecipe(pick);
                }

                if (_mill.SelectedRecipe != null && _mill.CanCreateRecipe(_mill.SelectedRecipe))
                    started = _mill.ToggleGrainMillTimer(true);
            }

            _settings.MillStockedAtUnix[character] = now;
            _settings.Save(_settingsPath, _ctx.Log);
            if (File.Exists(_settingsPath))
                _settingsStamp = File.GetLastWriteTimeUtc(_settingsPath);

            if (started)
                _ctx.Log($"Grain mill stocked and started for {character}. Next stock in 2 hours.");
            else if (_mill.IsActive)
                _ctx.Log($"Grain mill stocked for {character}; it was already running, so it was left running.");
            else
                _ctx.Log($"Grain mill stocked for {character}, but no recipe could be started.");
        }
        catch (Exception e)
        {
            LogOnce("Grain mill stocking failed: " + e.Message);
        }
    }

    static ModifierValues FreeValues(Game game)
    {
        if (s_freeValues != null && ReferenceEquals(s_freeFor, game)) return s_freeValues;

        // FlatAutomationCost is subtracted from every recipe's automation cost and clamped at 0.
        // A component-wide context covers every farming recipe and plot.
        const string json = """
            {
              "Stat": { "StatsType": "MelvorBase:FarmingAction", "Name": "FlatAutomationCost" },
              "Value": -1000000,
              "LocalContext": { "Component": { "Entity": "MelvorBase:Farming", "Component": "Passive" } },
              "FromContext": null,
              "Condition": null,
              "Scaling": null
            }
            """;

        var value = new ComponentModifierValue(game.Data, JsonDocument.Parse(json).RootElement);
        s_freeValues = new ModifierValues(
            new List<GlobalModifierValue>(),
            new List<ComponentModifierValue> { value },
            new List<CombatEffectModifierValue>());
        s_freeFor = game;
        return s_freeValues;
    }

    void LogOnce(string message)
    {
        if (_logged.Add(message)) _ctx.Log(message);
    }
}
