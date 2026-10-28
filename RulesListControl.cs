using System.Drawing.Drawing2D;

namespace EberoNest;

internal sealed class RulesListControl : Control
{
    private static readonly Color HoverSurface = Color.FromArgb(32, 36, 42);
    private static readonly Color SelectedSurface = Color.FromArgb(48, 52, 59);
    private static readonly Color SubtleDivider = Color.FromArgb(35, 38, 44);
    private static readonly Color MutedText = Color.FromArgb(166, 172, 182);
    private static readonly Color ChipSurface = Color.FromArgb(48, 52, 59);
    private static readonly Color ScrollTrack = Color.FromArgb(27, 29, 34);
    private static readonly Color ScrollThumb = Color.FromArgb(83, 89, 100);
    private const TextFormatFlags SingleLineText = TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding;

    private readonly List<RuleCategory> rules = [];
    private readonly List<RowLayout> rowLayouts = [];
    private readonly Font headingFont = new("Segoe UI", 9f, FontStyle.Regular, GraphicsUnit.Point);
    private readonly Font valueFont = new("Segoe UI", 9f, FontStyle.Regular, GraphicsUnit.Point);
    private readonly Font chipFont = new("Segoe UI", 8.25f, FontStyle.Regular, GraphicsUnit.Point);
    private int selectedIndex = -1;
    private int hoveredIndex = -1;
    private int scrollOffset;
    private int totalRowsHeight;
    private int headerHeight;
    private int rowBaseHeight;
    private int rowGap;
    private int chipHeight;
    private int chipGap;
    private int chipHorizontalPadding;
    private int contentInset;
    private int scrollbarWidth;
    private int scrollbarGutter;
    private int categoryColumnWidth;
    private int folderColumnWidth;
    private int scrollDragOffset;
    private bool draggingThumb;

    internal event EventHandler? ItemDoubleClick;

    internal RulesListControl()
    {
        BackColor = SettingsForm.Canvas;
        ForeColor = Color.FromArgb(236, 238, 242);
        Font = valueFont;
        AccessibleName = "Reglas de organización";
        AccessibleRole = AccessibleRole.List;
        TabStop = true;
        Cursor = Cursors.Hand;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        RecalculateMetrics();
        RebuildLayout();
    }

    internal int ItemCount => rules.Count;
    internal RuleCategory? SelectedRule => selectedIndex >= 0 && selectedIndex < rules.Count ? rules[selectedIndex] : null;
    internal int ChipCount => rowLayouts.Sum(row => row.Chips.Count);
    internal bool HasScrollableContent => totalRowsHeight > Math.Max(0, ClientSize.Height - headerHeight);
    internal int ScrollOffsetForSmoke => scrollOffset;

    internal void SetRules(IEnumerable<RuleCategory> items)
    {
        string? selectedName = SelectedRule?.Name;
        rules.Clear();
        rules.AddRange(items);
        selectedIndex = selectedName is null
            ? -1
            : rules.FindIndex(rule => rule.Name.Equals(selectedName, StringComparison.OrdinalIgnoreCase));
        if (selectedIndex < 0 && rules.Count > 0) selectedIndex = 0;
        hoveredIndex = -1;
        RebuildLayout();
        Invalidate();
        AccessibleDescription = $"{rules.Count} reglas";
    }

    internal void SelectIndex(int index)
    {
        if (index < 0 || index >= rules.Count) return;
        selectedIndex = index;
        EnsureSelectedVisible();
        Invalidate();
        OnSelectionChanged(EventArgs.Empty);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        RecalculateMetrics();
        RebuildLayout();
        Invalidate();
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        RebuildLayout();
        Invalidate();
    }

    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        RecalculateMetrics();
        RebuildLayout();
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Graphics graphics = e.Graphics;
        graphics.Clear(SettingsForm.Canvas);
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        int contentWidth = Math.Max(0, ClientSize.Width - scrollbarGutter);
        int categoryX = contentInset;
        int folderX = categoryX + categoryColumnWidth;
        int extensionX = folderX + folderColumnWidth;
        using (var headingBrush = new SolidBrush(MutedText))
        {
            TextRenderer.DrawText(graphics, "Categoría", headingFont,
                new Rectangle(categoryX, 0, Math.Max(0, categoryColumnWidth - Scale(12)), headerHeight), headingBrush.Color, SingleLineText);
            TextRenderer.DrawText(graphics, "Carpeta de destino", headingFont,
                new Rectangle(folderX, 0, Math.Max(0, folderColumnWidth - Scale(12)), headerHeight), headingBrush.Color, SingleLineText);
            TextRenderer.DrawText(graphics, "Extensiones", headingFont,
                new Rectangle(extensionX, 0, Math.Max(0, contentWidth - extensionX - contentInset), headerHeight), headingBrush.Color, SingleLineText);
        }

