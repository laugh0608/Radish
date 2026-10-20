using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace Radish.Api.Tests;

/// <summary>自有无依赖 PNG fixture：固定 RGBA8，独立于被测图片 codec。</summary>
internal static class ImageFixture
{
    public static MemoryStream Create(int width = 800, int height = 600)
    {
        using var pixels = new MemoryStream();
        using (var zlib = new ZLibStream(pixels, CompressionLevel.Fastest, true))
        {
            for (var y = 0; y < height; y++)
            {
                zlib.WriteByte(0);
                for (var x = 0; x < width; x++)
                {
                    zlib.WriteByte((byte)(x * 255 / width));
                    zlib.WriteByte((byte)(y * 255 / height));
                    zlib.WriteByte(100);
                    zlib.WriteByte(255);
                }
            }
        }
        var output = new MemoryStream();
        output.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), height);
        header[8] = 8;
        header[9] = 6;
        Chunk(output, "IHDR", header);
        Chunk(output, "IDAT", pixels.ToArray());
        Chunk(output, "IEND", []);
        output.Position = 0;
        return output;
    }

    public static (int Width, int Height) PngSize(string path)
    {
        var bytes = File.ReadAllBytes(path);
        if (!bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
            throw new InvalidDataException("Expected PNG fixture output");
        return (BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16, 4)), BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20, 4)));
    }

    private static void Chunk(Stream stream, string type, byte[] data)
    {
        Span<byte> number = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(number, data.Length);
        stream.Write(number);
        var tag = Encoding.ASCII.GetBytes(type);
        stream.Write(tag);
        stream.Write(data);
        uint crc = 0xffffffff;
        foreach (var bytes in new[] { tag, data })
            foreach (var value in bytes)
            {
                crc ^= value;
                for (var bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ ((crc & 1) != 0 ? 0xedb88320u : 0u);
            }
        BinaryPrimitives.WriteUInt32BigEndian(number, ~crc);
        stream.Write(number);
    }
}
