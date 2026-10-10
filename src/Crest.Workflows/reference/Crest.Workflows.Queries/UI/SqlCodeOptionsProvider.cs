using System.Reflection;
using Crest.Workflows.UIHints.CodeEditor;

namespace Crest.Workflows.Queries.UI;

public class SqlCodeOptionsProvider : CodeEditorOptionsProviderBase
{
    protected override string GetLanguage(PropertyInfo propertyInfo, object? context) => "sql";
}