using BenchmarkDotNet.Attributes;
using CSharp;
using System.Text;

namespace CSharpBenchmark
{
    [MemoryDiagnoser]
    public class Benchmark
    {
        private byte[] data_;

        private Stream DataStream
        {
            get
            {
                MemoryStream s = new (data_);
                StreamWriter sw = new (s);
                return sw.BaseStream;
            }
        }

        private List<string>? words_ = null;

        private List<string> LookupDataWords
        {
            get
            {
                if (words_ == null)
                {
                    Stream s = DataStream;
                    words_ = new();
                    StringBuilder sb = new();
                    bool inWord = false;
                    int current;

                    while (true)
                    {
                        current = s.ReadByte();
                        if (current == -1)
                        {
                            break;
                        }
                        char currentChar = (char)current;

                        if (char.IsLetter(currentChar))
                        {
                            if (!inWord)
                            {
                                inWord = true;
                                sb = new();
                                sb.Append(currentChar);
                            }
                            else
                            {
                                sb.Append(currentChar);
                            }
                        }
                        else
                        {
                            if (inWord)
                            {
                                inWord = false;
                                words_.Add(sb.ToString());
                                sb.Clear();
                            }
                        }
                    }
                }

                return words_;
            }
        }

        [Params(370105)]//10, 100, 1000, 10_000, 1001, 370105)]
        public int n_;

        [GlobalSetup]
        public void Setup()
        {
            if (n_ == 1001)
            {
                data_ = File.ReadAllBytes("c:\\SoftHead\\FastText\\1000 words.txt");
            }
            else if (n_ == 370105)
            {
                data_ = File.ReadAllBytes("c:\\SoftHead\\FastText\\words_alpha.txt");
            }
            else
            {
                StringBuilder sbData = new();
                Random r = new(12345);

                for (int i = 0; i < n_; i++)
                {
                    int length = r.Next(1, 10);
                    for (int j = 0; j < length; j++)
                    {
                        char newChar = (char)('a' + r.Next(0, 25));
                        sbData.Append(newChar);
                    }
                    sbData.Append(' ');
                }

                data_ = Encoding.UTF8.GetBytes(sbData.ToString());
            }


            hsl = new(DataStream);
            ll = new(DataStream);
            ll.Complete();
            tl32 = new(DataStream);
            tl32o = new(DataStream);
            tl32o.Cleanup();
            tl32o.SetupStorage();

            if (n_ < 10_000)
                tl64o = new(DataStream);

            tl128o = new(DataStream);

            _ = LookupDataWords;
        }

        public HashSetLatin hsl;
        public ListLatin ll;
        public TrieLatin32 tl32;
        public TrieLatin32Optimized tl32o;
        public TrieLatin64Optimized tl64o;
        public TrieLatin128Optimized tl128o;


        [Benchmark]
        public void LookupHashSetLatin()
        {
            foreach (string word in LookupDataWords)
            {
                if (!hsl.IsValidWord(word))
                {
                    throw new Exception();
                }
            }
        }

        //[Benchmark]
        //public void LookupListLatin()
        //{
        //    foreach (string word in LookupDataWords)
        //    {
        //        if (!ll.IsValidWord(word))
        //        {
        //            throw new Exception();
        //        }
        //    }
        //}

        [Benchmark]
        public void LookupListLatinSorted()
        {
            foreach (string word in LookupDataWords)
            {
                if (!ll.IsValidWordSorted(word))
                {
                    throw new Exception();
                }
            }
        }

        //[Benchmark]
        //public void LookupListLatinArray()
        //{
        //    foreach (string word in LookupDataWords)
        //    {
        //        if (!ll.IsValidWordArray(word))
        //        {
        //            throw new Exception();
        //        }
        //    }
        //}

        //[Benchmark]
        //public void LookupListLatinArraySearch()
        //{
        //    foreach (string word in LookupDataWords)
        //    {
        //        if (!ll.IsValidWordArraySearch(word))
        //        {
        //            throw new Exception();
        //        }
        //    }
        //}

        [Benchmark]
        public void LookupListLatinSearch()
        {
            foreach (string word in LookupDataWords)
            {
                if (!ll.IsValidWordSearch(word))
                {
                    throw new Exception();
                }
            }
        }

        //[Benchmark]
        //public void LookupListLatinExists()
        //{
        //    foreach (string word in LookupDataWords)
        //    {
        //        if (!ll.IsValidWordExists(word))
        //        {
        //            throw new Exception();
        //        }
        //    }
        //}

        [Benchmark]
        public void LookupTrieLatin32()
        {
            foreach (string word in LookupDataWords)
            {
                if (!tl32.IsValidWord(word))
                {
                    throw new Exception();
                }
            }
        }

        [Benchmark]
        public void LookupTrieLatin32Optimized()
        {
            foreach (string word in LookupDataWords)
            {
                if (!tl32o.IsValidWordOptimized(word))
                {
                    throw new Exception();
                }
            }
        }

        //[Benchmark]
        //public void LookupTrieLatin64Optimized()
        //{
        //    if (n_ < 10_000)
        //        foreach (string word in LookupDataWords)
        //        {
        //            if (!tl64o.IsValidWord(word))
        //            {
        //                throw new Exception();
        //            }
        //        }
        //}

        [Benchmark]
        public void LookupTrieLatin128Optimized()
        {
            foreach (string word in LookupDataWords)
            {
                if (!tl128o.IsValidWord(word))
                {
                    throw new Exception();
                }
            }
        }
    }
}
