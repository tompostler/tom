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
        [DataRow(-1, 42, "-1")]
        [DataRow(-1, 63, "-1")]
        [DataRow(0, 2, "0")]
        [DataRow(0, 10, "0")]
        [DataRow(0, 16, "0")]
        [DataRow(0, 36, "0")]
        [DataRow(0, 42, "0")]
        [DataRow(0, 63, "0")]
        [DataRow(1, 2, "1")]
        [DataRow(1, 10, "1")]
        [DataRow(1, 16, "1")]
        [DataRow(1, 36, "1")]
        [DataRow(1, 42, "1")]
        [DataRow(1, 63, "1")]
        [DataRow(42, 2, "101010")]
        [DataRow(42, 10, "42")]
        [DataRow(42, 16, "2A")]
        [DataRow(42, 36, "16")]
        [DataRow(42, 42, "10")]
        [DataRow(42, 63, "g")]
        [DataRow(1234567890, 2, "1001001100101100000001011010010")]
        [DataRow(1234567890, 10, "1234567890")]
        [DataRow(1234567890, 16, "499602D2")]
        [DataRow(1234567890, 36, "KF12OI")]
        [DataRow(1234567890, 42, "9IVMHO")]
        [DataRow(1234567890, 63, "1FNLdj")]
        [DataRow(9876543210, 2, "1001001100101100000001011011101010")]
        [DataRow(9876543210, 10, "9876543210")]
        [DataRow(9876543210, 16, "24CB016EA")]
        [DataRow(9876543210, 36, "4JC8LII")]
        [DataRow(9876543210, 42, "1XO0BGU")]
        [DataRow(9876543210, 63, "9xyl49")]
        [DataRow(-96869239493027, 2, "-10110000001101000100000100111000001000110100011")]
        [DataRow(-96869239493027, 10, "-96869239493027")]
        [DataRow(-96869239493027, 16, "-581A209C11A3")]
        [DataRow(-96869239493027, 36, "-YC5443RFN")]
        [DataRow(-96869239493027, 42, "-A07WaT9fT")]
        [DataRow(-96869239493027, 63, "-ObKUq_mo")]
        [DataRow(96869239493027, 2, "10110000001101000100000100111000001000110100011")]
        [DataRow(96869239493027, 10, "96869239493027")]
        [DataRow(96869239493027, 16, "581A209C11A3")]
        [DataRow(96869239493027, 36, "YC5443RFN")]
        [DataRow(96869239493027, 42, "A07WaT9fT")]
        [DataRow(96869239493027, 63, "ObKUq_mo")]
        public void ToBaseXTests(long source, int @base, string target)
        {
            // Arrange
            // Act
            string actual = source.ToBaseX((byte)@base);

            // Assert
            Assert.AreEqual(target, actual);
        }
    }
}
