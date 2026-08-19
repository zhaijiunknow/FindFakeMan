namespace PhotoshopFile
{
    using System;
    using System.IO;
    using System.Text;

    /// <summary>
    /// Reads primitive data types as binary values in in big-endian format
    /// </summary>
    public class BinaryReverseReader : BinaryReader
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BinaryReverseReader"/> class using the given stream.
        /// </summary>
        /// <param name="stream">The stream to read through.</param>
        public BinaryReverseReader(Stream stream)
            : base(stream, Encoding.UTF7)
        {
        }

        /// <summary>
        /// Reads a 16 bit int (2 bytes) from the stream.
        /// </summary>
        /// <returns>The read 16 bit int.</returns>
        public override short ReadInt16()
        {
            short num = base.ReadInt16();
            num = ReverseBytes(num);
            return num;
        }

        /// <summary>
        /// Reads a 32 bit int (4 bytes) from the stream.
        /// </summary>
        /// <returns>The read 32 bit int.</returns>
        public override int ReadInt32()
        {
            int num = base.ReadInt32();
            num = ReverseBytes(num);
            return num;
        }

        /// <summary>
        /// Reads a 64 bit int (8 bytes) from the stream.
        /// </summary>
        /// <returns>The read 64 bit int.</returns>
        public override long ReadInt64()
        {
            long num = base.ReadInt64();
            num = ReverseBytes(num);
            return num;
        }

        /// <summary>
        /// Reads an unsigned 16 bit int (2 bytes) from the stream.
        /// </summary>
        /// <returns>The read unsigned 16 bit int.</returns>
        public override ushort ReadUInt16()
        {
            ushort num = base.ReadUInt16();
            num = ReverseBytes(num);
            return num;
        }

        /// <summary>
        /// Reads an unsigned 32 bit int (4 bytes) from the stream.
        /// </summary>
        /// <returns>The read unsigned 32 bit int.</returns>
        public override uint ReadUInt32()
        {
            uint num = base.ReadUInt32();
            num = ReverseBytes(num);
            return num;
        }

        /// <summary>
        /// Reads an unsigned 64 bit int (8 bytes) from the stream.
        /// </summary>
        /// <returns>The read unsigned 64 bit int.</returns>
        public override ulong ReadUInt64()
        {
            ulong num = base.ReadUInt64();
            num = ReverseBytes(num);
            return num;
        }

        /// <summary>
        /// Reads a pascal string from the stream.
        /// </summary>
        /// <returns>The read string.</returns>
        public string ReadPascalString()
        {
            byte num1 = ReadByte();
            byte[] bytes = ReadBytes(num1);
            if (num1 % 2 == 0)
            {
                ReadByte();
            }

            return new string(Encoding.ASCII.GetChars(bytes));
        }

        /// <summary>
        /// Reads a floating point number from the stream.  It reads until the newline character '\n' is found.
        /// </summary>
        /// <returns>The read floating point number.</returns>
        public float ReadFloat()
        {
            string str = string.Empty;

            try
            {
                for (int index = PeekChar(); index != 10; index = PeekChar())
                {
                    if (index != 32)
                    {
                        str = str + ReadChar();
                    }
                    else
                    {
                        break;
                    }
                }
            }
            catch (ArgumentException)
            {
                UnityEngine.Debug.LogError("An invalid character was found in the string.");
            }

            if (string.IsNullOrEmpty(str))
            {
                return 0.0f;
            }

            return Convert.ToSingle(str);
        }

        /// <summary>
        /// Reads a string stored with a null byte preceding each character.
        /// </summary>
        /// <returns>The read string.</returns>
        public override string ReadString()
        {
            StringBuilder str = new StringBuilder();
            try
            {
                while (BaseStream.Position + 1 < BaseStream.Length)
                {
                    // PSD descriptor strings are UTF-16 Big Endian. They are terminated by
                    // either a NUL byte followed by the plist close marker "\r)", a bare "\r)"
                    // pair, a legacy NUL-NUL pair, or a ")" immediately followed by a newline.
                    byte high = ReadByte();
                    byte low = ReadByte();
                    if ((high == 0 && low == 0) ||
                        (high == 0 && low == 0x0d) ||
                        (high == 0x0d && low == 0x29) ||
                        (high == 0x29 && low == 0x0a))
                    {
                        break;
                    }

                    str.Append((char)((high << 8) | low));
                }
            }
            catch (ArgumentException)
            {
                UnityEngine.Debug.LogError("An invalid character was found in the string.");
            }

            return str.ToString();
        }

        /// <summary>
        /// Searches through the stream for the given string.  If found, the position in the stream
        /// will be the byte right AFTER the search string.  If it is not found, the position is set
        /// to the end of the stream.
        /// </summary>
        /// <param name="search">The string to search for.</param>
        /// <returns>True if the search string was found, otherwise false.</returns>
        public bool Seek(string search)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(search);
            return Seek(bytes);
        }

        /// <summary>
        /// Reverses the bytes in order to convert back and forth between Big and Little Endian.
        /// </summary>
        /// <returns>The number with the reversed bytes.</returns>
        /// <param name="value">The number to reverse the bytes of.</param>
        /// <remarks>
        /// See: http://www.csharp-examples.net/reverse-bytes/
        /// And: http://stackoverflow.com/questions/19560436/bitwise-endian-swap-for-various-types
        /// </remarks>
        private Int16 ReverseBytes(Int16 value)
        {
            return (Int16)ReverseBytes((UInt16)value);
        }

        /// <summary>
        /// Reverses the bytes in order to convert back and forth between Big and Little Endian.
        /// </summary>
        /// <returns>The number with the reversed bytes.</returns>
        /// <param name="value">The number to reverse the bytes of.</param>
        private Int32 ReverseBytes(Int32 value)
        {
            return (Int32)ReverseBytes((UInt32)value);
        }

        /// <summary>
        /// Reverses the bytes in order to convert back and forth between Big and Little Endian.
        /// </summary>
        /// <returns>The number with the reversed bytes.</returns>
        /// <param name="value">The number to reverse the bytes of.</param>
        private Int64 ReverseBytes(Int64 value)
        {
            return (Int64)ReverseBytes((UInt64)value);
        }

        /// <summary>
        /// Reverses the bytes in order to convert back and forth between Big and Little Endian.
        /// </summary>
        /// <returns>The number with the reversed bytes.</returns>
        /// <param name="value">The number to reverse the bytes of.</param>
        private UInt16 ReverseBytes(UInt16 value)
        {
            return (UInt16)((value & 0xFFU) << 8 | (value & 0xFF00U) >> 8);
        }

        /// <summary>
        /// Reverses the bytes in order to convert back and forth between Big and Little Endian.
        /// </summary>
        /// <returns>The number with the reversed bytes.</returns>
        /// <param name="value">The number to reverse the bytes of.</param>
        private UInt32 ReverseBytes(UInt32 value)
        {
            return (value & 0x000000FFU) << 24 | (value & 0x0000FF00U) << 8 |
                (value & 0x00FF0000U) >> 8 | (value & 0xFF000000U) >> 24;
        }

        /// <summary>
        /// Reverses the bytes in order to convert back and forth between Big and Little Endian.
        /// </summary>
        /// <returns>The number with the reversed bytes.</returns>
        /// <param name="value">The number to reverse the bytes of.</param>
        private UInt64 ReverseBytes(UInt64 value)
        {
            return (value & 0x00000000000000FFUL) << 56 | (value & 0x000000000000FF00UL) << 40 |
                (value & 0x0000000000FF0000UL) << 24 | (value & 0x00000000FF000000UL) << 8 |
                    (value & 0x000000FF00000000UL) >> 8 | (value & 0x0000FF0000000000UL) >> 24 |
                    (value & 0x00FF000000000000UL) >> 40 | (value & 0xFF00000000000000UL) >> 56;
        }

        /// <summary>
        /// Searches through the stream for the given byte array.  If found, the position in the stream
        /// will be the byte right AFTER the search array.  If it is not found, the position is set to
        /// the end of the stream.
        /// </summary>
        /// <param name="search">The byte array sequence to search for in the stream</param>
        /// <returns>True if the search array was found, otherwise false.</returns>
        private bool Seek(byte[] search)
        {
            if (search == null || search.Length == 0)
            {
                return false;
            }

            long length = BaseStream.Length;
            while (BaseStream.Position + search.Length <= length)
            {
                long start = BaseStream.Position;
                bool matched = true;
                for (int index = 0; index < search.Length; ++index)
                {
                    if (ReadByte() != search[index])
                    {
                        matched = false;
                        break;
                    }
                }

                if (matched)
                {
                    return true;
                }

                // advance a single byte and try again so overlapping matches are not skipped
                BaseStream.Position = start + 1;
            }

            // not found: leave the position at the end of the stream
            BaseStream.Position = length;
            return false;
        }
    }
}
