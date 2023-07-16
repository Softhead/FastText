using System.Text;

namespace CSharp
{
    // An array of lists.

    // New characters are added to the trie as a simple allocation of an additional 26 nodes.
    public class HashSetLatin
    {
        private HashSet<string> words_;
        private readonly Barrier ParseComplete_;

        public HashSetLatin(Stream s)
        {
            words_ = new HashSet<string>();

            ParseComplete_ = new Barrier(2);
            Task.Run(async () => { await ParseStream(s); })
                .ContinueWith((t) =>
                {
                    if (t.IsFaulted) throw t.Exception;
                });
            ParseComplete_.SignalAndWait();
        }

        private async Task ParseStream(Stream s)
        {
            Memory<byte> buffer = new byte[1024];
            int currentStart = 0;
            bool isCurrentlyInWord = false;
            char currentChar;
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

                            words_.Add(word);

                            // ready for next word
                            isCurrentlyInWord = false;
                        }
                        else
                        {
                            if (previousWord is not null)
                            {
                                words_.Add(previousWord);

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
                words_.Add(previousWord);
            }

            ParseComplete_.SignalAndWait();
        }

        public bool IsValidWord(ReadOnlySpan<char> word)
        {
            if (word.Length == 0 || !char.IsLetter(word[0]))
            {
                return false;
            }

            return words_.Contains(word.ToString().ToLower());
        }
    }
}
