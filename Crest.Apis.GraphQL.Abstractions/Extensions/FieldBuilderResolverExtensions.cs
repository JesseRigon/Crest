using GraphQL;
using GraphQL.Builders;
using Crest.Apis.GraphQL.Resolvers;

namespace Crest.Apis.GraphQL;

public static class FieldBuilderResolverExtensions
{
    public static FieldBuilder<TSourceType, TReturnType> ResolveLockedAsync<TSourceType, TReturnType>(this FieldBuilder<TSourceType, TReturnType> builder, Func<IResolveFieldContext<TSourceType>, ValueTask<TReturnType>> resolve)
    {
        return builder.Resolve(new LockedAsyncFieldResolver<TSourceType, TReturnType>(resolve));
    }
}
