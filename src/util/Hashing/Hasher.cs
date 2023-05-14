using System.Security.Cryptography;

namespace Unlimitedinf.Utilities.Hashing
{
    /// <summary>
    /// A wrapper for common hashing functions to operate on streams or files.
    /// </summary>
    public sealed class Hasher
    {
        /// <summary>
        /// Helper to make it obvious which hash algorithm we're looking for.
        /// </summary>
        public enum Algorithm
        {
            /// <summary>
            /// <see cref="System.Security.Cryptography.MD5"/>
            /// </summary>
            MD5,

            /// <summary>
            /// <see cref="Hashing.Crc32"/>
            /// </summary>
            Crc32,

            /// <summary>
            /// <see cref="System.Security.Cryptography.SHA1"/>
            /// </summary>
            SHA1,

            /// <summary>
            /// <see cref="System.Security.Cryptography.SHA256"/>
            /// </summary>
            SHA256,

            /// <summary>
            /// <see cref="System.Security.Cryptography.SHA512"/>
            /// </summary>
            SHA512,

            /// <summary>
            /// <see cref="Hashing.Blockhash"/>
            /// </summary>
            Blockhash   // http://blockhash.io/
        }

        private readonly Algorithm algorithm;
        private readonly HashAlgorithm hashAlgorithm;

        /// <summary>
        /// Ctor.
        /// </summary>
        public Hasher(Algorithm algorithm)
        {
            this.algorithm = algorithm;
            this.hashAlgorithm = algorithm switch
            {
                Algorithm.MD5 => MD5.Create(),
                Algorithm.Crc32 => Crc32.Create(),
                Algorithm.SHA1 => SHA1.Create(),
                Algorithm.SHA256 => SHA256.Create(),
                Algorithm.SHA512 => SHA512.Create(),
                Algorithm.Blockhash => Blockhash.Create(),
                _ => throw new NotImplementedException(),
            };
        }

        /// <summary>
        /// Compute the hash from a given stream.
        /// </summary>
        public byte[] ComputeHash(Stream inputStream) =>
            // Temporary workaround warranting further investigation.
            this.algorithm == Algorithm.Blockhash
            ? Blockhash.ComputeHash(inputStream)
            : this.hashAlgorithm.ComputeHash(inputStream);

        /// <summary>
        /// Compute the hash from a given stream and return it as a string.
        /// </summary>
        public string ComputeHashS(Stream inputStream) => BitConverter.ToString(this.ComputeHash(inputStream)).Replace("-", string.Empty);
    }
}
