using System;
using System.Collections.Generic;
using GamesByMalcs.Melvor2.Components.ArtisanAction;
using GamesByMalcs.Melvor2.Components.Milestones;
using GamesByMalcs.Melvor2.Entities.Smithing;
using Godot;
using MelvorModLoader;

namespace PassiveSmelting;

/// <summary>
/// Adds a "Passive Smelting" entry below the Smithing milestone categories. Clicking it shows
/// the current tier and the level table; clicking it again or choosing a category returns.
/// </summary>
internal sealed class PassiveSmeltingSection
{
    sealed class ViewRefs
    {
        public Button Button = null!;
        public ScrollContainer List = null!;
        public ScrollContainer Table = null!;
        public Label Current = null!;
        public Label Selected = null!;
    }

    readonly ModContext _ctx;
    readonly List<MilestonesView> _views = new();
    readonly Dictionary<MilestonesView, ViewRefs> _panels = new();
    bool _accessorBroken;

    public PassiveSmeltingSection(ModContext ctx) => _ctx = ctx;

    public void Track(Node node)
    {
        if (node is MilestonesView view) _views.Add(view);
    }

    public void Update(SmithingEntity smithing, ActiveArtisanRecipe? selected, int bars)
    {
        int level = smithing.Experience.Level;
        string recipe = selected != null && PassiveSmeltingMod.IsSmelting(selected.Recipe)
            ? selected.Recipe.DisplayName.Text
            : "none selected";

        for (int i = _views.Count - 1; i >= 0; i--)
        {
            MilestonesView view = _views[i];
            if (!GodotObject.IsInstanceValid(view))
            {
                _views.RemoveAt(i);
                _panels.Remove(view);
                continue;
            }

            if (!IsSmithing(view, smithing))
            {
                if (_panels.TryGetValue(view, out ViewRefs? hidden))
                {
                    hidden.Button.Visible = false;
                    hidden.Button.SetPressedNoSignal(false);
                    hidden.Table.Visible = false;
                    hidden.List.Visible = true;
                }
                continue;
            }

            if (!_panels.TryGetValue(view, out ViewRefs? refs))
            {
                refs = Build(view, smithing);
                if (refs == null) continue;
                _panels.Add(view, refs);
            }

            refs.Button.Visible = true;
            refs.Button.Text = $"Passive Smelting - {bars} bar{(bars == 1 ? "" : "s")}";
            refs.Current.Text = $"Current: {Tiers.Describe(bars)} at Smithing {level}";
            refs.Selected.Text = $"Smelting: {recipe}";
        }
    }

    public void ShowList()
    {
        foreach (ViewRefs refs in _panels.Values)
        {
            if (!GodotObject.IsInstanceValid(refs.List)) continue;
            refs.Table.Visible = false;
            refs.List.Visible = true;
            refs.Button.SetPressedNoSignal(false);
        }
    }

    bool IsSmithing(MilestonesView view, SmithingEntity smithing)
    {
        if (!_accessorBroken)
        {
            try { return ReferenceEquals(Accessors.MilestonesOf(view), smithing.Milestones); }
            catch { _accessorBroken = true; }
        }

        Label? skill = FindByName<Label>(view, "SkillName");
        return skill != null &&
               (skill.Text == smithing.DisplayName.Text || skill.Text == "Smithing");
    }