        using (var dividerPen = new Pen(SettingsForm.Divider))
            graphics.DrawLine(dividerPen, contentInset, headerHeight - 1, Math.Max(contentInset, contentWidth - contentInset), headerHeight - 1);

        Rectangle contentBounds = new(0, headerHeight, contentWidth, Math.Max(0, ClientSize.Height - headerHeight));
        GraphicsState state = graphics.Save();
        graphics.SetClip(contentBounds);
        using var selectedBrush = new SolidBrush(SelectedSurface);
        using var hoverBrush = new SolidBrush(HoverSurface);
        using var normalDividerPen = new Pen(SubtleDivider);
        using var markerBrush = new SolidBrush(SettingsForm.Accent);
        using var chipBrush = new SolidBrush(ChipSurface);
        using var textBrush = new SolidBrush(ForeColor);

        for (int index = 0; index < rowLayouts.Count; index++)
        {
            RowLayout row = rowLayouts[index];
            int top = headerHeight + row.Top - scrollOffset;
            Rectangle rowBounds = new(contentInset, top, Math.Max(0, contentWidth - contentInset * 2), row.Height - rowGap);
            if (rowBounds.Bottom < headerHeight || rowBounds.Top > ClientSize.Height) continue;

            if (index == selectedIndex)
            {
                graphics.FillRectangle(selectedBrush, rowBounds);
                graphics.FillRectangle(markerBrush, new Rectangle(rowBounds.Left, rowBounds.Top + Scale(2), Scale(3), Math.Max(0, rowBounds.Height - Scale(4))));
            }
            else if (index == hoveredIndex)
            {
                graphics.FillRectangle(hoverBrush, rowBounds);
            }

            int textTop = top + Math.Max(0, (row.Height - valueFont.Height) / 2);
            TextRenderer.DrawText(graphics, row.Rule.Name, valueFont,
                new Rectangle(categoryX + Scale(10), textTop, Math.Max(0, categoryColumnWidth - Scale(22)), valueFont.Height + Scale(2)), textBrush.Color, SingleLineText);
            TextRenderer.DrawText(graphics, row.Rule.FolderName, valueFont,
                new Rectangle(folderX + Scale(10), textTop, Math.Max(0, folderColumnWidth - Scale(22)), valueFont.Height + Scale(2)), textBrush.Color, SingleLineText);

            foreach (ChipLayout chip in row.Chips)
            {
                Rectangle chipBounds = chip.Bounds;
                chipBounds.Offset(0, top);
                using GraphicsPath path = RoundedRectangle(chipBounds, Scale(6));
                graphics.FillPath(chipBrush, path);
                TextRenderer.DrawText(graphics, chip.Text, chipFont, chipBounds, textBrush.Color,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
            }

            graphics.DrawLine(normalDividerPen, rowBounds.Left, rowBounds.Bottom, rowBounds.Right, rowBounds.Bottom);
        }

        graphics.Restore(state);
        DrawScrollbar(graphics, contentWidth);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (draggingThumb)
        {
            MoveThumb(e.Y);
            return;
        }

        int nextHover = IndexAt(e.Location);
        if (nextHover != hoveredIndex)
        {
            hoveredIndex = nextHover;
            Invalidate();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (hoveredIndex != -1)
        {
            hoveredIndex = -1;
            Invalidate();
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        Focus();
        Rectangle thumb = GetThumbBounds(ClientSize.Width - scrollbarGutter);
        if (HasScrollableContent && thumb.Contains(e.Location))
        {
            draggingThumb = true;
            scrollDragOffset = e.Y - thumb.Top;
            Capture = true;
            return;
        }

        if (HasScrollableContent && e.X >= ClientSize.Width - scrollbarGutter)
        {
            int direction = e.Y < thumb.Top ? -1 : 1;
            ScrollBy(direction * Math.Max(Scale(48), contentViewportHeight));
            return;
        }

        int index = IndexAt(e.Location);
        if (index >= 0) SelectIndex(index);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (!draggingThumb) return;
        draggingThumb = false;
        Capture = false;
    }

    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        base.OnMouseDoubleClick(e);
        int index = IndexAt(e.Location);
        if (e.Button == MouseButtons.Left && index >= 0)
        {
            SelectIndex(index);
            ItemDoubleClick?.Invoke(this, EventArgs.Empty);
        }
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        int lines = Math.Max(1, SystemInformation.MouseWheelScrollLines);
        ScrollBy(-Math.Sign(e.Delta) * Scale(36) * lines);
    }

    protected override bool IsInputKey(Keys keyData) => keyData is Keys.Up or Keys.Down or Keys.Home or Keys.End or Keys.PageUp or Keys.PageDown || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (rules.Count == 0) return;
        int next = e.KeyCode switch
        {
            Keys.Up => Math.Max(0, selectedIndex - 1),
            Keys.Down => Math.Min(rules.Count - 1, selectedIndex + 1),
            Keys.Home => 0,
            Keys.End => rules.Count - 1,
            Keys.PageUp => FindIndexAtOffset(Math.Max(0, scrollOffset - contentViewportHeight)),
            Keys.PageDown => FindIndexAtOffset(Math.Min(totalRowsHeight, scrollOffset + contentViewportHeight)),
            _ => -1
        };
        if (next >= 0)
        {
            SelectIndex(next);
            e.Handled = true;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            headingFont.Dispose();
            valueFont.Dispose();
            chipFont.Dispose();
        }
        base.Dispose(disposing);
    }

