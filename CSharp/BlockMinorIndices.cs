using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;

namespace CSharp
{
    internal class BlockMinorIndices
    {
        private static List<uint[]> blocks_ = null!;
        private static int blockSize_;
        private static int BitsToShiftForBlockSize;
        private static int BlockMinorIndexBitMask;
        private static uint AddressBitMask;
        private int blockIndex_;
        private int minorIndex_;

        public static void Initialize(List<uint[]> blocks, int blockSize, uint addressBitMask)
        {
            Contract.Assert(blocks is not null);

            blocks_ = blocks;
            blockSize_ = blockSize;
            BitsToShiftForBlockSize = (int)Math.Log2(blockSize);
            BlockMinorIndexBitMask = blockSize - 1;
            AddressBitMask = addressBitMask;
        }

        public BlockMinorIndices(uint address)
        {
            SetAddress(address);
        }

        public BlockMinorIndices()
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void IncrementMinorIndex(uint increment = 1)
        {
            minorIndex_ += (int)increment;
            if (minorIndex_ >= blockSize_)
            {
                blockIndex_++;
                minorIndex_ -= blockSize_;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DecrementMinorIndex(uint decrement = 1)
        {
            minorIndex_ -= (int)decrement;
            if (minorIndex_ < 0)
            {
                blockIndex_--;
                minorIndex_ += blockSize_;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetAddress(uint currentAddress)
        {
            blockIndex_ = (int)(currentAddress >> BitsToShiftForBlockSize);
            minorIndex_ = (int)(currentAddress & BlockMinorIndexBitMask);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint SetAddressAndGetValue(uint currentAddress)
        {
            blockIndex_ = (int)(currentAddress >> BitsToShiftForBlockSize);
            minorIndex_ = (int)(currentAddress & BlockMinorIndexBitMask);
            return blocks_[blockIndex_][minorIndex_];
        }

        public uint GetAddress()
        {
            return (uint)(blockIndex_ * blockSize_ + minorIndex_);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint GetValue()
        {
            return blocks_[blockIndex_][minorIndex_];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint GetValueAsAddress()
        {
            return blocks_[blockIndex_][minorIndex_] & AddressBitMask;
        }

        public void SetValue(uint value)
        {
            blocks_[blockIndex_][minorIndex_] = value;
        }
    }
}
