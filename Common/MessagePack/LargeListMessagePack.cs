﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using MessagePack;
using MessagePack.Formatters;
using MessagePack.Resolvers;
using MessagePack.LZ4;

namespace Rfx.Riken.OsakaUniv
{
    public static class LargeListMessagePack
    {
        public static int OffsetCutoff = 1073741824;
        public const sbyte ExtensionTypeCode = 99;
        public const int NotCompressionSize = 64;
        public const int HeaderSize = 11;

        static IFormatterResolver defaultResolver;

        static byte[] buffer = null;
        static byte[] bufferLz = null;

        public static byte[] GetBuffer()
        {
            if (buffer == null)
            {
                buffer = new byte[65536];
            }
            return buffer;
        }

        public static byte[] GetBufferLZ4()
        {
            if (bufferLz == null)
            {
                bufferLz = new byte[65536];
            }
            return bufferLz;
        }

        public static IFormatterResolver DefaultResolver {
            get {
                if (defaultResolver == null)
                {
                    return StandardResolver.Instance;
                }
                return defaultResolver;
            }
        }

        public static void Serialize<T>(Stream stream, List<T> value, IFormatterResolver resolver = null)
        {
            if (resolver == null) resolver = DefaultResolver;
            var bytes = GetBuffer();
            var offset = 0;
            if (value == null)
            {
                offset += MessagePackBinary.WriteNil(ref bytes, offset);
                stream.Write(bytes, 0, offset);
            }
            else
            {
                var formatter = resolver.GetFormatterWithVerify<T>();
                var startOffSet = offset;
                var c = value.Count;
                offset = 5;
                var lastCounter = -1;
                var bufferLz4 = GetBufferLZ4();
                for (int i = 0; i < c; i++)
                {
                    offset += formatter.Serialize(ref bytes, offset, value[i], resolver);
                    if (offset > OffsetCutoff)
                    {
                        MessagePackBinary.WriteArrayHeader(ref bytes, startOffSet, i - lastCounter);
                        lastCounter = i;
                        bufferLz4 = ToLZ4Binary(new ArraySegment<byte>(bytes, 0, offset));
                        stream.Write(bufferLz4, 0, bufferLz4.Length);
                        offset = 5; // size of MessagePackBinary.WriteArrayHeader
                        bytes = GetBuffer();
                    }
                }
                if (lastCounter < c - 1)
                {
                    MessagePackBinary.WriteArrayHeader(ref bytes, startOffSet, c - lastCounter - 1);
                    bufferLz4 = ToLZ4Binary(new ArraySegment<byte>(bytes, 0, offset));
                    stream.Write(bufferLz4, startOffSet, bufferLz4.Length);
                }
            }
        }

        public static byte[] ToLZ4Binary(ArraySegment<byte> messagePackBinary)
        {
            var buffer = ToLZ4BinaryCore(messagePackBinary);
            return MessagePackBinary.FastCloneWithResize(buffer.Array, buffer.Count);
        }


        static ArraySegment<byte> ToLZ4BinaryCore(ArraySegment<byte> serializedData)
        {
            if (serializedData.Count < NotCompressionSize)
            {
                return serializedData;
            }
            else
            {
                var offset = 0;
                var buffer = GetBufferLZ4();
                var maxOutCount = LZ4Codec.MaximumOutputLength(serializedData.Count);
                if (buffer.Length + 6 + 5 < maxOutCount) // (ext header size + fixed length size)
                {
                    buffer = new byte[6 + 5 + maxOutCount];
                }

                // acquire ext header position
                var extHeaderOffset = offset;
                offset += (6 + 5);

                // write body
                var lz4Length = LZ4Codec.Encode(serializedData.Array, serializedData.Offset, serializedData.Count, buffer, offset, buffer.Length - offset);
                // Console.WriteLine("lz4Length" + lz4Length);
                // write extension header(always 6 bytes)
                extHeaderOffset += MessagePackBinary.WriteExtensionFormatHeaderForceExt32Block(ref buffer, extHeaderOffset, (sbyte)ExtensionTypeCode, lz4Length + 5);

                // write length(always 5 bytes)
                MessagePackBinary.WriteInt32ForceInt32Block(ref buffer, extHeaderOffset, serializedData.Count);

                return new ArraySegment<byte>(buffer, 0, 6 + 5 + lz4Length);
            }
        }

