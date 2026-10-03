using System.Reflection;
using Crest.Workflows.UIHints.CodeEditor;

// ReSharper disable once CheckNamespace
namespace Crest.Workflows.Expressions.JavaScript.Activities;

internal class RunJavaScriptOptionsProvider : CodeEditorOptionsProviderBase
{
    protected override string GetLanguage(PropertyInfo propertyInfo, object? context) => "javascript";
}