﻿/* Perceptual image hash calculation tool based on algorithm descibed in
 * Block Mean Value Based Image Perceptual Hashing by Bian Yang, Fan Gu and Xiamu Niu
 *
 * Copyright 2014 Commons Machinery http://commonsmachinery.se/
 * Distributed under an MIT license, please see LICENSE in the top dir.
 */
// Source drawn from https://github.com/commonsmachinery/blockhash/blob/master/blockhash.c on 2022-05-25.
// Modified under the MIT license by Tom Postler to work with C#.
// Reformatted by Tom Postler, 2025-02-06.

using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Unlimitedinf.Utilities.Extensions;

namespace Unlimitedinf.Utilities.Hashing
{
    /// <summary>
    /// Perceptual image hash calculation tool based on algorithm descibed in 
    /// Block Mean Value Based Image Perceptual Hashing by Bian Yang, Fan Gu and Xiamu Niu.
    /// </summary>
    /// <remarks>
    /// Due to the nature of Image files, I have not yet found a way to implement this in standard stream-based
    /// processing methods. In that sense, this will load an entire image into memory from a stream. In fact, I'm
    /// pretty sure it does it twice in order to properly implement the HashAlgorithm class. Finding a better way is
    /// a TODO, but it works well enough for now.
    /// </remarks>
    public sealed class Blockhash : HashAlgorithm
    {
        [SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "Project is only built for windows.")]
        private static byte[] ImageToRGBA(Image image)
        {
            var bitmap = image as Bitmap;
            BitmapData data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            byte[] rgba = new byte[bitmap.Width * bitmap.Height * 4];
            int width = data.Width;

            try
            {
                _ = Parallel.For(0, data.Height, (scanline) =>
                {
                    byte[] pixelData = new byte[data.Stride];
                    Marshal.Copy(data.Scan0 + (scanline * data.Stride), pixelData, 0, data.Stride);
                    for (int pixeloffset = 0; pixeloffset < width; pixeloffset++)
                    {
                        // PixelFormat.Format32bppArgb means the data is stored in memory as BGRA. But we want RGBA.
                        int pixel = ((scanline * width) + pixeloffset) * 4;
                        rgba[pixel + 0] = pixelData[(pixeloffset * 4) + 2];
                        rgba[pixel + 1] = pixelData[(pixeloffset * 4) + 1];
                        rgba[pixel + 2] = pixelData[(pixeloffset * 4) + 0];
                        rgba[pixel + 3] = pixelData[(pixeloffset * 4) + 3];
                    }
                });
            }
            finally
            {
                bitmap.UnlockBits(data);
            }

            return rgba;
        }

        private static byte[] Bits_to_bytes(int[] bits)
        {
            byte[] result = new byte[bits.Length / 8];
            for (int i = 0; i < bits.Length; i += 8)
            {
                result[i / 8] = (byte)(
                    (bits[i] << 7)
                    + (bits[i + 1] << 6)
                    + (bits[i + 2] << 5)
                    + (bits[i + 3] << 4)
                    + (bits[i + 4] << 3)
                    + (bits[i + 5] << 2)
                    + (bits[i + 6] << 1)
                    + bits[i + 7]);
            }

            return result;
        }

        private static int[] Blockhash_quick(int bits, byte[] data, int width, int height)
        {
            int x, y, ix, iy;
            int ii, alpha, value;
            int block_width;
            int block_height;
            int[] blocks;

            block_width = width / bits;
            block_height = height / bits;

            blocks = new int[bits * bits];
            for (y = 0; y < bits; y++)
            {
                for (x = 0; x < bits; x++)
                {
                    value = 0;

                    for (iy = 0; iy < block_height; iy++)
                    {
                        for (ix = 0; ix < block_width; ix++)
                        {
                            ii = ((((y * block_height) + iy) * width) + (x * block_width) + ix) * 4;

                            alpha = data[ii + 3];
                            if (alpha == 0)
                            {
                                value += 765;
                            }
                            else
                            {
                                value += data[ii] + data[ii + 1] + data[ii + 2];
                            }
                        }
                    }

                    blocks[(y * bits) + x] = value;
                }
            }

            return Translate_blocks_to_bits(blocks, bits * bits, block_width * block_height);
        }

