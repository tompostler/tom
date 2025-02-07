﻿// Copyright (c) Damien Guard.  All rights reserved.
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
// Originally published at http://damieng.com/blog/2006/08/08/calculating_crc32_in_c_and_net
// Modified by Tom Postler, 2016-11-27.
// Reformatted by Tom Postler, 2025-02-06.

using System.Security.Cryptography;

namespace Unlimitedinf.Utilities.Hashing
{
    /// <summary>
    /// Implements a 32-bit CRC hash algorithm compatible with Zip etc.
    /// </summary>
    /// <remarks>
    /// Crc32 should only be used for backward compatibility with older file formats
    /// and algorithms. It is not secure enough for new applications.
    /// If you need to call multiple times for the same data either use the HashAlgorithm
    /// interface or remember that the result of one Compute call needs to be ~ (XOR) before
    /// being passed in as the seed for the next Compute call.
    /// </remarks>
    public sealed class Crc32 : HashAlgorithm
    {
        /// <summary>
        /// Default CRC32 polynomial.
        /// </summary>
        public const uint DefaultPolynomial = 0xedb88320u;
        /// <summary>
        /// Default CRC32 seed value.
        /// </summary>
        public const uint DefaultSeed = 0xffffffffu;

        private static uint[] defaultTable;

        private readonly uint seed;
        private readonly uint[] table;
        private uint hash;

        /// <summary>
        /// Ctor.
        /// </summary>
        public Crc32()
            : this(DefaultPolynomial, DefaultSeed)
        {
        }

        /// <summary>
        /// Ctor.
        /// </summary>
        public Crc32(uint polynomial, uint seed)
        {
            this.table = InitializeTable(polynomial);
            this.seed = this.hash = seed;
        }

        /// <summary>
        /// See <see cref="HashAlgorithm.Initialize"/>.
        /// </summary>
        public override void Initialize()
            => this.hash = this.seed;

        /// <summary>
        /// See <see cref="HashAlgorithm.Create()"/>.
        /// </summary>
        public new static Crc32 Create()
            => new();

        /// <summary>
        /// See <see cref="HashAlgorithm.HashCore(byte[], int, int)"/>.
        /// </summary>
        protected override void HashCore(byte[] buffer, int start, int length)
            => this.hash = CalculateHash(this.table, this.hash, buffer, start, length);

        /// <summary>
        /// See <see cref="HashAlgorithm.HashFinal"/>.
        /// </summary>
        protected override byte[] HashFinal()
        {
            byte[] hashBuffer = UInt32ToBigEndianBytes(~this.hash);
            this.HashValue = hashBuffer;
            return hashBuffer;
        }

        /// <summary>
        /// See <see cref="HashAlgorithm.HashSize"/>.
        /// </summary>
        public override int HashSize => 32;

        private static uint[] InitializeTable(uint polynomial)
        {
            if (polynomial == DefaultPolynomial && defaultTable != null)
            {
                return defaultTable;
            }

            uint[] createTable = new uint[256];
            for (int i = 0; i < 256; i++)
            {
                uint entry = (uint)i;
                for (int j = 0; j < 8; j++)
                {
                    if ((entry & 1) == 1)
                    {
                        entry = (entry >> 1) ^ polynomial;
                    }
                    else
                    {
                        entry >>= 1;
                    }
                }

                createTable[i] = entry;
            }

            if (polynomial == DefaultPolynomial)
            {
                defaultTable = createTable;
            }

            return createTable;
        }

        private static uint CalculateHash(uint[] table, uint seed, byte[] buffer, int start, int size)
        {
            uint crc = seed;
            for (int i = start; i < size - start; i++)
            {
                crc = (crc >> 8) ^ table[buffer[i] ^ (crc & 0xff)];
            }

            return crc;
        }

        private static byte[] UInt32ToBigEndianBytes(uint uint32)
        {
            byte[] result = BitConverter.GetBytes(uint32);

            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(result);
            }

            return result;
        }
    }
}
