using System.Globalization;
using System.Runtime.CompilerServices;

namespace CSharp
{
    // A trie with 64 bit nodes, optimized.
    //
    // Each 64 bit node contains:
    //
    // MSB
    // 4 bits: section and stop bits
    // 15 bits: the address of the next node in the trie, for section 0.
    // 15 bits: the address of the next node in the trie, for section 1.
    // 15 bits: the address of the next node in the trie, for section 2.
    // 15 bits: the address of the next node in the trie, for section 3.
    // LSB
    //
    // |-------------------------64 bits----------------------------------------|
    // |-4b-||----15 bits----||----15 bits----||----15 bits----||----15 bits----|
    //
    // 64 bit values come in groups of 4, 5, 6, or 11 values.  See the sections
    // and the letters in each section below.
    //
    // For each group, the top 4 bits in the first value are the section:
    //   0000 = section 0
    //   0001 = section 1
    //   0010 = section 2
    //   0011 = section 3
    //
    // The top 4 bits in the remaining values contain the stop bits:
    //   Section 0:
    //     1  E A R I
    //     2  - - - -
    //     3  - - - -
    //
    //   Section 1:
    //     1  O T N S
    //     2  L - - -
    //     3  - - - -
    //     4  - - - -
    //
    //   Section 2:
    //     1  C U D P
    //     2  M H - -
    //     3  - - - -
    //     4  - - - -
    //     5  - - - -
    //
    //   Section 3:
    //     1  G B F Y
    //     2  W K V X
    //     3  Z J Q -
    //     4  - - - -
    //     5  - - - -
    //     6  - - - -
    //     7  - - - -
    //     8  - - - -
    //     9  - - - -
    //     10 - - - -
    //

    // New characters are added to the trie only when need, in 4 sections.
    // The sections depend upon the relative frequency of character within English words.
    // The first section should be the hottest block (ie, the most often used letters) and should be as small as possible.
    // Additional sections should not be used as much, so may contain more letters.

    // Distribution analysis of letters, based upon an entire dictionary:
    //   percentage proportion
    //     -------- ----- 
    // E   11.1607%	56.88	    M	3.0129%	15.36
    // A	8.4966%	43.31	    H	3.0034%	15.31
    // R	7.5809%	38.64	    G	2.4705%	12.59
    // I	7.5448%	38.45	    B	2.0720%	10.56
    // O	7.1635%	36.51	    F	1.8121%	9.24
    // T	6.9509%	35.43	    Y	1.7779%	9.06
    // N	6.6544%	33.92	    W	1.2899%	6.57
    // S	5.7351%	29.23	    K	1.1016%	5.61
    // L	5.4893%	27.98	    V	1.0074%	5.13
    // C	4.5388%	23.13	    X	0.2902%	1.48
    // U	3.6308%	18.51	    Z	0.2722%	1.39
    // D	3.3844%	17.25	    J	0.1965%	1.00
    // P	3.1671%	16.14	    Q	0.1962%	1

    // Sections we will use:
    //    Letters              Percentage  Relative Weight
    //    -------              ----------  ---------------
    // 0  E A R I                 35%          1.39
    // 1  O T N S L               32%          1.60
    // 2  C U D P M H             23%          1.39
    // 3  G B F Y W K V X Z J Q   10%          1.20
    //
    // Relative Weight = Percentage * Letters count

    public class TrieLatin64Optimized
    {
        // allocate in blocks of 1024 nodes so that we don't need to allocate memory each time a letter is added

        // block operations
        private const int BlockSize = 1024;
        private readonly int BitsToShiftForBlockSize = (int)Math.Log2(BlockSize);
        private const uint BlockMinorIndexBitMask = BlockSize - 1;
        private const uint MaximumAddress = (1 << 15) - 1;

        // value bits operations
        private const int BitsToShiftSectionStopBits = 64 - 4;
        private static readonly int [] BitsToShiftAddress =
            {
                45,
                30,
                15,
                0
            };
        private const ulong AddressBitMask = (1 << 15) - 1;
        private const ulong SectionStopBitsBitMask = (1 << 4) - 1;

        // in place bit masks
        private const ulong SectionStopBitsInPlaceBitMask = ulong.MaxValue - (SectionStopBitsBitMask << BitsToShiftSectionStopBits);
        private static readonly ulong [] AddressInPlaceBitMask =
            {
                ulong.MaxValue - (((ulong)MaximumAddress) << 45),
                ulong.MaxValue - (((ulong)MaximumAddress) << 30),
                ulong.MaxValue - (((ulong)MaximumAddress) << 15),
                ulong.MaxValue - ((ulong)MaximumAddress)
            };

