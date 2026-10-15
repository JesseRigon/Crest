using BenchmarkDotNet.Running;

namespace Grove.Benchmarks;

public static class Program
{
    public static void Main(string[] args)
    {
        BenchmarkSwitcher.FromAssembly(typeof(ShapeFactoryBenchmark).Assembly).Run(args);
    }
}