    private int contentViewportHeight => Math.Max(0, ClientSize.Height - headerHeight);

    private void RecalculateMetrics()
    {
        headerHeight = Scale(36);
        rowBaseHeight = Scale(42);
        rowGap = Scale(2);
        chipHeight = Scale(21);
        chipGap = Scale(4);
        chipHorizontalPadding = Scale(14);
        contentInset = Scale(10);
        scrollbarWidth = Scale(6);
        scrollbarGutter = HasScrollableContent ? Scale(12) : 0;
    }

    private void RebuildLayout()
    {
        rowLayouts.Clear();
        if (rules.Count == 0 || ClientSize.Width <= 0)
        {
            totalRowsHeight = 0;
            scrollbarGutter = 0;
            scrollOffset = 0;
            return;
        }

        BuildRowsWithWidth(ClientSize.Width);
        bool needsScrollbar = totalRowsHeight > contentViewportHeight;
        scrollbarGutter = needsScrollbar ? Scale(12) : 0;
        if (needsScrollbar) BuildRowsWithWidth(ClientSize.Width);
        scrollOffset = Math.Clamp(scrollOffset, 0, MaximumScrollOffset);
    }

    private void BuildRowsWithWidth(int clientWidth)
    {
        rowLayouts.Clear();
        int contentWidth = Math.Max(Scale(220), clientWidth - scrollbarGutter);
        int innerWidth = Math.Max(0, contentWidth - contentInset * 2);
        categoryColumnWidth = (int)Math.Round(innerWidth * 0.22);
        folderColumnWidth = (int)Math.Round(innerWidth * 0.25);
        int extensionStart = contentInset + categoryColumnWidth + folderColumnWidth;
        int extensionWidth = Math.Max(0, contentWidth - contentInset - extensionStart);
        totalRowsHeight = 0;

        foreach (RuleCategory rule in rules)
        {
            List<ChipLayout> chips = [];
            int x = extensionStart + Scale(8);
            int line = 0;
            int lineHeight = chipHeight;
            int maxChipWidth = Math.Max(Scale(28), contentWidth - contentInset - x - Scale(6));
            foreach (string extension in rule.Extensions.Order(StringComparer.OrdinalIgnoreCase))
            {
                int chipWidth = Math.Max(Scale(30), TextRenderer.MeasureText(extension, chipFont, Size.Empty, TextFormatFlags.NoPadding).Width + chipHorizontalPadding);
                chipWidth = Math.Min(chipWidth, maxChipWidth);
                if (x + chipWidth > extensionStart + extensionWidth - Scale(4) && x > extensionStart + Scale(8))
                {
                    line++;
                    x = extensionStart + Scale(8);
                }
                chips.Add(new ChipLayout(extension, new Rectangle(x, Scale(6) + line * (chipHeight + chipGap), chipWidth, chipHeight)));
                x += chipWidth + chipGap;
            }

            int chipLines = chips.Count == 0 ? 0 : chips.Max(chip => (chip.Bounds.Y - Scale(6)) / (chipHeight + chipGap)) + 1;
            int chipBlockHeight = chipLines == 0 ? 0 : Scale(12) + chipLines * chipHeight + (chipLines - 1) * chipGap;
            int height = Math.Max(rowBaseHeight, chipBlockHeight);
            rowLayouts.Add(new RowLayout(rule, totalRowsHeight, height, chips));
            totalRowsHeight += height + rowGap;
        }
    }

