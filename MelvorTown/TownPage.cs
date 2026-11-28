using Godot;
using MelvorModLoader;

namespace MelvorTown;

internal sealed class TownPage : CanvasLayer
{
    readonly TownState _state;
    readonly Dictionary<string, Texture2D?> _art = new();
    readonly Texture2D? _cogTexture;

    readonly Label _popLabel;
    readonly Label _foodLabel;
    readonly Label _seedLabel;
    readonly Label _rateLabel;
    readonly Label _hintLabel;
    readonly TownView _view;

    readonly Control _popupHost;
    readonly CenterContainer _popupCenter;
    PanelContainer? _popupPanel;

    string _signature = "";
    double _refresh;
    double _save;

    public TownPage(ModContext ctx, TownState state)
    {
        _state = state;
        Layer = 110;
        Visible = false;

        foreach (string name in new[]
        {
            "bar", "house1", "house2", "house3", "farm", "seedstore", "merchant",
            "trees", "school", "balloon", "tab", "pop", "food", "seed", "ground",
        })
        {
            _art[name] = Ui.Texture(ctx.Directory, name);
        }
        _cogTexture = Ui.Texture(ctx.Directory, "cog");

        var root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(root);

        var dim = new ColorRect { Color = new Color(0, 0, 0, 0.62f) };
        dim.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        dim.GuiInput += ev =>
        {
            if (ev is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
                Close();
        };
        root.AddChild(dim);

        var margin = new MarginContainer
        {
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
        margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 28);
        margin.AddThemeConstantOverride("margin_right", 28);
        margin.AddThemeConstantOverride("margin_top", 22);
        margin.AddThemeConstantOverride("margin_bottom", 22);
        root.AddChild(margin);

        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", Ui.Box(Ui.Panel, Ui.Border, 6, 2));
        margin.AddChild(panel);

        var inner = new MarginContainer();
        inner.AddThemeConstantOverride("margin_left", 14);
        inner.AddThemeConstantOverride("margin_right", 14);
        inner.AddThemeConstantOverride("margin_top", 12);
        inner.AddThemeConstantOverride("margin_bottom", 10);
        panel.AddChild(inner);

        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 8);
        inner.AddChild(column);

        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 10);
        column.AddChild(header);

        var title = Ui.Label("Melvor Town", 24, Ui.Accent);
        header.AddChild(title);
        header.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

        header.AddChild(Ui.Icon(_art.GetValueOrDefault("pop"), 20));
        _popLabel = Ui.Label("", 15);
        header.AddChild(_popLabel);

        header.AddChild(Ui.Icon(_art.GetValueOrDefault("food"), 20));
        _foodLabel = Ui.Label("", 15);
        header.AddChild(_foodLabel);

        header.AddChild(Ui.Icon(_art.GetValueOrDefault("seed"), 18));
        _seedLabel = Ui.Label("", 15);
        header.AddChild(_seedLabel);

        header.AddChild(Ui.Button("Upgrades", OpenUpgrades));

        var cog = new CogButton();
        cog.CustomMinimumSize = new Vector2(34, 34);
        cog.TooltipText = "Town settings";
        cog.Pressed += OpenSettings;
        header.AddChild(cog);

