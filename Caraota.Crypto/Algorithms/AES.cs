using System.Security.Cryptography;
using System.Runtime.CompilerServices;

namespace Caraota.Crypto.Algorithms
{
    public class AES
    {
        private static ICryptoTransform? _encryptor;
        public static void StartAes(byte[] key)
        {
            if (_encryptor != null) return;

            var aes = Aes.Create();
            aes.KeySize = 256;
            aes.Key = key;
            aes.Mode = CipherMode.ECB;
            aes.Padding = PaddingMode.None;
            _encryptor = aes.CreateEncryptor();
        }
        public static void EncryptDecrypt(Span<byte> data, ReadOnlySpan<byte> iv)
        {
            int remaining = data.Length;
            int llength = 0x5B0;
            int start = 0;

            byte[] myIV = new byte[16];
            byte[] tempIV = new byte[16];
            byte[] expandedIV = MultiplyBytes(iv, 4, 4);
            expandedIV.CopyTo(myIV, 0);

            while (remaining > 0)
            {
                if (remaining < llength)
                {
                    llength = remaining;
                }

                for (int x = start; x < (start + llength); x++)
                {
                    if ((x - start) % 16 == 0)
                    {
                        _encryptor!.TransformBlock(myIV, 0, 16, tempIV, 0);
                        tempIV.CopyTo(myIV, 0);
                    }

                    // XOR
                    data[x] ^= myIV[(x - start) % 16];
                }

                start += llength;
                remaining -= llength;
                llength = 0x5B4;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private static byte[] MultiplyBytes(ReadOnlySpan<byte> input, int count, int mult)
        {
            byte[] ret = new byte[count * mult];
            for (int x = 0; x < ret.Length; x++)
            {
                ret[x] = input[x % count];
            }
            return ret;
        }
    }
}
