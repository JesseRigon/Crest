using GraphQL;
using Grove.ContentManagement.Utilities;

namespace System;

public static class StringExtensions
{
    public static string ToFieldName(this string name)
    {
        return name.TrimEndString("Part").ToCamelCase();
    }
}
