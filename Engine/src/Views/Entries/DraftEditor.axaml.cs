namespace BlueHeighliner.Comlink.Engine.Views.Entries;

/// <summary>User control for composing a draft message, including an AvaloniaEdit body editor with fill-in support.</summary>
[ExcludeFromCodeCoverage]
public partial class DraftEditor : UserControl
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
            if (caret - length < 0) { continue; }
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
    }

    private FillInElementGenerator? fillInGenerator;

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (fillInGenerator is not null)
        {
            BodyEditor.TextArea.TextView.ElementGenerators.Remove(fillInGenerator);
            BodyEditor.TextArea.RemoveHandler(InputElement.KeyDownEvent, OnBodyEditorKeyDown);
            BodyEditor.TextArea.RemoveHandler(InputElement.TextInputEvent, OnBodyEditorTextInput);
            fillInGenerator = null;
        }

        if (DataContext is not IDraftViewModel vm) { return; }

        // Set document explicitly — AXAML binding alone can miss timing edge cases
        BodyEditor.Document = ((TextDocumentBodyDocument)vm.BodyDocument).Document;

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
        FontFamily monoFont = new("avares://BlueHeighliner.Comlink.Engine/Assets/Fonts/DejaVuSansMono.ttf#DejaVu Sans Mono");
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
        string insertion = vm.PlsoMode == PlsoMode.Spaces ? word + " " : word;
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
            if (e.Key == Key.Back)
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

        // PLSO: backspacing when the text immediately to the left of the caret is a phonetic word
        // deletes the whole word at once, regardless of which word it is or how it got there.
        if (e.Key == Key.Back && vm is { PlsoMode: not PlsoMode.Off } &&
            TryFindPhoneticWordBeforeCaret(doc, caret, out int wordLength))
        {
            doc.Remove(caret - wordLength, wordLength);
            e.Handled = true;
            return;
        }

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

    private void OnUserInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Return) { return; }
        if (DataContext is IDraftViewModel vm && vm.AddAddressCommand.CanExecute(null))
        {
            vm.AddAddressCommand.Execute(null);
        }
    }

    private void OnAddFillInClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not IDraftViewModel vm) { return; }
        int offset = BodyEditor.CaretOffset;
        vm.InsertFillIn(offset);
        BodyEditor.CaretOffset = offset + FillInElementGenerator.MarkerLength;
        BodyEditor.Focus();
    }

    private void OnPlsoButtonClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not IDraftViewModel vm) { return; }
        vm.PlsoMode = vm.PlsoMode switch
        {
            PlsoMode.Off => PlsoMode.On,
            PlsoMode.On => PlsoMode.Spaces,
            PlsoMode.Spaces => PlsoMode.Off,
            _ => PlsoMode.Off
        };
    }
}
