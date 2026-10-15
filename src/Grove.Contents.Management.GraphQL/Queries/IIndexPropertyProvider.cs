namespace Grove.Contents.Management.GraphQL.Queries;

public interface IIndexPropertyProvider
{
    string IndexName { get; }
    bool TryGetValue(string propertyName, out string indexPropertyName);
}
