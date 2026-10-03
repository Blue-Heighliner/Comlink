namespace BlueHeighliner.Comlink.Tests;

/// <summary>Priority levels the handler tests send with, lowest first.</summary>
public enum TestPriority
{
    /// <summary>The level of an ordinary message.</summary>
    Normal,

    /// <summary>The level retrieval requests are sent with.</summary>
    Retrieval,

    /// <summary>The level receipts are sent with.</summary>
    Receipt
}
