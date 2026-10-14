#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Crest.Validation;

/// <summary>
/// A request DTO with cross-field, conditional and collection rules, expressed once in
/// <see cref="Rules"/> and run by ASP.NET's native validation and Blazor's
/// <c>DataAnnotationsValidator</c> alike (through <see cref="IValidatableObject"/>).
/// </summary>
/// <example>
/// <code>
/// protected override void Rules(RuleSet&lt;Transfer&gt; rules)
/// {
///     rules.For(x => x.To).NotEmpty().NotEqual(x => x.From);
///     rules.For(x => x.Amount).GreaterThan(0).When(x => x.Kind == "cash");
///     rules.ForEach(x => x.Lines, line => line.For(l => l.Quantity).GreaterThan(0));
/// }
/// </code>
/// </example>
public abstract class Validated<T> : IValidatableObject
    where T : Validated<T>
{
    protected abstract void Rules(RuleSet<T> rules);

    IEnumerable<ValidationResult> IValidatableObject.Validate(ValidationContext validationContext)
    {
        var rules = new RuleSet<T>();
        Rules(rules);
        return rules.Run((T)this, validationContext);
    }
}

/// <summary>The rules of one object type: one chain per member, object-level predicates, children and collections.</summary>
public sealed class RuleSet<T>
{
    private readonly List<Func<T, ValidationContext, IEnumerable<ValidationResult>>> _rules = [];

    /// <summary>Rules for one member.</summary>
    public Rule<T, TValue> For<TValue>(Expression<Func<T, TValue>> member)
    {
        var rule = new Rule<T, TValue>(member);
        _rules.Add(rule.Run);
        return rule;
    }

    /// <summary>Rules for each item of a collection member, reported under <c>member[index]</c>.</summary>
    public RuleSet<T> ForEach<TItem>(Expression<Func<T, IEnumerable<TItem>?>> member, Action<RuleSet<TItem>> rules)
    {
        var key = Keys.Of(member);
        var read = member.Compile();
        _rules.Add((instance, context) =>
        {
            var items = read(instance);
            if (items is null)
            {
                return [];
            }

            var itemRules = new RuleSet<TItem>();
            rules(itemRules);
            var results = new List<ValidationResult>();
            var index = 0;
            foreach (var item in items)
            {
                if (item is not null)
                {
                    foreach (var result in itemRules.Run(item, new ValidationContext(item, context, context.Items)))
                    {
                        results.Add(Prefixed(result, MemberKey.Join(key, $"[{index}]")));
                    }
                }

                index++;
            }

            return results;
        });
        return this;
    }

    /// <summary>A nested object validated with its own attributes and rules, reported under <c>member.child</c>.</summary>
    public RuleSet<T> Child<TChild>(Expression<Func<T, TChild?>> member)
        where TChild : class
    {
        var key = Keys.Of(member);
        var read = member.Compile();
        _rules.Add((instance, context) =>
        {
            var child = read(instance);
            if (child is null || context.GetService(typeof(NestedValidationHandled)) is not null)
            {
                return [];
            }

            var results = new List<ValidationResult>();
            ObjectRules.Validate(child, context, key, results);
            return results;
        });
        return this;
    }

    /// <summary>An object-level predicate; the error is reported under <paramref name="member"/>, or the object when none.</summary>
    public RuleSet<T> Must(Func<T, bool> predicate, string message, string? member = null)
    {
        _rules.Add((instance, context) => predicate(instance)
            ? []
            : [new ValidationResult(Messages.Format(message, context, member ?? string.Empty), member is null ? [] : [Keys.Wire(member)])]);
        return this;
    }

    /// <summary>Runs every rule of the set against <paramref name="instance"/>.</summary>
    public IEnumerable<ValidationResult> Run(T instance, ValidationContext context) =>
        _rules.SelectMany(rule => rule(instance, context)).ToList();

    private static ValidationResult Prefixed(ValidationResult result, string prefix)
    {
        var names = result.MemberNames.Select(name => MemberKey.Join(prefix, name)).ToList();
        return new ValidationResult(result.ErrorMessage, names.Count == 0 ? [prefix] : names);
    }
}

/// <summary>The rule chain of one member: each call adds a check; <see cref="When"/> and <see cref="Unless"/> gate the chain.</summary>
public sealed class Rule<T, TValue>
{
    private readonly string _key;

    private readonly Func<T, TValue> _read;

    private readonly List<(ValidationAttribute Attribute, Func<T, ValidationContext, object?>? Bound)> _checks = [];

    private readonly List<Func<T, bool>> _conditions = [];

    private string? _message;

    internal Rule(Expression<Func<T, TValue>> member)
    {
        _key = Keys.Of(member);
        _read = member.Compile();
    }

    public Rule<T, TValue> NotEmpty() => Add(new NotEmptyAttribute());

    public Rule<T, TValue> MinimumLength(int minimum) => Add(new MinimumLengthAttribute(minimum));

    public Rule<T, TValue> MaximumLength(int maximum) => Add(new MaximumLengthAttribute(maximum));

    public Rule<T, TValue> Length(int minimum, int maximum) => Add(new LengthBetweenAttribute(minimum, maximum));

    public Rule<T, TValue> Matches(string pattern) => Add(new MatchesAttribute(pattern));

    public Rule<T, TValue> Email() => Add(new EmailAttribute());

    public Rule<T, TValue> Url(params string[] schemes) => Add(new AbsoluteUrlAttribute(schemes));

    public Rule<T, TValue> Equal(Expression<Func<T, TValue>> other) => Add(new EqualToAttribute(Keys.Property(other)));

