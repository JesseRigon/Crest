using System.Text.Json.Nodes;

namespace Grove.Display.Management.Components;

/// <summary>
/// Conditions over a content item's fields (docs/content-engine.md › Validation and conditions run
/// on the one expression engine): a <see cref="DisplayBinding"/> predicate whose sources are fields
/// (<see cref="DisplayBinding.Field"/>, <c>FieldSet.Field</c>) and literals, evaluated by
/// <see cref="DisplayPredicates"/>. A field's visibility and a type's field rules are such
/// conditions; the editor evaluates them as it re-renders and the save evaluates the same ones.
/// </summary>
public static class FieldConditions
{
    /// <summary>
    /// Whether <paramref name="condition"/> holds. <paramref name="readField"/> answers a field's
    /// comparison value (<see cref="Value"/> of its comparison values).
    /// </summary>
    public static bool IsMet(DisplayBinding condition, Func<string, JsonNode?> readField) =>
        DisplayPredicates.IsTrue(DisplayPredicates.Read(condition, source => source.Field is { } field ? readField(field) : source.Value?.DeepClone()));

    /// <summary>The field paths a condition reads, its operands' and terms' included.</summary>
    public static IEnumerable<string> Fields(DisplayBinding binding)
    {
        if (binding.Field is { } field)
        {
            yield return field;
        }

        if (binding.Operand is { } operand)
        {
            foreach (var path in Fields(operand))
            {
                yield return path;
            }
        }

        foreach (var term in binding.Terms ?? [])
        {
            foreach (var path in Fields(term))
            {
                yield return path;
            }
        }
    }

    /// <summary>
    /// The condition with each relative field path (a bare <c>Field</c>, as a visibility condition on
    /// a shared field set names its siblings) qualified by <paramref name="fieldSetName"/>, the
    /// attachment the condition is evaluated under.
    /// </summary>
    public static DisplayBinding Qualify(DisplayBinding condition, string fieldSetName) =>
        Rewrite(condition, binding => binding.Field is { } field && !field.Contains('.') ? With(binding, $"{fieldSetName}.{field}", binding.Operand) : binding);

    /// <summary>
    /// The condition with the text literals compared with an <paramref name="ignoreCase"/> field
    /// lower-cased: such a field's comparison values (option keys) are lower-cased, so they compare
    /// case-insensitively.
    /// </summary>
    public static DisplayBinding IgnoreCase(DisplayBinding condition, Func<string, bool> ignoreCase) =>
        Rewrite(condition, binding => binding is { Field: { } field, Operand: { IsLiteral: true, Value: JsonValue value } } && ignoreCase(field) && value.TryGetValue<string>(out var text)
            ? With(binding, field, DisplayBinding.Literal(JsonValue.Create(text.ToLowerInvariant())))
            : binding);

    private static DisplayBinding Rewrite(DisplayBinding binding, Func<DisplayBinding, DisplayBinding> map) =>
        binding.Terms is { } terms
            ? new DisplayBinding { Combinator = binding.Combinator, Terms = [.. terms.Select(term => Rewrite(term, map))] }
            : map(binding.Operand is { } operand ? With(binding, binding.Field, Rewrite(operand, map)) : binding);

    private static DisplayBinding With(DisplayBinding binding, string? field, DisplayBinding? operand) => new()
    {
        Field = field,
        Value = binding.Value?.DeepClone(),
        Path = binding.Path,
        Operator = binding.Operator,
        Operand = operand,
        Combinator = binding.Combinator,
        Terms = binding.Terms,
    };

    /// <summary>
    /// The values a field stores at <paramref name="path"/> (<c>FieldSet.Field</c>) in an item's
    /// content: a reference field's <c>SelectedIds</c>, a scalar field's <c>Text</c> or
    /// <c>Value</c>, or a value stored bare.
    /// </summary>
    public static IReadOnlyList<string> StoredValues(JsonNode? content, string path)
    {
        var node = content;
        foreach (var segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            node = node is JsonObject members ? members[segment] : null;
        }

        if (node is null)
        {
            return [];
        }

        if (node is JsonValue)
        {
            var text = node.ToString();
            return string.IsNullOrWhiteSpace(text) ? [] : [text];
        }

        if (node is not JsonObject value)
        {
            return [];
        }

        if (value["SelectedIds"] is JsonArray selected)
        {
            return [.. selected.Select(entry => entry?.ToString()).Where(entry => !string.IsNullOrWhiteSpace(entry)).Select(entry => entry!)];
        }

        var scalar = (value["Text"] ?? value["Value"])?.ToString();
        return string.IsNullOrWhiteSpace(scalar) ? [] : [scalar];
    }

    /// <summary>A field's comparison values as the value a condition compares: none is empty, one is that value, several are a list.</summary>
    public static JsonNode? Value(IReadOnlyList<string> values) => values.Count switch
    {
        0 => null,
        1 => JsonValue.Create(values[0]),
        _ => new JsonArray([.. values.Select(value => (JsonNode?)JsonValue.Create(value))]),
    };

    /// <summary>
    /// What is wrong with <paramref name="condition"/>: an operator or combinator outside the
    /// grammar, a comparison whose left side is not a field, a missing right side, a field
    /// <paramref name="fieldExists"/> does not know. Empty when the condition is sound.
    /// </summary>
    public static IReadOnlyList<string> Check(DisplayBinding condition, Func<string, bool> fieldExists)
    {
        var problems = new List<string>();
        Check(condition, fieldExists, problems);
        return problems;
    }

    private static void Check(DisplayBinding binding, Func<string, bool> fieldExists, List<string> problems)
    {
        if (binding.Node is not null || binding.Variable is not null || binding.Event)
        {
            problems.Add("A field condition reads fields and literals only.");
            return;
        }

        if (binding.Combinator is { } combinator)
        {
            var terms = binding.Terms ?? [];
            if (!DisplayPredicates.Combinators.Contains(combinator, StringComparer.Ordinal))
            {
                problems.Add($"'{combinator}' is not one of {string.Join(", ", DisplayPredicates.Combinators)}.");
            }
            else if (terms.Count == 0 || (combinator == DisplayPredicates.Not && terms.Count != 1))
            {
                problems.Add(combinator == DisplayPredicates.Not ? "'not' takes one term." : $"'{combinator}' needs its terms.");
            }

            foreach (var term in terms)
            {
                Check(term, fieldExists, problems);
            }

            return;
        }

        if (binding.Operator is { } @operator)
        {
            if (!DisplayPredicates.Operators.Contains(@operator, StringComparer.Ordinal))
            {
                problems.Add($"'{@operator}' is not one of {string.Join(", ", DisplayPredicates.Operators)}.");
            }
            else if (DisplayPredicates.TakesOperand(@operator) && binding.Operand is null)
            {
                problems.Add($"'{@operator}' needs a right side.");
            }

            if (binding.Field is null)
            {
                problems.Add("A comparison compares a field.");
            }

            if (binding.Operand is { } operand)
            {
                if (operand.IsPredicate)
                {
                    problems.Add("A comparison's right side is a field or a value.");
                }
                else if (operand.Field is { } operandField && !fieldExists(operandField))
                {
                    // The right side is a value, or a field the condition must be able to read.
                    problems.Add($"'{operandField}' is not a field this condition can read.");
                }
            }
        }
        else if (binding.Field is null)
        {
            problems.Add("A condition is a comparison, a combination or a field.");
            return;
        }

        if (binding.Field is { } field && !fieldExists(field))
        {
            problems.Add($"'{field}' is not a field this condition can read.");
        }
    }
}