        private static int[] Blockhash_full(int bits, byte[] data, int width, int height)
        {
            float block_width;
            float block_height;
            float y_frac, y_int;
            float x_frac, x_int;
            float x_mod, y_mod;
            float weight_top, weight_bottom, weight_left, weight_right;
            int block_top, block_bottom, block_left, block_right;
            int x, y, ii, alpha;
            float value;
            float[] blocks;

            if (width % bits == 0 && height % bits == 0)
            {
                return Blockhash_quick(bits, data, width, height);
            }

            block_width = (float)width / bits;
            block_height = (float)height / bits;

            blocks = new float[bits * bits];

            for (y = 0; y < height; y++)
            {
                y_mod = ((float)y + 1) % block_height;
                y_int = (int)y_mod;
                y_frac = y_mod % 1;

                weight_top = (1 - y_frac);
                weight_bottom = y_frac;

                // y_int will be 0 on bottom/right borders and on block boundaries
                if (y_int > 0 || (y + 1) == height)
                {
                    block_top = block_bottom = (int)Math.Floor(y / block_height);
                }
                else
                {
                    block_top = (int)Math.Floor(y / block_height);
                    block_bottom = (int)Math.Ceiling(y / block_height);
                }

                for (x = 0; x < width; x++)
                {
                    x_mod = ((float)x + 1) % block_width;
                    x_int = (int)x_mod;
                    x_frac = x_mod % 1;

                    weight_left = (1 - x_frac);
                    weight_right = x_frac;

                    // x_int will be 0 on bottom/right borders and on block boundaries
                    if (x_int > 0 || (x + 1) == width)
                    {
                        block_left = block_right = (int)Math.Floor(x / block_width);
                    }
                    else
                    {
                        block_left = (int)Math.Floor(x / block_width);
                        block_right = (int)Math.Ceiling(x / block_width);
                    }

                    ii = ((y * width) + x) * 4;

                    alpha = data[ii + 3];
                    value = alpha == 0 ? 765 : data[ii] + data[ii + 1] + data[ii + 2];

                    // add weighted pixel value to relevant blocks
                    blocks[(block_top * bits) + block_left] += value * weight_top * weight_left;
                    blocks[(block_top * bits) + block_right] += value * weight_top * weight_right;
                    blocks[(block_bottom * bits) + block_left] += value * weight_bottom * weight_left;
                    blocks[(block_bottom * bits) + block_right] += value * weight_bottom * weight_right;
                }
            }

            return Translate_blocks_to_bitsf(blocks, bits * bits, (int)(block_width * block_height));
        }

        private static int[] Translate_blocks_to_bits(int[] blocks, int nblocks, int pixels_per_block)
        {
            float half_block_value;
            int bandsize, i, j;
            int m, v;

            half_block_value = pixels_per_block * 256 * 3 / 2;
            bandsize = nblocks / 4;

            int[] subblocks = new int[bandsize];
            for (i = 0; i < 4; i++)
            {
                Array.Copy(blocks, i * bandsize, subblocks, 0, bandsize);
                m = (int)subblocks.Median();
                for (j = i * bandsize; j < (i + 1) * bandsize; j++)
                {
                    v = blocks[j];
                    blocks[j] = (v > m || (Math.Abs(v - m) < 1 && m > half_block_value)) ? 1 : 0;
                }
            }
            return blocks;
        }

        private static int[] Translate_blocks_to_bitsf(float[] blocks, int nblocks, int pixels_per_block)
        {
            int[] result = new int[nblocks];
            float half_block_value;
            int bandsize, i, j;
            float m, v;

            half_block_value = pixels_per_block * 256 * 3 / 2;
            bandsize = nblocks / 4;

            float[] subblocks = new float[bandsize];
            for (i = 0; i < 4; i++)
            {
                Array.Copy(blocks, i * bandsize, subblocks, 0, bandsize);
                m = (float)subblocks.Median();
                for (j = i * bandsize; j < (i + 1) * bandsize; j++)
                {
                    v = blocks[j];
                    result[j] = (v > m || (Math.Abs(v - m) < 1 && m > half_block_value)) ? 1 : 0;
                }
            }
            return result;
        }

