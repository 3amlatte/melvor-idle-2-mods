using System;
using System.Collections.Generic;
using Godot;
using MelvorModLoader;

namespace MasteryEta;

/// <summary>
/// Compact mastery readout that sits inside the game's action footer, to the right of the
/// item-depletion timer. One cell is shown per active skill that has a matching mastery task.
/// </summary>
internal sealed class MasteryBar
{
    public sealed record Entry(Texture2D? ItemIcon, string Text, double Current, double Max, string Eta, Action? OnClick = null);

    static readonly Color Accent = new(0.929f, 0.749f, 0.349f);
    static readonly Color Text = new(0.92f, 0.92f, 0.94f);
    static readonly Color Track = new(0.18f, 0.19f, 0.24f);

    const float Height = 30f;
    const int MaxCells = 4;
    const float SingleRightMargin = 590f;
    const float MultiRightMargin = 300f;

    sealed class Cell
    {
        public required VBoxContainer Root;
        public required TextureRect Icon;
        public required Label Task;
        public required Label Eta;
        public required ProgressBar Progress;
        public Action? OnClick;

        public void Set(Entry entry)
        {
            OnClick = entry.OnClick;
            Icon.Texture = entry.ItemIcon;
            Task.Text = entry.Text;
            Eta.Text = entry.Eta;
            Progress.Value = entry.Max > 0 ? Math.Clamp(entry.Current / entry.Max, 0, 1) : 0;
            Root.Visible = true;
        }
    }

    readonly CanvasLayer _layer;
    readonly PanelContainer _panel;
    readonly HBoxContainer _content;
    readonly List<Cell> _cells = new();
    readonly List<Control> _separators = new();

