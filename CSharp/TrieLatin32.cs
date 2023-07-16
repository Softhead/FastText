namespace CSharp
{
    // A trie with 32 bit nodes.
    //
    // Each node contains:
    // MSB: 1 indicates that this is a valid stop point.
    // Lower 3 bits: the index of the next node in the trie.

    // New characters are added to the trie as a simple allocation of an additional 26 nodes.
    public class TrieLatin32
    {
        // allocate in blocks of 1024 so that we don't need to allocate memory each time a letter is added
        private const int BlockSize = 1024;
        private readonly int BitsToShiftForBlockSize = (int)Math.Log2(BlockSize);
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
            Task.Run(async () => { await ParseStream(s); });
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
                bytesRead = await s.ReadAsync(buffer);

                for (int i = 0; i < bytesRead; i++)
                {
                    currentChar = (char)buffer.Span[i];

                    // sequences of letters are treated as a word, and all other characters are considered whitespace
                    if (char.IsLetter(currentChar))
                    {
                        currentChar = char.ToLower(currentChar);

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
            uint currentAddress = (uint)(char.ToLower(word[0]) - 'a');
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
                currentAddress += (uint)(char.ToLower(currentChar) - 'a');
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
    }
}