        // letter map to section that letter is in
        private static readonly int[] LetterToSectionMap = new int[] 
            { 
                0, 3, 2, 2, 0,  // A-E
                3, 3, 2, 0, 3,  // F-J
                3, 1, 2, 1, 1,  // K-O
                2, 3, 0, 1, 1,  // P-T
                2, 3, 3, 3, 3,  // U-Y
                3               // Z
            };

        // section map to space required for that section
        private static readonly int[] SectionToSpaceMap = new int[] { 4, 5, 6, 11 };

        // letter map to index in it's section
        private static readonly int[] LetterToIndexMap = new int[]
            {
                1, 1, 0, 2, 0,  // A-E
                2, 0, 5, 3, 9,  // F-J
                5, 4, 4, 2, 0,  // K-O
                3, 10, 2, 3, 1, // P-T
                1, 6, 4, 7, 3,  // U-Y
                8               // Z
            };

        // other
        private uint nextAvailableAddress_ = 0;
        private uint maximumAvailableAddress_ = 0;
        private readonly List<ulong[]> blocks_ = new();
        private readonly Barrier parseComplete_;

        private uint GetNextAvailableAddress()
        {
            uint result = nextAvailableAddress_;

            nextAvailableAddress_++;
            if (nextAvailableAddress_ > maximumAvailableAddress_)
            {
                maximumAvailableAddress_ += BlockSize;
                blocks_.Add(new ulong[BlockSize]);
            }

            return result;
        }

        public TrieLatin64Optimized()
        {
            parseComplete_ = new Barrier(1);
        }

