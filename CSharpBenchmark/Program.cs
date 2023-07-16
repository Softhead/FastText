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
            //b.n_ = 10;
            //b.Setup();
            //b.LookupTrieLatin128Optimized();

            //string data = "b mtuesgm c";
            //MemoryStream ms = new(Encoding.UTF8.GetBytes(data));
            //TrieLatin64Optimized tl64o = new(ms);
            //tl64o.IsValidWord("a");
            //tl64o.IsValidWord("mtuesgm");

            BenchmarkRunner.Run<Benchmark>();
        }
    }
}