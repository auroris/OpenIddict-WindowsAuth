using System;

namespace IdentityServer.ActiveDirectory
{
    /// <summary>
    /// Because the C# Guid class likes to re-arrange bytes internally
    /// </summary>
    public class ADGuid
    {
        public ADGuid()
        {
            Bytes = null;
        }

        public ADGuid(byte[] bytes)
        {
            this.Bytes = bytes;
        }

        public ADGuid(string hex)
        {
            Bytes = HexToByte(hex);
        }

        private byte[] bytes;
        public byte[] Bytes
        {
            get
            {
                return bytes;
            }

            set
            {
                bytes = value;
            }
        }

        public string Guid
        {
            get
            {
                return ByteToHex(bytes);
            }
            set
            {
                bytes = HexToByte(value);
            }
        }

        public override string ToString()
        {
            return Guid;
        }

        public static string Convert(byte[] guid)
        {
            return ByteToHex(guid);
        }

        public static Byte[] Convert(string guid)
        {
            return HexToByte(guid);
        }

        public Byte[] ToByteArray()
        {
            return bytes;
        }

        static byte[] HexToByte(string input)
        {
            var outputLength = input.Length / 2;
            var output = new byte[outputLength];
            var numeral = new char[2];
            for (int i = 0; i < outputLength; i++)
            {
                input.CopyTo(i * 2, numeral, 0, 2);
                output[i] = System.Convert.ToByte(new string(numeral), 16);
            }
            return output;
        }

        static string ByteToHex(byte[] bytes)
        {
            char[] c = new char[bytes.Length * 2];
            int b;
            for (int i = 0; i < bytes.Length; i++)
            {
                b = bytes[i] >> 4;
                c[i * 2] = (char)(87 + b + (((b - 10) >> 31) & -39));
                b = bytes[i] & 0xF;
                c[i * 2 + 1] = (char)(87 + b + (((b - 10) >> 31) & -39));
            }
            return new string(c);
        }
    }
}