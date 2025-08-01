using Unlimitedinf.Utilities.Extensions;

namespace tests.UtilTests
{
    [TestClass]
    public sealed class StringExtensionTests
    {
        [DataTestMethod]
        [DataRow(null, 1, new string[0])]
        [DataRow("", 1, new string[0])]
        [DataRow("123412341234", 4, new string[] { "1234", "1234", "1234" })]
        [DataRow("1234123412", 4, new string[] { "1234", "1234", "12" })]
        [DataRow("1234512345", 5, new string[] { "12345", "12345" })]
        [DataRow("12345123", 5, new string[] { "12345", "123" })]
        public void ChunkTests(string source, int chunkSize, string[] target)
        {
            // Act
            string[] result = source.Chunk(chunkSize);

            // Assert
            Assert.AreEqual(target.Length, result.Length);
            for (int i = 0; i < target.Length; i++)
            {
                Assert.AreEqual(target[i], result[i]);
            }
        }
    }
}
