#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Crest.Validation;

/// <summary>Not null, not a blank string, not an empty collection, not a value type's default.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class NotEmptyAttribute() : RuleAttribute("{0} must not be empty.")
{
    protected override bool Check(object? value, ValidationContext validationContext) => !IsEmpty(value);

    internal static bool IsEmpty(object? value) => value switch
    {
        null => true,
        string text => string.IsNullOrWhiteSpace(text),
        ICollection collection => collection.Count == 0,
        IEnumerable enumerable => !enumerable.Cast<object?>().Any(),
        _ => value.GetType() is { IsValueType: true } type && value.Equals(Activator.CreateInstance(type)),
    };
}

/// <summary>A string's length, or a collection's count, is at least <paramref name="minimum"/>.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class MinimumLengthAttribute(int minimum) : RuleAttribute("{0} must be at least {1} characters long.")
{
    public int Minimum { get; } = minimum;

    protected override object?[] Arguments => [Minimum];

    protected override bool Check(object? value, ValidationContext validationContext) => value is null || Lengths.Of(value) >= Minimum;
}

/// <summary>A string's length, or a collection's count, is at most <paramref name="maximum"/>.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class MaximumLengthAttribute(int maximum) : RuleAttribute("{0} must be {1} characters or fewer.")
{
    public int Maximum { get; } = maximum;

    protected override object?[] Arguments => [Maximum];

    protected override bool Check(object? value, ValidationContext validationContext) => value is null || Lengths.Of(value) <= Maximum;
}

/// <summary>A string's length, or a collection's count, is between <paramref name="minimum"/> and <paramref name="maximum"/>, inclusive.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class LengthBetweenAttribute(int minimum, int maximum) : RuleAttribute("{0} must be between {1} and {2} characters long.")
{
    public int Minimum { get; } = minimum;

    public int Maximum { get; } = maximum;

    protected override object?[] Arguments => [Minimum, Maximum];

    protected override bool Check(object? value, ValidationContext validationContext) =>
        value is null || Lengths.Of(value) is var length && length >= Minimum && length <= Maximum;
}

internal static class Lengths
{
    public static int Of(object value) => value switch
    {
        string text => text.Length,
        ICollection collection => collection.Count,
        IEnumerable enumerable => enumerable.Cast<object?>().Count(),
        _ => value.ToString()?.Length ?? 0,
    };
}

/// <summary>A string matches a regular expression (an empty value passes; combine with <see cref="NotEmptyAttribute"/>).</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class MatchesAttribute(string pattern) : RuleAttribute("{0} is not in the correct format.")
{
    private readonly Regex _regex = new(pattern, RegexOptions.None, TimeSpan.FromSeconds(1));

    public string Pattern { get; } = pattern;

    protected override bool Check(object? value, ValidationContext validationContext) =>
        value is null || value is string { Length: 0 } || (value is string text && _regex.IsMatch(text));
}

/// <summary>An e-mail address: something, one <c>@</c>, something, no whitespace (what a mail server would accept to route).</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class EmailAttribute() : RuleAttribute("{0} is not a valid email address.")
{
    protected override bool Check(object? value, ValidationContext validationContext) =>
        value is null || value is string { Length: 0 } || (value is string text && IsEmail(text));

    internal static bool IsEmail(string text)
    {
        var at = text.IndexOf('@');
        return at > 0 && at == text.LastIndexOf('@') && at < text.Length - 1 && !text.Any(char.IsWhiteSpace);
    }
}

/// <summary>An absolute URL with one of the allowed schemes (http and https by default).</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class AbsoluteUrlAttribute(params string[] schemes) : RuleAttribute("{0} is not a valid URL.")
{
    public IReadOnlyList<string> Schemes { get; } = schemes.Length == 0 ? ["http", "https"] : schemes;

    protected override bool Check(object? value, ValidationContext validationContext) =>
        value is null || value is string { Length: 0 } || (value is string text && IsUrl(text, Schemes));

    internal static bool IsUrl(string text, IReadOnlyList<string> schemes) =>
        Uri.TryCreate(text, UriKind.Absolute, out var uri) && schemes.Contains(uri.Scheme, StringComparer.OrdinalIgnoreCase);
}

/// <summary>Equal to another property of the same object (a password confirmation).</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class EqualToAttribute(string otherProperty) : RuleAttribute("{0} must be equal to {1}.")
{
    public string OtherProperty { get; } = otherProperty;

    protected override object?[] Arguments => [OtherProperty];

    protected override bool Check(object? value, ValidationContext validationContext) =>
        TryGetOther(validationContext, OtherProperty, out var other) && Equals(value, other);
}

