using System.Text.Json;
using System.Text.Json.Serialization;

namespace MelvorTown;

public sealed record UpgradeDef(
    string Id, string Name, string Description, int PopRequired, int FoodCost,
    string? Requires = null, int Max = 1);

public sealed class TownState
{
    public static readonly UpgradeDef[] Upgrades =
    {
        new("cottage", "Cottage", "+2 population capacity. A thatched cottage joins the town.", 3, 30),
        new("grove", "Tree Grove", "+1 tree plot. Forage food from the grove.", 2, 20),
        new("farm", "New Farm", "+1 farm plot. More carrots and chickens.", 4, 60),
        new("timber", "Timber House", "+4 population capacity. A two-storey house.", 8, 150, "cottage"),
        new("school", "School", "Villagers grow faster and lessons can be funded.", 12, 260, "timber"),
        new("manor", "Manor", "+8 population capacity. A stone manor for a growing town.", 16, 400, "school"),
        new("balloon", "Hot Air Balloon", "Scout the roads; merchants visit twice as often.", 20, 650, "manor"),
    };

    public double Food { get; set; } = 28;
    public int Population { get; set; } = 2;
    public int Lessons { get; set; }
    public bool Paused { get; set; }
    public bool AlwaysMerchant { get; set; } = true;
    public bool ShowRates { get; set; } = true;
    public bool MerchantHere { get; set; }
    public double MerchantTimer { get; set; } = 120;
    public double MerchantLeft { get; set; }
    public Dictionary<string, int> Owned { get; set; } = new()
    {
        ["cottage"] = 1,
        ["farm"] = 1,
        ["grove"] = 1,
    };
    public long LastMs { get; set; }

    [JsonIgnore] public double HarvestCooldown { get; set; }
    [JsonIgnore] public double ForageCooldown { get; set; }
    [JsonIgnore] public double ScoutCooldown { get; set; }
    [JsonIgnore] double _farmAcc;
    [JsonIgnore] double _growthAcc;
    [JsonIgnore] double _starveAcc;
    [JsonIgnore] string _path = "";

    public int Count(string id) => Owned.TryGetValue(id, out int n) ? n : 0;

    public bool Has(string id) => Count(id) > 0;

    public int Farms => Math.Max(1, Count("farm"));
    public int Groves => Math.Max(1, Count("grove"));
    public bool HasSchool => Has("school");
    public bool HasBalloon => Has("balloon");

    public int Capacity =>
        4 + Count("cottage") * 2 + Count("timber") * 4 + Count("manor") * 8;

    public double GrowthInterval => (HasSchool ? 15.0 : 25.0) * (1 - 0.1 * Lessons);

    public double FoodPerMinute =>
        Farms * 30.0 - Population * 0.9;

    public void Tick(double dt)
    {
        if (dt <= 0 || Paused) return;
        dt = Math.Min(dt, 2.0);

        HarvestCooldown = Math.Max(0, HarvestCooldown - dt);
        ForageCooldown = Math.Max(0, ForageCooldown - dt);
        ScoutCooldown = Math.Max(0, ScoutCooldown - dt);

        // Farms: carrots every 8 seconds; chickens add a little anyway.
        _farmAcc += dt;
        while (_farmAcc >= 8.0)
        {
            _farmAcc -= 8.0;
            Food += 4;
        }

        // Villagers eat.
        Food -= Population * 0.015 * dt;

        // Growth.
        if (Population < Capacity && Food > 6 + Population * 2)
        {
            _growthAcc += dt;
            if (_growthAcc >= GrowthInterval)
            {
                _growthAcc = 0;
                Population += 1;
                Food = Math.Max(0, Food - 4);
                if (Population >= Capacity) _growthAcc = 0;
            }
        }
        else
        {
            _growthAcc = 0;
        }

        // Starvation.
        if (Food <= 0)
        {
            Food = 0;
            _starveAcc += dt;
            if (_starveAcc >= 25 && Population > 1)
            {
                _starveAcc = 0;
                Population -= 1;
            }
        }
        else
        {
            _starveAcc = 0;
        }

        // Merchant visits.
        if (AlwaysMerchant)
        {
            MerchantHere = true;
            MerchantLeft = Math.Max(MerchantLeft, 30);
        }
        else if (MerchantHere)
        {
            MerchantLeft -= dt;
            if (MerchantLeft <= 0)
            {
                MerchantHere = false;
                MerchantLeft = 0;
                MerchantTimer = HasBalloon ? 90 : 200;
            }
        }
        else
        {
            MerchantTimer -= dt;
            if (MerchantTimer <= 0)
            {
                MerchantHere = true;
                MerchantLeft = 45;
            }
        }
    }

    public bool CanBuy(UpgradeDef def, out string reason)
    {
        if (Count(def.Id) >= def.Max) { reason = "Already built."; return false; }
        if (def.Requires != null && !Has(def.Requires)) { reason = "Needs " + NameOf(def.Requires) + "."; return false; }
        if (Population < def.PopRequired) { reason = $"Needs {def.PopRequired} villagers."; return false; }
        if (Food < def.FoodCost) { reason = $"Needs {def.FoodCost} food."; return false; }
        reason = "";
        return true;
    }

    public bool Buy(string id, out string message)
    {
        UpgradeDef? def = Upgrades.FirstOrDefault(u => u.Id == id);
        if (def == null) { message = "Unknown upgrade."; return false; }
        if (!CanBuy(def, out string reason)) { message = reason; return false; }

        Food -= def.FoodCost;
        Owned[id] = Count(id) + 1;
        message = def.Name + " built.";
        return true;
    }

    public void Reset()
    {
        Food = 28;
        Population = 2;
        Lessons = 0;
        Paused = false;
        AlwaysMerchant = true;
        MerchantHere = false;
        MerchantTimer = 120;
        MerchantLeft = 0;
        Owned = new Dictionary<string, int>
        {
            ["cottage"] = 1,
            ["farm"] = 1,
            ["grove"] = 1,
        };
    }

    public static string NameOf(string id) =>
        Upgrades.FirstOrDefault(u => u.Id == id)?.Name ?? id;

    public void Save()
    {
        if (_path.Length == 0) return;
        try
        {
            LastMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            File.WriteAllText(_path, JsonSerializer.Serialize(this));
        }
        catch { }
    }

    public static TownState Load(string directory)
    {
        string path = Path.Combine(directory, "town.json");
        TownState state;
        try
        {
            state = File.Exists(path)
                ? JsonSerializer.Deserialize<TownState>(File.ReadAllText(path)) ?? new TownState()
                : new TownState();
        }
        catch
        {
            state = new TownState();
        }

        state._path = path;
        state.UpgradesSetup();

        // Catch up while the game was closed, capped at 8 hours.
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (state.LastMs > 0)
        {
            double seconds = Math.Clamp((now - state.LastMs) / 1000.0, 0, 8 * 3600);
            while (seconds > 0)
            {
                double step = Math.Min(2, seconds);
                state.Tick(step);
                seconds -= step;
            }
        }
        state.LastMs = now;
        return state;
    }

    void UpgradesSetup()
    {
        Owned ??= new Dictionary<string, int>();
        if (Owned.Count == 0)
        {
            Owned["cottage"] = 1;
            Owned["farm"] = 1;
            Owned["grove"] = 1;
        }
        Population = Math.Clamp(Population, 1, Math.Max(1, Capacity));
        Food = Math.Max(0, Food);
    }
}
