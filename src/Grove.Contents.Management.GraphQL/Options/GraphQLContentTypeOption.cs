namespace Grove.Contents.Management.GraphQL.Options;

public class GraphQLContentTypeOption
{
    public GraphQLContentTypeOption(string contentType)
    {
        ArgumentException.ThrowIfNullOrEmpty(contentType);

        ContentType = contentType;
    }

    public string ContentType { get; }

    public bool Collapse { get; set; }

    public bool Hidden { get; set; }

    public IEnumerable<GraphQLContentFieldSetOption> FieldSetOptions { get; set; }
        = [];

    public GraphQLContentTypeOption ConfigureFieldSet<TContentFieldSet>(Action<GraphQLContentFieldSetOption> action)
        where TContentFieldSet : ContentFieldSet
    {
        var option = new GraphQLContentFieldSetOption<TContentFieldSet>();

        action(option);

        FieldSetOptions = FieldSetOptions.Union(new[] { option });

        return this;
    }

    public GraphQLContentTypeOption ConfigureFieldSet(string fieldSetName, Action<GraphQLContentFieldSetOption> action)
    {
        var option = new GraphQLContentFieldSetOption(fieldSetName);

        action(option);

        FieldSetOptions = FieldSetOptions.Union(new[] { option });

        return this;
    }
}
