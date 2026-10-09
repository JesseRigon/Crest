namespace Crest.Search.Abstractions;

public interface ISearchHandler
{
    Task SearchedAsync(SearchContext context);
}
