using Grove.Components.Primitives.Documents.Spreadsheet;

namespace Grove.Components.Primitives.Spreadsheet;

/// <summary>
/// Interface for commands that are subject to sheet protection.
/// </summary>
public interface IProtectedCommand
{
    /// <summary>
    /// Gets the sheet action this command requires.
    /// </summary>
    SheetAction RequiredAction { get; }
}
