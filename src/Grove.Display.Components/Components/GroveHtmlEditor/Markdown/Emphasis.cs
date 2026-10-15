using System;

namespace Grove.Display.Components.Primitives.Documents.Markdown;

/// <summary>
/// Represents an emphasis element in a markdown document: <c>_emphasis_</c> or <c>*emphasis*</c>.
/// </summary>
public class Emphasis : InlineContainer
{
    /// <inheritdoc />
    public override void Accept(INodeVisitor visitor)
    {
        ArgumentNullException.ThrowIfNull(visitor);
        visitor.VisitEmphasis(this);
    }
}