        _view = new TownView(_art, _art.GetValueOrDefault("ground"))
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 360),
        };
        _view.SetState(_state);
        _view.Clicked += OpenBuilding;
        column.AddChild(_view);

        var footer = new HBoxContainer();
        footer.AddThemeConstantOverride("separation", 10);
        column.AddChild(footer);

        _hintLabel = Ui.Label("Click a building to open it. The town grows as you build.", 12, Ui.Muted);
        footer.AddChild(_hintLabel);
        footer.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        _rateLabel = Ui.Label("", 12, Ui.Muted);
        footer.AddChild(_rateLabel);

        _popupHost = new Control { Visible = false, MouseFilter = Control.MouseFilterEnum.Stop };
        _popupHost.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(_popupHost);

        var popDim = new ColorRect { Color = new Color(0, 0, 0, 0.45f) };
        popDim.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        popDim.GuiInput += ev =>
        {
            if (ev is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
                CloseWindow();
        };
        _popupHost.AddChild(popDim);

        _popupCenter = new CenterContainer();
        _popupCenter.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _popupHost.AddChild(_popupCenter);

        ApplyGameFont(root);
    }

    static void ApplyGameFont(Control root)
    {
        Label? donor = null;
        Control? bank = root.GetTree()?.Root?.FindChild("BankNavigationButton", true, false) as Control;
        if (bank != null) donor = bank.FindChild("Title", true, false) as Label;
        Font? font = donor?.LabelSettings?.Font;
        if (font == null) return;

        var theme = new Theme();
        theme.SetFont("font", "Label", font);
        theme.SetFont("font", "Button", font);
        theme.SetFont("font", "CheckButton", font);
        root.Theme = theme;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!Visible) return;
        if (!@event.IsActionPressed("ui_cancel")) return;

        if (_popupHost.Visible) CloseWindow();
        else Close();
        GetViewport().SetInputAsHandled();
    }

    public void Toggle()
    {
        if (Visible) Close();
        else Open();
    }

    public void Open()
    {
        Visible = true;
        Refresh();
        _view.Rebuild();
    }

    public void Close()
    {
        Visible = false;
        CloseWindow();
        _state.Save();
    }

    public override void _Process(double delta)
    {
        _save += delta;
        if (_save >= 10)
        {
            _save = 0;
            _state.Save();
        }

        if (!Visible) return;
        _refresh += delta;
        if (_refresh >= 0.4)
        {
            _refresh = 0;
            Refresh();
        }
    }

    void Refresh()
    {
        _popLabel.Text = $"{_state.Population} / {_state.Capacity}";
        _foodLabel.Text = $"{_state.Food:0}";
        _seedLabel.Text = $"{_state.Seeds:0}";
        _rateLabel.Text = (_state.FoodPerMinute >= 0 ? "+" : "") + $"{_state.FoodPerMinute:0} food/min";
        _rateLabel.Visible = _state.ShowRates;

        string signature = string.Join(",",
            _state.Owned.Where(p => p.Value > 0).Select(p => p.Key),
            _state.MerchantHere ? "merchant" : "",
            _state.Farms, _state.Groves);

        if (signature != _signature)
        {
            _signature = signature;
            _view.Rebuild();
        }

        _hintLabel.Text = _state.MerchantHere
            ? "A travelling merchant is in town."
            : "Click a building to open it. The town grows as you build.";
    }

    void OpenBuilding(string key)
    {
        switch (key)
        {
            case "house1": OpenHouse("Cottage", 2); break;
            case "house2": OpenHouse("Timber House", 4); break;
            case "house3": OpenHouse("Manor", 8); break;
            case "farm1":
            case "farm2": OpenFarm(); break;
            case "seedstore": OpenSeedStore(); break;
            case "merchant": OpenMerchant(); break;
            case "trees1":
            case "trees2": OpenGrove(); break;
            case "school": OpenSchool(); break;
            case "balloon": OpenBalloon(); break;
        }
    }

    void OpenHouse(string name, int capacity)
    {
        OpenWindow(name, body =>
        {
            body.AddChild(Ui.Label($"+{capacity} population capacity", 15));
            body.AddChild(Ui.Label($"Villagers: {_state.Population} / {_state.Capacity}", 14, Ui.Text));
            body.AddChild(Ui.Label("Houses raise the number of villagers the town can support. Food and schools decide how fast it fills.", 13, Ui.Muted));
        });
    }

    void OpenFarm()
    {
        OpenWindow("Farm", body =>
        {
            void Render()
            {
                Clear(body);
                body.AddChild(Ui.Label($"Farms: {_state.Farms}    Seeds: {_state.Seeds:0}    Food: {_state.Food:0}", 15));
                body.AddChild(Ui.Label(
                    _state.Seeds > 0
                        ? "Each farm turns a seed into carrots every 8 seconds. Chickens add a little food on their own."
                        : "No seeds left. The chickens still produce a little, but the carrots have stopped.",
                    13, Ui.Muted));

                var harvest = Ui.Button(
                    _state.HarvestCooldown > 0
                        ? $"Harvest in {_state.HarvestCooldown:0}s"
                        : "Harvest now (+4 food per farm)",
                    () =>
                    {
                        double gain = _state.Farms * 4;
                        int used = (int)Math.Min(_state.Seeds, _state.Farms);
                        _state.Seeds -= used;
                        _state.Food += gain;
                        _state.HarvestCooldown = 20;
                        _state.Save();
                        Render();
                    });
                harvest.Disabled = _state.HarvestCooldown > 0 || _state.Seeds < 1;
                body.AddChild(harvest);
            }
            Render();
        });
    }

    void OpenSeedStore()
    {
        OpenWindow("Seed Store", body =>
        {
            void Render()
            {
                Clear(body);
                body.AddChild(Ui.Label($"Seeds: {_state.Seeds:0}    Food: {_state.Food:0}", 15));
                body.AddChild(Ui.Label("Seeds keep the farms growing. The store trades food for seed sacks.", 13, Ui.Muted));

                var small = Ui.Button("Buy 10 seeds - 15 food", () =>
                {
                    if (_state.Food < 15) return;
                    _state.Food -= 15;
                    _state.Seeds += 10;
                    _state.Save();
                    Render();
                });
                small.Disabled = _state.Food < 15;
                body.AddChild(small);

                var large = Ui.Button("Buy 30 seeds - 40 food", () =>
                {
                    if (_state.Food < 40) return;
                    _state.Food -= 40;
                    _state.Seeds += 30;
                    _state.Save();
                    Render();
                });
                large.Disabled = _state.Food < 40;
                body.AddChild(large);
            }
            Render();
        });
    }

    void OpenMerchant()
    {
        OpenWindow("Travelling Merchant", body =>
        {
            void Render()
            {
                Clear(body);
                if (!_state.MerchantHere)
                {
                    body.AddChild(Ui.Label($"The merchant is on the road. Back in about {Math.Max(0, _state.MerchantTimer):0}s.", 14));
                    body.AddChild(Ui.Label("Build a hot air balloon to bring merchants in twice as often.", 13, Ui.Muted));
                    return;
                }

                body.AddChild(Ui.Label($"The cart leaves in {Math.Max(0, _state.MerchantLeft):0}s.", 14, Ui.Accent));
                body.AddChild(Ui.Label("A few things the merchant will trade for food.", 13, Ui.Muted));

                var seeds = Ui.Button("10 seeds - 18 food", () =>
                {
                    if (_state.Food < 18) return;
                    _state.Food -= 18;
                    _state.Seeds += 10;
                    _state.Save();
                    Render();
                });
                seeds.Disabled = _state.Food < 18;
                body.AddChild(seeds);

                var recruit = Ui.Button("Recruit a villager - 35 food", () =>
                {
                    if (_state.Food < 35 || _state.Population >= _state.Capacity) return;
                    _state.Food -= 35;
                    _state.Population += 1;
                    _state.Save();
                    Render();
                });
                recruit.Disabled = _state.Food < 35 || _state.Population >= _state.Capacity;
                body.AddChild(recruit);
                if (_state.Population >= _state.Capacity)
                    body.AddChild(Ui.Label("No room for another villager. Build more housing.", 12, Ui.Muted));
            }
            Render();
        });
    }

    void OpenGrove()
    {
        OpenWindow("Tree Grove", body =>
        {
            void Render()
            {
                Clear(body);
                body.AddChild(Ui.Label($"Grove plots: {_state.Groves}", 15));
                body.AddChild(Ui.Label("Forage the grove for mushrooms, berries and firewood.", 13, Ui.Muted));
                var forage = Ui.Button(
                    _state.ForageCooldown > 0 ? $"Forage in {_state.ForageCooldown:0}s" : "Forage (+4 food)",
                    () =>
                    {
                        _state.Food += 4;
                        _state.ForageCooldown = 60;
                        _state.Save();
                        Render();
                    });
                forage.Disabled = _state.ForageCooldown > 0;
                body.AddChild(forage);
            }
            Render();
        });
    }

    void OpenSchool()
    {
        OpenWindow("School", body =>
        {
            void Render()
            {
                Clear(body);
                body.AddChild(Ui.Label($"Lessons funded: {_state.Lessons} / 3", 15));
                body.AddChild(Ui.Label($"Villagers arrive {15 * (1 - 0.1 * _state.Lessons):0}% faster than an unschooled town.", 13, Ui.Muted));

                var fund = Ui.Button("Fund a lesson - 80 food", () =>
                {
                    if (_state.Food < 80 || _state.Lessons >= 3) return;
                    _state.Food -= 80;
                    _state.Lessons += 1;
                    _state.Save();
                    Render();
                });
                fund.Disabled = _state.Food < 80 || _state.Lessons >= 3;
                body.AddChild(fund);
            }
            Render();
        });
    }

    void OpenBalloon()
    {
        OpenWindow("Hot Air Balloon", body =>
        {
            void Render()
            {
                Clear(body);
                body.AddChild(Ui.Label("The balloon watches the roads for merchants.", 14));
                body.AddChild(Ui.Label("\"A speck on the horizon!\" it says, mostly to itself.", 13, Ui.Muted));
                var scout = Ui.Button(
                    _state.ScoutCooldown > 0 ? $"Scout in {_state.ScoutCooldown:0}s" : "Scout for a merchant",
                    () =>
                    {
                        _state.MerchantHere = true;
                        _state.MerchantLeft = Math.Max(_state.MerchantLeft, 30);
                        _state.ScoutCooldown = 120;
                        _state.Save();
                        Render();
                    });
                scout.Disabled = _state.ScoutCooldown > 0 || _state.MerchantHere;
                body.AddChild(scout);
            }
            Render();
        });
    }

    void OpenUpgrades()
    {
        OpenWindow("Town Upgrades", body =>
        {
            void Render()
            {
                Clear(body);
                body.AddChild(Ui.Label($"Villagers {_state.Population} / {_state.Capacity}    Food {_state.Food:0}", 14, Ui.Muted));

                foreach (UpgradeDef def in TownState.Upgrades)
                {
                    var row = new HBoxContainer();
                    row.AddThemeConstantOverride("separation", 10);

                    var name = Ui.Label(def.Name, 15);
                    name.CustomMinimumSize = new Vector2(120, 0);
                    row.AddChild(name);

                    var desc = Ui.Label(def.Description, 12, Ui.Muted);
                    desc.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                    desc.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                    row.AddChild(desc);

                    bool built = _state.Count(def.Id) >= def.Max;
                    bool can = _state.CanBuy(def, out _);
                    var buy = Ui.Button(
                        built ? "Built" : $"{def.FoodCost} food (pop {def.PopRequired})",
                        () =>
                        {
                            _state.Buy(def.Id, out _);
                            _state.Save();
                            Render();
                        });
                    buy.Disabled = built || !can;
                    buy.CustomMinimumSize = new Vector2(170, 0);
                    row.AddChild(buy);

                    body.AddChild(row);
                }
            }
            Render();
        });
    }

    void OpenSettings()
    {
        OpenWindow("Town Settings", body =>
        {
            void Render()
            {
                Clear(body);

                var rates = new CheckButton { Text = "Show food per minute", ButtonPressed = _state.ShowRates };
                rates.AddThemeFontSizeOverride("font_size", 14);
                rates.Toggled += on =>
                {
                    _state.ShowRates = on;
                    _state.Save();
                };
                body.AddChild(rates);

                var pause = new CheckButton { Text = "Pause the town", ButtonPressed = _state.Paused };
                pause.AddThemeFontSizeOverride("font_size", 14);
                pause.Toggled += on =>
                {
                    _state.Paused = on;
                    _state.Save();
                };
                body.AddChild(pause);

                body.AddChild(Ui.Label("The town keeps growing while this page is closed, and catches up on time spent offline (up to 8 hours).", 12, Ui.Muted));

                bool confirming = false;
                Button reset = null!;
                reset = Ui.Button("Reset the town", () =>
                {
                    if (!confirming)
                    {
                        confirming = true;
                        reset.Text = "Click again to confirm reset";
                        return;
                    }
                    _state.Reset();
                    _state.Save();
                    Render();
                });
                reset.AddThemeColorOverride("font_color", Ui.Bad);
                body.AddChild(reset);
            }
            Render();
        });
    }

    static void Clear(Node node)
    {
        foreach (Node child in node.GetChildren())
        {
            node.RemoveChild(child);
            child.QueueFree();
        }
    }

    void OpenWindow(string title, Action<VBoxContainer> build)
    {
        CloseWindow();
        _popupHost.Visible = true;

        _popupPanel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(560, 0),
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
        _popupPanel.AddThemeStyleboxOverride("panel", Ui.Box(Ui.Panel, Ui.Border, 6, 2));
        _popupCenter.AddChild(_popupPanel);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 16);
        margin.AddThemeConstantOverride("margin_right", 16);
        margin.AddThemeConstantOverride("margin_top", 12);
        margin.AddThemeConstantOverride("margin_bottom", 14);
        _popupPanel.AddChild(margin);

        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 10);
        margin.AddChild(column);

        var header = new HBoxContainer();
        header.AddChild(Ui.Label(title, 20, Ui.Accent));
        header.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        var close = Ui.Button("X", CloseWindow, 14);
        close.CustomMinimumSize = new Vector2(34, 30);
        header.AddChild(close);
        column.AddChild(header);

        var body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", 8);
        column.AddChild(body);

        build(body);
    }

    void CloseWindow()
    {
        if (_popupPanel != null && GodotObject.IsInstanceValid(_popupPanel))
        {
            _popupCenter.RemoveChild(_popupPanel);
            _popupPanel.QueueFree();
        }
        _popupPanel = null;
        _popupHost.Visible = false;
    }

    sealed class CogButton : Button
    {
        public CogButton()
        {
            Flat = true;
            FocusMode = FocusModeEnum.None;
            MouseDefaultCursorShape = CursorShape.PointingHand;
            AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
            AddThemeStyleboxOverride("hover", Ui.Box(new Color(1, 1, 1, 0.08f), Colors.Transparent, 4, 0));
            AddThemeStyleboxOverride("pressed", Ui.Box(new Color(1, 1, 1, 0.12f), Colors.Transparent, 4, 0));
            AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
            Resized += QueueRedraw;
        }

        public override void _Draw()
        {
            Vector2 center = Size / 2f;
            float r = Mathf.Min(Size.X, Size.Y) * 0.24f;
            Color color = new(0.886f, 0.859f, 0.804f);

            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.Pi / 4f;
                var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                DrawLine(center + dir * r * 0.9f, center + dir * r * 1.45f, color, 3f);
            }
            DrawCircle(center, r, color);
            DrawCircle(center, r * 0.45f, new Color(0.11f, 0.10f, 0.09f));
        }
    }
}
