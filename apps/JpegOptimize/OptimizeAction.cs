using JpegLibrary;
using System;
using System.Buffers;
using System.IO;
using System.Threading.Tasks;

namespace JpegOptimize
{
    public class OptimizeAction
    {
        public static Task<int> Optimize(FileInfo source, FileInfo output)
        {
            using var bytes = new MemoryPoolBufferWriter();

            using (FileStream stream = source.OpenRead())
            {
                ReadAllBytes(stream, bytes);
            }

            var optimizer = new JpegOptimizer();
            optimizer.SetInput(bytes.GetReadOnlySequence());
            optimizer.Scan();

            using var writer = new MemoryPoolBufferWriter();
            optimizer.SetOutput(writer);
            optimizer.Optimize();

            using (FileStream stream = output.OpenWrite())
            {
                WriteAllBytes(writer.GetReadOnlySequence(), stream);
            }

            return Task.FromResult(0);
        }

        const int BufferSize = 16384;

        private static void ReadAllBytes(Stream stream, IBufferWriter<byte> writer)
        {
            long length = stream.Length;
            while (length > 0)
            {
                int readSize = (int)Math.Min(length, BufferSize);
                Span<byte> buffer = writer.GetSpan(readSize);
                readSize = stream.Read(buffer);
                if (readSize == 0)
                {
                    break;
                }
                writer.Advance(readSize);
                length -= readSize;
            }
        }

        private static void WriteAllBytes(ReadOnlySequence<byte> bytes, Stream stream)
        {
            foreach (ReadOnlyMemory<byte> segment in bytes)
            {
                stream.Write(segment.Span);
            }
        }
    }
}
