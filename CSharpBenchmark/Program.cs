using BenchmarkDotNet.Running;
using CSharp;
using System.Text;

namespace CSharpBenchmark
{
    internal class Program
    {
        static void Main(string[] args)
        {
            //BenchmarkUtf b = new();
            //b.n_ = 10_000;
            //b.Setup();
            //b.Stats();
            //b.LookupTrieUtf32Optimized();
            BenchmarkRunner.Run<BenchmarkUtf>();
        }
    }
}