    public MasteryBar(ModContext ctx)
    {
        _layer = new CanvasLayer { Layer = 200, Visible = false };

        _panel = new PanelContainer
        {
            MouseFilter = Control.MouseFilterEnum.Pass
        };
        _panel.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
        _layer.AddChild(_panel);

        _content = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        _content.AddThemeConstantOverride("separation", 8);
        _panel.AddChild(_content);

        var masteryIcon = new TextureRect
        {
            CustomMinimumSize = new Vector2(16, 16),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        try { masteryIcon.Texture = GD.Load<Texture2D>("res://Assets/Icons/Mastery_Icon.png"); }
        catch { /* texture missing */ }
        _content.AddChild(masteryIcon);

        var title = new Label
        {
            Text = "Mastery",
            VerticalAlignment = VerticalAlignment.Center,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        title.AddThemeFontSizeOverride("font_size", 11);
        title.AddThemeColorOverride("font_color", Accent);
        _content.AddChild(title);

        ctx.Tree.Root.AddChild(_layer);
    }

    public string LastPlacement { get; private set; } = "";

    public void Show(Control? footer, IReadOnlyList<Entry> entries)
    {
        Vector2 viewport = _panel.GetViewportRect().Size;
        if (viewport.X < 200 || entries.Count == 0) { Hide(); return; }

        Rect2 footerRect = footer != null && GodotObject.IsInstanceValid(footer)
            ? footer.GetGlobalRect()
            : new Rect2();

        float x, y, width;
        float desiredWidth = entries.Count <= 1
            ? 300f
            : Math.Min(520f, 220f + 150f * entries.Count);

        if (footerRect.Size.X >= 200)
        {
            width = desiredWidth;
            y = footerRect.End.Y - Height - 4f;

            // Sit just after the item-rate section (the rates end at the orange separator),
            // so growing numbers push the cell right instead of overlapping them.
            Control? rate = footer != null && GodotObject.IsInstanceValid(footer)
                ? FindRightmostRate(footer)
                : null;

            if (rate != null)
            {
                Rect2 rr = rate.GetGlobalRect();
                x = rr.End.X + 48f;
                x = (float)(Math.Ceiling(x / 10.0) * 10.0);
                // Keep it out of the far-right music/New buttons.
                x = Math.Min(x, footerRect.End.X - width - 70f);
            }
            else
            {
                x = footerRect.End.X - width - (entries.Count <= 1 ? SingleRightMargin : MultiRightMargin);
            }
        }
        else
        {
            width = Math.Min(desiredWidth, viewport.X - 32f);
            x = (viewport.X - width) / 2f;
            y = viewport.Y - Height - 150f;
        }

        x = Math.Clamp(x, 8f, Math.Max(8f, viewport.X - width - 8f));
        y = Math.Clamp(y, 8f, Math.Max(8f, viewport.Y - Height - 8f));

        EnsureCells(entries.Count);

        for (int i = 0; i < _cells.Count; i++)
        {
            bool used = i < entries.Count;
            _cells[i].Root.Visible = used;
            if (i > 0) _separators[i - 1].Visible = used;
            if (used) _cells[i].Set(entries[i]);
        }

        _panel.Position = new Vector2(x, y);
        _panel.Size = new Vector2(width, Height);

        LastPlacement = $"footer {footerRect.Position:0}/{footerRect.Size:0} -> panel {x:0},{y:0} {width:0}x{Height:0}, {entries.Count} task(s)";

        _layer.Visible = true;
    }

    public void Hide()
    {
        _layer.Visible = false;
    }

    void EnsureCells(int count)
    {
        for (int i = _cells.Count; i < Math.Min(count, MaxCells); i++)
        {
            if (i > 0)
            {
                var separator = new ColorRect
                {
                    Color = new Color(0.55f, 0.58f, 0.66f, 0.28f),
                    CustomMinimumSize = new Vector2(1, 18),
                    SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
                    MouseFilter = Control.MouseFilterEnum.Ignore
                };
                _content.AddChild(separator);
                _separators.Add(separator);
            }

            var root = new VBoxContainer
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                MouseFilter = Control.MouseFilterEnum.Stop,
                MouseDefaultCursorShape = Control.CursorShape.PointingHand
            };
            root.GuiInput += ev =>
            {
                if (ev is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
                {
                    _cells.FirstOrDefault(c => ReferenceEquals(c.Root, root))?.OnClick?.Invoke();
                }
            };
            root.AddThemeConstantOverride("separation", 2);
            _content.AddChild(root);

            var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            row.AddThemeConstantOverride("separation", 5);
            root.AddChild(row);

            var icon = new TextureRect
            {
                CustomMinimumSize = new Vector2(16, 16),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            row.AddChild(icon);

            var task = new Label
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
                VerticalAlignment = VerticalAlignment.Center,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            task.AddThemeFontSizeOverride("font_size", 11);
            task.AddThemeColorOverride("font_color", Text);
            row.AddChild(task);

            var eta = new Label
            {
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                CustomMinimumSize = new Vector2(68, 0),
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            eta.AddThemeFontSizeOverride("font_size", 11);
            eta.AddThemeColorOverride("font_color", Accent);
            row.AddChild(eta);

            var progress = new ProgressBar
            {
                MinValue = 0,
                MaxValue = 1,
                Value = 0,
                ShowPercentage = false,
                Step = 0.001,
                CustomMinimumSize = new Vector2(0, 3),
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            var trackStyle = new StyleBoxFlat { BgColor = Track };
            trackStyle.SetCornerRadiusAll(1);
            var fillStyle = new StyleBoxFlat { BgColor = Accent };
            fillStyle.SetCornerRadiusAll(1);
            progress.AddThemeStyleboxOverride("background", trackStyle);
            progress.AddThemeStyleboxOverride("fill", fillStyle);
            root.AddChild(progress);

            _cells.Add(new Cell
            {
                Root = root,
                Icon = icon,
                Task = task,
                Eta = eta,
                Progress = progress
            });
        }
    }

    static Control? FindRightmostRate(Node node)
    {
        Control? best = null;
        float bestX = float.MinValue;

        void Walk(Node root)
        {
            foreach (Node child in root.GetChildren())
            {
                if (child is Label label &&
                    label.IsVisibleInTree() &&
                    (label.Text.Contains("/hr", StringComparison.OrdinalIgnoreCase) ||
                     label.Text.Contains("Show all", StringComparison.OrdinalIgnoreCase)) &&
                    label.Size.X > 10)
                {
                    float x = label.GetGlobalRect().End.X;
                    if (x > bestX) { bestX = x; best = label; }
                }
                Walk(child);
            }
        }

        Walk(node);
        return best;
    }

    static Control? FindDepletionLabel(Node node)
    {
        foreach (Node child in node.GetChildren())
        {
            if (child is Label label && label.Text.Contains("depleted", StringComparison.OrdinalIgnoreCase) &&
                label.IsVisibleInTree() && label.Size.X > 10)
            {
                return label;
            }
            if (child is RichTextLabel rich && rich.Text.Contains("depleted", StringComparison.OrdinalIgnoreCase) &&
                rich.IsVisibleInTree() && rich.Size.X > 10)
            {
                return rich;
            }

            Control? found = FindDepletionLabel(child);
            if (found != null) return found;
        }
        return null;
    }
}
