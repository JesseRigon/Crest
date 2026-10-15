#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace Grove.Validation;

/// <summary>
/// The error contract's map (docs/MVC/api.md › The error contract): member wire name → messages,
/// <c>""</c> for the object. Built by a handler for the checks that need a service (a name not
/// taken, a referenced record that exists) and answered through <c>ApiProblems.Validation</c>
/// in the same shape ASP.NET's native validation answers with.
/// </summary>
public sealed class ValidationErrors : IReadOnlyDictionary<string, string[]>
{
    private readonly Dictionary<string, List<string>> _errors = new(StringComparer.OrdinalIgnoreCase);

    public int Count => _errors.Count;

    public bool IsEmpty => _errors.Count == 0;

    public IEnumerable<string> Keys => _errors.Keys;

    public IEnumerable<string[]> Values => _errors.Values.Select(messages => messages.ToArray());

    public string[] this[string key] => _errors[key].ToArray();

    /// <summary>Adds a message under a member's wire name (a property name is camel-cased), or under the object with an empty name.</summary>
    public ValidationErrors Add(string member, string message)
    {
        var key = member.Length == 0 || member.Contains('.') || member.Contains('[') ? member : Keys_Wire(member);
        if (!_errors.TryGetValue(key, out var messages))
        {
            _errors[key] = messages = [];
        }

        messages.Add(message);
        return this;
    }

    /// <summary>Adds the results of attribute or <see cref="IValidatableObject"/> validation.</summary>
    public ValidationErrors Add(IEnumerable<ValidationResult> results)
    {
        foreach (var result in results)
        {
            var names = result.MemberNames.ToList();
            if (names.Count == 0)
            {
                Add(string.Empty, result.ErrorMessage ?? string.Empty);
            }

            foreach (var name in names)
            {
                Add(name, result.ErrorMessage ?? string.Empty);
            }
        }

        return this;
    }

    public bool ContainsKey(string key) => _errors.ContainsKey(key);

    public bool TryGetValue(string key, out string[] value)
    {
        if (_errors.TryGetValue(key, out var messages))
        {
            value = messages.ToArray();
            return true;
        }

        value = [];
        return false;
    }

    public Dictionary<string, string[]> ToDictionary() => _errors.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray(), StringComparer.OrdinalIgnoreCase);

    public IEnumerator<KeyValuePair<string, string[]>> GetEnumerator() =>
        _errors.Select(pair => new KeyValuePair<string, string[]>(pair.Key, pair.Value.ToArray())).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private static string Keys_Wire(string property) => System.Text.Json.JsonNamingPolicy.CamelCase.ConvertName(property);
}

/// <summary>
/// Rules a handler runs with its services, after the native validation accepted the request's
/// shape: asynchronous predicates on members, reported in the same error map.
/// </summary>
/// <example>
/// <code>
/// var errors = await new AsyncRules&lt;QueryWrite&gt;()
///     .For(x => x.Name).MustAsync(async (name, ct) => await manager.GetQueryAsync(name) is null, "A query named '{1}' already exists.")
///     .ValidateAsync(write, HttpContext.RequestServices, cancellationToken);
/// if (!errors.IsEmpty) return ApiProblems.Validation(errors);
/// </code>
/// </example>
public sealed class AsyncRules<T>
{
    private readonly List<Func<T, IValidationMessageLocalizer?, CancellationToken, Task<(string Key, string? Error)>>> _rules = [];

    public AsyncRule<T, TValue> For<TValue>(Expression<Func<T, TValue>> member) => new(this, member);

    /// <summary>An object-level asynchronous predicate; the error is reported under <paramref name="member"/>, or the object when none.</summary>
    public AsyncRules<T> MustAsync(Func<T, CancellationToken, Task<bool>> predicate, string message, string? member = null)
    {
        var key = member is null ? string.Empty : Keys.Wire(member);
        _rules.Add(async (instance, localizer, cancellationToken) =>
            (key, await predicate(instance, cancellationToken) ? null : Format(message, localizer, key, null)));
        return this;
    }

    internal void Add(Func<T, IValidationMessageLocalizer?, CancellationToken, Task<(string, string?)>> rule) => _rules.Add(rule);

    /// <summary>Runs the rules; <paramref name="services"/> supplies the message localizer when one is registered.</summary>
    public async Task<ValidationErrors> ValidateAsync(T instance, IServiceProvider? services = null, CancellationToken cancellationToken = default)
    {
        var localizer = services?.GetService(typeof(IValidationMessageLocalizer)) as IValidationMessageLocalizer;
        var errors = new ValidationErrors();
        foreach (var rule in _rules)
        {
            var (key, error) = await rule(instance, localizer, cancellationToken);
            if (error is not null)
            {
                errors.Add(key, error);
            }
        }

        return errors;
    }

    internal static string Format(string template, IValidationMessageLocalizer? localizer, string key, object? value) =>
        string.Format(System.Globalization.CultureInfo.CurrentCulture, localizer?.Localize(template) ?? template, key, value);
}

/// <summary>An asynchronous rule chain on one member; <c>{0}</c> in a message is the member, <c>{1}</c> its value.</summary>
public sealed class AsyncRule<T, TValue>
{
    private readonly AsyncRules<T> _rules;

    private readonly string _key;

    private readonly Func<T, TValue> _read;

    internal AsyncRule(AsyncRules<T> rules, Expression<Func<T, TValue>> member)
    {
        _rules = rules;
        _key = Keys.Of(member);
        _read = member.Compile();
    }

    public AsyncRule<T, TValue> MustAsync(Func<TValue, CancellationToken, Task<bool>> predicate, string message)
    {
        _rules.Add(async (instance, localizer, cancellationToken) =>
        {
            var value = _read(instance);
            return (_key, await predicate(value, cancellationToken) ? null : AsyncRules<T>.Format(message, localizer, _key, value));
        });
        return this;
    }

    /// <summary>Back to the set, to add rules for another member.</summary>
    public AsyncRules<T> And => _rules;

    public Task<ValidationErrors> ValidateAsync(T instance, IServiceProvider? services = null, CancellationToken cancellationToken = default) =>
        _rules.ValidateAsync(instance, services, cancellationToken);
}
