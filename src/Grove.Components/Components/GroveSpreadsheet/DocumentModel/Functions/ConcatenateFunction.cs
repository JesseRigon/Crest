#nullable enable

namespace Grove.Components.Primitives.Documents.Spreadsheet;

// CONCATENATE is the legacy name for CONCAT; shares the same implementation.
class ConcatenateFunction : ConcatFunction
{
    public override string Name => "CONCATENATE";
}
