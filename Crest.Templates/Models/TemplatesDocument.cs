using Crest.Data.Documents;

namespace Crest.Templates.Models;

public class TemplatesDocument : Document
{
    public Dictionary<string, Template> Templates { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

public class Template
{
    public string Content { get; set; }
    public string Description { get; set; }
}
