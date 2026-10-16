using System.Text;

namespace Grove.Data.Protocols.GraphQL.Client;

public class ContentFieldSetBuilder
{
    private readonly string _contentFieldSetName;
    private readonly Dictionary<string, string> _keysWithValues = [];
    private readonly List<string> _keys = [];

    public ContentFieldSetBuilder(string contentFieldSetName)
    {
        _contentFieldSetName = contentFieldSetName;
    }

    public ContentFieldSetBuilder AddField(string name, string value)
    {
        _keysWithValues.Add(name.ToGraphQLStringFormat(), value);

        return this;
    }

    public ContentFieldSetBuilder AddField(string name)
    {
        _keys.Add(name);

        return this;
    }

    internal string Build()
    {
        var sb = new StringBuilder();
        sb.Append(_contentFieldSetName).Append(": {{ ");

        for (var i = 0; i < _keysWithValues.Count; i++)
        {
            var item = _keysWithValues.ElementAt(i);
            sb.Append(item.Key).Append(": \"").Append(item.Value).Append('"');

            if (i < (_keysWithValues.Count - 1))
            {
                sb.Append(' ');
            }
        }

        foreach (var item in _keys)
        {
            sb.Append(item).Append(' ');
        }

        sb.Append(" }");

        return sb.ToString();
    }
}
