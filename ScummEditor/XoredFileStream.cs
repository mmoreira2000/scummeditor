using System;
using System.IO;
using System.Security.AccessControl;
using Microsoft.Win32.SafeHandles;

namespace ScummEditor
{
    public class XoredFileStream : FileStream
    {
        private readonly int _xorKey;

        #region Constructors

        public XoredFileStream(int xorKey, string path, FileMode mode)
            : base(path, mode)
        {
            _xorKey = xorKey;
        }

        public XoredFileStream(int xorKey, string path, FileMode mode, FileAccess access)
            : base(path, mode, access)
        {
            _xorKey = xorKey;
        }

        public XoredFileStream(int xorKey, string path, FileMode mode, FileAccess access, FileShare share)
            : base(path, mode, access, share)
        {
            _xorKey = xorKey;
        }

        public XoredFileStream(int xorKey, string path, FileMode mode, FileAccess access, FileShare share, int bufferSize)
            : base(path, mode, access, share, bufferSize)
        {
            _xorKey = xorKey;
        }

        public XoredFileStream(int xorKey, string path, FileMode mode, FileAccess access, FileShare share, int bufferSize, FileOptions options)
            : base(path, mode, access, share, bufferSize, options)
        {
            _xorKey = xorKey;
        }

        public XoredFileStream(int xorKey, string path, FileMode mode, FileAccess access, FileShare share, int bufferSize, bool useAsync)
            : base(path, mode, access, share, bufferSize, useAsync)
        {
            _xorKey = xorKey;
        }

        public XoredFileStream(int xorKey, string path, FileMode mode, FileSystemRights rights, FileShare share, int bufferSize, FileOptions options, FileSecurity fileSecurity)
            : base(path, mode, rights, share, bufferSize, options, fileSecurity)
        {
            _xorKey = xorKey;
        }

        public XoredFileStream(int xorKey, string path, FileMode mode, FileSystemRights rights, FileShare share, int bufferSize, FileOptions options)
            : base(path, mode, rights, share, bufferSize, options)
        {
            _xorKey = xorKey;
        }

#pragma warning disable 618

        public XoredFileStream(int xorKey, IntPtr handle, FileAccess access)
            : base(handle, access)
        {
            _xorKey = xorKey;
        }

        public XoredFileStream(int xorKey, IntPtr handle, FileAccess access, bool ownsHandle)
            : base(handle, access, ownsHandle)
        {
            _xorKey = xorKey;
        }

        public XoredFileStream(int xorKey, IntPtr handle, FileAccess access, bool ownsHandle, int bufferSize)
            : base(handle, access, ownsHandle, bufferSize)
        {
            _xorKey = xorKey;
        }

        public XoredFileStream(int xorKey, IntPtr handle, FileAccess access, bool ownsHandle, int bufferSize, bool isAsync)
            : base(handle, access, ownsHandle, bufferSize, isAsync)
        {
            _xorKey = xorKey;
        }

#pragma warning restore 618

        public XoredFileStream(int xorKey, SafeFileHandle handle, FileAccess access)
            : base(handle, access)
        {
            _xorKey = xorKey;
        }

        public XoredFileStream(int xorKey, SafeFileHandle handle, FileAccess access, int bufferSize)
            : base(handle, access, bufferSize)
        {
            _xorKey = xorKey;
        }

        public XoredFileStream(int xorKey, SafeFileHandle handle, FileAccess access, int bufferSize, bool isAsync)
            : base(handle, access, bufferSize, isAsync)
        {
            _xorKey = xorKey;
        }

        #endregion

        public int XorKey { get { return _xorKey; } }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int result = base.Read(buffer, offset, count);
            if (result > 0)
            {
                for (int i = offset; i < (offset + count); i++)
                {
                    buffer[i] = (byte)(buffer[i] ^ _xorKey);
                }
            }
            return result;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            var xoredArray = new byte[buffer.Length];
            Array.Copy(buffer, xoredArray, buffer.Length);

            for (int i = 0; i < xoredArray.Length; i++)
            {
                xoredArray[i] = (byte)(xoredArray[i] ^ _xorKey);
            }

            base.Write(xoredArray, offset, count);
        }

        public override int ReadByte()
        {
            return base.ReadByte() ^ _xorKey;
        }

        public override void WriteByte(byte value)
        {
            base.WriteByte((byte) (value ^_xorKey));
        }
    }
}