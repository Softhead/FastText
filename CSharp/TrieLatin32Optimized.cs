using System.Globalization;

namespace CSharp
{
    // A trie with 32 bit nodes.
    //
    // Since it was found that 95% of the 26 character tries contained 3 or fewer characters,
    // create 2 kinds of tries:  a 3 character trie and a 26 character trie.
    //
    // 3 character trie:
    // 1st node:
    //      MSB = 0 indicates that this is a 3 character trie.
    //      Next 2 bits: unused
    //      Next 3 bits: indicates that a word can stop here, in order MSB-LSB of 1, 2, 3.
    //      Next 11 bits: unused
    //      Next 15 bits:  5 bits to store the 3 letters that are represented, in order MSB-LSB of 1, 2, 3.
    //                     Set 5 bits to 11111 to indicate that value has not been set yet.
    // Next 3 nodes:
    //      MSB = 0
    //      Next 2 bits: 00 = this is the first character in the 3 character trie
    //                   01 = second
    //                   10 = third
    //      Lower 29 bits: the address of the next node in the trie.
    //
    // 26 character trie:
    // Each 26 character trie node contains:
    //      MSB = 1 indicates that this is a 26 character trie.
    //      Next 1 bit: 1 indicates that a word can stop here.
    //      Next 1 bit: not used
    //      Lower 29 bits: the address of the next node in the trie.

    public class TrieLatin32Optimized
    {
        // allocate in blocks of 1024 so that we don't need to allocate memory each time a letter is added
        private const int BlockSize = 1024;
        private const uint TrieFlagBitMask = 0x8000_0000;
        private const uint Trie26StopFlagBitMask = 0x4000_0000;
        private const uint Trie3StopFlagBitMask = 0b00011100_00000000_00000000_00000000;
        private const uint Trie3CharNumberBitMask = 0b01100000_00000000_00000000_00000000;
        private const uint AddressBitMask = 0b00011111_11111111_11111111_11111111;
        private const uint ControlBitMask = 0b11100000_00000000_00000000_00000000;
        private const int AddressBitCount = 29;
        private const int Trie3StopBitShiftCount = 26;
        private const uint CharacterBitMask = 0b11111;
        private uint nextAvailableAddress_ = 0;
        private uint maximumAvailableNode_ = 0;
        private readonly List<uint[]> blocks_ = new();
        private readonly Dictionary<uint, uint> backPointersKeyBlockAddress_ = new();
        private readonly Dictionary<uint, uint> backPointersKeyDestinationAddress_ = new();
        private readonly Queue<uint> free3Tries_ = new();
        private readonly Barrier parseComplete_;
        private uint[] storage_ = Array.Empty<uint>();

        public TrieLatin32Optimized(Stream s)
        {
            BlockMinorIndices.Initialize(blocks_, BlockSize, AddressBitMask);
            blocks_.Add(new uint[BlockSize]);
            maximumAvailableNode_ = BlockSize;

            // allocate first 26 nodes for A-Z as a 26 character trie
            for (int i = 0; i < 26; i++)
            {
                blocks_[0][i] = TrieFlagBitMask;
            }
            nextAvailableAddress_ = 26;

            parseComplete_ = new Barrier(2);
            _ = Task.Run(async () => { await ParseStream(s).ConfigureAwait(false); })
                .ContinueWith((t) =>
                {
                    if (t.IsFaulted && t.Exception is not null) throw t.Exception;
                });
            parseComplete_.SignalAndWait();
        }

        private uint GetNextAvailableAddress(uint count)
        {
            // take a free 3 character trie if possible
            if (count == 4)
            {
                if (free3Tries_.Count != 0)
                {
                    return free3Tries_.Dequeue();
                }
            }

            uint result = nextAvailableAddress_;

            nextAvailableAddress_ += count;
            if (nextAvailableAddress_ >= maximumAvailableNode_)
            {
                maximumAvailableNode_ += BlockSize;
                blocks_.Add(new uint[BlockSize]);
            }

            return result;
        }

