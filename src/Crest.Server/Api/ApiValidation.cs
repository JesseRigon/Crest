#pragma warning disable ASP0029 // The validation resolver contracts are marked experimental in .NET 10; the registry relies on them deliberately (docs/MVC/api.md › Validation).

using System.Collections;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Crest.Validation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Validation;

namespace Crest.Api;

/// <summary>
/// The registry's validation (docs/MVC/api.md › Validation): ASP.NET's native validation filter on
/// every route group, resolving the request DTOs' rules by reflection (the DTOs live in the modules,
/// where the compile-time generator cannot see them), nested objects and collections included,
/// with the error map keyed by the members' wire names.
/// </summary>
internal static class ApiValidation
{
    public static IServiceCollection AddApiValidation(this IServiceCollection services)
    {
        services.AddValidation(options => options.Resolvers.Insert(0, new ReflectionValidatableInfoResolver()));
        services.AddSingleton<IValidationMessageLocalizer, StringLocalizerValidationMessages>();
        services.AddSingleton(NestedValidationHandled.Instance);
        return services;
    }
}

/// <summary>Describes a DTO by reflection: the properties carrying rules, the nested ones to recurse into, and its <see cref="IValidatableObject"/> rules.</summary>
internal sealed class ReflectionValidatableInfoResolver : IValidatableInfoResolver
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<Type, IValidatableInfo?> _cache = new();

    public bool TryGetValidatableTypeInfo(Type type, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out IValidatableInfo? validatableInfo)
    {
        validatableInfo = _cache.GetOrAdd(type, Build);
        return validatableInfo is not null;
    }

    public bool TryGetValidatableParameterInfo(ParameterInfo parameterInfo, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out IValidatableInfo? validatableInfo)
    {
        validatableInfo = null;
        return false;
    }

    private IValidatableInfo? Build(Type type)
    {
        if (!IsCandidate(type))
        {
            return null;
        }

        var members = new List<ReflectedMember>();
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.GetIndexParameters().Length > 0 || !property.CanRead)
            {
                continue;
            }

            var attributes = property.GetCustomAttributes<ValidationAttribute>(inherit: true).ToArray();
            var nested = Element(property.PropertyType) is { } element && element != type && IsCandidate(element) && Build(element) is not null;
            if (attributes.Length > 0 || nested)
            {
                members.Add(new ReflectedMember(property, MemberKey.Of(property), attributes, nested));
            }
        }

        return members.Count == 0 && !typeof(IValidatableObject).IsAssignableFrom(type)
            ? null
            : new ReflectedType(members);
    }

    private static Type? Element(Type type)
    {
        if (type == typeof(string))
        {
            return null;
        }

        if (typeof(IEnumerable).IsAssignableFrom(type))
        {
            if (type.IsArray)
            {
                return type.GetElementType();
            }

            var enumerable = type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>)
                ? type
                : type.GetInterfaces().FirstOrDefault(candidate => candidate.IsGenericType && candidate.GetGenericTypeDefinition() == typeof(IEnumerable<>));
            return enumerable?.GetGenericArguments()[0];
        }

        return type;
    }

    private static bool IsCandidate(Type type) =>
        type.IsClass
        && !type.IsAbstract
        && type != typeof(string)
        && type.Namespace is { } ns
        && !ns.StartsWith("System", StringComparison.Ordinal)
        && !ns.StartsWith("Microsoft", StringComparison.Ordinal);

    private sealed record ReflectedMember(PropertyInfo Property, string Key, ValidationAttribute[] Attributes, bool Nested);

    private sealed class ReflectedType(IReadOnlyList<ReflectedMember> members) : IValidatableInfo
    {
        public async Task ValidateAsync(object? value, ValidateContext context, CancellationToken cancellationToken)
        {
            if (value is null || context.CurrentDepth >= context.ValidationOptions.MaxDepth)
            {
                return;
            }

            var services = context.ValidationContext;
            var items = context.ValidationContext.Items;
            var path = context.CurrentValidationPath ?? string.Empty;

            foreach (var member in members)
            {
                var propertyValue = member.Property.GetValue(value);
                var key = MemberKey.Join(path, member.Key);

                if (member.Attributes.Length > 0)
                {
                    var propertyContext = new ValidationContext(value, services, items) { MemberName = member.Property.Name, DisplayName = member.Key };
                    foreach (var attribute in member.Attributes)
                    {
                        if (attribute.GetValidationResult(propertyValue, propertyContext) is { } result && result != ValidationResult.Success)
                        {
                            Add(context, key, result.ErrorMessage ?? string.Empty);
                        }
                    }
                }

                if (member.Nested && propertyValue is not null)
                {
                    await ValidateNestedAsync(propertyValue, key, context, cancellationToken);
                }
            }

            if (value is IValidatableObject validatable)
            {
                var objectContext = new ValidationContext(value, services, items);
                foreach (var result in validatable.Validate(objectContext))
                {
                    if (result == ValidationResult.Success)
                    {
                        continue;
                    }

                    var names = result.MemberNames.ToList();
                    if (names.Count == 0)
                    {
                        Add(context, path, result.ErrorMessage ?? string.Empty);
                    }

                    foreach (var name in names)
                    {
                        Add(context, MemberKey.Join(path, name), result.ErrorMessage ?? string.Empty);
                    }
                }
            }
        }

        private static async Task ValidateNestedAsync(object propertyValue, string key, ValidateContext context, CancellationToken cancellationToken)
        {
            var original = context.CurrentValidationPath;
            context.CurrentDepth++;
            try
            {
                if (propertyValue is IEnumerable items and not string)
                {
                    var index = 0;
                    foreach (var item in items)
                    {
                        if (item is not null && context.ValidationOptions.TryGetValidatableTypeInfo(item.GetType(), out var itemInfo))
                        {
                            context.CurrentValidationPath = MemberKey.Join(key, $"[{index}]");
                            await itemInfo.ValidateAsync(item, context, cancellationToken);
                        }

                        index++;
                    }
                }
                else if (context.ValidationOptions.TryGetValidatableTypeInfo(propertyValue.GetType(), out var info))
                {
                    context.CurrentValidationPath = key;
                    await info.ValidateAsync(propertyValue, context, cancellationToken);
                }
            }
            finally
            {
                context.CurrentDepth--;
                context.CurrentValidationPath = original;
            }
        }

        private static void Add(ValidateContext context, string key, string message)
        {
            context.ValidationErrors ??= new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            context.ValidationErrors[key] = context.ValidationErrors.TryGetValue(key, out var existing) ? [.. existing, message] : [message];
        }
    }
}

/// <summary>The ruleset's messages through the platform's string localizer, in the request's culture.</summary>
internal sealed class StringLocalizerValidationMessages(IStringLocalizerFactory factory) : IValidationMessageLocalizer
{
    private readonly IStringLocalizer _localizer = factory.Create(typeof(StringLocalizerValidationMessages));

    public string Localize(string template) => _localizer[template];
}