    ViewRefs? Build(MilestonesView view, SmithingEntity smithing)
    {
        ScrollContainer? list = FindByName<ScrollContainer>(view, "MilestonesScrollContainer");
        if (list?.GetParent() is not Node rightHost)
        {
            _ctx.Log("Could not find the milestones list; the Passive Smelting section was not added.");
            return null;
        }

        VBoxContainer? leftColumn = null;
        VBoxContainer? categories = FindByName<VBoxContainer>(view, "CategoryContainer");
        if (categories?.GetParent() is ScrollContainer categoryScroll &&
            categoryScroll.GetParent() is VBoxContainer leftVBox)
        {
            leftColumn = leftVBox;
        }

        try
        {
            var column = new VBoxContainer { Name = "PassiveSmeltingColumn" };
            column.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            column.SizeFlagsVertical = Control.SizeFlags.ExpandFill;

            rightHost.RemoveChild(list);
            rightHost.AddChild(column);

            list.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            list.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            column.AddChild(list);

            ScrollContainer table = BuildTable(smithing, out Label current, out Label selected);
            table.Visible = false;
            column.AddChild(table);

            var button = new Button
            {
                Name = "PassiveSmeltingButton",
                Text = "Passive Smelting",
                ToggleMode = true,
                Alignment = HorizontalAlignment.Left,
                ExpandIcon = true,
                CustomMinimumSize = new Vector2(0, 50),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            button.AddThemeFontSizeOverride("font_size", 14);
            if (smithing.Icon != null) button.Icon = smithing.Icon;

            var refs = new ViewRefs
            {
                Button = button,
                List = list,
                Table = table,
                Current = current,
                Selected = selected
            };

            button.Toggled += pressed =>
            {
                if (pressed)
                {
                    refs.List.Visible = false;
                    refs.Table.Visible = true;
                }
                else
                {
                    ShowList();
                }
            };

            if (leftColumn != null) leftColumn.AddChild(button);
            else _ctx.Log("Could not find the milestone category column; the Passive Smelting entry was not added.");

            return refs;
        }
        catch (Exception e)
        {
            _ctx.Log($"Could not add the Passive Smelting section: {e.Message}");
            return null;
        }
    }

    static ScrollContainer BuildTable(SmithingEntity smithing, out Label current, out Label selected)
    {
        var scroll = new ScrollContainer
        {
            Name = "PassiveSmeltingTable",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };

        var column = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(column);

        var title = new Label { Text = "Passive Smelting" };
        title.AddThemeFontSizeOverride("font_size", 20);
        title.AddThemeColorOverride("font_color", new Color(0.93f, 0.75f, 0.35f));
        column.AddChild(title);

        var explain = new Label
        {
            Text = "The smelting recipe selected on the Smithing page keeps running while you train another skill, including offline. Each tier shortens the smelting interval: 2x from level 51 and 3x from level 86.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        explain.AddThemeFontSizeOverride("font_size", 12);
        explain.AddThemeColorOverride("font_color", new Color(0.70f, 0.70f, 0.70f));
        column.AddChild(explain);

        current = new Label();
        current.AddThemeFontSizeOverride("font_size", 15);
        column.AddChild(current);

        selected = new Label();
        selected.AddThemeFontSizeOverride("font_size", 13);
        column.AddChild(selected);

        column.AddChild(new HSeparator());

        var grid = new GridContainer { Columns = 2 };
        grid.AddThemeConstantOverride("h_separation", 28);
        grid.AddThemeConstantOverride("v_separation", 3);
        AddCell(grid, "Levels", true);
        AddCell(grid, "Bars at a time", true);
        AddCell(grid, "1-50", false);
        AddCell(grid, "1", false);
        AddCell(grid, "51-85", false);
        AddCell(grid, "2", false);
        AddCell(grid, "86-99", false);
        AddCell(grid, "3", false);
        column.AddChild(grid);

        var note = new Label
        {
            Text = "Only one smelting recipe runs at a time; the game gives Smithing a single action. Change it on the Smithing page.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        note.AddThemeFontSizeOverride("font_size", 12);
        note.AddThemeColorOverride("font_color", new Color(0.70f, 0.70f, 0.70f));
        column.AddChild(note);

        return scroll;
    }

    static void AddCell(GridContainer grid, string text, bool header)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", header ? 14 : 13);
        if (header) label.AddThemeColorOverride("font_color", new Color(0.93f, 0.75f, 0.35f));
        grid.AddChild(label);
    }

    static T? FindByName<T>(Node root, string name) where T : Node
    {
        if (root is T match && root.Name.ToString() == name) return match;
        foreach (Node child in root.GetChildren())
        {
            T? found = FindByName<T>(child, name);
            if (found != null) return found;
        }
        return null;
    }
}
