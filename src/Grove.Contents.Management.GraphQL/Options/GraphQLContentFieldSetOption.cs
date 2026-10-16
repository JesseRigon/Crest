namespace Grove.Contents.Management.GraphQL.Options;

public class GraphQLContentFieldSetOption<TContentFieldSet> : GraphQLContentFieldSetOption where TContentFieldSet : ContentFieldSet
{
    public GraphQLContentFieldSetOption() : base(ContentFieldSetName.Of<TContentFieldSet>())
    {
    }
}

public class GraphQLContentFieldSetOption
{
    public GraphQLContentFieldSetOption(string contentFieldSetName)
    {
        ArgumentException.ThrowIfNullOrEmpty(contentFieldSetName);

        Name = contentFieldSetName;
    }

    public string Name { get; }

    public bool Collapse { get; set; }

    public bool Hidden { get; set; }
}
