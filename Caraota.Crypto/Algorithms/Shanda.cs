using System.Runtime.CompilerServices;

namespace Caraota.Crypto.Algorithms
{
    public static unsafe class Shanda
    {

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static void Encrypt(Span<byte> data)
        {
            int size = data.Length;
            if (size == 0) return;

            fixed (byte* pData = data)
            {
                for (int i = 0; i < 3; i++)
                {
                    byte a = 0;
                    for (int j = size; j > 0; j--)
                    {
                        int idx = size - j;
                        byte* pCurr = pData + idx;
                        byte c = *pCurr;

                        c = RotateLeft(c, 3);
                        c = (byte)(c + j);
                        c ^= a;
                        a = c;
                        c = RotateRight(a, j);
                        c ^= 0xFF;
                        c += 0x48;
                        *pCurr = c;
                    }

                    a = 0;
                    for (int j = size; j > 0; j--)
                    {
                        int idx = j - 1;
                        byte* pCurr = pData + idx;
                        byte c = *pCurr;

                        c = RotateLeft(c, 4);
                        c = (byte)(c + j);
                        c ^= a;
                        a = c;
                        c ^= 0x13;
                        c = RotateRight(c, 3);
                        *pCurr = c;
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static void Decrypt(Span<byte> data)
        {
            int size = data.Length;
            if (size == 0) return;

            fixed (byte* pData = data)
            {
                for (int i = 0; i < 3; i++)
                {
                    byte a = 0;
                    byte b = 0;
                    for (int j = size; j > 0; j--)
                    {
                        int idx = j - 1;
                        byte* pCurr = pData + idx;
                        byte c = *pCurr;

                        c = RotateLeft(c, 3);
                        c ^= 0x13;
                        a = c;
                        c ^= b;
                        c = (byte)(c - j);
                        c = RotateRight(c, 4);
                        b = a;
                        *pCurr = c;
                    }

                    a = 0;
                    b = 0;
                    for (int j = size; j > 0; j--)
                    {
                        int idx = size - j;
                        byte* pCurr = pData + idx;
                        byte c = *pCurr;

                        c -= 0x48;
                        c ^= 0xFF;
                        c = RotateLeft(c, j);
                        a = c;
                        c ^= b;
                        c = (byte)(c - j);
                        c = RotateRight(c, 3);
                        b = a;
                        *pCurr = c;
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte RotateLeft(byte val, int num)
        {
            num &= 7;
            return (byte)((val << num) | (val >> (8 - num)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte RotateRight(byte val, int num)
        {
            num &= 7;
            return (byte)((val >> num) | (val << (8 - num)));
        }
    }
}

