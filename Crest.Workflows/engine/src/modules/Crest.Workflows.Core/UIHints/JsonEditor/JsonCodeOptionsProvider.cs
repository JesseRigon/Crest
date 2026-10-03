using System.Reflection;
using Crest.Workflows.UIHints.CodeEditor;

// ReSharper disable once CheckNamespace
namespace Crest.Workflows.UIHints.JsonEditor;

internal class JsonCodeOptionsProvider : CodeEditorOptionsProviderBase
{
    protected override string GetLanguage(PropertyInfo propertyInfo, object? context) => "json";
}