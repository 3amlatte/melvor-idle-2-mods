using GamesByMalcs.Melvor2;
using GamesByMalcs.Melvor2.Data.Currencies;
using GamesByMalcs.Melvor2.Entities.Bank;
using Godot;
using MelvorModLoader;

namespace MelvorTown;

internal sealed class TownPanel : Control
{
    readonly TownState _state;
    readonly Dictionary<string, Texture2D?> _art = new();

    readonly Label _tierLabel;
    readonly Label _popLabel;
    readonly Label _workerLabel;
    readonly Label _foodLabel;
    readonly Label _rateLabel;
    readonly Label _hintLabel;
    readonly TownView _view;
    readonly Button _moveButton;

    readonly PanelContainer _statTip;
    readonly Label _statTitle;
    readonly Label _statBody;

    readonly Button _relicButton;
    readonly PanelContainer _relicPanel;
    readonly VBoxContainer _relicList;

    readonly Button _buildingsButton;
    readonly PanelContainer _buildingsPanel;
    readonly VBoxContainer _buildingsList;

    readonly Control _popupHost;
    readonly CenterContainer _popupCenter;
    PanelContainer? _popupPanel;

    string _signature = "";
    string? _hoverName;
    double _refresh;
    double _save;

    public Texture2D? Ground => _art.GetValueOrDefault("ground");

    public TownPanel(ModContext ctx, TownState state)
    {
        _state = state;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;

        foreach (string name in new[]
        {
            "bar1", "bar2", "bar3", "bar4",
            "townhall1", "townhall2", "townhall3", "townhall4",
            "house1", "house2", "house3", "farm", "merchant",
            "trees", "school", "balloon", "tab", "pop", "worker", "food", "ground",
        })
        {
            _art[name] = Ui.Texture(ctx.Directory, name);
        }
        // The town fills the whole page, like the Quests page does.
        _view = new TownView(_art, _art.GetValueOrDefault("ground"), ctx.Directory);
        _view.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _view.SetState(_state);
        _view.SetGroundVisible(false);
        _view.Clicked += OpenBuilding;
        _view.Hovered += name =>
        {
            _hoverName = name;
            Refresh();
        };
        _state.MerchantArrived += () => MerchantStock.Roll(_state);
        AddChild(_view);

        // Top HUD floats over the town instead of framing it.
        var top = new PanelContainer { MouseFilter = MouseFilterEnum.Stop };
        top.AddThemeStyleboxOverride("panel", HudBox());
        top.SetAnchorsAndOffsetsPreset(LayoutPreset.TopWide);
        top.OffsetLeft = 10;
        top.OffsetRight = -10;
        top.OffsetTop = 8;
        AddChild(top);

        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 10);
        top.AddChild(header);

        header.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        _tierLabel = Ui.Label("", 15, Ui.Accent);
        Stat(header, "tab", _tierLabel, "Town tier",
            () => $"Tier {_state.Tier} - {_state.TierName}.\n{_state.TierRequirement}");
        _popLabel = Ui.Label("", 15);
        Stat(header, "pop", _popLabel, "Population",
            () => $"{_state.Population} of {_state.Capacity} places filled.\nPeople arrive while there is spare food and housing. Cottages, timber houses and manors raise the cap.");
        _workerLabel = Ui.Label("", 15);
        Stat(header, "worker", _workerLabel, "Workers",
            () => $"{_state.Workers} hired.\nWorkers take town jobs; the list of who is doing what is coming later.");
        _foodLabel = Ui.Label("", 15);
        Stat(header, "food", _foodLabel, "Food",
            () => $"{_state.Food:0} stored, net {(_state.FoodPerMinute >= 0 ? "+" : "")}{_state.FoodPerMinute:0} per minute.\nFarms, groves and merchants add food. Everyone eats over time.");
        _relicButton = Ui.Button("Relics", ToggleRelics);
        header.AddChild(_relicButton);
        _buildingsButton = Ui.Button("Buildings", ToggleBuildings);
        header.AddChild(_buildingsButton);
        header.AddChild(Ui.Button("Upgrades", OpenUpgrades));
        _moveButton = Ui.Button("Move", ToggleEdit);
        header.AddChild(_moveButton);