        /// <summary>
        /// Given a file name for an image, return the blockhash.
        /// </summary>
        [SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "Project is only built for windows.")]
        public static byte[] ProcessImage(string fileName, int bits = 16, bool quick = false)
            => ProcessImage(Image.FromFile(fileName), bits, quick);

        /// <summary>
        /// Given a steam containing an image, return the blockhash.
        /// </summary>
        [SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "Project is only built for windows.")]
        public static byte[] ProcessImage(Stream stream, int bits = 16, bool quick = false)
            => ProcessImage(Image.FromStream(stream), bits, quick);

        /// <summary>
        /// Given an image, return the blockhash.
        /// </summary>
        [SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "Project is only built for windows.")]
        public static byte[] ProcessImage(Image image, int bits = 16, bool quick = false)
        {
            ArgumentNullException.ThrowIfNull(image);

            byte[] image_data = ImageToRGBA(image);
            int width = image.Width;
            int height = image.Height;
            image.Dispose();

            int[] hash = quick
                        ? Blockhash_quick(bits, image_data, width, height)
                        : Blockhash_full(bits, image_data, width, height);
            return Bits_to_bytes(hash);
        }

        /// <summary>
        /// Compute the hamming distances between two byte arrays.
        /// </summary>
        public static int HammingDistance(byte[] left, byte[] right)
        {
            ArgumentNullException.ThrowIfNull(left);
            ArgumentNullException.ThrowIfNull(right);

            if (left.Length != right.Length)
            {
                throw new ArgumentException("Should only compare equal-length arrays");
            }

            int count = 0;
            for (int i = 0; i < left.Length; i++)
            {
                int diff = left[i] ^ right[i];
                count += ((diff >> 7) & 1)
                    + ((diff >> 6) & 1)
                    + ((diff >> 5) & 1)
                    + ((diff >> 4) & 1)
                    + ((diff >> 3) & 1)
                    + ((diff >> 2) & 1)
                    + ((diff >> 1) & 1)
                    + (diff & 1);
            }

            return count;
        }

        #region HashAlgorithm overrides and implmentation

        private MemoryStream haImageData;

        /// <summary>
        /// See <see cref="HashAlgorithm.HashAlgorithm"/>.
        /// </summary>
        public Blockhash()
        {
            this.haImageData = new MemoryStream();
        }

        /// <summary>
        /// See <see cref="HashAlgorithm.Initialize"/>.
        /// </summary>
        public override void Initialize()
        {
            this.haImageData.Dispose();
            this.haImageData = new MemoryStream();
        }

        /// <summary>
        /// See <see cref="HashAlgorithm.HashCore(byte[], int, int)"/>.
        /// </summary>
        protected override void HashCore(byte[] array, int ibStart, int cbSize)
            => this.haImageData.Write(array, ibStart, cbSize);

        /// <summary>
        /// See <see cref="HashAlgorithm.HashFinal"/>.
        /// </summary>
        protected override byte[] HashFinal()
        {
            _ = this.haImageData.Seek(0, SeekOrigin.Begin);
            return ComputeHash(this.haImageData);
        }

        /// <summary>
        /// See <see cref="HashAlgorithm.Create()"/>.
        /// </summary>
        new public static HashAlgorithm Create()
            => new Blockhash();

        /// <summary>
        /// See <see cref="HashAlgorithm.ComputeHash(Stream)"/>.
        /// </summary>
        new public static byte[] ComputeHash(Stream inputStream)
            => ProcessImage(inputStream);

        /// <summary>
        /// See <see cref="HashAlgorithm.HashSize"/>.
        /// </summary>
        public override int HashSize => 256;

        #endregion
    }
}
