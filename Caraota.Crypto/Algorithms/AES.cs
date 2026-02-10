using System.Security.Cryptography;
using System.Runtime.CompilerServices;

namespace Caraota.Crypto.Algorithms
{
    public class AES
    {
        private static ICryptoTransform? _encryptor;
        private static readonly byte[] _myIvBuffer = new byte[16];
        private static readonly byte[] _tempIvBuffer = new byte[16];

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

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static unsafe void EncryptDecrypt(Span<byte> data, ReadOnlySpan<byte> iv)
        {
            int remaining = data.Length;

            fixed (byte* pIvSrc = iv)
            fixed (byte* pMyIv = _myIvBuffer)
            {
                uint ivUint = *(uint*)pIvSrc;

                uint* pDestUint = (uint*)pMyIv;
                pDestUint[0] = ivUint;
                pDestUint[1] = ivUint;
                pDestUint[2] = ivUint;
                pDestUint[3] = ivUint;
            }

            int start = 0;
            int llength = 0x5B0;

            fixed (byte* pData = data)
            fixed (byte* pMyIv = _myIvBuffer)
            fixed (byte* pTempIv = _tempIvBuffer)
            {
                while (remaining > 0)
                {
                    if (remaining < llength) llength = remaining;

                    for (int x = 0; x < llength; x++)
                    {
                        int ivIdx = x & 15;

                        if (ivIdx == 0)
                        {
                            _encryptor!.TransformBlock(_myIvBuffer, 0, 16, _tempIvBuffer, 0);

                            *(long*)pMyIv = *(long*)pTempIv;
                            *(long*)(pMyIv + 8) = *(long*)(pTempIv + 8);
                        }

                        *(pData + start + x) ^= *(pMyIv + ivIdx);
                    }

                    start += llength;
                    remaining -= llength;
                    llength = 0x5B4;
                }
            }
        }
    }
}
