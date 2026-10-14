#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Crest.Validation;

/// <summary>
/// Localizes the ruleset's messages. The rules carry invariant templates (<c>{0}</c> is the field's
/// display name, the following placeholders the rule's arguments); the implementation the host
/// registers translates the template through the platform's string localizer before it is formatted.
/// Without one, the template is used as is.
/// </summary>
public interface IValidationMessageLocalizer
{
    string Localize(string template);
}

/// <summary>
/// The base of the ruleset's attributes (docs/MVC/api.md › Validation): a <see cref="ValidationAttribute"/>
/// that runs in ASP.NET's native validation and in Blazor's <c>DataAnnotationsValidator</c> alike,
/// reports its error under the member's wire name (the JSON name the client sends), and localizes
/// its message through <see cref="IValidationMessageLocalizer"/> when the validation context offers one.
/// </summary>
public abstract class RuleAttribute : ValidationAttribute
{
    protected RuleAttribute(string template) => Template = template;

    /// <summary>The invariant message template; <c>{0}</c> is the display name.</summary>
    public string Template { get; }

    /// <summary>The rule's arguments after the display name, for the template.</summary>
    protected virtual object?[] Arguments => [];

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext) =>
        Check(value, validationContext) ? ValidationResult.Success : Fail(validationContext);

    /// <summary>True when <paramref name="value"/> satisfies the rule.</summary>
    protected abstract bool Check(object? value, ValidationContext validationContext);

    protected ValidationResult Fail(ValidationContext validationContext) =>
        new(Message(validationContext), MemberKey.Names(validationContext));

    /// <summary>The formatted, localized message for this context.</summary>
    public string Message(ValidationContext validationContext)
    {
        var template = ErrorMessage ?? Template;
        if (validationContext.GetService(typeof(IValidationMessageLocalizer)) is IValidationMessageLocalizer localizer)
        {
            template = localizer.Localize(template);
        }

        return string.Format(CultureInfo.CurrentCulture, template, [MemberKey.DisplayName(validationContext), .. Arguments]);
    }

    public sealed override string FormatErrorMessage(string name) =>
        string.Format(CultureInfo.CurrentCulture, ErrorMessage ?? Template, [name, .. Arguments]);

    /// <summary>Reads another property of the validated object, for cross-field rules.</summary>
    protected static bool TryGetOther(ValidationContext validationContext, string property, out object? value)
    {
        value = null;
        var member = validationContext.ObjectType.GetProperty(property, BindingFlags.Public | BindingFlags.Instance);
        if (member is null || validationContext.ObjectInstance is null)
        {
            return false;
        }

        value = member.GetValue(validationContext.ObjectInstance);
        return true;
    }
}

/// <summary>
/// The error map's keys: the member's wire name, which is its JSON name (<c>[JsonPropertyName]</c>, else
/// the camel-cased property name), so a key matches the property the client sent.
/// </summary>
public static class MemberKey
{
    /// <summary>The wire name of a property.</summary>
    public static string Of(PropertyInfo property) =>
        property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? JsonNamingPolicy.CamelCase.ConvertName(property.Name);

    /// <summary>The wire name of the validated member, resolved on the object type when it is a property.</summary>
    public static string Of(ValidationContext validationContext)
    {
        var name = validationContext.MemberName ?? string.Empty;
        var property = name.Length > 0 ? validationContext.ObjectType.GetProperty(name, BindingFlags.Public | BindingFlags.Instance) : null;
        return property is null ? (name.Length > 0 ? JsonNamingPolicy.CamelCase.ConvertName(name) : name) : Of(property);
    }

    /// <summary>The member names a <see cref="ValidationResult"/> reports under: the wire name, or none for an object-level error.</summary>
    public static IEnumerable<string> Names(ValidationContext validationContext)
    {
        var key = Of(validationContext);
        return key.Length == 0 ? [] : [key];
    }

    /// <summary>The name the message shows: the context's display name, else the member's wire name.</summary>
    public static string DisplayName(ValidationContext validationContext) =>
        !string.IsNullOrEmpty(validationContext.DisplayName) && validationContext.DisplayName != validationContext.MemberName
            ? validationContext.DisplayName
            : Of(validationContext);

    /// <summary>Joins a parent path and a member key (<c>items[2].name</c>).</summary>
    public static string Join(string parent, string key) =>
        parent.Length == 0 ? key : key.Length == 0 ? parent : key.StartsWith('[') ? parent + key : parent + "." + key;
}
