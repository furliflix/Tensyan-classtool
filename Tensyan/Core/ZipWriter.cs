using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace Tensyan.Core
{
    /// <summary>
    /// 零依赖的最小 ZIP 写入器（只用 DeflateStream，2.0 就有）。
    /// 为什么自己写：System.IO.Compression.ZipArchive 是 .NET 4.5 才有的，
    /// XP 特别版（net35）拿不到它；索性三个版本共用这一份实现，行为完全一致。
    /// 用法：using (var zip = new ZipWriter(fs)) using (var s = zip.CreateEntry("a.xml")) { ...写内容... }
    /// </summary>
    public sealed class ZipWriter : IDisposable
    {
        private readonly Stream _out;
        private readonly bool _leaveOpen;
        private readonly List<Entry> _entries = new List<Entry>();
        private bool _finished;

        private sealed class Entry
        {
            public string Name;
            public uint Crc;
            public long Offset;
            public long CompSize;
            public long RawSize;
        }

        public ZipWriter(Stream output) : this(output, false) { }

        public ZipWriter(Stream output, bool leaveOpen)
        {
            if (output == null) throw new ArgumentNullException("output");
            _out = output;
            _leaveOpen = leaveOpen;
        }

        /// <summary>建一个条目并返回可写流；流写完（Dispose）后本条目就固定下来。</summary>
        public Stream CreateEntry(string name)
        {
            return new EntryStream(this, name);
        }

        private void FinishEntry(EntryStream es)
        {
            // 回填本地文件头里的 CRC 与长度
            long end = _out.Position;
            _out.Position = es.Offset + 14;             // 本地头里 crc32 的位置
            WriteUInt32(_out, es.Crc);
            WriteUInt32(_out, (uint)es.CompSize);
            WriteUInt32(_out, (uint)es.RawSize);
            _out.Position = end;
            _entries.Add(new Entry
            {
                Name = es.Name,
                Crc = es.Crc,
                Offset = es.Offset,
                CompSize = es.CompSize,
                RawSize = es.RawSize
            });
        }

        public void Dispose()
        {
            if (_finished) return;
            _finished = true;
            WriteCentralDirectory();
            try { _out.Flush(); } catch { }
            if (!_leaveOpen) { try { _out.Dispose(); } catch { } }
        }

        private void WriteLocalHeader(string name)
        {
            byte[] nameBytes = Encoding.UTF8.GetBytes(name);
            WriteUInt32(_out, 0x04034b50);      // 本地文件头签名
            WriteUInt16(_out, 20);              // 需要版本 2.0
            WriteUInt16(_out, 0x0800);          // 标志位：文件名为 UTF-8
            WriteUInt16(_out, 8);               // 压缩方法：deflate
            WriteUInt16(_out, 0);               // 时间
            WriteUInt16(_out, 0);               // 日期
            WriteUInt32(_out, 0);               // CRC（回填）
            WriteUInt32(_out, 0);               // 压缩后长度（回填）
            WriteUInt32(_out, 0);               // 原始长度（回填）
            WriteUInt16(_out, (ushort)nameBytes.Length);
            WriteUInt16(_out, 0);               // 扩展字段长度
            _out.Write(nameBytes, 0, nameBytes.Length);
        }

        private void WriteCentralDirectory()
        {
            long start = _out.Position;
            foreach (var e in _entries)
            {
                byte[] nameBytes = Encoding.UTF8.GetBytes(e.Name);
                WriteUInt32(_out, 0x02014b50);  // 中央目录签名
                WriteUInt16(_out, 20);          // 生成版本
                WriteUInt16(_out, 20);          // 需要版本
                WriteUInt16(_out, 0x0800);
                WriteUInt16(_out, 8);
                WriteUInt16(_out, 0);
                WriteUInt16(_out, 0);
                WriteUInt32(_out, e.Crc);
                WriteUInt32(_out, (uint)e.CompSize);
                WriteUInt32(_out, (uint)e.RawSize);
                WriteUInt16(_out, (ushort)nameBytes.Length);
                WriteUInt16(_out, 0);           // 扩展字段
                WriteUInt16(_out, 0);           // 注释
                WriteUInt16(_out, 0);           // 磁盘号
                WriteUInt16(_out, 0);           // 内部属性
                WriteUInt32(_out, 0);           // 外部属性
                WriteUInt32(_out, (uint)e.Offset);
                _out.Write(nameBytes, 0, nameBytes.Length);
            }
            long end = _out.Position;

            // 中央目录结束记录
            WriteUInt32(_out, 0x06054b50);
            WriteUInt16(_out, 0);               // 本磁盘号
            WriteUInt16(_out, 0);               // 开始磁盘号
            WriteUInt16(_out, (ushort)_entries.Count);
            WriteUInt16(_out, (ushort)_entries.Count);
            WriteUInt32(_out, (uint)(end - start));
            WriteUInt32(_out, (uint)start);
            WriteUInt16(_out, 0);               // 注释长度
        }

        private static void WriteUInt16(Stream s, ushort v)
        {
            s.WriteByte((byte)(v & 0xFF));
            s.WriteByte((byte)((v >> 8) & 0xFF));
        }

        private static void WriteUInt32(Stream s, uint v)
        {
            s.WriteByte((byte)(v & 0xFF));
            s.WriteByte((byte)((v >> 8) & 0xFF));
            s.WriteByte((byte)((v >> 16) & 0xFF));
            s.WriteByte((byte)((v >> 24) & 0xFF));
        }

        /// <summary>单个条目：把写入内容用 Deflate 压进 ZIP，同时统计 CRC 与长度。</summary>
        private sealed class EntryStream : Stream
        {
            private readonly ZipWriter _owner;
            private readonly DeflateStream _deflate;
            private readonly uint[] _crcTable;
            private uint _crc = 0xFFFFFFFF;
            private long _raw;
            private long _comp;
            private bool _done;

            public readonly string Name;
            public long Offset;
            public uint Crc { get { return _crc ^ 0xFFFFFFFF; } }
            public long RawSize { get { return _raw; } }
            public long CompSize { get { return _comp; } }

            public EntryStream(ZipWriter owner, string name)
            {
                _owner = owner;
                Name = name;
                _crcTable = Crc32Table;
                Offset = owner._out.Position;
                owner.WriteLocalHeader(name);
                var counting = new CountingStream(owner._out, v => _comp = v);
                _deflate = new DeflateStream(counting, CompressionMode.Compress, true);
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                for (int i = 0; i < count; i++)
                    _crc = (_crc >> 8) ^ _crcTable[(_crc ^ buffer[offset + i]) & 0xFF];
                _raw += count;
                _deflate.Write(buffer, offset, count);
            }

            public override void Flush() { _deflate.Flush(); }

            public override void Close()
            {
                if (_done) return;
                _done = true;
                try { _deflate.Close(); } catch { }
                _owner.FinishEntry(this);
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing) Close();
                base.Dispose(disposing);
            }

            public override bool CanRead { get { return false; } }
            public override bool CanSeek { get { return false; } }
            public override bool CanWrite { get { return true; } }
            public override long Length { get { return _raw; } }
            public override long Position { get { return _raw; } set { throw new NotSupportedException(); } }
            public override int Read(byte[] buffer, int offset, int count) { throw new NotSupportedException(); }
            public override long Seek(long offset, SeekOrigin origin) { throw new NotSupportedException(); }
            public override void SetLength(long value) { throw new NotSupportedException(); }

            private static uint[] _table;

            private static uint[] Crc32Table
            {
                get
                {
                    if (_table != null) return _table;
                    var t = new uint[256];
                    for (uint i = 0; i < 256; i++)
                    {
                        uint c = i;
                        for (int k = 0; k < 8; k++)
                            c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                        t[i] = c;
                    }
                    _table = t;
                    return t;
                }
            }
        }

        /// <summary>统计已写入字节数（Deflate 输出长度）。</summary>
        private sealed class CountingStream : Stream
        {
            private readonly Stream _inner;
            private readonly Action<long> _report;
            private long _count;

            public CountingStream(Stream inner, Action<long> report) { _inner = inner; _report = report; }

            public override void Write(byte[] buffer, int offset, int count)
            {
                _inner.Write(buffer, offset, count);
                _count += count;
                _report(_count);
            }

            public override void Flush() { _inner.Flush(); }
            public override bool CanRead { get { return false; } }
            public override bool CanSeek { get { return false; } }
            public override bool CanWrite { get { return true; } }
            public override long Length { get { return _count; } }
            public override long Position { get { return _count; } set { throw new NotSupportedException(); } }
            public override int Read(byte[] buffer, int offset, int count) { throw new NotSupportedException(); }
            public override long Seek(long offset, SeekOrigin origin) { throw new NotSupportedException(); }
            public override void SetLength(long value) { throw new NotSupportedException(); }
        }
    }
}
