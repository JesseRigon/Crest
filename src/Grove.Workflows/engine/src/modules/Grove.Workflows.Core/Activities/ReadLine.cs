using System.Runtime.CompilerServices;
using Grove.Workflows.Expressions.Models;
using Grove.Workflows.Attributes;
using Grove.Workflows.Models;

namespace Grove.Workflows.Activities;

/// <summary>
/// Read a line of text from the console.
/// </summary>
[Activity("Grove.Workflows", "Console", "Read a line of text from the console.")]
public class ReadLine : CodeActivity<string>
{
    /// <inheritdoc />
    public ReadLine([CallerFilePath] string? source = null, [CallerLineNumber] int? line = null) : base(source, line)
    {
    }

    /// <inheritdoc />
    public ReadLine(MemoryBlockReference output, [CallerFilePath] string? source = null, [CallerLineNumber] int? line = null) : base(output, source, line)
    {
    }

    /// <inheritdoc />
    public ReadLine(Output<string>? output, [CallerFilePath] string? source = null, [CallerLineNumber] int? line = null) : base(output, source, line)
    {
    }

    /// <inheritdoc />
    protected override void Execute(ActivityExecutionContext context)
    {
        var provider = context.GetService<IStandardInStreamProvider>() ?? new StandardInStreamProvider(Console.In);
        var reader = provider.GetTextReader();
        var text = reader.ReadLine()!;
        context.Set(Result, text);
    }
}