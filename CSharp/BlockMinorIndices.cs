namespace CSharp
{
    internal class BlockMinorIndices
    {
        private static List<uint[]> blocks_;
        private static int blockSize_;
        private static int BitsToShiftForBlockSize;
        private static int BlockMinorIndexBitMask;
        private int blockIndex_;
        private int minorIndex_;

        public static void Initialize(List<uint[]> blocks, int blockSize)
        {
            blocks_ = blocks;
            blockSize_ = blockSize;
            BitsToShiftForBlockSize = (int)Math.Log2(blockSize);
            BlockMinorIndexBitMask = blockSize - 1;
        }

        public BlockMinorIndices(uint address)
        {
            SetAddress(address);
        }

        public BlockMinorIndices()
        {
        }

        public void IncrementMinorIndex(int increment = 1)
        {
            minorIndex_ += increment;
            if (increment > 0 && minorIndex_ >= blockSize_)
            {
                blockIndex_++;
                minorIndex_ -= blockSize_;
            }
            else if (increment < 0 && minorIndex_ < 0)
            {
                blockIndex_--;
                minorIndex_ += blockSize_;
            }
        }

        public void SetAddress(uint currentAddress)
        {
            blockIndex_ = (int)(currentAddress >> BitsToShiftForBlockSize);
            minorIndex_ = (int)(currentAddress & BlockMinorIndexBitMask);
        }

        public uint GetAddress()
        {
            return (uint) (blockIndex_ * blockSize_ + minorIndex_);
        }

        public uint GetValue()
        {
            return blocks_[blockIndex_][minorIndex_];
        }

        public void SetValue(uint value)
        {
            blocks_[blockIndex_][minorIndex_] = value;
        }
    }
}
