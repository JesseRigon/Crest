using System.Reflection;

namespace Grove.Data.Migration;

/// <summary>
/// The <see cref="DataMigration"/> convention itself - <c>Create</c>/<c>CreateAsync</c>, then
/// <c>UpdateFromN</c>/<c>UpdateFromNAsync</c> - independent of where the applied versions are
/// recorded. A tenant's <see cref="DataMigrationManager"/> runs migrations with it, and so does
/// any store outside a tenant (the global store), so there is one migration system.
/// </summary>
public static class DataMigrationSteps
{
    private const string UpdateFromPrefix = "UpdateFrom";
    private const string AsyncSuffix = "Async";
    private const BindingFlags MigrationMethodFlags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;

    /// <summary>
    /// Brings <paramref name="migration"/> from <paramref name="current"/> (0 when never
    /// applied) to its latest version and returns it; 0 when it has no Create method.
    /// <paramref name="onStep"/> is told the version each step starts from.
    /// </summary>
    public static async Task<int> RunAsync(IDataMigration migration, int current, Action<int> onStep = null)
    {
        if (current == 0)
        {
            var createMethod = GetCreateMethod(migration);
            if (createMethod == null)
            {
                return 0;
            }

            onStep?.Invoke(0);
            current = await InvokeAsync(createMethod, migration);
        }

        var lookupTable = GetUpdateMethods(migration);
        while (current != 0 && lookupTable.TryGetValue(current, out var methodInfo))
        {
            onStep?.Invoke(current);
            current = await InvokeAsync(methodInfo, migration);
        }

        return current;
    }

    public static async Task<int> InvokeAsync(MethodInfo method, IDataMigration migration)
    {
        if (method.ReturnType == typeof(Task<int>))
        {
            return await (Task<int>)method.Invoke(InvocationTarget(method, migration), []);
        }

        if (method.ReturnType == typeof(int))
        {
            return (int)method.Invoke(InvocationTarget(method, migration), []);
        }

        throw new InvalidOperationException("Invalid return type used in a migration method.");
    }

    public static object InvocationTarget(MethodInfo method, IDataMigration migration)
        => method.IsStatic ? null : migration;

    /// <summary>
    /// Every Update method of a migration class, indexed by the version it updates from.
    /// </summary>
    public static Dictionary<int, MethodInfo> GetUpdateMethods(IDataMigration dataMigration)
        => dataMigration
            .GetType()
            .GetMethods(MigrationMethodFlags)
            .Select(GetUpdateFromMethod)
            .Where(tuple => tuple != null)
            .ToDictionary(tuple => tuple.Item1, tuple => tuple.Item2);

    public static MethodInfo GetCreateMethod(IDataMigration dataMigration)
    {
        var methodName = "Create";
        // First try to find a method that match the given name. (Ex. Create())
        var methodInfo = dataMigration.GetType().GetMethod(methodName, MigrationMethodFlags);

        if (methodInfo != null && (methodInfo.ReturnType == typeof(int) || methodInfo.ReturnType == typeof(Task<int>)))
        {
            return methodInfo;
        }

        // At this point, try to find a method that matches the given name and ends with Async. (Ex. CreateAsync())
        methodInfo = dataMigration.GetType().GetMethod(methodName + AsyncSuffix, MigrationMethodFlags);

        if (methodInfo != null && methodInfo.ReturnType == typeof(Task<int>))
        {
            return methodInfo;
        }

        return null;
    }

    private static Tuple<int, MethodInfo> GetUpdateFromMethod(MethodInfo methodInfo)
    {
        if (methodInfo.Name.StartsWith(UpdateFromPrefix, StringComparison.Ordinal) &&
            (methodInfo.ReturnType == typeof(int) || methodInfo.ReturnType == typeof(Task<int>)))
        {
            var version = methodInfo.Name.EndsWith(AsyncSuffix, StringComparison.Ordinal)
                ? methodInfo.Name.Substring(UpdateFromPrefix.Length, methodInfo.Name.Length - UpdateFromPrefix.Length - AsyncSuffix.Length)
                : methodInfo.Name[UpdateFromPrefix.Length..];

            if (int.TryParse(version, out var versionValue))
            {
                return new Tuple<int, MethodInfo>(versionValue, methodInfo);
            }
        }

        return null;
    }
}
