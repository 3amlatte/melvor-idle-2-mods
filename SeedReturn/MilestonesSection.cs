using System;
using System.Collections.Generic;
using GamesByMalcs.Melvor2.Components.Milestones;
using GamesByMalcs.Melvor2.Entities.Farming;
using Godot;
using MelvorModLoader;

namespace SeedReturn;

/// <summary>
/// Adds a "Seed Return Lite" entry below the milestone categories (after The Mill). Clicking it
/// replaces the right-hand milestone list with the full chance table; choosing any real
/// category or "View All Milestones" brings the list back. The entry's label shows the
/// player's current chance, so the value is visible without opening the table.
/// </summary>
internal sealed class MilestonesSection
{
    sealed class ViewRefs
    {
        public Button Button = null!;
        public ScrollContainer List = null!;
        public ScrollContainer Table = null!;
        public Label Current = null!;
        public Label Next = null!;
    }

    readonly ModContext _ctx;
    readonly List<MilestonesView> _views = new();
    readonly Dictionary<MilestonesView, ViewRefs> _panels = new();
    bool _accessorBroken;

    public MilestonesSection(ModContext ctx) => _ctx = ctx;

    public void Track(Node node)
    {
        if (node is MilestonesView view) _views.Add(view);
    }

    public void Update(FarmingEntity farming)
    {
        int level = farming.Experience.Level;
        int percent = Chance.Percent(level);
        int next = Chance.NextLevel(level);

        for (int i = _views.Count - 1; i >= 0; i--)
        {
            MilestonesView view = _views[i];
            if (!GodotObject.IsInstanceValid(view))
            {
                _views.RemoveAt(i);
                _panels.Remove(view);
                continue;
            }

            if (!IsFarming(view, farming))
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
                refs = Build(view, farming);
                if (refs == null) continue;
                _panels.Add(view, refs);
            }

            refs.Button.Visible = true;
            refs.Button.Text = $"Seed Return Lite - {percent}%";
            refs.Current.Text = $"Your chance: {percent}% at Farming {level}";
            refs.Next.Text = next > 0
                ? $"Next: {Chance.Percent(next)}% at Farming {next}"
                : "Maximum chance reached.";
        }
    }

    /// <summary>Back to the milestone list, used when a real category is selected.</summary>
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

    bool IsFarming(MilestonesView view, FarmingEntity farming)
    {
        if (!_accessorBroken)
        {
            try { return ReferenceEquals(Accessors.MilestonesOf(view), farming.Milestones); }
            catch { _accessorBroken = true; }
        }

        // Fallback if the private field ever changes: use the window's title.
        Label? skill = FindByName<Label>(view, "SkillName");
        return skill != null &&
               (skill.Text == farming.DisplayName.Text || skill.Text == "Farming");
    }

    ViewRefs? Build(MilestonesView view, FarmingEntity farming)
    {
        ScrollContainer? list = FindByName<ScrollContainer>(view, "MilestonesScrollContainer");
        if (list?.GetParent() is not Node rightHost)
        {
            _ctx.Log("Could not find the milestones list; the Seed Return Lite section was not added.");
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
            // Right side: keep the milestone list and add a hidden table view as its sibling.
            var column = new VBoxContainer { Name = "SeedReturnColumn" };
            column.AddThemeConstantOverride("separation", 0);
            column.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            column.SizeFlagsVertical = Control.SizeFlags.ExpandFill;

            rightHost.RemoveChild(list);
            rightHost.AddChild(column);

            list.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            list.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            column.AddChild(list);

            ScrollContainer table = BuildTable(farming, out Label current, out Label next);
            table.Visible = false;
            column.AddChild(table);

            // Left side: a category-like entry below the last category.
            var button = new Button
            {
                Name = "SeedReturnButton",
                Text = "Seed Return Lite",
                ToggleMode = true,
                Alignment = HorizontalAlignment.Left,
                ExpandIcon = true,
                CustomMinimumSize = new Vector2(0, 50),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            button.AddThemeFontSizeOverride("font_size", 14);
            if (farming.Icon != null) button.Icon = farming.Icon;

            var refs = new ViewRefs
            {
                Button = button,
                List = list,
                Table = table,
                Current = current,
                Next = next
            };

            button.Toggled += pressed =>
            {
                if (pressed) ShowTable(refs);
                else ShowList();
            };

            if (leftColumn != null) leftColumn.AddChild(button);
            else _ctx.Log("Could not find the milestone category column; the Seed Return Lite entry was not added.");

            return refs;
        }
        catch (Exception e)
        {
            _ctx.Log($"Could not add the Seed Return Lite section: {e.Message}");
            return null;
        }
    }

    static void ShowTable(ViewRefs refs)
    {
        refs.List.Visible = false;
        refs.Table.Visible = true;
    }

    ScrollContainer BuildTable(FarmingEntity farming, out Label current, out Label next)
    {
        var scroll = new ScrollContainer
        {
            Name = "SeedReturnTable",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };

        var column = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(column);

        var title = new Label { Text = "Seed Return Lite" };
        title.AddThemeFontSizeOverride("font_size", 20);
        title.AddThemeColorOverride("font_color", new Color(0.93f, 0.75f, 0.35f));
        column.AddChild(title);

        current = new Label();
        current.AddThemeFontSizeOverride("font_size", 15);
        column.AddChild(current);

        next = new Label();
        next.AddThemeFontSizeOverride("font_size", 13);
        column.AddChild(next);

        var explain = new Label
        {
            Text = "Chance scales with Farming level: 1% at level 1, 20% at level 99.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        explain.AddThemeFontSizeOverride("font_size", 12);
        explain.AddThemeColorOverride("font_color", new Color(0.70f, 0.70f, 0.70f));
        column.AddChild(explain);

        column.AddChild(new HSeparator());

        var grid = new GridContainer { Columns = 2 };
        grid.AddThemeConstantOverride("h_separation", 28);
        grid.AddThemeConstantOverride("v_separation", 3);
        AddCell(grid, "Levels", true);
        AddCell(grid, "Chance", true);
        foreach (var (from, to, percent) in Chance.Bands())
        {
            AddCell(grid, from == to ? $"Level {from}" : $"Levels {from}-{to}", false);
            AddCell(grid, $"{percent}%", false);
        }
        column.AddChild(grid);

        var seeds = new Label
        {
            Text = "Seeds per harvest: allotments 3, herbs 2, trees/flowers/orchard 1, mill farm 2, compost 0.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        seeds.AddThemeFontSizeOverride("font_size", 12);
        seeds.AddThemeColorOverride("font_color", new Color(0.70f, 0.70f, 0.70f));
        column.AddChild(seeds);

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
