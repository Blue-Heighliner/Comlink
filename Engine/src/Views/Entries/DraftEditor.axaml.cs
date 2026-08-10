namespace BlueHeighliner.Comlink.Engine.Views.Entries;

/// <summary>User control for composing a draft message, including an AvaloniaEdit body editor with fill-in support.</summary>
[ExcludeFromCodeCoverage]
public partial class DraftEditor : UserControl
{
    private FillInElementGenerator? _fillInGenerator;

    /// <summary>Initializes the control, loads the AXAML layout, and wires up input handlers.</summary>
    public DraftEditor()
    {
        InitializeComponent();

        SiteInput.AddHandler(
            InputElement.TextInputEvent,
            OnSiteInputTextInput,
            RoutingStrategies.Tunnel);
        SiteInput.AddHandler(
            InputElement.KeyDownEvent,
            OnSiteInputKeyDown,
            RoutingStrategies.Bubble,
            handledEventsToo: true);

        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_fillInGenerator is not null)
        {
            BodyEditor.TextArea.TextView.ElementGenerators.Remove(_fillInGenerator);
            BodyEditor.TextArea.RemoveHandler(InputElement.KeyDownEvent, OnBodyEditorKeyDown);
            BodyEditor.TextArea.RemoveHandler(InputElement.TextInputEvent, OnBodyEditorTextInput);
            _fillInGenerator = null;
        }

        if (DataContext is not IDraftViewModel vm) return;

        // Set document explicitly — AXAML binding alone can miss timing edge cases
        BodyEditor.Document = ((TextDocumentBodyDocument)vm.BodyDocument).Document;

        _fillInGenerator = new FillInElementGenerator(vm.FillIns);
        BodyEditor.TextArea.TextView.ElementGenerators.Add(_fillInGenerator);
        // Tunnel priority ensures our handlers fire before AvaloniaEdit's own input handling
        BodyEditor.TextArea.AddHandler(InputElement.KeyDownEvent, OnBodyEditorKeyDown, RoutingStrategies.Tunnel);
        BodyEditor.TextArea.AddHandler(InputElement.TextInputEvent, OnBodyEditorTextInput, RoutingStrategies.Tunnel);

        BodyEditor.Options.EnableEmailHyperlinks = false;
        BodyEditor.Options.EnableHyperlinks = false;

        // Apply dark theme colors and monospace font — AvaloniaEdit uses its own text run properties
        BodyEditor.Background = new SolidColorBrush(Color.Parse("#1E1E1E"));
        BodyEditor.Foreground = new SolidColorBrush(Color.Parse("#CCCCCC"));
        BodyEditor.TextArea.Foreground = new SolidColorBrush(Color.Parse("#CCCCCC"));
        FontFamily monoFont = new("avares://BlueHeighliner.Comlink.Engine/Assets/Fonts/DejaVuSansMono.ttf#DejaVu Sans Mono");
        BodyEditor.FontFamily = monoFont;
        BodyEditor.TextArea.FontFamily = monoFont;
    }

    private static IFillInViewModel? GetActiveFillIn(IDraftViewModel? vm) =>
        vm?.FillIns.Values.FirstOrDefault(f => f.IsPopupOpen);

    private void OnBodyEditorTextInput(object? sender, TextInputEventArgs e)
    {
        IFillInViewModel? activeFillIn = GetActiveFillIn(DataContext as IDraftViewModel);
        if (activeFillIn is null || e.Text is null) return;
        activeFillIn.NewOption += e.Text;
        e.Handled = true;
    }

    private void OnBodyEditorKeyDown(object? sender, KeyEventArgs e)
    {
        // When a fill-in popup is open, redirect keyboard input to it instead of the body editor.
        // The Popup is a separate X11 window and cannot receive X11 keyboard focus, so we
        // intercept here at tunnel priority and forward to the fill-in's NewOption property.
        IFillInViewModel? activeFillIn = GetActiveFillIn(DataContext as IDraftViewModel);
        if (activeFillIn is not null)
        {
            if (e.Key == Key.Back)
            {
                if (activeFillIn.NewOption.Length > 0)
                    activeFillIn.NewOption = activeFillIn.NewOption[..^1];
                e.Handled = true;
                return;
            }
            if (e.Key is Key.Return or Key.Enter)
            {
                if (activeFillIn.AddOptionCommand.CanExecute(null))
                    activeFillIn.AddOptionCommand.Execute(null);
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Escape)
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

        // Delete key: if caret is at fill-in sentinel, delete the whole marker
        if (e.Key == Key.Delete && caret < doc.TextLength &&
            doc.GetCharAt(caret) == FillInElementGenerator.Sentinel)
        {
            doc.Remove(caret, FillInElementGenerator.MarkerLength);
            e.Handled = true;
            return;
        }

        // Backspace key: if caret is right after a fill-in marker, delete the whole marker
        if (e.Key == Key.Back && caret >= FillInElementGenerator.MarkerLength)
        {
            int markerStart = caret - FillInElementGenerator.MarkerLength;
            if (doc.GetCharAt(markerStart) == FillInElementGenerator.Sentinel)
            {
                doc.Remove(markerStart, FillInElementGenerator.MarkerLength);
                e.Handled = true;
            }
        }
    }

    private static void OnSiteInputTextInput(object? sender, TextInputEventArgs e)
    {
        if (e.Text is not null)
            e.Text = e.Text.ToUpperInvariant();
    }

    private void OnSiteInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Return) return;
        if (DataContext is IDraftViewModel vm && vm.AddAddressCommand.CanExecute(null))
            vm.AddAddressCommand.Execute(null);
    }

    private void OnAddFillInClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not IDraftViewModel vm) return;
        int offset = BodyEditor.CaretOffset;
        vm.InsertFillIn(offset);
        BodyEditor.CaretOffset = offset + FillInElementGenerator.MarkerLength;
        BodyEditor.Focus();
    }
}
