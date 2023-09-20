using BenchmarkDotNet.Attributes;
using CSharp;
using System.Text;

namespace CSharpBenchmark
{
    [MemoryDiagnoser]
    public class BenchmarkUtf
    {
        private byte[] data_;

        private Stream DataStream
        {
            get
            {
                MemoryStream s = new(data_);
                StreamWriter sw = new(s);
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
                    byte[] current = new byte[32];
                    int currentIndex = 0;
                    bool isStartOfFile = true;

                    while (true)
                    {
                        int temp = s.ReadByte();
                        if (temp == -1)
                        {
                            break;
                        }
                        current[currentIndex++] = (byte)temp;
                        temp = s.ReadByte();
                        if (temp == -1)
                        {
                            break;
                        }
                        current[currentIndex++] = (byte)temp;

                        if (isStartOfFile)
                        {
                            // check for utf-16 preamble bytes 0xfffe (little endian)
                            isStartOfFile = false;
                            if (current[0] == 0xff && current[1] == 0xfe)
                            {
                                currentIndex = 0;
                                continue;
                            }
                        }

                        // check for end of word with bytes 0x0d0a
                        if (currentIndex < 4 || !(current[currentIndex - 4] == 0x0d && current[currentIndex - 2] == 0x0a))
                        {
                            continue;
                        }

                        words_.Add(Encoding.Unicode.GetString(current, 0, currentIndex - 4));
                        currentIndex = 0;
                    }
                }

                return words_;
            }
        }

        public void Stats()
        {
            Dictionary<int, int> chars = new();
            Dictionary<int, int> words = new();

            foreach (string word in words_)
            {
                byte[] bytes = Encoding.Unicode.GetBytes(word);
                if (words.ContainsKey(bytes.Length))
                {
                    words[bytes.Length]++;
                }
                else
                {
                    words.Add(bytes.Length, 1);
                }

                for (int index = 0; index < word.Length; index++)
                {
                    bytes = Encoding.Unicode.GetBytes(word, index, 1);
                    if (chars.ContainsKey(bytes.Length))
                    {
                        chars[bytes.Length]++;
                    }
                    else
                    {
                        chars.Add(bytes.Length, 1);
                    }
                }
            }

            foreach (int key in chars.Keys.Order())
            {
                Console.WriteLine($"Char length {key} count {chars[key]}");
            }

            foreach (int key in words.Keys.Order())
            {
                Console.WriteLine($"Word length {key} count {words[key]}");
            }
        }

        [Params(3000)]//10, 100, 1000, 10_000)]
        public int n_;

        [GlobalSetup]
        public void Setup()
        {
            if (n_ == 3000)
            {
                data_ = File.ReadAllBytes("c:\\SoftHead\\FastText\\3000 common JP words.txt");
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

            _ = LookupDataWords;

            hsl = new(words_);
            tu32 = new(words_);
        }

        public HashSet hsl;
        public TrieUtf32 tu32;

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
        public void LookupTrieUtf32()
        {
            foreach (string word in LookupDataWords)
            {
                if (!tu32.IsValidWord(word))
                {
                    throw new Exception();
                }
            }
        }
    }
}
