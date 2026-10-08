namespace BlueHeighliner.Comlink;

/// <summary>User control for composing a draft message, including an AvaloniaEdit body editor with fill-in support.</summary>
[ExcludeFromCodeCoverage]
internal partial class DraftEditor : UserControl
{
    private static IFillInViewModel? GetActiveFillIn(IDraftViewModel? vm)
        => vm?.FillIns.Values.FirstOrDefault(f => f.IsPopupOpen);

    /// <summary>
    /// Finds the longest phonetic word (see <see cref="PhoneticAlphabet"/>) ending exactly at
    /// <paramref name="caret"/>, if any — checked against the raw document text regardless of how it
    /// got there (typed via PLSO, pasted, etc.), per PLSO's whole-word backspace behavior.
    /// </summary>
    private static bool TryFindPhoneticWordBeforeCaret(TextDocument doc, int caret, out int wordLength)
    {
        foreach (int length in PhoneticAlphabet.Lengths)
        {
            if (caret - length < 0)
            {
                continue;
            }
            if (PhoneticAlphabet.IsWord(doc.GetText(caret - length, length)))
            {
                wordLength = length;
                return true;
            }
        }
        wordLength = 0;
        return false;
    }

    private static void OnUserInputTextInput(object? sender, TextInputEventArgs e)
    {
        if (e.Text is not null)
        {
            e.Text = e.Text.ToUpperInvariant();
        }
    }

    /// <summary>Initializes the control, loads the AXAML layout, and wires up input handlers.</summary>
    public DraftEditor()
    {
        InitializeComponent();

        UserInput.AddHandler(
            InputElement.TextInputEvent,
            OnUserInputTextInput,
            RoutingStrategies.Tunnel);
        UserInput.AddHandler(
            InputElement.KeyDownEvent,
            OnUserInputKeyDown,
            RoutingStrategies.Bubble,
            handledEventsToo: true);

        DataContextChanged += OnDataContextChanged;
        TagBox.TextChanged += OnTagTextChanged;
        BodyHost.SizeChanged += (_, _) => ApplyViewWidth();
        Toolbar.SizeChanged += (_, _) => ArrangeToolbar();
        Toolbar.LayoutUpdated += (_, _) => ArrangeToolbar();
        Aspects.SizeChanged += (_, _) => ArrangeToolbar();
        Actions.SizeChanged += (_, _) => ArrangeToolbar();
    }

    private void ArrangeToolbar()
    {
        double needed = Aspects.Children.Where(child => child.IsVisible).Sum(child => child.DesiredSize.Width) + Actions.Children.Where(child => child.IsVisible).Sum(child => child.DesiredSize.Width) + (Actions.Spacing * Actions.Children.Count(child => child.IsVisible)) + Actions.Margin.Left;
        bool beside = Toolbar.Bounds.Width <= 0 || Toolbar.Bounds.Width >= needed;
        Grid.SetRow(Actions, beside ? 0 : 1);
        Grid.SetColumn(Actions, beside ? 1 : 0);
        Grid.SetColumnSpan(Actions, beside ? 1 : 2);
        Actions.HorizontalAlignment = beside ? HorizontalAlignment.Left : HorizontalAlignment.Right;
    }

    private FillInElementGenerator? fillInGenerator;
    private System.ComponentModel.INotifyPropertyChanged? watchedViewModel;
    private System.Collections.ObjectModel.ObservableCollection<AddressData>? watchedAddresses;
    private double? chrome;