        private async Task ParseStream(Stream s)
        {
            Memory<byte> buffer = new byte[BlockSize];
            uint currentAddress;
            uint previousAddress;
            BlockMinorIndices currentIndices = new();
            bool isCurrentlyInWord = false;
            char currentChar;
            int bytesRead = -1;
            bool isCurrent26CharTrie;

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
                            currentIndices.SetAddress(currentAddress);
                        }
                        else  // continue the current word
                        {
                            // current indices are for previous character
                            uint value = currentIndices.GetValue();
                            uint nextAddress = value & AddressBitMask;

                            // move to trie for current character
                            if (nextAddress != 0)  // already points to a trie
                            {
                                uint currentCharIndex = (uint)(currentChar - 'a');

                                // check what kind of trie we are moving to
                                currentIndices.SetAddress(nextAddress);
                                uint currentValue = currentIndices.GetValue();
                                isCurrent26CharTrie = (currentValue & TrieFlagBitMask) == TrieFlagBitMask;

                                if (isCurrent26CharTrie)
                                {
                                    // lookup current character in 26 character trie
                                    currentIndices.IncrementMinorIndex(currentCharIndex);
                                }
                                else
                                {
                                    // lookup current character in 3 character trie
                                    // get characters stored in this 3 character trie
                                    uint[] currentNodeCharIndex = new uint[3];
                                    currentNodeCharIndex[2] = currentValue & CharacterBitMask;
                                    currentValue >>= 5;
                                    currentNodeCharIndex[1] = currentValue & CharacterBitMask;
                                    currentValue >>= 5;
                                    currentNodeCharIndex[0] = currentValue & CharacterBitMask;

                                    if (currentCharIndex == currentNodeCharIndex[0])
                                    {
                                        currentIndices.IncrementMinorIndex(1);
                                    }
                                    else if (currentCharIndex == currentNodeCharIndex[1])
                                    {
                                        currentIndices.IncrementMinorIndex(2);
                                    }
                                    else if (currentCharIndex == currentNodeCharIndex[2])
                                    {
                                        currentIndices.IncrementMinorIndex(3);
                                    }
                                    else  // none of the 3 characters match
                                    {
                                        // find a free character slot
                                        uint freeIndex = 0;
                                        if (currentNodeCharIndex[1] == 0b11111)
                                        {
                                            freeIndex = 1;
                                        }
                                        else if (currentNodeCharIndex[2] == 0b11111)
                                        {
                                            freeIndex = 2;
                                        }

                                        if (freeIndex != 0)
                                        {
                                            // fill in this slot with the current character
                                            currentNodeCharIndex[freeIndex] = currentCharIndex;
                                            currentValue = currentNodeCharIndex[0];
                                            currentValue <<= 5;
                                            currentValue |= currentNodeCharIndex[1];
                                            currentValue <<= 5;
                                            currentValue |= currentNodeCharIndex[2];
                                            currentIndices.SetValue((currentIndices.GetValue() & Trie3StopFlagBitMask) | currentValue);

                                            // we are now at the slot for the newly added character
                                            currentIndices.IncrementMinorIndex(freeIndex + 1);
                                        }
                                        else  // no free slots found; expand this 3 character trie into a 26 character trie
                                        {
                                            // read the first value of the 3 character trie to get the stop bits
                                            uint stopBits = currentIndices.GetValue();
                                            stopBits &= Trie3StopFlagBitMask;
                                            stopBits >>= Trie3StopBitShiftCount;

                                            // get and remove the back pointer
                                            backPointersKeyBlockAddress_.Remove(currentIndices.GetAddress(), out uint backAddress);
                                            backPointersKeyDestinationAddress_.Remove(backAddress);

                                            // add current 3 character trie to list of free tries
                                            free3Tries_.Enqueue(currentIndices.GetAddress());

                                            // get a new address
                                            uint newAddress = GetNextAvailableAddress(26);

                                            // replace back address with the new 26 character trie address
                                            BlockMinorIndices previousIndices = new(backAddress);
                                            previousIndices.SetValue((previousIndices.GetValue() & ControlBitMask) | newAddress);

                                            // copy the 3 character trie data to the 26 character trie
                                            BlockMinorIndices copyIndices = new(newAddress);

                                            // move to slot in 26 character trie
                                            copyIndices.IncrementMinorIndex(currentNodeCharIndex[0]);
                                            currentIndices.IncrementMinorIndex();
                                            currentValue = currentIndices.GetValue() & AddressBitMask;
                                            if ((stopBits & 0b100) != 0)
                                            {
                                                currentValue |= Trie26StopFlagBitMask;
                                            }
                                            copyIndices.SetValue(currentValue);

                                            // find if there is a back pointer to this 3 character trie slot
                                            currentAddress = currentIndices.GetAddress();
                                            if (backPointersKeyDestinationAddress_.TryGetValue(currentAddress, out uint blockAddress))
                                            {
                                                uint copyAddress = copyIndices.GetAddress();
                                                backPointersKeyBlockAddress_[blockAddress] = copyAddress;
                                                backPointersKeyDestinationAddress_.Remove(currentAddress);
                                                backPointersKeyDestinationAddress_.Add(copyAddress, blockAddress);
                                            }

                                            // move back to base of 26 character trie
                                            copyIndices.DecrementMinorIndex(currentNodeCharIndex[0]);

                                            // move to slot in 26 character trie
                                            copyIndices.IncrementMinorIndex(currentNodeCharIndex[1]);
                                            currentIndices.IncrementMinorIndex();
                                            currentValue = currentIndices.GetValue() & AddressBitMask;
                                            if ((stopBits & 0b10) != 0)
                                            {
                                                currentValue |= Trie26StopFlagBitMask;
                                            }
                                            copyIndices.SetValue(currentValue);

                                            // find if there is a back pointer to this 3 character trie slot
                                            currentAddress = currentIndices.GetAddress();
                                            if (backPointersKeyDestinationAddress_.TryGetValue(currentAddress, out blockAddress))
                                            {
                                                uint copyAddress = copyIndices.GetAddress();
                                                backPointersKeyBlockAddress_[blockAddress] = copyAddress;
                                                backPointersKeyDestinationAddress_.Remove(currentAddress);
                                                backPointersKeyDestinationAddress_.Add(copyAddress, blockAddress);
                                            }

                                            // move back to base of 26 character trie
                                            copyIndices.DecrementMinorIndex(currentNodeCharIndex[1]);

                                            // move to slot in 26 character trie
                                            copyIndices.IncrementMinorIndex(currentNodeCharIndex[2]);
                                            currentIndices.IncrementMinorIndex();
                                            currentValue = currentIndices.GetValue() & AddressBitMask;
                                            if ((stopBits & 0b1) != 0)
                                            {
                                                currentValue |= Trie26StopFlagBitMask;
                                            }
                                            copyIndices.SetValue(currentValue);

                                            // find if there is a back pointer to this 3 character trie slot
                                            currentAddress = currentIndices.GetAddress();
                                            if (backPointersKeyDestinationAddress_.TryGetValue(currentAddress, out blockAddress))
                                            {
                                                uint copyAddress = copyIndices.GetAddress();
                                                backPointersKeyBlockAddress_[blockAddress] = copyAddress;
                                                backPointersKeyDestinationAddress_.Remove(currentAddress);
                                                backPointersKeyDestinationAddress_.Add(copyAddress, blockAddress);
                                            }

                                            // move back to base of 26 character trie
                                            copyIndices.DecrementMinorIndex(currentNodeCharIndex[2]);

                                            // set MSB in each address of 26 character trie
                                            // reserve next 25 addresses for use in this 26 character trie
                                            for (int count = 0; count < 26; count++)
                                            {
                                                uint newValue = copyIndices.GetValue();
                                                newValue |= TrieFlagBitMask;
                                                copyIndices.SetValue(newValue);

                                                copyIndices.IncrementMinorIndex();
                                            }

                                            // get the address of the new character within the 26 character trie
                                            currentAddress = newAddress + currentCharIndex;
                                            currentIndices.SetAddress(currentAddress);
                                        }

                                        // go on to next character in the stream
                                        continue;
                                    }
                                }
                            }
                            else  // does not point to a trie, so point to a new 3 character trie
                            {
                                uint newAddress = GetNextAvailableAddress(4);

                                // set previous value to point to new address
                                currentIndices.SetValue((currentIndices.GetValue() & ControlBitMask) | newAddress);

                                // first slot is the new character index, shifted left 10 bits to get it into the first slot
                                uint currentCharIndex = ((uint)(currentChar - 'a')) << 10;

                                // mark other slots as unused
                                currentCharIndex |= 0b11111_11111;

                                // remember previous address
                                previousAddress = currentIndices.GetAddress();
                                backPointersKeyBlockAddress_.Add(newAddress, previousAddress);
                                backPointersKeyDestinationAddress_.Add(previousAddress, newAddress);

                                // move to new address
                                currentIndices.SetAddress(newAddress);

                                // first value is the characters stored here
                                currentIndices.SetValue(currentCharIndex);

                                // second value is the first character slot, which we will initialize
                                currentIndices.IncrementMinorIndex();
                                currentIndices.SetValue(0);

                                // third value is the second character slot, which we will initialize
                                currentIndices.IncrementMinorIndex();
                                currentIndices.SetValue(0b01 << AddressBitCount);

                                // fourth value is the third character slot, which we will initialize
                                currentIndices.IncrementMinorIndex();
                                currentIndices.SetValue(0b10 << AddressBitCount);

                                // current address is now the first character slot
                                currentIndices.DecrementMinorIndex(2);
                            }
                        }
                    }
                    else
                    {
                        // end the current word
                        if (isCurrentlyInWord)
                        {
                            // mark the current position as the ending for a valid word
                            uint currentValue = currentIndices.GetValue();
                            isCurrent26CharTrie = (currentValue & TrieFlagBitMask) == TrieFlagBitMask;
                            if (isCurrent26CharTrie)
                            {
                                currentIndices.SetValue(currentIndices.GetValue() | Trie26StopFlagBitMask);
                            }
                            else
                            {
                                // find the slot number at this address
                                uint number = currentValue & Trie3CharNumberBitMask;
                                number >>= AddressBitCount;

                                // move back to the first value in this 3 character trie
                                currentIndices.DecrementMinorIndex(number + 1);

                                // get current stop bits
                                currentValue = currentIndices.GetValue();
                                uint stopBits = currentValue & Trie3StopFlagBitMask;
                                stopBits >>= Trie3StopBitShiftCount;

                                // set the stop bit for the required slot
                                stopBits |= (uint)0b1 << (int)(2 - number);
                                stopBits <<= Trie3StopBitShiftCount;
                                currentValue |= stopBits;
                                currentIndices.SetValue(currentValue);
                            }

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
                uint currentValue = currentIndices.GetValue();
                isCurrent26CharTrie = (currentValue & TrieFlagBitMask) == TrieFlagBitMask;
                if (isCurrent26CharTrie)
                {
                    currentIndices.SetValue(currentIndices.GetValue() | Trie26StopFlagBitMask);
                }
                else
                {
                    // find the slot number at this address
                    uint number = currentValue & Trie3CharNumberBitMask;
                    number >>= AddressBitCount;

                    // move back to the first value in this 3 character trie
                    currentIndices.DecrementMinorIndex(number + 1);

                    // get current stop bits
                    currentValue = currentIndices.GetValue();
                    uint stopBits = currentValue & Trie3StopFlagBitMask;
                    stopBits >>= AddressBitCount;

                    // set the stop bit for the required slot
                    stopBits |= (uint)0b1 << (int)currentValue;
                    stopBits <<= AddressBitCount;
                    currentValue |= stopBits;
                    currentIndices.SetValue(currentValue);
                }
            }

            parseComplete_.SignalAndWait();
        }