        var cog = new CogButton();
        cog.CustomMinimumSize = new Vector2(34, 34);
        cog.TooltipText = "Town settings";
        cog.Pressed += OpenSettings;
        header.AddChild(cog);

        // Bottom HUD.
        var bottom = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore };
        bottom.AddThemeStyleboxOverride("panel", HudBox());
        bottom.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomWide);
        bottom.OffsetLeft = 10;
        bottom.OffsetRight = -10;
        bottom.OffsetBottom = -8;
        AddChild(bottom);

        var footer = new HBoxContainer();
        footer.AddThemeConstantOverride("separation", 10);
        bottom.AddChild(footer);

        _hintLabel = Ui.Label("Click a building to open it. The town grows as you build.", 12, Ui.Muted);
        footer.AddChild(_hintLabel);
        footer.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        _rateLabel = Ui.Label("", 12, Ui.Muted);
        footer.AddChild(_rateLabel);

        _statTip = new PanelContainer { Visible = false, MouseFilter = MouseFilterEnum.Ignore };
        var tipStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.10f, 0.09f, 0.07f, 0.97f),
            BorderColor = new Color(0.24f, 0.21f, 0.16f),
        };
        tipStyle.SetBorderWidthAll(1);
        tipStyle.SetCornerRadiusAll(4);
        tipStyle.ContentMarginLeft = 10;
        tipStyle.ContentMarginRight = 10;
        tipStyle.ContentMarginTop = 8;
        tipStyle.ContentMarginBottom = 8;
        _statTip.AddThemeStyleboxOverride("panel", tipStyle);
        AddChild(_statTip);

        var tipColumn = new VBoxContainer();
        tipColumn.AddThemeConstantOverride("separation", 3);
        _statTip.AddChild(tipColumn);

        _statTitle = Ui.Label("", 14, Ui.Accent);
        tipColumn.AddChild(_statTitle);

        _statBody = Ui.Label("", 12, Ui.Text);
        _statBody.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _statBody.CustomMinimumSize = new Vector2(280, 0);
        tipColumn.AddChild(_statBody);

        _relicPanel = new PanelContainer { Visible = false, MouseFilter = MouseFilterEnum.Stop };
        var relicStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.10f, 0.09f, 0.07f, 0.97f),
            BorderColor = new Color(0.24f, 0.21f, 0.16f),
        };
        relicStyle.SetBorderWidthAll(1);
        relicStyle.SetCornerRadiusAll(4);
        relicStyle.ContentMarginLeft = 10;
        relicStyle.ContentMarginRight = 10;
        relicStyle.ContentMarginTop = 8;
        relicStyle.ContentMarginBottom = 8;
        _relicPanel.AddThemeStyleboxOverride("panel", relicStyle);
        AddChild(_relicPanel);

        _relicList = new VBoxContainer();
        _relicList.AddThemeConstantOverride("separation", 6);
        _relicPanel.AddChild(_relicList);

        _buildingsPanel = new PanelContainer { Visible = false, MouseFilter = MouseFilterEnum.Stop };
        var buildingStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.10f, 0.09f, 0.07f, 0.97f),
            BorderColor = new Color(0.24f, 0.21f, 0.16f),
        };
        buildingStyle.SetBorderWidthAll(1);
        buildingStyle.SetCornerRadiusAll(4);
        buildingStyle.ContentMarginLeft = 10;
        buildingStyle.ContentMarginRight = 10;
        buildingStyle.ContentMarginTop = 8;
        buildingStyle.ContentMarginBottom = 8;
        _buildingsPanel.AddThemeStyleboxOverride("panel", buildingStyle);
        AddChild(_buildingsPanel);

        _buildingsList = new VBoxContainer();
        _buildingsList.AddThemeConstantOverride("separation", 5);
        _buildingsPanel.AddChild(_buildingsList);

        _popupHost = new Control { Visible = false, MouseFilter = MouseFilterEnum.Stop };
        _popupHost.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_popupHost);

        var popDim = new ColorRect { Color = new Color(0, 0, 0, 0.45f) };
        popDim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        popDim.GuiInput += ev =>
        {
            if (ev is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
                CloseWindow();
        };
        _popupHost.AddChild(popDim);

        _popupCenter = new CenterContainer();
        _popupCenter.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _popupHost.AddChild(_popupCenter);

    }

    void Stat(HBoxContainer row, string art, Label label, string title, Func<string> body)
    {
        var group = new HBoxContainer { MouseFilter = MouseFilterEnum.Stop };
        group.AddThemeConstantOverride("separation", 4);

        var icon = Ui.Icon(_art.GetValueOrDefault(art), 20);
        icon.MouseFilter = MouseFilterEnum.Ignore;
        group.AddChild(icon);

        label.MouseFilter = MouseFilterEnum.Ignore;
        group.AddChild(label);

        group.MouseEntered += () => ShowStatTip(group, title, body());
        group.MouseExited += () => _statTip.Visible = false;

        row.AddChild(group);
    }

    void ShowStatTip(Control owner, string title, string body)
    {
        _statTitle.Text = title;
        _statBody.Text = body;
        _statTip.Visible = true;
        _statTip.ResetSize();

        Vector2 origin = owner.GlobalPosition - GlobalPosition;
        float x = Math.Clamp(origin.X, 8f, Math.Max(8f, Size.X - 320f));
        _statTip.Position = new Vector2(x, origin.Y + owner.Size.Y + 6f);
    }

    static StyleBoxFlat HudBox()
    {
        var style = new StyleBoxFlat { BgColor = new Color(0.08f, 0.07f, 0.06f, 0.78f) };
        style.SetCornerRadiusAll(5);
        style.ContentMarginLeft = 10;
        style.ContentMarginRight = 10;
        style.ContentMarginTop = 4;
        style.ContentMarginBottom = 4;
        return style;
    }

    void ApplyGameFont()
    {
        if (!IsInsideTree()) return;
        Label? donor = null;
        Control? bank = GetTree().Root.FindChild("BankNavigationButton", true, false) as Control;
        if (bank != null) donor = bank.FindChild("Title", true, false) as Label;
        Font? font = donor?.LabelSettings?.Font;
        if (font == null) return;

        var theme = new Theme();
        theme.SetFont("font", "Label", font);
        theme.SetFont("font", "Button", font);
        theme.SetFont("font", "CheckButton", font);
        Theme = theme;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!Visible) return;
        if (!@event.IsActionPressed("ui_cancel")) return;

        if (_popupHost.Visible)
        {
            CloseWindow();
            GetViewport().SetInputAsHandled();
        }
    }

    public void Open()
    {
        Visible = true;
        if (_state.Offers.Count == 0) MerchantStock.Roll(_state);
        ApplyGameFont();
        CloseWindow();
        Refresh();
        _view.Rebuild();
    }

    public void Close()
    {
        Visible = false;
        _statTip.Visible = false;
        _relicPanel.Visible = false;
        _buildingsPanel.Visible = false;
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
        _tierLabel.Text = $"Tier {_state.Tier}";
        _popLabel.Text = $"{_state.Population} / {_state.Capacity}";
        _workerLabel.Text = $"{_state.Workers}";
        _foodLabel.Text = $"{_state.Food:0}";
        _rateLabel.Text = (_state.FoodPerMinute >= 0 ? "+" : "") + $"{_state.FoodPerMinute:0} food/min";
        _rateLabel.Visible = _state.ShowRates;

        string signature = string.Join(",",
            _state.Owned.Where(p => p.Value > 0).Select(p => p.Key),
            _state.MerchantHere ? "merchant" : "",
            _state.Farms, _state.Groves, _state.Tier);

        if (signature != _signature)
        {
            _signature = signature;
            _view.Rebuild();
        }

        _moveButton.Text = _view.Editing ? "Done" : "Move";
        _hintLabel.Text = _view.Editing
            ? (_hoverName is { Length: > 0 } ? _hoverName + " - " : "") +
              "drag to move - gold handle or scroll to resize"
            : _state.MerchantHere
                ? "A travelling merchant is in town."
                : "Click a building to open it. The town grows as you build.";
    }

    void ToggleRelics()
    {
        if (_relicPanel.Visible)
        {
            _relicPanel.Visible = false;
            return;
        }

        _buildingsPanel.Visible = false;

        FillRelics();
        _relicPanel.Visible = true;
        _relicPanel.ResetSize();
        Vector2 origin = _relicButton.GlobalPosition - GlobalPosition;
        _relicPanel.Position = new Vector2(
            Math.Clamp(origin.X - 240f, 8f, Math.Max(8f, Size.X - 420f)),
            origin.Y + _relicButton.Size.Y + 6f);
    }

    void FillRelics()
    {
        Clear(_relicList);
        _relicList.AddChild(Ui.Label("Relics", 15, Ui.Accent));

        foreach (MerchantStock.TownItem relic in MerchantStock.TownItems)
        {
            bool owned = _state.HasPerk(relic.Id);
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 10);

            var text = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            text.AddThemeConstantOverride("separation", 1);
            text.AddChild(Ui.Label(relic.Name, 13, owned ? Ui.Good : Ui.Text));

            var desc = Ui.Label(relic.Description, 11, Ui.Muted);
            desc.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            desc.CustomMinimumSize = new Vector2(260, 0);
            text.AddChild(desc);
            row.AddChild(text);

            row.AddChild(Ui.Label(owned ? "Owned" : "-", 12, owned ? Ui.Good : Ui.Muted));
            _relicList.AddChild(row);
        }
    }

    void ToggleBuildings()
    {
        if (_buildingsPanel.Visible)
        {
            _buildingsPanel.Visible = false;
            return;
        }

        _relicPanel.Visible = false;
        FillBuildings();
        _buildingsPanel.Visible = true;
        _buildingsPanel.ResetSize();
        Vector2 origin = _buildingsButton.GlobalPosition - GlobalPosition;
        _buildingsPanel.Position = new Vector2(
            Math.Clamp(origin.X - 240f, 8f, Math.Max(8f, Size.X - 420f)),
            origin.Y + _buildingsButton.Size.Y + 6f);
    }

    void FillBuildings()
    {
        Clear(_buildingsList);
        _buildingsList.AddChild(Ui.Label("Buildings", 15, Ui.Accent));

        BuildingRow("Tavern", $"Tier {_state.Tier}", true);
        BuildingRow("Town Hall", $"Tier {_state.Tier}", true);
        BuildingRow("Cottage", _state.Has("cottage") ? "Tier 1" : "Not built", _state.Has("cottage"));
        BuildingRow("Timber House", _state.Has("timber") ? "Tier 1" : "Not built", _state.Has("timber"));
        BuildingRow("Manor", _state.Has("manor") ? "Tier 1" : "Not built", _state.Has("manor"));
        BuildingRow("Farm", $"{_state.Farms} plot(s)", true);
        BuildingRow("Tree Grove", $"{_state.Groves} plot(s)", true);
        BuildingRow("School", _state.HasSchool ? "Built" : "Not built", _state.HasSchool);
        BuildingRow("Hot Air Balloon", _state.HasBalloon ? "Built" : "Not built", _state.HasBalloon);
        BuildingRow("Travelling Merchant", _state.MerchantHere ? "In town" : "On the road", _state.MerchantHere);
    }

    void BuildingRow(string name, string value, bool active)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 12);

        var label = Ui.Label(name, 13, Ui.Text);
        label.CustomMinimumSize = new Vector2(160, 0);
        row.AddChild(label);
        row.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        row.AddChild(Ui.Label(value, 12, active ? Ui.Good : Ui.Muted));

        _buildingsList.AddChild(row);
    }

    void ToggleEdit()
    {
        MelvorModLoader.Log.Write("[MelvorTown] Move pressed, was " + _view.Editing);
        _view.SetEditing(!_view.Editing);
        _moveButton.Text = _view.Editing ? "Done" : "Move";
        Refresh();
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
            case "merchant": OpenMerchant(); break;
            case "trees1":
            case "trees2": OpenGrove(); break;
            case "townhall": OpenTownHall(); break;
            case "school": OpenSchool(); break;
            case "balloon": OpenBalloon(); break;
        }
    }

    void OpenHouse(string name, int capacity)
    {
        OpenWindow(name, body =>
        {
            body.AddChild(Ui.Label($"+{capacity} population capacity", 15));
            body.AddChild(Ui.Label($"Population: {_state.Population} / {_state.Capacity}", 14, Ui.Text));
            body.AddChild(Ui.Label("Houses raise the population the town can support. Food and schools decide how fast it fills.", 13, Ui.Muted));
        });
    }

    void OpenFarm()
    {
        OpenWindow("Farm", body =>
        {
            void Render()
            {
                Clear(body);
                body.AddChild(Ui.Label($"Farms: {_state.Farms}    Food: {_state.Food:0}", 15));
                body.AddChild(Ui.Label("Each farm grows carrots every 8 seconds. Chickens add a little food on their own.", 13, Ui.Muted));

                var harvest = Ui.Button(
                    _state.HarvestCooldown > 0
                        ? $"Harvest in {_state.HarvestCooldown:0}s"
                        : "Harvest now (+4 food per farm)",
                    () =>
                    {
                        _state.Food += _state.Farms * 4;
                        _state.HarvestCooldown = 20;
                        _state.Save();
                        Render();
                    });
                harvest.Disabled = _state.HarvestCooldown > 0;
                body.AddChild(harvest);
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
                    return;
                }

                body.AddChild(Ui.Label($"The cart leaves in {Math.Max(0, _state.MerchantLeft):0}s.", 14, Ui.Accent));
                body.AddChild(Ui.Label("Six items, limited stock. New stock every visit.", 13, Ui.Muted));

                if (_state.Offers.Count == 0) MerchantStock.Roll(_state);

                foreach (MerchantOffer offer in _state.Offers)
                {
                    var row = new HBoxContainer();
                    row.AddThemeConstantOverride("separation", 8);

                    row.AddChild(Ui.Icon(_art.GetValueOrDefault("tab"), 34));

                    var text = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
                    text.AddThemeConstantOverride("separation", 1);
                    text.AddChild(Ui.Label(offer.Name, 14));
                    if (offer.Description.Length > 0)
                        text.AddChild(Ui.Label(offer.Description, 12, Ui.Muted));
                    int left = offer.Quantity - offer.Bought;
                    text.AddChild(Ui.Label(left > 0 ? $"{left} left" : "Sold out", 11, left > 0 ? Ui.Muted : Ui.Bad));
                    row.AddChild(text);

                    long price = MerchantStock.PriceOf(_state, offer);
                    var buy = Ui.Button($"{price:N0} GP", () =>
                    {
                        MerchantStock.Buy(_state, offer, out _);
                        Render();
                    });
                    buy.Disabled = left <= 0 || MerchantStock.GoldAmount() < price;
                    buy.CustomMinimumSize = new Vector2(110, 0);
                    row.AddChild(buy);

                    body.AddChild(row);
                }
            }
            Render();
        });
    }

    void OpenTownHall()
    {
        OpenWindow("Town Hall", body =>
        {
            Clear(body);
            body.AddChild(Ui.Label($"Tier {_state.Tier} - {_state.TierName}", 18, Ui.Accent));
            body.AddChild(Ui.Label($"Population: {_state.Population} / {_state.Capacity}", 14));
            body.AddChild(Ui.Label(_state.TierRequirement, 13, Ui.Muted));
            body.AddChild(Ui.Label("Buildings", 14, Ui.Accent));

            foreach (UpgradeDef def in TownState.Upgrades)
            {
                if (!_state.Has(def.Id)) continue;
                body.AddChild(Ui.Label($"{def.Name} x{_state.Count(def.Id)}", 13, Ui.Text));
            }
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
                body.AddChild(Ui.Label($"Population grows {15 * (1 - 0.1 * _state.Lessons):0}% faster than an unschooled town.", 13, Ui.Muted));

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
                body.AddChild(Ui.Label($"Population {_state.Population} / {_state.Capacity}    Food {_state.Food:0}", 14, Ui.Muted));

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
        _relicPanel.Visible = false;
        _buildingsPanel.Visible = false;
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
