using System.Text.Json;
using System.Text.Json.Nodes;

namespace MorePets;

internal sealed class MorePetsSettings
{
    readonly string _path;
    readonly Action<string> _log;
    JsonObject _values = new();
    DateTime _lastWriteUtc;

    public MorePetsSettings(string path, Action<string> log)
    {
        _path = path;
        _log = log;
        Reload();
        EnsureDefaults();
    }

    public bool IsEnabled(string petName)
    {
        if (_values.TryGetPropertyValue(petName, out JsonNode? node) && node is JsonValue value &&
            value.TryGetValue(out bool enabled))
        {
            return enabled;
        }
        return true;
    }

    public bool ReloadIfChanged()
    {
        try
        {
            if (!File.Exists(_path)) return false;
            DateTime stamp = File.GetLastWriteTimeUtc(_path);
            if (stamp == _lastWriteUtc) return false;
            Reload();
            return true;
        }
        catch (Exception e)
        {
            _log("Settings reload failed: " + e.Message);
            return false;
        }
    }

    void Reload()
    {
        try
        {
            if (!File.Exists(_path))
            {
                _values = new JsonObject();
                _lastWriteUtc = DateTime.MinValue;
                return;
            }

            JsonNode? root = JsonNode.Parse(File.ReadAllText(_path));
            _values = root as JsonObject ?? new JsonObject();
            _lastWriteUtc = File.GetLastWriteTimeUtc(_path);
        }
        catch (Exception e)
        {
            _log("Settings could not be read: " + e.Message);
            _values = new JsonObject();
            _lastWriteUtc = DateTime.MinValue;
        }
    }

    bool EnsureDefaults()
    {
        bool changed = false;
        foreach (MorePetDef pet in PetCatalog.All)
        {
            if (_values.ContainsKey(pet.Name)) continue;
            _values[pet.Name] = true;
            changed = true;
        }
        if (changed) Save();
        return changed;
    }

    public void Save()
    {
        try
        {
            string json = _values.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_path, json);
            _lastWriteUtc = File.GetLastWriteTimeUtc(_path);
        }
        catch (Exception e)
        {
            _log("Settings could not be saved: " + e.Message);
        }
    }
}

internal sealed class MorePetsStateData
{
    public string Character { get; set; } = "";
    public Dictionary<string, int> Counts { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

internal sealed class MorePetsState
{
    readonly string _path;
    readonly Action<string> _log;
    readonly object _gate = new();
    MorePetsStateData _data = new();

    public MorePetsState(string path, string character, Action<string> log)
    {
        _path = path;
        _log = log;
        Load(character);
    }

    public int Count(string id)
    {
        lock (_gate)
        {
            return _data.Counts.TryGetValue(id, out int count) ? count : 0;
        }
    }

    public bool Owned(string id) => Count(id) > 0;

    /// <summary>Adds one obtained pet and returns the new count.</summary>
    public int Add(string id)
    {
        int count;
        lock (_gate)
        {
            _data.Counts.TryGetValue(id, out count);
            count++;
            _data.Counts[id] = count;
        }
        Save();
        return count;
    }

    void Load(string character)
    {
        try
        {
            if (File.Exists(_path))
            {
                MorePetsStateData? loaded = JsonSerializer.Deserialize<MorePetsStateData>(File.ReadAllText(_path));
                if (loaded != null)
                {
                    _data = loaded;
                    _data.Counts ??= new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    _data.Counts = new Dictionary<string, int>(_data.Counts, StringComparer.OrdinalIgnoreCase);
                }
            }
        }
        catch (Exception e)
        {
            _log("Pet state could not be read: " + e.Message);
        }

        _data.Character = character;
    }

    public void Save()
    {
        try
        {
            string? dir = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            string temp = _path + ".tmp";
            string json;
            lock (_gate)
            {
                json = JsonSerializer.Serialize(_data, new JsonSerializerOptions { WriteIndented = true });
            }
            File.WriteAllText(temp, json);
            File.Move(temp, _path, overwrite: true);
        }
        catch (Exception e)
        {
            _log("Pet state could not be saved: " + e.Message);
        }
    }
}
