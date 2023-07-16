using FastText;

namespace CSharpUnitTest
{
    [TestClass]
    public class WordCollectionTest
    {
        [TestMethod]
        public void EnumerateEmpty()
        {
            // arrange
            WordCollection s = new();

            // act
            List<string?> list = new();
            foreach (string? item in s)
            {
                list.Add(item);
            }

            // assert
            Assert.AreEqual(0, list.Count());
        }

        [TestMethod]
        public void EnumerateStringString()
        {
            // arrange
            WordCollection s = new()
            {
                "A",
                "B"
            };

            // act
            List<string?> list = new();
            foreach (string? item in s)
            {
                list.Add(item);
            }

            // assert
            Assert.AreEqual("A", list.First());
            Assert.AreEqual("B", list.Last());
        }

        [TestMethod]
        public void EnumerateStringEnumerable()
        {
            // arrange
            WordCollection s = new()
            {
                "A",
                new List<string> { "B", "C" }
            };

            // act
            List<string?> list = new();
            foreach (string? item in s)
            {
                list.Add(item);
            }

            // assert
            Assert.AreEqual("A", list.First());
            Assert.AreEqual("C", list.Last());
        }

        [TestMethod]
        public void EnumerateEnumerableString()
        {
            // arrange
            WordCollection s = new()
            {
                new List<string> { "B", "C" },
                "D"
            };

            // act
            List<string?> list = new();
            foreach (string? item in s)
            {
                list.Add(item);
            }

            // assert
            Assert.AreEqual("B", list.First());
            Assert.AreEqual("D", list.Last());
        }


        [TestMethod]
        public void EnumerateEnumerableEnumerable()
        {
            // arrange
            WordCollection s = new()
            {
                new List<string> { "B", "C" },
                new List<string> { "D", "E" },
            };

            // act
            List<string?> list = new();
            foreach (string? item in s)
            {
                list.Add(item);
            }

            // assert
            Assert.AreEqual("B", list.First());
            Assert.AreEqual("E", list.Last());
        }
    }
}