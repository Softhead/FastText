using System.Text;

namespace CSharp
{
    // An array of lists.

    // New characters are added to the trie as a simple allocation of an additional 26 nodes.
    public class ListLatin
    {
        private List<List<string>> words_;
        private readonly Barrier ParseComplete_;

        public ListLatin(Stream s)
        {
            words_ = new List<List<string>>();

            ParseComplete_ = new Barrier(2);
            Task.Run(async () => { await ParseStream(s); });
            ParseComplete_.SignalAndWait();
        }

        private async Task ParseStream(Stream s)
        {
            Memory<byte> buffer = new byte[1024];
            int currentStart = 0;
            bool isCurrentlyInWord = false;
            char currentChar;
            Dictionary<int, List<string>> lengthAndList = new();
            string? previousWord = null;
            int bytesRead = -1;

            while (bytesRead != 0)
            {
                bytesRead = await s.ReadAsync(buffer);
                for (int i = 0; i < bytesRead; i++)
                {
                    currentChar = (char)buffer.Span[i];

                    // sequences of letters are treated as a word, and all other characters are considered whitespace
                    if (char.IsLetter(currentChar))
                    {
                        if (isCurrentlyInWord)
                        {
                            // check for end of buffer
                            if (i == bytesRead - 1)
                            {
                                previousWord = Encoding.UTF8.GetString(buffer[currentStart..bytesRead].ToArray()).ToLower();
                                currentStart = bytesRead;
                                isCurrentlyInWord = false;
                            }
                        }
                        else
                        {
                            // start a new word
                            isCurrentlyInWord = true;
                            currentStart = i;
                        }
                    }
                    else
                    {
                        // end the current word
                        if (isCurrentlyInWord)
                        {
                            string word = Encoding.UTF8.GetString(buffer[currentStart..i].ToArray()).ToLower();

                            if (previousWord is not null)
                            {
                                word = previousWord + word;
                                previousWord = null;
                            }

                            if (!lengthAndList.TryGetValue(word.Length, out List<string>? list))
                            {
                                list = new List<string>();
                                lengthAndList.Add(word.Length, list);
                            }
                            list.Add(word);

                            // ready for next word
                            isCurrentlyInWord = false;
                        }
                        else
                        {
                            if (previousWord is not null)
                            {
                                if (!lengthAndList.TryGetValue(previousWord.Length, out List<string>? list))
                                {
                                    list = new List<string>();
                                    lengthAndList.Add(previousWord.Length, list);
                                }
                                list.Add(previousWord);

                                previousWord = null;
                            }
                        }
                    }
                }

                if (isCurrentlyInWord)
                {
                    previousWord = Encoding.UTF8.GetString(buffer[currentStart..bytesRead].ToArray()).ToLower();
                    currentStart = 0;
                }
            }

            if (previousWord is not null)
            {
                if (!lengthAndList.TryGetValue(previousWord.Length, out List<string>? list))
                {
                    list = new List<string>();
                    lengthAndList.Add(previousWord.Length, list);
                }
                list.Add(previousWord);
            }

            if (lengthAndList.Count > 0)
            {
                int maxLength = lengthAndList.Select(o => o.Key).Max();
                for (int i = 0; i <= maxLength; i++)
                {
                    if (lengthAndList.TryGetValue(i, out List<string>? list))
                    {
                        words_.Add(list);
                    }
                    else
                    {
                        words_.Add(new List<string>());
                    }
                }
            }

            ParseComplete_.SignalAndWait();
        }

        public bool IsValidWord(ReadOnlySpan<char> word)
        {
            if (word.Length == 0 || !char.IsLetter(word[0]))
            {
                return false;
            }

            if (word.Length + 1 > words_.Count)
            {
                return false;
            }

            List<string> list = words_[word.Length];
            return list.Contains(word.ToString().ToLower());
        }

        public void CompleteSort()
        {
            if (!hasSorted_)
            {
                hasSorted_ = true;
                for (int i = 0; i < words_.Count; i++)
                {
                    words_[i].Sort();
                }
            }
        }

        private bool hasSorted_ = false;
        public bool IsValidWordSorted(ReadOnlySpan<char> word)
        {
            if (word.Length == 0 || !char.IsLetter(word[0]))
            {
                return false;
            }

            if (word.Length + 1 > words_.Count)
            {
                return false;
            }

            CompleteSort();

            List<string> list = words_[word.Length];
            string value = word.ToString().ToLower();
            int currentIndex = 0;
            bool doContinue = false;
            for (int i = 0; i < value.Length; i++)
            {
                char currentChar = list[currentIndex][i];
                if (currentChar == value[i])
                {
                    continue;
                }

                if (currentChar > value[i])
                {
                    return false;
                }

                while (++currentIndex < list.Count)
                {
                    currentChar = list[currentIndex][i];

                    if (currentChar == value[i])
                    {
                        doContinue = true;
                        break;
                    }

                    if (currentChar > value[i])
                    {
                        return false;
                    }
                }

                if (doContinue)
                {
                    doContinue = false;
                    continue;
                }

                return false;
            }

            return true;
        }
    }
}
