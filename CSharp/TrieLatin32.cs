using System.Globalization;

namespace CSharp
{
    // A trie with 32 bit nodes.
    //
    // Each node contains:
    // MSB: 1 indicates that this is a valid stop point.
    // Lower 31 bits: the index of the next node in the trie.

    // New characters are added to the trie as a simple allocation of an additional 26 nodes.
    public class TrieLatin32
    {
        // allocate in blocks of 1024 so that we don't need to allocate memory each time a letter is added
        private const int BlockSize = 1024;
        private readonly int BitsToShiftForBlockSize =  (int)Math.Log2(BlockSize);
        private const int BlockMinorIndexBitMask = BlockSize - 1;
        private const uint StopFlagBitMask = 0x8000_0000;
        private const uint AddressBitMask = 0x7fff_ffff;
        private uint nextAvailableNode_ = 0;
        private uint maximumAvailableNode_ = 0;
        private readonly List<uint[]> blocks_ = new();
        private readonly Barrier parseComplete_;

        public TrieLatin32(Stream s)
        {
            blocks_.Add(new uint[BlockSize]);
            maximumAvailableNode_ = BlockSize;

            // allocate first 26 nodes for A-Z
            nextAvailableNode_ = 26;

            parseComplete_ = new Barrier(2);
            _ = Task.Run(async () => { await ParseStream(s).ConfigureAwait(false); })
                .ContinueWith((t) =>
                {
                    if (t.IsFaulted && t.Exception is not null) throw t.Exception;
                });
            parseComplete_.SignalAndWait();
        }

        private uint GetNextAvailableNode()
        {
            uint result = nextAvailableNode_;

            nextAvailableNode_++;
            if (nextAvailableNode_ == maximumAvailableNode_)
            {
                maximumAvailableNode_ += BlockSize;
                blocks_.Add(new uint[BlockSize]);
            }

            return result;
        }

        private async Task ParseStream(Stream s)
        {
            Memory<byte> buffer = new byte[BlockSize];
            uint currentAddress = 0;
            bool isCurrentlyInWord = false;
            char currentChar;
            int bytesRead = -1;

            while (bytesRead != 0)
            {
                bytesRead = await s.ReadAsync(buffer).ConfigureAwait(false);

                for (int i = 0; i < bytesRead; i++)
                {
                    currentChar = (char)buffer.Span[i];

                    // sequences of letters are treated as a word, and all other characters are considered whitespace
                    if (char.IsLetter(currentChar))
                    {
                        currentChar = char.ToLower(currentChar, CultureInfo.InvariantCulture);

                        if (!isCurrentlyInWord)  // start a new word
                        {
                            isCurrentlyInWord = true;
                            currentAddress = (uint)(currentChar - 'a');
                        }
                        else  // continue the current word
                        {
                            int blockIndex = (int)(currentAddress >> BitsToShiftForBlockSize);
                            int minorIndex = (int)(currentAddress & BlockMinorIndexBitMask);
                            uint currentValue = blocks_[blockIndex][minorIndex];

                            if ((currentValue & AddressBitMask) != 0)
                            {
                                currentAddress = currentValue & AddressBitMask;
                                currentAddress += (uint)(currentChar - 'a');
                            }
                            else
                            {
                                currentAddress = GetNextAvailableNode();
                                blocks_[blockIndex][minorIndex] = (currentValue & StopFlagBitMask) + currentAddress;
                                currentAddress += (uint)(currentChar - 'a');
                                for (int j = 0; j < 25; j++)
                                {
                                    GetNextAvailableNode();
                                }
                            }
                        }
                    }
                    else
                    {
                        // end the current word
                        if (isCurrentlyInWord)
                        {
                            // mark the current position as the ending for a valid word
                            int blockIndex = (int)(currentAddress >> BitsToShiftForBlockSize);
                            int minorIndex = (int)(currentAddress & BlockMinorIndexBitMask);
                            blocks_[blockIndex][minorIndex] |= StopFlagBitMask;

                            // ready for next word
                            isCurrentlyInWord = false;
                        }
                    }
                }
            }

            // end the current word
            if (isCurrentlyInWord)
            {
                // mark the current position as the ending for a valid word
                int blockIndex = (int)(currentAddress >> BitsToShiftForBlockSize);
                int minorIndex = (int)(currentAddress & BlockMinorIndexBitMask);
                blocks_[blockIndex][minorIndex] |= StopFlagBitMask;
            }

            parseComplete_.SignalAndWait();
        }

        public bool IsValidWord(ReadOnlySpan<char> word)
        {
            if (word.Length == 0 || !char.IsLetter(word[0]))
            {
                return false;
            }

            // get address for first char
            uint currentAddress = (uint)(char.ToLower(word[0], CultureInfo.InvariantCulture) - 'a');
            uint currentValue;
            int blockIndex;
            int minorIndex;

            // parse through each subsequent character
            for (int i = 1; i < word.Length; i++)
            {
                char currentChar = word[i];

                // if the character is not a letter, then the word is not in the trie
                if (!char.IsLetter(currentChar))
                {
                    return false;
                }

                // if there's no address entry at the address of the previous character, then the word is not in the trie
                blockIndex = (int)(currentAddress >> BitsToShiftForBlockSize);
                minorIndex = (int)(currentAddress & BlockMinorIndexBitMask);
                currentValue = blocks_[blockIndex][minorIndex];
                if ((currentValue & AddressBitMask) == 0)
                {
                    return false;
                }
                currentAddress = (currentValue & AddressBitMask);
                currentAddress += (uint)(char.ToLower(currentChar, CultureInfo.InvariantCulture) - 'a');
            }

            blockIndex = (int)(currentAddress >> BitsToShiftForBlockSize);
            minorIndex = (int)(currentAddress & BlockMinorIndexBitMask);
            currentValue = blocks_[blockIndex][minorIndex];

            // if the node is marked as a valid ending point for a word, then the word is in the trie
            if ((currentValue & StopFlagBitMask) != 0)
            {
                return true;
            }

            return false;
        }

        public void Stats()
        {
            int[] count = new int[26];
            int blockIndex;
            int minorIndex;
            UInt128 currentValue;
            int letterCount;
            Dictionary<int, int> usage = new();

            for (uint currentAddress = 1; currentAddress < nextAvailableNode_; currentAddress += 26)
            {
                letterCount = 0;
                for (int i = 0; i < 26; i++)
                {
                    blockIndex = (int)((currentAddress + i) >> BitsToShiftForBlockSize);
                    minorIndex = (int)((currentAddress + i) & BlockMinorIndexBitMask);
                    currentValue = blocks_[blockIndex][minorIndex];
                    if ((currentValue & AddressBitMask) == 0)
                    {
                        count[i]++;
                        letterCount++;
                    }
                }
                if (usage.ContainsKey(letterCount))
                {
                    usage[letterCount]++;
                }
                else
                {
                    usage.Add(letterCount, 1);
                }
            }

            for (int i = 0; i < 26; i++)
            {
                Console.WriteLine($"{(char)(i + 'a')}: {count[i]}");
            }

            int totalCount = 0;
            foreach (int i in usage.Keys.Order())
            {
                Console.WriteLine($"{i}: {usage[i]}");
                totalCount += usage[i];
            }
            Console.WriteLine($"Total: {totalCount}");
        }
    }
}