        public void Cleanup()
        {
            backPointersKeyBlockAddress_.Clear();
            backPointersKeyDestinationAddress_.Clear();
        }

        private readonly BlockMinorIndices lookupIndices_ = new();

        public bool IsValidWord(ReadOnlySpan<char> word)
        {
            int length = word.Length;
            uint currentCharIndex = word[0];
            currentCharIndex |= 0x20;  // convert to upper case

            if (length == 0 || currentCharIndex < 'a' || currentCharIndex > 'z')
            {
                return false;
            }

            uint currentAddress;
            uint currentValue;
            bool isCurrent26Chars;
            uint currentNumber;
            uint stopBits;
            uint compareBits;

            // get address for first char
            currentCharIndex -= 'a';
            lookupIndices_.SetAddress(currentCharIndex);

            // parse through each subsequent character
            for (int i = 1; i < length; i++)
            {
                currentCharIndex = word[i];
                currentCharIndex |= 0x20;  // convert to upper case

                // if the character is not a letter, then the word is not in the trie
                if (currentCharIndex < 'a' || currentCharIndex > 'z')
                {
                    return false;
                }

                // convert to a-based index
                currentCharIndex -= 'a';

                // if there's no address entry at the address of the previous character, then the word is not in the trie
                currentAddress = lookupIndices_.GetValueAsAddress();
                if (currentAddress == 0)
                {
                    return false;
                }

                currentValue = lookupIndices_.SetAddressAndGetValue(currentAddress);
                isCurrent26Chars = (currentValue & TrieFlagBitMask) == TrieFlagBitMask;

                if (isCurrent26Chars)
                {
                    // find character in 26 character trie
                    lookupIndices_.IncrementMinorIndex(currentCharIndex);
                }
                else
                {
                    // match characters stored in 3 character trie
                    if (currentCharIndex == (currentValue & CharacterBitMask))
                    {
                        lookupIndices_.IncrementMinorIndex(3);
                        continue;
                    }
                    currentValue >>= 5;
                    if (currentCharIndex == (currentValue & CharacterBitMask))
                    {
                        lookupIndices_.IncrementMinorIndex(2);
                        continue;
                    }
                    currentValue >>= 5;
                    if (currentCharIndex == (currentValue & CharacterBitMask))
                    {
                        lookupIndices_.IncrementMinorIndex(1);
                        continue;
                    }

                    return false;
                }
            }

            currentValue = lookupIndices_.GetValue();
            isCurrent26Chars = (currentValue & TrieFlagBitMask) == TrieFlagBitMask;

            // if the node is marked as a valid ending point for a word, then the word is in the trie
            if (isCurrent26Chars)
            {
                if ((currentValue & Trie26StopFlagBitMask) != 0)
                {
                    return true;
                }
            }
            else
            {
                // back track to first value in 3 char trie
                currentNumber = currentValue & Trie3CharNumberBitMask;
                currentNumber >>= AddressBitCount;
                lookupIndices_.DecrementMinorIndex(currentNumber + 1);
                currentValue = lookupIndices_.GetValue();

                stopBits = currentValue & Trie3StopFlagBitMask;
                stopBits >>= Trie3StopBitShiftCount;
                compareBits = (uint)0b100 >> (int)currentNumber;
                if ((stopBits & compareBits) != 0)
                {
                    return true;
                }
            }

            return false;
        }

