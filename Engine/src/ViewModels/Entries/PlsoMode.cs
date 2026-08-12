namespace BlueHeighliner.Comlink.Engine.ViewModels.Entries;

/// <summary>The three states of PLSO (Phonetic Language Spell Out) mode in the draft body editor.</summary>
public enum PlsoMode
{
    /// <summary>Typed characters are inserted as-is.</summary>
    Off,

    /// <summary>Typed letters and digits are replaced with their phonetic word, with no separator between words.</summary>
    On,

    /// <summary>Same as <see cref="On"/>, but a space is inserted after each phonetic word.</summary>
    Spaces
}