        public static List<T> Deserialize<T>(Stream stream, IFormatterResolver resolver = null)
        {
            return DeserializeCore<T>(stream, resolver);
        }

        static bool FillFromStream(Stream input, ref byte[] buffer, int offset, int readSize)
        {
            int length = 0;
            int read;
            if ((read = input.Read(buffer, offset, readSize)) > 0)
            {
                length += read;
                // Console.WriteLine("read length: " + length);
                if (length == buffer.Length)
                {
                    MessagePackBinary.FastResize(ref buffer, length * 2);
                }
                return true;
            }
            return false;
        }


        static List<T> DeserializeCore<T>(Stream stream, IFormatterResolver resolver)
        {
            var buffer = GetBuffer();
            var res = new List<T>();
            // HeaderSize: extension header(always 6 bytes) + length(always 5 bytes) = 11
            while (FillFromStream(stream, ref buffer, 0, HeaderSize))
            {
                var tmp = DeserializeEach<T>(stream, buffer, resolver);
                if (tmp != null && tmp.Count > 0)
                {
                    AddList(res, tmp);
                }
            }
            return res;
        }

        static void AddList<T>(List<T> original, List<T> tmp)
        {
            foreach (var t in tmp)
            {
                original.Add(t);
            }
        }

        static List<T> DeserializeEach<T>(Stream stream, byte[] buffer, IFormatterResolver resolver)
        {
            var bytes = new ArraySegment<byte>(buffer, 0, HeaderSize);
            int readSize;
            // Console.WriteLine("MessagePackType: " + MessagePackBinary.GetMessagePackType(bytes.Array, bytes.Offset));
            if (MessagePackBinary.GetMessagePackType(bytes.Array, bytes.Offset) == MessagePackType.Extension)
            {
                var header = MessagePackBinary.ReadExtensionFormatHeader(bytes.Array, bytes.Offset, out readSize);
                if (header.TypeCode == ExtensionTypeCode)
                {
                    // decode lz4
                    var offset = bytes.Offset + readSize;
                    var length = MessagePackBinary.ReadInt32(bytes.Array, offset, out readSize);
                    offset += readSize;
                    int bufferLength = (int)header.Length - 5;
                    buffer = GetBuffer(); // use LZ4 Pool

                    if (buffer.Length < bufferLength)
                    {
                        buffer = new byte[bufferLength];
                    }

                    if (FillFromStream(stream, ref buffer, 0, bufferLength))
                    {
                        bytes = new ArraySegment<byte>(buffer, 0, bufferLength);
                        offset = 0;
                        // LZ4 Decode
                        var len = bytes.Count;
                        var bufferLz4 = new byte[length];
                        LZ4Codec.Decode(bytes.Array, bytes.Offset, len, bufferLz4, 0, length);
                        return DeserializeList<T>(bufferLz4, offset, resolver, out readSize);
                    }
                }
            }
            // Console.WriteLine("Not working well");
            return new List<T>();
        }
        static List<T> DeserializeList<T>(byte[] bytes, int offset, IFormatterResolver formatterResolver, out int readSize)
        {
            if (formatterResolver == null) formatterResolver = DefaultResolver;
            if (MessagePackBinary.IsNil(bytes, offset))
            {
                readSize = 1;
                return null;
            }
            else
            {
                var startOffset = 0;
                var formatter = formatterResolver.GetFormatterWithVerify<T>();
                var len = MessagePackBinary.ReadArrayHeader(bytes, offset, out readSize);
                offset = 5;
                var list = new List<T>();
                for (int i = 0; i < len; i++)
                {
                    list.Add(formatter.Deserialize(bytes, offset, formatterResolver, out readSize));
                    offset += readSize;
                }
                readSize = offset - startOffset;
                return list;
            }
        }
    }
}
