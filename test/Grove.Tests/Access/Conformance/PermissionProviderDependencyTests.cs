using System.Reflection;
using Grove.Security.Permissions;
using Microsoft.AspNetCore.Authorization;

namespace Grove.Tests.Access.Conformance;

/// <summary>
/// No permission provider, resource mapper, ceiling or auditor reaches the authorization stack through its constructor. The
/// permission service enumerates every <see cref="IPermissionProvider"/>, and the authorization
/// handler needs the permission service, so a provider that takes anything holding
/// <see cref="IAuthorizationService"/> closes a DI cycle and stops tenant setup (regression,
/// 2026-10-15: CustomSettings' provider took CustomSettingsService).
/// </summary>
public class PermissionProviderDependencyTests
{
    private static readonly Type[] s_forbidden = [typeof(IAuthorizationService), typeof(IAuthorizationHandler), typeof(IPermissionService), typeof(global::Grove.Access.IAccessDecision)];

    // What the access decision itself enumerates: none of these may reach it back.
    private static readonly Type[] s_roots =
    [
        typeof(IPermissionProvider),
        typeof(global::Grove.Access.IResourcePermissionMapper),
        typeof(global::Grove.Access.IAccessCeiling),
        typeof(global::Grove.Access.IAccessAuditor),
    ];

    private static ILookup<Type, Type> s_implementations = Array.Empty<Type>().ToLookup(type => type);

    [Fact]
    public void No_permission_provider_depends_on_the_authorization_stack()
    {
        var directory = Path.GetDirectoryName(typeof(PermissionProviderDependencyTests).Assembly.Location)!;
        var providers = new List<Type>();
        foreach (var path in Directory.EnumerateFiles(directory, "Grove.*.dll").Concat(Directory.EnumerateFiles(directory, "Fruitful.*.dll")))
        {
            Assembly assembly;
            try
            {
                assembly = Assembly.LoadFrom(path);
            }
            catch (BadImageFormatException)
            {
                continue;
            }

            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                types = exception.Types.Where(type => type is not null).ToArray()!;
            }

            providers.AddRange(types.Where(type => type is { IsClass: true, IsAbstract: false } && s_roots.Any(root => root.IsAssignableFrom(type))));
        }

        Assert.NotEmpty(providers);

        var all = AppDomain.CurrentDomain.GetAssemblies()
            .Where(assembly => assembly.GetName().Name is { } name && (name.StartsWith("Grove.", StringComparison.Ordinal) || name.StartsWith("Fruitful.", StringComparison.Ordinal)))
            .SelectMany(assembly => { try { return assembly.GetTypes(); } catch (ReflectionTypeLoadException exception) { return exception.Types.Where(type => type is not null)!; } })
            .Where(type => type is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false })
            .ToList();
        s_implementations = all.SelectMany(type => type.GetInterfaces().Select(contract => (contract, type))).ToLookup(pair => pair.contract, pair => pair.type);

        var violations = providers
            .Select(provider => (provider, path: FindPath(provider, [], 0)))
            .Where(result => result.path is not null)
            .Select(result => $"{result.provider.FullName}: {string.Join(" -> ", result.path!)}")
            .ToList();

        Assert.True(violations.Count == 0, string.Join(System.Environment.NewLine, violations));
    }

    // Walks concrete constructor dependencies (interfaces are leaves: their implementation is not
    // known here, so only a concrete class a provider asks for by type is followed).
    private static List<string>? FindPath(Type type, HashSet<Type> seen, int depth)
    {
        if (depth > 10 || !seen.Add(type))
        {
            return null;
        }

        foreach (var constructor in type.GetConstructors())
        {
            foreach (var parameter in constructor.GetParameters())
            {
                var dependency = parameter.ParameterType;
                if (dependency.IsGenericType && dependency.GetGenericTypeDefinition() is var open
                    && (open == typeof(IEnumerable<>) || open == typeof(Lazy<>)))
                {
                    dependency = dependency.GetGenericArguments()[0];
                }
                if (s_forbidden.Any(forbidden => forbidden.IsAssignableFrom(dependency)))
                {
                    return [type.Name, dependency.Name];
                }

                if (dependency.IsInterface)
                {
                    foreach (var implementation in s_implementations[dependency])
                    {
                        if (FindPath(implementation, seen, depth + 1) is { } viaInterface)
                        {
                            viaInterface.Insert(0, $"{type.Name}({dependency.Name})");
                            return viaInterface;
                        }
                    }

                    continue;
                }

                if (dependency is { IsClass: true, IsAbstract: false } && dependency.Assembly.GetName().Name is { } name
                    && (name.StartsWith("Grove.", StringComparison.Ordinal) || name.StartsWith("Fruitful.", StringComparison.Ordinal))
                    && FindPath(dependency, seen, depth + 1) is { } path)
                {
                    path.Insert(0, type.Name);
                    return path;
                }
            }
        }

        return null;
    }
}