    // After a recipient is added the name box is ready for the next one.
    private void OnAddressesChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
        {
            Dispatcher.UIThread.Post(() => UserInput.Focus());
        }
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IDraftViewModel.LineWidth))
        {
            ApplyViewWidth();
        }
    }

    /// <summary>
    /// Shows the body, and the header above it, as wide as the draft's line width in monospace characters, so the text wraps there. It only changes how the text is shown: no line break
    /// is added to the text, so changing the width never changes the draft.
    /// </summary>
    private void ApplyViewWidth()
    {
        if (DataContext is not IDraftViewModel vm)
        {
            return;
        }

        if (vm.LineWidth is not { } width)
        {
            BodyEditor.ClearValue(WidthProperty);
            HeaderBorder.ClearValue(WidthProperty);
            BodyEditor.Options.ShowColumnRulers = false;
            BodyEditor.HorizontalAlignment = HorizontalAlignment.Stretch;
            return;
        }

        if (chrome is null)
        {
            if (BodyEditor.Bounds.Width <= 0 || BodyEditor.TextArea.TextView.Bounds.Width <= 0)
            {
                BodyEditor.LayoutUpdated += OnBodyLayoutUpdated;
                return;
            }

            chrome = BodyEditor.Bounds.Width - BodyEditor.TextArea.TextView.Bounds.Width;
        }

        FormattedText probe = new("0", System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface(BodyEditor.FontFamily), BodyEditor.FontSize, Brushes.White);
        double text = (width * probe.Width) + 0.5;
        BodyEditor.HorizontalAlignment = HorizontalAlignment.Left;
        // A line is never shown wider than the room there is, so the body never runs under the recipients; the cut-off is exact whenever it fits.
        double room = BodyHost.Bounds.Width > 0 ? BodyHost.Bounds.Width : double.PositiveInfinity;
        BodyEditor.Width = Math.Min(text + chrome.Value, room);
        // The box has a one pixel border on the right, which is not room for text.
        HeaderBorder.Width = Math.Min(text + 1, room);

        // The cut-off: a ruler at the last column of a line in the body, and the right edge of the header's box, which is as wide as a line.
        BodyEditor.Options.ColumnRulerPositions = [width];
        BodyEditor.Options.ShowColumnRulers = true;
        BodyEditor.TextArea.TextView.ColumnRulerPen = new Pen(new SolidColorBrush(Color.Parse("#4A4A4F")), 1, DashStyle.Dash);
    }

    /// <summary>Sizes the tag box to fit exactly as many monospace characters as a tag may have, and keeps more from being typed. Without a maximum it keeps its usual width.</summary>
    private void ApplyTagBoxWidth(IDraftViewModel vm)
    {
        if (vm.TagMaxLength is not { } max)
        {
            TagBox.MaxLength = 0;
            return;
        }

        FontFamily monoFont = new("avares://BlueHeighliner.Comlink/Assets/Fonts/DejaVuSansMono.ttf#DejaVu Sans Mono");
        FormattedText probe = new("0", System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface(monoFont), TagBox.FontSize, Brushes.White);
        TagBox.FontFamily = monoFont;
        TagBox.MaxLength = max;
        TagBox.Padding = new Thickness(6, 4);
        // The characters, the padding on both sides, and room for the caret after the last one.
        TagBox.Width = Math.Ceiling((max * probe.Width) + 12 + 2);
    }

    private void OnBodyLayoutUpdated(object? sender, EventArgs e)
    {
        BodyEditor.LayoutUpdated -= OnBodyLayoutUpdated;
        ApplyViewWidth();
    }

    private void OnTagTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (DataContext is not IDraftViewModel vm || TagBox.Text is not { } text)
        {
            return;
        }

        string allowed = vm.FilterTag(text);
        if (allowed == text)
        {
            return;
        }

        int caret = TagBox.CaretIndex;
        TagBox.Text = allowed;
        TagBox.CaretIndex = Math.Min(caret, allowed.Length);
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (watchedViewModel is not null)
        {
            watchedViewModel.PropertyChanged -= OnViewModelPropertyChanged;
            watchedViewModel = null;
        }

        if (watchedAddresses is not null)
        {
            watchedAddresses.CollectionChanged -= OnAddressesChanged;
            watchedAddresses = null;
        }

        if (fillInGenerator is not null)
        {
            BodyEditor.TextArea.TextView.ElementGenerators.Remove(fillInGenerator);
            BodyEditor.TextArea.RemoveHandler(InputElement.KeyDownEvent, OnBodyEditorKeyDown);
            BodyEditor.TextArea.RemoveHandler(InputElement.TextInputEvent, OnBodyEditorTextInput);
            fillInGenerator = null;
        }

        if (DataContext is not IDraftViewModel vm)
        {
            return;
        }

        // Set document explicitly - AXAML binding alone can miss timing edge cases. A draft built without the UI's
        // body document factory (a host registering its own) still opens, as a copy of its text, rather than crashing.
        BodyEditor.Document = vm.BodyDocument is TextDocumentBodyDocument textDocument
            ? textDocument.Document
            : new TextDocument(vm.BodyDocument.Text);

        watchedViewModel = vm as System.ComponentModel.INotifyPropertyChanged;
        if (watchedViewModel is not null)
        {
            watchedViewModel.PropertyChanged += OnViewModelPropertyChanged;
        }
        watchedAddresses = vm.Addresses;
        watchedAddresses.CollectionChanged += OnAddressesChanged;
        ApplyViewWidth();
        ApplyTagBoxWidth(vm);

        fillInGenerator = new FillInElementGenerator(vm.FillIns);
        BodyEditor.TextArea.TextView.ElementGenerators.Add(fillInGenerator);
        // Tunnel priority ensures our handlers fire before AvaloniaEdit's own input handling
        BodyEditor.TextArea.AddHandler(InputElement.KeyDownEvent, OnBodyEditorKeyDown, RoutingStrategies.Tunnel);
        BodyEditor.TextArea.AddHandler(InputElement.TextInputEvent, OnBodyEditorTextInput, RoutingStrategies.Tunnel);

        BodyEditor.Options.EnableEmailHyperlinks = false;
        BodyEditor.Options.EnableHyperlinks = false;

        // Apply dark theme colors and monospace font — AvaloniaEdit uses its own text run properties
        BodyEditor.Background = new SolidColorBrush(Color.Parse("#1E1E1E"));
        BodyEditor.Foreground = new SolidColorBrush(Color.Parse("#CCCCCC"));
        BodyEditor.TextArea.Foreground = new SolidColorBrush(Color.Parse("#CCCCCC"));
        FontFamily monoFont = new("avares://BlueHeighliner.Comlink/Assets/Fonts/DejaVuSansMono.ttf#DejaVu Sans Mono");
        BodyEditor.FontFamily = monoFont;
        BodyEditor.TextArea.FontFamily = monoFont;
    }

    private void OnBodyEditorTextInput(object? sender, TextInputEventArgs e)
    {
        IDraftViewModel? vm = DataContext as IDraftViewModel;

        IFillInViewModel? activeFillIn = GetActiveFillIn(vm);
        if (activeFillIn is not null)
        {
            if (e.Text is not null)
            {
                activeFillIn.NewOption += e.Text;
                e.Handled = true;
            }
            return;
        }

        // PLSO (Phonetic Language Spell Out): substitute a single typed letter or digit with its
        // phonetic word instead of inserting the character itself.
        if (vm is not { PlsoMode: not PlsoMode.Off } || e.Text is not { Length: 1 } text || !PhoneticAlphabet.TryGetWord(text[0], out string word))
        {
            return;
        }

        TextDocument doc = BodyEditor.Document;
        int caret = BodyEditor.CaretOffset;
        string insertion = vm.PlsoMode is PlsoMode.Spaces ? word + " " : word;
        doc.Insert(caret, insertion);
        BodyEditor.CaretOffset = caret + insertion.Length;
        e.Handled = true;
    }

    private void OnBodyEditorKeyDown(object? sender, KeyEventArgs e)
    {
        IDraftViewModel? vm = DataContext as IDraftViewModel;

        // When a fill-in popup is open, redirect keyboard input to it instead of the body editor.
        // The Popup is a separate X11 window and cannot receive X11 keyboard focus, so we
        // intercept here at tunnel priority and forward to the fill-in's NewOption property.
        IFillInViewModel? activeFillIn = GetActiveFillIn(vm);
        if (activeFillIn is not null)
        {
            if (e.Key is Key.Back)
            {
                if (activeFillIn.NewOption.Length > 0)
                {
                    activeFillIn.NewOption = activeFillIn.NewOption[..^1];
                }
                e.Handled = true;
                return;
            }
            if (e.Key is Key.Return or Key.Enter)
            {
                if (activeFillIn.AddOptionCommand.CanExecute(null))
                {
                    activeFillIn.AddOptionCommand.Execute(null);
                }
                e.Handled = true;
                return;
            }
            if (e.Key is Key.Escape)
            {
                activeFillIn.IsPopupOpen = false;
                e.Handled = true;
                return;
            }
            // Other keys (arrows, modifiers, etc.) pass through without modifying the body
            return;
        }

        TextDocument doc = BodyEditor.Document;
        int caret = BodyEditor.CaretOffset;

        // PLSO: backspacing when the text immediately to the left of the caret is a phonetic word
        // deletes the whole word at once, regardless of which word it is or how it got there.
        if (e.Key is Key.Back && vm is { PlsoMode: not PlsoMode.Off } &&
            TryFindPhoneticWordBeforeCaret(doc, caret, out int wordLength))
        {
            doc.Remove(caret - wordLength, wordLength);
            e.Handled = true;
            return;
        }

        // Delete key: if caret is at fill-in sentinel, delete the whole marker
        if (e.Key is Key.Delete && caret < doc.TextLength &&
            doc.GetCharAt(caret) is FillInElementGenerator.Sentinel)
        {
            doc.Remove(caret, FillInElementGenerator.MarkerLength);
            e.Handled = true;
            return;
        }

        // Backspace key: if caret is right after a fill-in marker, delete the whole marker
        if (e.Key is Key.Back && caret >= FillInElementGenerator.MarkerLength)
        {
            int markerStart = caret - FillInElementGenerator.MarkerLength;
            if (doc.GetCharAt(markerStart) is FillInElementGenerator.Sentinel)
            {
                doc.Remove(markerStart, FillInElementGenerator.MarkerLength);
                e.Handled = true;
            }
        }
    }

    private void OnUserInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is not Key.Return)
        {
            return;
        }
        if (DataContext is IDraftViewModel vm && vm.AddAddressCommand.CanExecute(null))
        {
            vm.AddAddressCommand.Execute(null);
        }
    }

    private void OnAddFillInClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not IDraftViewModel vm)
        {
            return;
        }
        int offset = BodyEditor.CaretOffset;
        vm.InsertFillIn(offset);
        BodyEditor.CaretOffset = offset + FillInElementGenerator.MarkerLength;
        BodyEditor.Focus();
    }

    private void OnPlsoButtonClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not IDraftViewModel vm)
        {
            return;
        }
        vm.PlsoMode = vm.PlsoMode switch
        {
            PlsoMode.Off => PlsoMode.On,
            PlsoMode.On => PlsoMode.Spaces,
            PlsoMode.Spaces => PlsoMode.Off,
            _ => PlsoMode.Off
        };
    }
}
