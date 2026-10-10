#nullable enable
namespace Crest.Queries;

/// <summary>One column of a query's result: its name and the CLR type of its values.</summary>
public sealed record QueryColumn(string Name, Type Type);