        public void SetupStorage()
        {
            storage_ = new uint[nextAvailableAddress_];
            BlockMinorIndices ind = new(0);
            for (int i = 0; i < nextAvailableAddress_; i++)
            {
                storage_[i] = ind.GetValue();
                ind.IncrementMinorIndex();
            }
        }

        public bool IsValidWordOptimized(ReadOnlySpan<char> word)
        {
            int length = word.Length;
            uint currentCharIndex = word[0];
            currentCharIndex |= 0x20;  // convert to upper case

            if (length == 0 || currentCharIndex < 'a' || currentCharIndex > 'z')
            {
                return false;
            }

            uint currentAddress;
            uint currentValue;
            bool isCurrent26Chars;
            uint currentNumber;
            uint stopBits;
            uint compareBits;
            uint currentIndex;

            // get address for first char
            currentCharIndex -= 'a';
            currentIndex = currentCharIndex;

            // parse through each subsequent character
            for (int i = 1; i < length; i++)
            {
                currentCharIndex = word[i];
                currentCharIndex |= 0x20;  // convert to lower case

                // if the character is not a letter, then the word is not in the trie
                if (currentCharIndex < 'a' || currentCharIndex > 'z')
                {
                    return false;
                }

                // convert to a-based index
                currentCharIndex -= 'a';

                // if there's no address entry at the address of the previous character, then the word is not in the trie
                currentAddress = storage_[currentIndex] & AddressBitMask;
                if (currentAddress == 0)
                {
                    return false;
                }

                currentIndex = currentAddress;
                currentValue = storage_[currentIndex];
                isCurrent26Chars = (currentValue & TrieFlagBitMask) == TrieFlagBitMask;

                if (isCurrent26Chars)
                {
                    // find character in 26 character trie
                    currentIndex += currentCharIndex;
                }
                else
                {
                    // match characters stored in 3 character trie
                    if (currentCharIndex == (currentValue & CharacterBitMask))
                    {
                        currentIndex += 3;
                        continue;
                    }
                    currentValue >>= 5;
                    if (currentCharIndex == (currentValue & CharacterBitMask))
                    {
                        currentIndex += 2;
                        continue;
                    }
                    currentValue >>= 5;
                    if (currentCharIndex == (currentValue & CharacterBitMask))
                    {
                        currentIndex++;
                        continue;
                    }

                    return false;
                }
            }

            currentValue = storage_[currentIndex];
            isCurrent26Chars = (currentValue & TrieFlagBitMask) == TrieFlagBitMask;

            // if the node is marked as a valid ending point for a word, then the word is in the trie
            if (isCurrent26Chars)
            {
                if ((currentValue & Trie26StopFlagBitMask) != 0)
                {
                    return true;
                }
            }
            else
            {
                // back track to first value in 3 char trie
                currentNumber = currentValue & Trie3CharNumberBitMask;
                currentNumber >>= AddressBitCount;
                currentIndex -= currentNumber + 1;
                currentValue = storage_[currentIndex];

                stopBits = currentValue & Trie3StopFlagBitMask;
                stopBits >>= Trie3StopBitShiftCount;
                compareBits = (uint)0b100 >> (int)currentNumber;
                if ((stopBits & compareBits) != 0)
                {
                    return true;
                }
            }

            return false;
        }

