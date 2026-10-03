using Crest.Workflows.Expressions.Models;

namespace Crest.Workflows.Models;

public class Output : Argument
{
    public Output() : base(new MemoryBlockReference())
    {
    }

    public Output(MemoryBlockReference memoryBlockReference) : base(memoryBlockReference)
    {
    }
    
    public Output(Func<MemoryBlockReference> memoryBlockReference) : base(memoryBlockReference)
    {
    }
}

public class Output<T> : Output
{
    public Output()
    {
    }
    
    public Output(MemoryBlockReference memoryBlockReference) : base(memoryBlockReference)
    {
    }
    
    public Output(Func<MemoryBlockReference> memoryBlockReference) : base(memoryBlockReference)
    {
    }
}