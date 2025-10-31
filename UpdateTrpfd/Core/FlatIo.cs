using System;
using System.Buffers;
using System.IO;
using FlatSharp;

namespace TrpfdManager.Core;
internal static class FlatIo
{
    public static T Load<T>(string path) where T : class
    {
        var data = File.ReadAllBytes(path);
        return FlatBufferSerializer.Default.Parse<T>(data);
    }

    public static void Save<T>(T o, string path) where T : class
    {
        var size = FlatBufferSerializer.Default.GetMaxSize(o);
        var buf = ArrayPool<byte>.Shared.Rent(size);
        try
        {
            var len = FlatBufferSerializer.Default.Serialize(o, buf);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using var fs = File.Create(path);
            fs.Write(buf, 0, len);
        }
        finally { ArrayPool<byte>.Shared.Return(buf); }
    }
}