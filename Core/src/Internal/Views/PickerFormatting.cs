namespace BlueHeighliner.Comlink.Views;

/// <summary>Makes Avalonia's date and time pickers follow the application's date and time conventions where their own properties and styles cannot reach.</summary>
internal interface IPickerFormatting
{
    /// <summary>Registers the process-wide hooks; call once after the Avalonia application is set up.</summary>
    void Apply();
}

/// <summary>
/// Pads single-digit hours to two digits, centers every cell of the picker flyouts, and gives the current culture uppercase
/// month abbreviations, so pickers show <c>SEP</c>, <c>05</c> and <c>09</c> like every other date and time in the application.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed class PickerFormatting : IPickerFormatting
{
    /// <inheritdoc />
    public void Apply()
    {
        CultureInfo culture = (CultureInfo)CultureInfo.CurrentCulture.Clone();
        culture.DateTimeFormat.AbbreviatedMonthNames = [.. culture.DateTimeFormat.AbbreviatedMonthNames.Select(name => name.ToUpperInvariant())];
        culture.DateTimeFormat.AbbreviatedMonthGenitiveNames = [.. culture.DateTimeFormat.AbbreviatedMonthGenitiveNames.Select(name => name.ToUpperInvariant())];
        CultureInfo.CurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;

        Avalonia.Controls.Control.LoadedEvent.AddClassHandler<DatePickerPresenter>((presenter, _) => Format(presenter));
        Avalonia.Controls.Control.LoadedEvent.AddClassHandler<TimePickerPresenter>((presenter, _) => Format(presenter));

        Avalonia.Controls.Control.LoadedEvent.AddClassHandler<DatePicker>((picker, _) => CenterWhenBuilt(picker));
        Avalonia.Controls.Control.LoadedEvent.AddClassHandler<TimePicker>((picker, _) => CenterWhenBuilt(picker));

        // The picker sets these strings itself and offers no format for the hour, so pad them as they change.
        ContentControl.ContentProperty.Changed.AddClassHandler<ListBoxItem>((item, e) =>
        {
            if (e.NewValue is string { Length: 1 } digit && char.IsDigit(digit[0]) && IsHourItem(item))
            {
                item.Content = "0" + digit;
            }
        });

        TextBlock.TextProperty.Changed.AddClassHandler<TextBlock>((block, e) =>
        {
            if (block.Name == "PART_HourTextBlock" && e.NewValue is string { Length: 1 } digit && char.IsDigit(digit[0]))
            {
                block.SetCurrentValue(TextBlock.TextProperty, "0" + digit);
            }
            else if (block.Name is "PART_MonthTextBlock" or "PART_DayTextBlock" or "PART_YearTextBlock" or "PART_HourTextBlock" or "PART_MinuteTextBlock" && e.NewValue is string { Length: > 0 } text && text.Any(char.IsLower))
            {
                block.SetCurrentValue(TextBlock.TextProperty, text.ToUpperInvariant());
            }
        });
    }

    private void CenterWhenBuilt(Avalonia.Controls.Control picker)
    {
        void Try(object? sender, EventArgs e)
        {
            List<TextBlock> blocks = [.. picker.GetVisualDescendants().OfType<TextBlock>()];
            if (blocks.Count == 0) { return; }

            picker.LayoutUpdated -= Try;
            foreach (TextBlock block in blocks)
            {
                double vertical = (block.Padding.Top + block.Padding.Bottom) / 2;
                block.Padding = new Thickness(block.Padding.Left, vertical, block.Padding.Right, vertical);
            }
        }

        picker.LayoutUpdated += Try;
    }

    private bool IsHourItem(ListBoxItem item) => item.FindAncestorOfType<DateTimePickerPanel>() is { Name: "PART_HourSelector" };

    private void Format(Avalonia.Controls.Control presenter)
    {
        foreach (ListBoxItem item in presenter.GetVisualDescendants().OfType<ListBoxItem>())
        {
            item.HorizontalContentAlignment = HorizontalAlignment.Center;
            item.VerticalContentAlignment = VerticalAlignment.Center;

            if (item.Content is string { Length: 1 } digit && char.IsDigit(digit[0]) && IsHourItem(item))
            {
                item.Content = "0" + digit;
            }
        }
    }
}
