using System.Collections.Immutable;
using System.Net;

namespace CSharp
{
    // A trie with 32 bit nodes for UTF-16 strings.
    //
    // To store this and the stop bit, we can use:
    // Word 0 = count of children
    // Word 1 to 1+N = two UTF-16 characters for N children, sorted
    // Word 2+N to 2+2N = stop bit and address of next node for N children
    //      stop bit and address of next node
    //      MSB
    //      Bit 31 = stop bit = 1 if word ends here
    //      Bit 30-0 = 31 bits for address of next node
    //

    public class TrieUtf32Optimized
    {
        private const uint StopFlagBitMask = 0x8000_0000;
        private const uint AddressBitMask = 0x7fff_ffff;

        public TrieUtf32Optimized(List<string> words)
        {
            ParseWords(words);
        }

        private class Trie32
        {
            public uint Data { get; set; } = 0;
            public bool IsValidWordEnding { get; set; } = false;
            public List<Trie32> Children { get; set; } = new();
        }

        private void ParseWords(List<string> words)
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

            int count = 0;
            Queue<Trie32> children = new();
            children.Enqueue(root);
            while (children.Count > 0)
            {
                Trie32 child = children.Dequeue();
                count++;
                count += child.Children.Count;
                foreach (Trie32 childNode in child.Children)
                {
                    if (childNode.Children.Count > 0)
                    {
                        children.Enqueue(childNode);
                    }
                }
            }

            // convert to flat array
            data_ = new uint[nodeCount + count];
            childQueue_.Enqueue((0, root));
            PopulateNode(0);
        }

        public uint[] data_ = Array.Empty<uint>();
        private Queue<(uint, Trie32)> childQueue_ = new();

        private void PopulateNode(uint currentIndex)
        {
            while (childQueue_.Count > 0)
            {
                (uint dequeuedChildIndex, Trie32 dequeuedChild) = childQueue_.Dequeue();

                if (dequeuedChildIndex != 0)
                {
                    uint address = 0;
                    if (dequeuedChild.IsValidWordEnding)
                    {
                        address = StopFlagBitMask;
                    }

                    data_[dequeuedChildIndex] = address | currentIndex;
                }

                if (dequeuedChild.Children.Count > 0)
                {
                    // fill count of children
                    data_[currentIndex++] = (uint)dequeuedChild.Children.Count;

                    // sort child values
                    IOrderedEnumerable<Trie32> sortedChildren = dequeuedChild.Children.OrderBy(o => o.Data);

                    // fill in UTF-16 character pair values
                    foreach (Trie32 child in sortedChildren)
                    {
                        data_[currentIndex++] = child.Data;
                    }

                    foreach (Trie32 child in sortedChildren)
                    {
                        childQueue_.Enqueue((currentIndex++, child));
                    }
                }
            }
        }

        public bool IsValidWord(string word)
        {
            if (word.Length == 0)
            {
                return false;
            }

            uint currentAddress = 0;
            unsafe
            {
                fixed (char* wordPtr = word)
                {
                    uint currentValue;
                    char* ptr = wordPtr;

                    // parse through each 32 bits of data
                    while (true)
                    {
                        currentValue = *ptr++;
                        if (currentValue == '\0')
                        {
                            break;
                        }
                        currentValue <<= 16;
                        currentValue |= *ptr++;

                        currentAddress &= AddressBitMask;

                        uint childCount = data_[currentAddress++] & AddressBitMask;
                        if (childCount == 0)  // no children
                        {
                            return false;
                        }

                        // search children
                        int foundIndex = data_.AsSpan().Slice((int)currentAddress, (int)childCount).BinarySearch(currentValue);

                        // not found
                        if (foundIndex < 0)
                        {
                            return false;
                        }

                        int foundAddress = (int)currentAddress + (int)childCount + foundIndex;
                        currentAddress = data_.AsSpan().Slice(foundAddress, 1)[0];
                    }
                }
            }

            return (currentAddress & StopFlagBitMask) != 0;
        }
    }
}
