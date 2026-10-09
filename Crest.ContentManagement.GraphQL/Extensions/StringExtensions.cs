using GraphQL;
using Crest.ContentManagement.Utilities;

namespace System;

public static class StringExtensions
{
    public static string ToFieldName(this string name)
    {
        return name.TrimEndString("Part").ToCamelCase();
    }
}