/// <summary>Not equal to another property of the same object.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class NotEqualToAttribute(string otherProperty) : RuleAttribute("{0} must not be equal to {1}.")
{
    public string OtherProperty { get; } = otherProperty;

    protected override object?[] Arguments => [OtherProperty];

    protected override bool Check(object? value, ValidationContext validationContext) =>
        !TryGetOther(validationContext, OtherProperty, out var other) || !Equals(value, other);
}

/// <summary>
/// A comparison to a constant or to another property. Numbers compare as decimals; anything else
/// through <see cref="IComparable"/>. A null value passes (combine with <see cref="NotEmptyAttribute"/>).
/// </summary>
public abstract class ComparisonAttribute : RuleAttribute
{
    private readonly object? _value;

    private readonly string? _otherProperty;

    protected ComparisonAttribute(string template, double value) : base(template) => _value = value;

    protected ComparisonAttribute(string template, string otherProperty) : base(template) => _otherProperty = otherProperty;

    /// <summary>The constant compared to, when the rule is not against another property.</summary>
    public object? Value => _value;

    /// <summary>The other property compared to, when the rule is not against a constant.</summary>
    public string? OtherProperty => _otherProperty;

    protected override object?[] Arguments => [_otherProperty ?? _value];

    /// <summary>True when the comparison result (negative, zero, positive) satisfies the rule.</summary>
    protected abstract bool Accepts(int comparison);

    protected override bool Check(object? value, ValidationContext validationContext)
    {
        if (value is null)
        {
            return true;
        }

        object? bound = _value;
        if (_otherProperty is not null && !TryGetOther(validationContext, _otherProperty, out bound))
        {
            return true;
        }

        return bound is null || Accepts(Compare(value, bound));
    }

    internal static int Compare(object value, object bound)
    {
        if (IsNumber(value) && IsNumber(bound))
        {
            return Convert.ToDecimal(value, CultureInfo.InvariantCulture).CompareTo(Convert.ToDecimal(bound, CultureInfo.InvariantCulture));
        }

        if (value is IComparable comparable)
        {
            var typed = bound.GetType() == value.GetType() ? bound : Convert.ChangeType(bound, value.GetType(), CultureInfo.InvariantCulture);
            return comparable.CompareTo(typed);
        }

        throw new InvalidOperationException($"'{value.GetType().Name}' is not comparable.");
    }

