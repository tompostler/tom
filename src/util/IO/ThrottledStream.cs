namespace Unlimitedinf.Utilities.IO
{
    /// <summary>
    /// Class for streaming data with throttling support.
    /// </summary>
    /// <remarks>
    /// Based on source from http://www.codeproject.com/Articles/18243/Bandwidth-throttling
    /// </remarks>
    public sealed class ThrottledStream : Stream
    {
        /// <summary>
        /// A constant used to specify an infinite number of bytes that can be transferred per second.
        /// </summary>
        public const long Infinite = 0;

        /// <summary>
        /// The base stream.
        /// </summary>
        private readonly Stream _baseStream;

        /// <summary>
        /// The maximum bytes per second that can be transferred through the base stream.
        /// </summary>
        private long _maximumBytesPerSecond;

        /// <summary>
        /// The number of bytes that has been transferred since the last throttle.
        /// </summary>
        private long _byteCount;

        /// <summary>
        /// The start time in milliseconds of the last throttle.
        /// </summary>
        private long _start;

        /// <summary>
        /// Gets the current milliseconds.
        /// </summary>
        /// <value>The current milliseconds.</value>
        private static long CurrentMilliseconds => Environment.TickCount;

        /// <summary>
        /// Gets or sets the maximum bytes per second that can be transferred through the base stream.
        /// </summary>
        /// <value>The maximum bytes per second.</value>
        public long MaximumBytesPerSecond
        {
            get => this._maximumBytesPerSecond;
            set
            {
                if (this.MaximumBytesPerSecond != value)
                {
                    this._maximumBytesPerSecond = value;
                    this.Reset();
                }
            }
        }

        /// <summary>
        /// Gets or sets the maximum kilobytes per second that can be transferred through the base stream.
        /// </summary>
        /// <value>The maximum kilobytes per second.</value>
        public float MaximumKilobytesPerSecond
        {
            get => this.MaximumBytesPerSecond / 1000f;
            set => this.MaximumBytesPerSecond = (long)(value * 1000);
        }

        /// <summary>
        /// Gets or sets the maximum megabytes per second that can be transferred through the base stream.
        /// </summary>
        /// <value>The maximum megabytes per second.</value>
        public float MaximumMegabytesPerSecond
        {
            get => this.MaximumKilobytesPerSecond / 1000f;
            set => this.MaximumKilobytesPerSecond = (long)(value * 1000);
        }

        /// <inheritdoc/>
        public override bool CanRead => this._baseStream.CanRead;

        /// <inheritdoc/>
        public override bool CanSeek => this._baseStream.CanSeek;

        /// <inheritdoc/>
        public override bool CanWrite => this._baseStream.CanWrite;

        /// <inheritdoc/>
        public override long Length => this._baseStream.Length;

        /// <inheritdoc/>
        public override long Position
        {
            get => this._baseStream.Position;
            set => this._baseStream.Position = value;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ThrottledStream"/> class with an
        /// infinite amount of bytes that can be processed.
        /// </summary>
        /// <param name="baseStream">The base stream.</param>
        public ThrottledStream(Stream baseStream)
            : this(baseStream, Infinite)
        {
            // Nothing todo.
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ThrottledStream"/> class.
        /// </summary>
        /// <param name="baseStream">The base stream.</param>
        /// <param name="maximumBytesPerSecond">The maximum bytes per second that can be transferred through the base stream.</param>
        /// <exception cref="ArgumentNullException">Thrown when <c>baseStream</c> is a null reference.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <c>maximumBytesPerSecond</c> is a negative value.</exception>
        public ThrottledStream(Stream baseStream, long maximumBytesPerSecond)
        {
            if (maximumBytesPerSecond < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumBytesPerSecond), maximumBytesPerSecond, "The maximum number of bytes per second can't be negatie.");
            }

            this._baseStream = baseStream ?? throw new ArgumentNullException(nameof(baseStream));
            this._maximumBytesPerSecond = maximumBytesPerSecond;
            this._start = CurrentMilliseconds;
            this._byteCount = 0;
        }

        /// <inheritdoc/>
        public override void Flush() => this._baseStream.Flush();

        /// <inheritdoc/>
        public override int Read(byte[] buffer, int offset, int count)
        {
            this.Throttle(count);

            return this._baseStream.Read(buffer, offset, count);
        }

        /// <inheritdoc/>
        public override long Seek(long offset, SeekOrigin origin) => this._baseStream.Seek(offset, origin);

        /// <inheritdoc/>
        public override void SetLength(long value) => this._baseStream.SetLength(value);

        /// <inheritdoc/>
        public override void Write(byte[] buffer, int offset, int count)
        {
            this.Throttle(count);

            this._baseStream.Write(buffer, offset, count);
        }

        /// <inheritdoc/>
        public override string ToString() => this._baseStream.ToString();

        /// <summary>
        /// Throttles for the specified buffer size in bytes.
        /// </summary>
        /// <param name="bufferSizeInBytes">The buffer size in bytes.</param>
        private void Throttle(int bufferSizeInBytes)
        {
            // Make sure the buffer isn't empty.
            if (this._maximumBytesPerSecond <= 0 || bufferSizeInBytes <= 0)
            {
                return;
            }

            this._byteCount += bufferSizeInBytes;
            long elapsedMilliseconds = CurrentMilliseconds - this._start;

            if (elapsedMilliseconds > 0)
            {
                // Calculate the current bps.
                long bps = this._byteCount * 1000L / elapsedMilliseconds;

                // If the bps are more then the maximum bps, try to throttle.
                if (bps > this._maximumBytesPerSecond)
                {
                    // Calculate the time to sleep.
                    long wakeElapsed = this._byteCount * 1000L / this._maximumBytesPerSecond;
                    int toSleep = (int)(wakeElapsed - elapsedMilliseconds);

                    if (toSleep > 1)
                    {
                        try
                        {
                            // The time to sleep is more then a millisecond, so sleep.
                            Thread.Sleep(toSleep);
                        }
                        catch (ThreadAbortException)
                        {
                            // Eatup ThreadAbortException.
                        }

                        // A sleep has been done, reset.
                        this.Reset();
                    }
                }
            }
        }

        /// <summary>
        /// Will reset the bytecount to 0 and reset the start time to the current time.
        /// </summary>
        private void Reset()
        {
            long difference = CurrentMilliseconds - this._start;

            // Only reset counters when a known history is available of more then 1 second.
            if (difference > 1000)
            {
                this._byteCount = 0;
                this._start = CurrentMilliseconds;
            }
        }
    }
}
