using GraphQL;
using Grove.Contents.Management.Utilities;

namespace System;

public static class StringExtensions
{
    public static string ToFieldName(this string name)
    {
        return name.TrimEndString("Part").ToCamelCase();
    }
}
