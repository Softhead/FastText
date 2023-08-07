using BenchmarkDotNet.Running;
using CSharp;
using System.Text;

namespace CSharpBenchmark
{
    internal class Program
    {
        static void Main(string[] args)
        {
            //Benchmark b = new();
            //b.n_ = 1000;
            //b.Setup();
            //b.LookupTrieLatin32Optimized();

            BenchmarkRunner.Run<Benchmark>();
        }
    }
}