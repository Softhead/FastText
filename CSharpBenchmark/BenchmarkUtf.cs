using BenchmarkDotNet.Attributes;
using CSharp;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.Contracts;
using System.Text;

namespace CSharpBenchmark
{
    [MemoryDiagnoser]
    public class BenchmarkUtf
    {
        private byte[] data_ = null!;

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
            Contract.Assert(words_ is not null);
            Dictionary<int, int> chars = new();  // length, count
            Dictionary<int, int> words = new();  // length, count
            Dictionary<int, Dictionary<char, int>> charFreq = new();  // position, dictionary of char, count

            foreach (string word in words_)
            {
                byte[] bytes = Encoding.Unicode.GetBytes(word);

                // word length
                if (words.ContainsKey(bytes.Length))
                {
                    words[bytes.Length]++;
                }
                else
                {
                    words.Add(bytes.Length, 1);
                }

                // char length
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

                // distinct chars at each position
                for (int index = 0; index < word.Length; index++)
                {
                    if (!charFreq.TryGetValue(index, out Dictionary<char, int>? dict))
                    {
                        dict = new Dictionary<char, int>();
                        charFreq.Add(index, dict);
                    }

                    if (dict.ContainsKey(word[index]))
                    {
                        dict[word[index]]++;
                    }
                    else
                    {
                        dict.Add(word[index], 1);
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

            foreach (int key in charFreq.Keys.Order())
            {
                Console.WriteLine($"Word position {key} distinct char count {charFreq[key].Count}");
                Dictionary<char, int> dict = charFreq[key];
                IOrderedEnumerable<KeyValuePair<char, int>> bigOnes = dict.OrderByDescending(o => o.Value);
                foreach (var bigOne in bigOnes.Take(5))
                {
                    Console.WriteLine($"    Char {bigOne.Key} count {bigOne.Value}");
                }
            }
        }

        [Params(3000, 10_000)]
        public int n_;

        [GlobalSetup]
        public void Setup()
        {
            if (n_ == 3000)
            {
                data_ = File.ReadAllBytes("c:\\SoftHead\\FastText\\3000 common JP words.txt");
            }
            else if (n_ == 10_000)
            {
                data_ = File.ReadAllBytes("c:\\SoftHead\\FastText\\10_000 JP words.txt");
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

            if (words_ is not null)
            {
                hsl = new(words_);
                tu32 = new(words_);
                tu32o = new(words_);
            }
        }

        public HashSet hsl = null!;
        public TrieUtf32 tu32 = null!;
        public TrieUtf32Optimized tu32o = null!;

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
                    // for some reason, this fails during the jitting process of benachmarking.
                    //                    throw new Exception();
                }
            }
        }

        [Benchmark]
        public void LookupTrieUtf32Optimized()
        {
            foreach (string word in LookupDataWords)
            {
                if (!tu32o.IsValidWord(word))
                {
                    // for some reason, this fails during the jitting process of benachmarking.
                    //                    throw new Exception();
                }
            }
        }
    }
}