        public TrieLatin64Optimized(Stream s)
        {
            blocks_.Add(new ulong[BlockSize]);
            maximumAvailableAddress_ = BlockSize - 1;

            // Allocate 1 node to start;  this node is a placeholder that only contains addresses
            // and doesn't represent a character.
            nextAvailableAddress_ = 1;

            parseComplete_ = new Barrier(2);
            _ = Task.Run(async () => { await ParseStream(s).ConfigureAwait(false); })
                .ContinueWith((t) =>
                {
                    if (t.IsFaulted && t.Exception is not null) throw t.Exception;
                });
            parseComplete_.SignalAndWait();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetSection(char c)
        {
            return LetterToSectionMap[c - 'a'];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetIndex(char c)
        {
            return LetterToIndexMap[c - 'a'];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetSpace(int section)
        {
            return SectionToSpaceMap[section];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint GetAddressBySection(ulong value, int section)
        {
            return (uint)((value >> BitsToShiftAddress[section]) & AddressBitMask);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong SetAddressBySection(ulong value, uint address, int section)
        {
            if (address > MaximumAddress)
            {
                throw new Exception("Maximum address exceeded.");
            }

            ulong tempBits;

            tempBits = ((ulong)address) << BitsToShiftAddress[section];
            value &= AddressInPlaceBitMask[section];
            return value | tempBits;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint GetSectionStopBitsFromValue(ulong value)
        {
            return (uint)(value >> BitsToShiftSectionStopBits);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong SetSectionStopBitsInValue(ulong value, uint sectionStopBits)
        {
            ulong tempBits = ((ulong)sectionStopBits) << BitsToShiftSectionStopBits;
            value &= SectionStopBitsInPlaceBitMask;
            return value | tempBits;
        }

        private async Task ParseStream(Stream s)
        {
            Memory<byte> buffer = new byte[1024];
            uint currentAddress = 0;
            bool isCurrentlyInWord = false;
            ulong currentValue;
            char currentChar = '\0';
            char previousChar;
            int bytesRead = -1;

            while (bytesRead != 0)
            {
                bytesRead = await s.ReadAsync(buffer).ConfigureAwait(false);

                for (int i = 0; i < bytesRead; i++)
                {
                    previousChar = currentChar;
                    currentChar = char.ToLower((char)buffer.Span[i], CultureInfo.InvariantCulture);

                    // sequences of letters are treated as a word, and all other characters are considered whitespace
                    if (char.IsLetter(currentChar))
                    {
                        int currentSection = GetSection(currentChar);

                        int currentSpace;
                        if (!isCurrentlyInWord)  // start a new word
                        {
                            isCurrentlyInWord = true;
                            currentSpace = GetSpace(currentSection);

                            // first node is a placeholder, so check if our character's address has been filled in
                            currentValue = blocks_[0][0];
                            currentAddress = GetAddressBySection(currentValue, currentSection);

                            if (currentAddress == 0)
                            {
                                // initialize new block
                                currentAddress = GetNextAvailableAddress();
                                int newBlockIndex = (int)(currentAddress >> BitsToShiftForBlockSize);
                                int newMinorIndex = (int)(currentAddress & BlockMinorIndexBitMask);
                                blocks_[newBlockIndex][newMinorIndex] = ((ulong)currentSection) << BitsToShiftSectionStopBits;

                                // reserve space for values in this section
                                for (int j = 1; j < currentSpace; j++)
                                {
                                    GetNextAvailableAddress();
                                }

                                // fill in address in placeholder block
                                blocks_[0][0] = SetAddressBySection(currentValue, currentAddress, currentSection);
                            }
                        }
                        else  // continue the current word
                        {
                            int blockIndex = (int)(currentAddress >> BitsToShiftForBlockSize);
                            int minorIndex = (int)(currentAddress & BlockMinorIndexBitMask);
                            currentValue = blocks_[blockIndex][minorIndex];
                            currentAddress = GetAddressBySection(currentValue, currentSection);

                            if (currentAddress == 0)
                            {
                                // initialize new block
                                currentAddress = GetNextAvailableAddress();
                                int newBlockIndex = (int)(currentAddress >> BitsToShiftForBlockSize);
                                int newMinorIndex = (int)(currentAddress & BlockMinorIndexBitMask);
                                blocks_[newBlockIndex][newMinorIndex] = SetSectionStopBitsInValue(0, (uint)currentSection);

                                // reserve space for values in this section
                                currentSpace = SectionToSpaceMap[currentSection];
                                for (int j = 1; j < currentSpace; j++)
                                {
                                    GetNextAvailableAddress();
                                }

                                // fill in address in preceding block
                                blocks_[blockIndex][minorIndex] = SetAddressBySection(currentValue, currentAddress, currentSection);
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

                            int currentIndex = GetIndex(previousChar);
                            minorIndex += 1 + currentIndex / 4;
                            if (minorIndex >= BlockSize)
                            {
                                blockIndex++;
                                minorIndex -= BlockSize;
                            }
                            currentValue = blocks_[blockIndex][minorIndex];

                            uint stopBits = GetSectionStopBitsFromValue(currentValue);
                            currentIndex %= 4;
                            stopBits |= (uint)(1 << currentIndex);

                            currentValue = SetSectionStopBitsInValue(currentValue, stopBits);
                            blocks_[blockIndex][minorIndex] = currentValue;

                            // ready for next word
                            currentAddress = 0;
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

                int currentIndex = GetIndex(currentChar);
                minorIndex += 1 + currentIndex / 4;
                if (minorIndex >= BlockSize)
                {
                    blockIndex++;
                    minorIndex -= BlockSize;
                }
                currentValue = blocks_[blockIndex][minorIndex];

                uint stopBits = GetSectionStopBitsFromValue(currentValue);
                currentIndex %= 4;
                stopBits |= (uint)(1 << currentIndex);

                currentValue = SetSectionStopBitsInValue(currentValue, stopBits);
                blocks_[blockIndex][minorIndex] = currentValue;
            }

            parseComplete_.SignalAndWait();
        }

        public bool IsValidWord(ReadOnlySpan<char> word)
        {
            if (word.Length == 0)
            {
                return false;
            }

            ulong currentValue;
            uint currentAddress = 0;
            char currentChar = '\0';
            int blockIndex;
            int minorIndex;
            for (int i = 0; i < word.Length; i++)
            {
                currentChar = char.ToLower(word[i], CultureInfo.InvariantCulture);
                blockIndex = (int)(currentAddress >> BitsToShiftForBlockSize);
                minorIndex = (int)(currentAddress & BlockMinorIndexBitMask);
                currentValue = blocks_[blockIndex][minorIndex];
                int currentSection = GetSection(currentChar);
                currentAddress = GetAddressBySection(currentValue, currentSection);
                if (currentAddress == 0)
                {
                    return false;
                }
            }

            blockIndex = (int)(currentAddress >> BitsToShiftForBlockSize);
            minorIndex = (int)(currentAddress & BlockMinorIndexBitMask);
            int currentIndex = GetIndex(currentChar);
            minorIndex += 1 + currentIndex / 4;
            if (minorIndex >= BlockSize)
            {
                blockIndex++;
                minorIndex -= BlockSize;
            }
            currentValue = blocks_[blockIndex][minorIndex];
            uint stopBits = GetSectionStopBitsFromValue(currentValue);
            currentIndex %= 4;
            if ((stopBits & (1 << currentIndex)) > 0)
            {
                return true;
            }

            return false;
        }
    }
}
