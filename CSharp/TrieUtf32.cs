using System;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace CSharp
{
    // A trie with 32 bit nodes for UTF-32 strings.
    //
    // To store this and the stop bit, we can use:
    // Word 0 = the UTF-32 character
    // Word 1 = stop bit and address of next node
    //     MSB
    //     Bit 31 = stop bit = 1 if word ends here
    //     Bit 30-0 = 31 bits for address of next node
    //
    // In a sample of 3,000 common Japanese words, the character distribution is:
    // 1 = 324
    // 2 = 1450
    // 3 = 693
    // 4 = 372
    // >4 = 160
    //

    public class TrieUtf32
    {
        private const uint StopFlagBitMask = 0x8000_0000;
        private const uint AddressBitMask = 0x7fff_ffff;
        private readonly Barrier parseComplete_;

        public TrieUtf32(List<string> words)
        {
            parseComplete_ = new Barrier(2);
            _ = Task.Run(async () => { await ParseWords(words).ConfigureAwait(false); })
                .ContinueWith((t) =>
                {
                    if (t.IsFaulted && t.Exception is not null) throw t.Exception;
                });
            parseComplete_.SignalAndWait();
        }

        private class Trie32
        {
            public uint Data { get; set; } = 0;
            public bool IsValidWordEnding { get; set; } = false;
            public List<Trie32> Children { get; set; } = new();
        }

        private async Task ParseWords(List<string> words)
        {
            Trie32 root = new();
            Trie32 currentNode;
            int nodeCount = 0;

            foreach (string word in words)
            {
                currentNode = root;

                unsafe
                {
                    fixed (char* wordPtr = word)
                    {
                        uint current;
                        char* ptr = wordPtr;

                        // parse through each 32 bits of data
                        while (true)
                        {
                            current = *ptr++;
                            if (current == '\0')
                            {
                                break;
                            }
                            current <<= 16;
                            current |= *ptr++;

                            var foundNode = currentNode.Children.Where(o => o.Data == current).FirstOrDefault();

                            if (foundNode is not null)
                            {
                                currentNode = foundNode;
                            }
                            else
                            {
                                nodeCount++;
                                Trie32 newNode = new Trie32
                                {
                                    Data = current,
                                };
                                currentNode.Children.Add(newNode);
                                currentNode = newNode;
                            }
                        }

                        currentNode.IsValidWordEnding = true;
                    }
                }
            }

            // convert to flat array
            data_ = new uint[nodeCount * 4];
            PopulateNode(root, 0);

            parseComplete_.SignalAndWait();
        }

        uint[] data_;

        private uint PopulateNode(Trie32 currentNode, uint currentIndex)
        {
            uint baseIndex = currentIndex + 1;

            // fill in UTF-32 character values
            foreach (Trie32 child in currentNode.Children)
            {
                data_[currentIndex] = child.Data;
                currentIndex += 2;
            }

            // fill in pointer to children if needed, and fill in children recursively
            foreach (Trie32 child in currentNode.Children)
            {
                uint address = 0;
                if (child.IsValidWordEnding)
                {
                    address = StopFlagBitMask;
                }

                // fill in children
                if (child.Children.Count > 0)
                {
                    data_[baseIndex] = address | currentIndex;
                    currentIndex = PopulateNode(child, currentIndex);
                }
                else
                {
                    data_[baseIndex] = address;
                }

                baseIndex += 2;
            }

            // add end marker
            currentIndex += 2;

            return currentIndex;
        }

        public bool IsValidWord(string word)
        {
            if (word.Length == 0 || !char.IsLetter(word[0]))
            {
                return false;
            }

            uint currentAddress = 0;
            unsafe
            {
                fixed (char* wordPtr = word)
                {
                    uint current;
                    char* ptr = wordPtr;

                    // parse through each 32 bits of data
                    while (true)
                    {
                        current = *ptr++;
                        if (current == '\0')
                        {
                            break;
                        }
                        current <<= 16;
                        current |= *ptr++;

                        currentAddress &= AddressBitMask;

                        while (true)
                        {
                            if (data_[currentAddress] == 0)
                            {
                                return false;
                            }

                            if (data_[currentAddress] == current)
                            {
                                currentAddress = data_[currentAddress + 1];
                                break;
                            }

                            currentAddress += 2;
                        }
                    }
                }
            }

            return (currentAddress & StopFlagBitMask) != 0;
        }
    }
}
