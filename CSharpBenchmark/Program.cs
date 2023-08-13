using BenchmarkDotNet.Running;
using CSharp;
using System.Text;

namespace CSharpBenchmark
{
    internal class Program
    {
        static void Main(string[] args)
        {
            //System.Threading.Thread.Sleep(10000);
            //Benchmark b = new();
            //b.n_ = 10000;
            //b.Setup();
            //b.LookupListLatin();

            BenchmarkRunner.Run<Benchmark>();
        }
    }
}