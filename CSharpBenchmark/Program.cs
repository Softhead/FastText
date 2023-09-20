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
            //b.n_ = 3000;
            //b.Setup();
            //b.Stats();
            //b.LookupTrieUtf32();
            BenchmarkRunner.Run<BenchmarkUtf>();
        }
    }
}