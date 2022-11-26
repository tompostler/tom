using System.Drawing;

namespace Unlimitedinf.Utilities.Extensions
{
    /// <summary>
    /// Extensions for images.
    /// </summary>
    public static class ImageExtensions
    {
        /// <summary>
        /// Using the width and height of an image, return its megapixel count;
        /// </summary>
#pragma warning disable CA1416 // Validate platform compatibility
        public static double GetMegapixels(this Image image) => image.Width * image.Height / 1e6;
#pragma warning restore CA1416 // Validate platform compatibility
    }
}
