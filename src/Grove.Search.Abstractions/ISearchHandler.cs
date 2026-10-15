namespace Grove.Search.Abstractions;

public interface ISearchHandler
{
    Task SearchedAsync(SearchContext context);
}
