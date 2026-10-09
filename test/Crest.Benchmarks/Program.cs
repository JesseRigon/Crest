using BenchmarkDotNet.Running;

namespace Crest.Benchmarks;

public static class Program
{
    public static void Main(string[] args)
    {
        BenchmarkSwitcher.FromAssembly(typeof(ShapeFactoryBenchmark).Assembly).Run(args);
    }
}
