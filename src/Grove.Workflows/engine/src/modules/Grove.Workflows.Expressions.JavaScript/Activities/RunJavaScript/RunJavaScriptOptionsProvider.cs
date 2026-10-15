using System.Reflection;
using Grove.Workflows.UIHints.CodeEditor;

// ReSharper disable once CheckNamespace
namespace Grove.Workflows.Expressions.JavaScript.Activities;

internal class RunJavaScriptOptionsProvider : CodeEditorOptionsProviderBase
{
    protected override string GetLanguage(PropertyInfo propertyInfo, object? context) => "javascript";
}