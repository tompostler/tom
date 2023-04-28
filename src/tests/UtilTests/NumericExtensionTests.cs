using Microsoft.VisualStudio.TestTools.UnitTesting;
using Unlimitedinf.Utilities.Extensions;

namespace tests.UtilTests
{
    [TestClass]
    public sealed class NumericExtensionTests
    {
        [DataTestMethod]
        [DataRow(-1, 2, "-1")]
        [DataRow(-1, 10, "-1")]
        [DataRow(-1, 16, "-1")]
        [DataRow(-1, 36, "-1")]
        [DataRow(0, 2, "0")]
        [DataRow(0, 10, "0")]
        [DataRow(0, 16, "0")]
        [DataRow(0, 36, "0")]
        [DataRow(1, 2, "1")]
        [DataRow(1, 10, "1")]
        [DataRow(1, 16, "1")]
        [DataRow(1, 36, "1")]
        [DataRow(-96869239493027, 2, "-10110000001101000100000100111000001000110100011")]
        [DataRow(-96869239493027, 10, "-96869239493027")]
        [DataRow(-96869239493027, 16, "-581A209C11A3")]
        [DataRow(-96869239493027, 36, "-YC5443RFN")]
        [DataRow(96869239493027, 2, "10110000001101000100000100111000001000110100011")]
        [DataRow(96869239493027, 10, "96869239493027")]
        [DataRow(96869239493027, 16, "581A209C11A3")]
        [DataRow(96869239493027, 36, "YC5443RFN")]
        public void ToBase36Tests(long source, int @base, string target)
        {
            // Arrange
            // Act
            string actual = source.ToBaseX((byte)@base);

            // Assert
            Assert.AreEqual(target, actual);
        }
    }
}
