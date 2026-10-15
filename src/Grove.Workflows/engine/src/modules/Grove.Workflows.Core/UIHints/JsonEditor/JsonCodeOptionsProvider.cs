using System.Reflection;
using Grove.Workflows.UIHints.CodeEditor;

// ReSharper disable once CheckNamespace
namespace Grove.Workflows.UIHints.JsonEditor;

internal class JsonCodeOptionsProvider : CodeEditorOptionsProviderBase
{
    protected override string GetLanguage(PropertyInfo propertyInfo, object? context) => "json";
}