    private void DrawScrollbar(Graphics graphics, int contentWidth)
    {
        if (!HasScrollableContent) return;
        Rectangle track = new(contentWidth + Scale(2), headerHeight + Scale(3), scrollbarWidth, Math.Max(Scale(12), contentViewportHeight - Scale(6)));
        Rectangle thumb = GetThumbBounds(contentWidth);
        using var trackBrush = new SolidBrush(ScrollTrack);
        using var thumbBrush = new SolidBrush(ScrollThumb);
        using GraphicsPath trackPath = RoundedRectangle(track, scrollbarWidth / 2);
        using GraphicsPath thumbPath = RoundedRectangle(thumb, scrollbarWidth / 2);
        graphics.FillPath(trackBrush, trackPath);
        graphics.FillPath(thumbBrush, thumbPath);
    }

    private Rectangle GetThumbBounds(int contentWidth)
    {
        Rectangle track = new(contentWidth + Scale(2), headerHeight + Scale(3), scrollbarWidth, Math.Max(Scale(12), contentViewportHeight - Scale(6)));
        int thumbHeight = Math.Max(Scale(24), (int)Math.Round((double)track.Height * contentViewportHeight / Math.Max(contentViewportHeight, totalRowsHeight)));
        int travel = Math.Max(0, track.Height - thumbHeight);
        int top = track.Top + (MaximumScrollOffset == 0 ? 0 : (int)Math.Round((double)scrollOffset / MaximumScrollOffset * travel));
        return new Rectangle(track.Left, top, track.Width, thumbHeight);
    }

    private void MoveThumb(int mouseY)
    {
        Rectangle track = new(ClientSize.Width - scrollbarGutter + Scale(2), headerHeight + Scale(3), scrollbarWidth, Math.Max(Scale(12), contentViewportHeight - Scale(6)));
        Rectangle thumb = GetThumbBounds(ClientSize.Width - scrollbarGutter);
        int thumbHeight = thumb.Height;
        int travel = Math.Max(1, track.Height - thumbHeight);
        int position = Math.Clamp(mouseY - scrollDragOffset - track.Top, 0, travel);
        scrollOffset = (int)Math.Round((double)position / travel * MaximumScrollOffset);
        Invalidate();
    }

    private void ScrollBy(int amount)
    {
        int next = Math.Clamp(scrollOffset + amount, 0, MaximumScrollOffset);
        if (next == scrollOffset) return;
        scrollOffset = next;
        hoveredIndex = -1;
        Invalidate();
    }

    private void EnsureSelectedVisible()
    {
        if (selectedIndex < 0 || selectedIndex >= rowLayouts.Count) return;
        RowLayout selected = rowLayouts[selectedIndex];
        if (selected.Top < scrollOffset) scrollOffset = selected.Top;
        else if (selected.Top + selected.Height > scrollOffset + contentViewportHeight)
            scrollOffset = Math.Min(MaximumScrollOffset, selected.Top + selected.Height - contentViewportHeight);
    }

    private int IndexAt(Point point)
    {
        if (point.Y < headerHeight || point.X >= ClientSize.Width - scrollbarGutter) return -1;
        int contentY = point.Y - headerHeight + scrollOffset;
        for (int index = 0; index < rowLayouts.Count; index++)
        {
            RowLayout row = rowLayouts[index];
            if (contentY >= row.Top && contentY < row.Top + row.Height - rowGap) return index;
        }
        return -1;
    }

    private int FindIndexAtOffset(int offset)
    {
        for (int index = 0; index < rowLayouts.Count; index++)
            if (offset < rowLayouts[index].Top + rowLayouts[index].Height) return index;
        return Math.Max(0, rowLayouts.Count - 1);
    }

    private int MaximumScrollOffset => Math.Max(0, totalRowsHeight - contentViewportHeight);
    private int Scale(int logicalPixels) => Math.Max(1, (int)Math.Round(logicalPixels * Math.Max(0.75, DeviceDpi / 96d)));

    private static GraphicsPath RoundedRectangle(Rectangle rectangle, int radius)
    {
        var path = new GraphicsPath();
        int diameter = Math.Max(2, radius * 2);
        path.AddArc(rectangle.X, rectangle.Y, diameter, diameter, 180, 90);
        path.AddArc(rectangle.Right - diameter, rectangle.Y, diameter, diameter, 270, 90);
        path.AddArc(rectangle.Right - diameter, rectangle.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rectangle.X, rectangle.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    private void OnSelectionChanged(EventArgs e) => AccessibilityNotifyClients(AccessibleEvents.Selection, selectedIndex);

    private sealed record RowLayout(RuleCategory Rule, int Top, int Height, List<ChipLayout> Chips);
    private sealed record ChipLayout(string Text, Rectangle Bounds);
}