    private static bool IsNumber(object value) => value is sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal;
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class GreaterThanAttribute : ComparisonAttribute
{
    public GreaterThanAttribute(double value) : base("{0} must be greater than {1}.", value) { }

    public GreaterThanAttribute(string otherProperty) : base("{0} must be greater than {1}.", otherProperty) { }

    protected override bool Accepts(int comparison) => comparison > 0;
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class GreaterThanOrEqualAttribute : ComparisonAttribute
{
    public GreaterThanOrEqualAttribute(double value) : base("{0} must be greater than or equal to {1}.", value) { }

    public GreaterThanOrEqualAttribute(string otherProperty) : base("{0} must be greater than or equal to {1}.", otherProperty) { }

    protected override bool Accepts(int comparison) => comparison >= 0;
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class LessThanAttribute : ComparisonAttribute
{
    public LessThanAttribute(double value) : base("{0} must be less than {1}.", value) { }

    public LessThanAttribute(string otherProperty) : base("{0} must be less than {1}.", otherProperty) { }

    protected override bool Accepts(int comparison) => comparison < 0;
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class LessThanOrEqualAttribute : ComparisonAttribute
{
    public LessThanOrEqualAttribute(double value) : base("{0} must be less than or equal to {1}.", value) { }

    public LessThanOrEqualAttribute(string otherProperty) : base("{0} must be less than or equal to {1}.", otherProperty) { }

    protected override bool Accepts(int comparison) => comparison <= 0;
}

/// <summary>Between two bounds, both included.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class InclusiveBetweenAttribute(double from, double to) : RuleAttribute("{0} must be between {1} and {2}.")
{
    public double From { get; } = from;

    public double To { get; } = to;

    protected override object?[] Arguments => [From, To];

    protected override bool Check(object? value, ValidationContext validationContext) =>
        value is null || (ComparisonAttribute.Compare(value, From) >= 0 && ComparisonAttribute.Compare(value, To) <= 0);
}

/// <summary>Between two bounds, both excluded.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class ExclusiveBetweenAttribute(double from, double to) : RuleAttribute("{0} must be between {1} and {2} (exclusive).")
{
    public double From { get; } = from;

    public double To { get; } = to;

    protected override object?[] Arguments => [From, To];

    protected override bool Check(object? value, ValidationContext validationContext) =>
        value is null || (ComparisonAttribute.Compare(value, From) > 0 && ComparisonAttribute.Compare(value, To) < 0);
}

/// <summary>A decimal with at most <paramref name="precision"/> digits in all and <paramref name="scale"/> after the point.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class PrecisionScaleAttribute(int precision, int scale) : RuleAttribute("{0} must not be more than {1} digits in total, with allowance for {2} decimals.")
{
    public int Precision { get; } = precision;

    public int Scale { get; } = scale;

    protected override object?[] Arguments => [Precision, Scale];

    protected override bool Check(object? value, ValidationContext validationContext)
    {
        if (value is null)
        {
            return true;
        }

        var number = Math.Abs(Convert.ToDecimal(value, CultureInfo.InvariantCulture));
        var text = number.ToString(CultureInfo.InvariantCulture).TrimEnd('0').TrimEnd('.');
        var point = text.IndexOf('.');
        var scaleUsed = point < 0 ? 0 : text.Length - point - 1;
        var integerDigits = (point < 0 ? text : text[..point]).TrimStart('0').Length;
        return scaleUsed <= Scale && integerDigits + scaleUsed <= Precision && integerDigits <= Precision - Scale;
    }
}

/// <summary>
/// A predicate on the validated object: <paramref name="method"/> names a public <c>bool</c> method of
/// the object (static or instance) taking the value, or the value and the <see cref="ValidationContext"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class MustAttribute(string method, string message) : RuleAttribute(message)
{
    public string Method { get; } = method;

    protected override bool Check(object? value, ValidationContext validationContext)
    {
        var candidates = validationContext.ObjectType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
            .Where(candidate => candidate.Name == Method && candidate.ReturnType == typeof(bool))
            .ToList();
        var method = candidates.FirstOrDefault(candidate => candidate.GetParameters().Length == 2)
            ?? candidates.FirstOrDefault(candidate => candidate.GetParameters().Length == 1)
            ?? throw new InvalidOperationException($"'{validationContext.ObjectType.Name}' has no public bool method '{Method}' taking the value (and optionally the validation context).");
        var target = method.IsStatic ? null : validationContext.ObjectInstance;
        var arguments = method.GetParameters().Length == 2 ? new[] { value, validationContext } : [value];
        return (bool)method.Invoke(target, arguments)!;
    }
}

/// <summary>
/// Validates a nested object, or each item of a collection, with its own rules, reporting under
/// <c>member.child</c> and <c>member[index].child</c>. The server's validation recurses on its own;
/// Blazor's <c>DataAnnotationsValidator</c> does not, which is what this is for.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class ValidateChildAttribute() : RuleAttribute("{0} is not valid.")
{
    private List<ValidationResult>? _results;

    protected override bool Check(object? value, ValidationContext validationContext)
    {
        _results = null;
        if (value is null || validationContext.GetService(typeof(NestedValidationHandled)) is not null)
        {
            return true;
        }

        var results = new List<ValidationResult>();
        var key = MemberKey.Of(validationContext);
        if (value is IEnumerable items and not string)
        {
            var index = 0;
            foreach (var item in items)
            {
                if (item is not null)
                {
                    ObjectRules.Validate(item, validationContext, MemberKey.Join(key, $"[{index}]"), results);
                }

                index++;
            }
        }
        else
        {
            ObjectRules.Validate(value, validationContext, key, results);
        }

        _results = results;
        return results.Count == 0;
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var result = base.IsValid(value, validationContext);
        return result is null || _results is not { Count: > 0 } ? result : new CompositeValidationResult(result.ErrorMessage ?? string.Empty, result.MemberNames, _results);
    }
}

/// <summary>A validation result that carries the nested results it summarizes.</summary>
public sealed class CompositeValidationResult(string message, IEnumerable<string> memberNames, IReadOnlyList<ValidationResult> results) : ValidationResult(message, memberNames)
{
    public IReadOnlyList<ValidationResult> Results { get; } = results;
}

/// <summary>Present in the validation context's services when the host validates nested objects itself (ASP.NET's validation does).</summary>
public sealed class NestedValidationHandled
{
    public static readonly NestedValidationHandled Instance = new();
}

/// <summary>Runs an object's attribute and <see cref="IValidatableObject"/> rules, prefixing the member keys.</summary>
public static class ObjectRules
{
    public static void Validate(object instance, ValidationContext parent, string prefix, ICollection<ValidationResult> results)
    {
        var context = new ValidationContext(instance, parent, parent.Items);
        var nested = new List<ValidationResult>();
        Validator.TryValidateObject(instance, context, nested, validateAllProperties: true);
        foreach (var result in nested)
        {
            Add(result, prefix, results);
        }
    }

    private static void Add(ValidationResult result, string prefix, ICollection<ValidationResult> results)
    {
        if (result is CompositeValidationResult composite)
        {
            foreach (var inner in composite.Results)
            {
                Add(inner, prefix, results);
            }

            return;
        }

        var names = result.MemberNames.Select(name => MemberKey.Join(prefix, name)).ToList();
        results.Add(new ValidationResult(result.ErrorMessage, names.Count == 0 ? [prefix] : names));
    }
}
