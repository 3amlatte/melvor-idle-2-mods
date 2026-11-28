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

    const float BaseHeight = 30f;
    const int BaseFontSize = 11;
    const int MaxCells = 4;
    const float SingleRightMargin = 590f;
    const float MultiRightMargin = 300f;

    sealed class Cell
    {
        public required VBoxContainer Root;
        public required HBoxContainer Row;
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

    Font? _font;
    int _fontSize = BaseFontSize;
    int _donorSize = BaseFontSize;
    Color _fontColor = Text;
    float _height = BaseHeight;

    float Scale => _fontSize / (float)BaseFontSize;

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

        ctx.Tree.Root.AddChild(_layer);
    }

    public string LastPlacement { get; private set; } = "";

    public void Show(Control? footer, IReadOnlyList<Entry> entries, string? donorText = null)
    {
        CopyFooterStyle(footer, donorText);
        Vector2 viewport = _panel.GetViewportRect().Size;
        if (viewport.X < 200 || entries.Count == 0) { Hide(); return; }

        Rect2 footerRect = footer != null && GodotObject.IsInstanceValid(footer)
            ? footer.GetGlobalRect()
            : new Rect2();

        float x, y, width;
        float desiredWidth = (entries.Count <= 1
            ? 300f
            : Math.Min(520f, 220f + 150f * entries.Count)) * 0.767f * Scale;

        if (footerRect.Size.X >= 200)
        {
            width = desiredWidth;
            y = footerRect.End.Y - _height + 4f;

            // Sit just after the item-rate section (the rates end at the orange separator),
            // so growing numbers push the cell right instead of overlapping them.
            Control? divider = footer != null && GodotObject.IsInstanceValid(footer)
                ? FindOrangeDivider(footer)
                : null;
            Control? rate = footer != null && GodotObject.IsInstanceValid(footer)
                ? FindRightmostRate(footer)
                : null;

            if (divider != null)
            {
                x = divider.GetGlobalRect().End.X + 14f;
                // Never let the cell slide back over the orange divider; trim instead.
                width = Math.Min(width, Math.Max(120f, viewport.X - x - 8f));
            }
            else if (rate != null)
            {
                Rect2 rr = rate.GetGlobalRect();
                x = rr.End.X + 48f;
                x = (float)(Math.Ceiling(x / 10.0) * 10.0);
                // Keep it out of the far-right music/New buttons.
                x = Math.Min(x, footerRect.End.X - width - 70f);
            }
            else
            {
                Control? depleted = footer != null && GodotObject.IsInstanceValid(footer)
                    ? FindDepletionLabel(footer)
                    : null;
                if (depleted != null)
                {
                    x = depleted.GetGlobalRect().End.X + 48f;
                }
                else
                {
                    x = footerRect.End.X - width - (entries.Count <= 1 ? 420f : MultiRightMargin);
                }
            }
        }
        else
        {
            width = Math.Min(desiredWidth, viewport.X - 32f);
            x = (viewport.X - width) / 2f;
            y = viewport.Y - _height - 150f;
        }

        x = Math.Clamp(x, 8f, Math.Max(8f, viewport.X - width - 8f));
        y = Math.Clamp(y, 8f, Math.Max(8f, viewport.Y - _height - 8f));

        EnsureCells(entries.Count);

        for (int i = 0; i < _cells.Count; i++)
        {
            bool used = i < entries.Count;
            _cells[i].Root.Visible = used;
            if (i > 0) _separators[i - 1].Visible = used;
            if (used)
            {
                Cell cell = _cells[i];
                cell.Set(entries[i]);
                StyleLabel(cell.Task);
                StyleLabel(cell.Eta);
                cell.Task.CustomMinimumSize = new Vector2(130f * Scale, 0);
                cell.Eta.CustomMinimumSize = new Vector2(55f * Scale, 0);
                float iconSize = Math.Clamp(16f * Scale, 16f, 22f);
                cell.Icon.CustomMinimumSize = new Vector2(iconSize, iconSize);
                cell.Progress.CustomMinimumSize = new Vector2(0, Math.Clamp(3f * Scale, 3f, 5f));
                cell.Row.AddThemeConstantOverride("separation", Mathf.RoundToInt(5f * Scale));
            }
        }

        _panel.Position = new Vector2(x, y);
        _panel.Size = new Vector2(width, _height);

        LastPlacement = $"footer {footerRect.Position:0}/{footerRect.Size:0} -> panel {x:0},{y:0} {width:0}x{_height:0}, {entries.Count} task(s), font {_fontSize} (donor {_donorSize})";

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
                SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin,
                CustomMinimumSize = new Vector2(130, 0),
                TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
                VerticalAlignment = VerticalAlignment.Center,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            StyleLabel(task);
            row.AddChild(task);

            var eta = new Label
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin,
                CustomMinimumSize = new Vector2(55, 0),
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            StyleLabel(eta);
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
                Row = row,
                Icon = icon,
                Task = task,
                Eta = eta,
                Progress = progress
            });
        }
    }

    void CopyFooterStyle(Control? footer, string? donorText)
    {
        Label? donor = footer != null && GodotObject.IsInstanceValid(footer)
            ? FindDonorLabel(footer, donorText)
            : null;

        if (donor == null)
        {
            _font = null;
            _fontSize = BaseFontSize;
            _donorSize = BaseFontSize;
            _fontColor = Text;
            _height = BaseHeight;
            return;
        }

        _font = donor.GetThemeFont("font");
        int size = donor.GetThemeFontSize("font_size");
        if (size <= 0) size = BaseFontSize;
        _donorSize = size;

        // Match the footer text, but keep the cell inside the space next to the divider.
        _fontSize = Math.Clamp(size, BaseFontSize, 15);
        _fontColor = donor.GetThemeColor("font_color");
        _height = Math.Max(BaseHeight, _fontSize * 1.2f + 10f);
    }

    void StyleLabel(Label label)
    {
        if (_font != null) label.AddThemeFontOverride("font", _font);
        label.AddThemeFontSizeOverride("font_size", _fontSize);
        label.AddThemeColorOverride("font_color", _fontColor);
    }

    static Label? FindDonorLabel(Node node, string? text)
    {
        Label? named = null;
        Label? exact = null;
        Label? widest = null;
        float widestWidth = 0;

        void Walk(Node root)
        {
            foreach (Node child in root.GetChildren())
            {
                if (child is Label label && label.IsVisibleInTree() && label.Text.Length > 2)
                {
                    string name = label.Name.ToString();
                    string value = label.Text.Trim();

                    if (name.Contains("CurrentAction", StringComparison.OrdinalIgnoreCase))
                        named ??= label;

                    if (text != null && value.Equals(text, StringComparison.OrdinalIgnoreCase))
                        exact ??= label;

                    bool numeric = value.All(c => char.IsDigit(c) || char.IsPunctuation(c) || char.IsWhiteSpace(c));
                    bool rate = value.Contains("/hr", StringComparison.OrdinalIgnoreCase) ||
                                value.Contains("Show all", StringComparison.OrdinalIgnoreCase);
                    if (!numeric && !rate && label.Size.X > widestWidth)
                    {
                        widestWidth = label.Size.X;
                        widest = label;
                    }
                }

                Walk(child);
            }
        }

        Walk(node);
        return named ?? exact ?? widest;
    }

    static Control? FindOrangeDivider(Node node)
    {
        Control? best = null;
        float bestX = float.MinValue;

        void Walk(Node root)
        {
            foreach (Node child in root.GetChildren())
            {
                if (child is Control control && control.IsVisibleInTree() &&
                    control.Size.X > 0 && control.Size.X <= 8 && control.Size.Y >= 18)
                {
                    // Any narrow vertical divider; the orange one is the rightmost in the
                    // footer, and may be a texture rather than a coloured ColorRect.
                    float x = control.GetGlobalRect().Position.X;
                    if (x > bestX) { bestX = x; best = control; }
                }
                Walk(child);
            }
        }

        Walk(node);
        return best;
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
