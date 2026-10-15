namespace Grove.Navigation;

public interface INavigationProvider
{
    ValueTask BuildNavigationAsync(string name, NavigationBuilder builder);
}
