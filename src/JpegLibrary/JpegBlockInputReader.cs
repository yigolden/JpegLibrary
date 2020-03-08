#nullable enable

namespace JpegLibrary
{
    /// <summary>
    /// A input reader that read spatial block from the buffer.
    /// </summary>
    public abstract class JpegBlockInputReader
    {
        /// <summary>
        /// The width of the image.
        /// </summary>
        public abstract int Width { get; }

        /// <summary>
        /// The height of the image.
        /// </summary>
        public abstract int Height { get; }

        /// <summary>
        /// Read a 8x8 spatial block from the source buffer.
        /// </summary>
        /// <param name="blockRef">The reference to the block that the implementation should write to.</param>
        /// <param name="componentIndex">The index of the component.</param>
        /// <param name="x">The X offset in the image.</param>
        /// <param name="y">The Y offset in the image.</param>
        public abstract void ReadBlock(ref short blockRef, int componentIndex, int x, int y);
    }
}