    public Rule<T, TValue> Equal(TValue value) => Must(current => Equals(current, value), "{0} must be equal to '{1}'.", value);

    public Rule<T, TValue> NotEqual(Expression<Func<T, TValue>> other) => Add(new NotEqualToAttribute(Keys.Property(other)));

    public Rule<T, TValue> NotEqual(TValue value) => Must(current => !Equals(current, value), "{0} must not be equal to '{1}'.", value);

    public Rule<T, TValue> GreaterThan(double value) => Add(new GreaterThanAttribute(value));

    public Rule<T, TValue> GreaterThan(Expression<Func<T, TValue>> other) => Add(new GreaterThanAttribute(Keys.Property(other)));

    public Rule<T, TValue> GreaterThanOrEqual(double value) => Add(new GreaterThanOrEqualAttribute(value));

    public Rule<T, TValue> GreaterThanOrEqual(Expression<Func<T, TValue>> other) => Add(new GreaterThanOrEqualAttribute(Keys.Property(other)));

    public Rule<T, TValue> LessThan(double value) => Add(new LessThanAttribute(value));

    public Rule<T, TValue> LessThan(Expression<Func<T, TValue>> other) => Add(new LessThanAttribute(Keys.Property(other)));

    public Rule<T, TValue> LessThanOrEqual(double value) => Add(new LessThanOrEqualAttribute(value));

    public Rule<T, TValue> LessThanOrEqual(Expression<Func<T, TValue>> other) => Add(new LessThanOrEqualAttribute(Keys.Property(other)));

    public Rule<T, TValue> InclusiveBetween(double from, double to) => Add(new InclusiveBetweenAttribute(from, to));

    public Rule<T, TValue> ExclusiveBetween(double from, double to) => Add(new ExclusiveBetweenAttribute(from, to));

    public Rule<T, TValue> PrecisionScale(int precision, int scale) => Add(new PrecisionScaleAttribute(precision, scale));

    /// <summary>A predicate on the value; <c>{0}</c> in the message is the member's display name.</summary>
    public Rule<T, TValue> Must(Func<TValue, bool> predicate, string message) => Must((_, value) => predicate(value), message);

    /// <summary>A predicate on the object and the value.</summary>
    public Rule<T, TValue> Must(Func<T, TValue, bool> predicate, string message)
    {
        _checks.Add((new PredicateAttribute(message), (instance, _) => predicate(instance, _read(instance)) ? PredicateAttribute.Pass : PredicateAttribute.Fail));
        return this;
    }

    private Rule<T, TValue> Must(Func<TValue, bool> predicate, string message, object? argument)
    {
        _checks.Add((new PredicateAttribute(message, argument), (instance, _) => predicate(_read(instance)) ? PredicateAttribute.Pass : PredicateAttribute.Fail));
        return this;
    }

    /// <summary>The message of every check of this chain added so far and after.</summary>
    public Rule<T, TValue> WithMessage(string message)
    {
        _message = message;
        return this;
    }

    /// <summary>Runs the chain only when the condition holds.</summary>
    public Rule<T, TValue> When(Func<T, bool> condition)
    {
        _conditions.Add(condition);
        return this;
    }

    /// <summary>Runs the chain only when the condition does not hold.</summary>
    public Rule<T, TValue> Unless(Func<T, bool> condition) => When(instance => !condition(instance));

    private Rule<T, TValue> Add(ValidationAttribute attribute)
    {
        _checks.Add((attribute, null));
        return this;
    }

    internal IEnumerable<ValidationResult> Run(T instance, ValidationContext parent)
    {
        if (_conditions.Any(condition => !condition(instance)))
        {
            yield break;
        }

        var value = _read(instance);
        foreach (var (attribute, bound) in _checks)
        {
            if (_message is not null)
            {
                attribute.ErrorMessage = _message;
            }

            var context = new ValidationContext(instance!, parent, parent.Items) { MemberName = _key, DisplayName = _key };
            var checkedValue = bound is null ? value : bound(instance, context);
            if (attribute.GetValidationResult(checkedValue, context) is { } result && result != ValidationResult.Success)
            {
                yield return new ValidationResult(result.ErrorMessage, [_key]);
            }
        }
    }
}

/// <summary>A check whose outcome a predicate decided; the value handed to it is the verdict.</summary>
internal sealed class PredicateAttribute(string template, object? argument = null) : RuleAttribute(template)
{
    public static readonly object Pass = new();

    public static readonly object Fail = new();

    protected override object?[] Arguments => [argument];

    protected override bool Check(object? value, ValidationContext validationContext) => ReferenceEquals(value, Pass);
}

internal static class Keys
{
    public static string Of(LambdaExpression member) => Wire(Property(member));

    public static string Property(LambdaExpression member) =>
        (member.Body as MemberExpression ?? (member.Body as UnaryExpression)?.Operand as MemberExpression)?.Member.Name
        ?? throw new ArgumentException("The rule must name a property.", nameof(member));

    public static string Wire(string property) => System.Text.Json.JsonNamingPolicy.CamelCase.ConvertName(property);
}

internal static class Messages
{
    public static string Format(string template, ValidationContext context, string key)
    {
        if (context.GetService(typeof(IValidationMessageLocalizer)) is IValidationMessageLocalizer localizer)
        {
            template = localizer.Localize(template);
        }

        return string.Format(System.Globalization.CultureInfo.CurrentCulture, template, key.Length == 0 ? context.DisplayName : Keys.Wire(key));
    }
}
