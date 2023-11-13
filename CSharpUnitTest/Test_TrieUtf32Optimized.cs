using CSharp;

namespace CSharpUnitTest
{
    [TestClass]
    public class Test_TrieUtf32Optimized
    {
        [TestMethod]
        public void Storage1()
        {
            // arrange
            var data = new List<string> { "\u1234\u5678", "\u5678\u9ABC" };

            // act
            var trie = new TrieUtf32Optimized(data);

            // assert
            Test_Assert(new uint[] { 2, 0x12345678, 0x56789ABC, 0x80000005, 0x80000005 }, trie.data_);
        }

        [TestMethod]
        public void Storage2()
        {
            // arrange
            var data = new List<string> { "\u0001\u0001\u0001\u0001", "\u0001\u0001\u0002\u0002" };

            // act
            var trie = new TrieUtf32Optimized(data);

            // assert
            Test_Assert(new uint[] { 1, 0x00010001, 0x00000003, 2, 0x00010001, 0x00020002, 0x80000008, 0x80000008 }, trie.data_);
        }

        [TestMethod]
        public void Storage3()
        {
            // arrange
            var data = new List<string> { "\u0001\u0001\u0001\u0001", "\u0001\u0001\u0002\u0002", "\u0003\u0003\u0003\u0003" };

            // act
            var trie = new TrieUtf32Optimized(data);

            // assert
            Test_Assert(new uint[] {
                2, 0x00010001, 0x00030003, 0x00000005, 0x0000000A, 2, 0x00010001, 0x00020002,
                0x8000000D, 0x8000000D, 1, 0x00030003, 0x8000000D
            }, trie.data_);
        }

        private void Test_Assert(uint[] expected, uint[] actual)
        {
            Assert.IsTrue(Enumerable.SequenceEqual(expected, actual.Take(expected.Length)));
        }
    }
}
