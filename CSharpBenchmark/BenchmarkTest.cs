﻿using BenchmarkDotNet.Attributes;
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

        private List<string> LookupDataWords
        {
            get
            {
                Stream s = DataStream;
                List<string> words = new();
                StringBuilder sb = new();
                bool inWord = false;

                while (true)
                {
                    int current = s.ReadByte();
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
                            words.Add(sb.ToString());
                        }
                    }
                }

                return words;
            }
        }

        [Params(10, 100)]//, 1000, 10_000, 1001)]
        public int n_;

        [GlobalSetup]
        public void Setup()
        {
            if (n_ == 1001)
            {
                data_ = File.ReadAllBytes("c:\\SoftHead\\FastText\\1000 words.txt");
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
            ll.CompleteSort();
            tl32 = new(DataStream);

            if (n_ < 10_000)
                tl64o = new(DataStream);

            tl128o = new(DataStream);
        }

        HashSetLatin hsl;
        ListLatin ll;
        TrieLatin32 tl32;
        TrieLatin64Optimized tl64o;
        TrieLatin128Optimized tl128o;


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

        [Benchmark]
        public void LookupListLatin()
        {
            foreach (string word in LookupDataWords)
            {
                if (!ll.IsValidWord(word))
                {
                    throw new Exception();
                }
            }
        }

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
        public void LookupTrieLatin64Optimized()
        {
            if (n_ < 10_000)
                foreach (string word in LookupDataWords)
                {
                    if (!tl64o.IsValidWord(word))
                    {
                        throw new Exception();
                    }
                }
        }

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