        public void Stats()
        {
            int[] count26 = new int[26];
            int[] count3 = new int[3];
            int total26 = 0;
            int totalCount = 0;

            for (uint currentAddress = 0; currentAddress < nextAvailableAddress_; )
            {
                totalCount++;
                BlockMinorIndices i = new(currentAddress);
                uint value = i.GetValue();
                bool isCurrent26Chars = (value & TrieFlagBitMask) == TrieFlagBitMask;

                if (isCurrent26Chars)
                {
                    currentAddress += 26;
                    total26++;
                    int count = 0;
                    for (uint j = 0; j < 26; j++)
                    {
                        if ((i.GetValue() & AddressBitMask) != 0)
                        {
                            count++;
                        }
                        i.IncrementMinorIndex(j);
                    }

                    count26[count]++;
                }
                else
                {
                    currentAddress += 4;
                    int count = 0;
                    if ((value & CharacterBitMask) != 0b11111)
                    {
                        count++;
                    }
                    value >>= 5;
                    if ((value & CharacterBitMask) != 0b11111)
                    {
                        count++;
                    }
                    value >>= 5;
                    if ((value & CharacterBitMask) != 0b11111)
                    {
                        count++;
                    }

                    count3[count - 1]++;
                }
            }

            for (int i = 0; i < 26; i++)
            {
                Console.WriteLine($"{i}: {count26[i]}");
            }
            for (int i = 0; i < 3; i++)
            {
                Console.WriteLine($"{i}: {count3[i]}");
            }


            Console.WriteLine($"26 char tries: {total26}");
            Console.WriteLine($"Total: {totalCount}");
        }
    }
}
