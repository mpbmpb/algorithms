using System.Collections.Specialized;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;

namespace Algorithms;

public static class Karatsuba
{
    public static BigInteger Multiply(ulong x, ulong y)
    {
        var xHigh = x >> 32;
        var yHigh = y >> 32;
        var xTemp = x << 32;
        var yTemp = y << 32;
        var xLow = xTemp >> 32;
        var yLow = yTemp >> 32;

        var a = (BigInteger)MultiplyRecursive(xHigh, yHigh, 32);
        var d = (BigInteger)MultiplyRecursive(xLow, yLow, 32);
        var e = Multiply34Bits((xHigh + xLow), (yHigh + yLow)) - a - d;

        return (a << 64) + (e << 32) + d;
    }
     private static BigInteger Multiply34Bits(ulong x, ulong y)
    {
        var xHigh = x >> 17;
        var yHigh = y >> 17;
        var xTemp = x << 47;
        var yTemp = y << 47;
        var xLow = xTemp >> 47;
        var yLow = yTemp >> 47;

        var a = (BigInteger)MultiplyRecursive(xHigh, yHigh, 18);
        var d = (BigInteger)MultiplyRecursive(xLow, yLow, 18);
        var e = MultiplyRecursive((xHigh + xLow), (yHigh + yLow), 18) - a - d;

        return (a << 34) + (e << 17) + d;
    }

    private static ulong MultiplyRecursive(ulong x, ulong y, int significantBits)
    {
        if (significantBits <= 8)
            return x * y;
        
        significantBits /= 2;
        if ((significantBits & 1) == 1)
            significantBits++;
        
        var xHigh = x >> significantBits;
        var yHigh = y >> significantBits;
        var xTemp = x << (64 - significantBits);
        var yTemp = y << (64 - significantBits);
        var xLow = xTemp >> (64 - significantBits);
        var yLow = yTemp >> (64 - significantBits);

        var a = MultiplyRecursive(xHigh, yHigh, significantBits);
        var d = MultiplyRecursive(xLow, yLow, significantBits);
        var e = MultiplyRecursive(xHigh + xLow, yHigh + yLow, (significantBits + 2)) - a - d;

        return (a << (significantBits * 2)) + (e << significantBits) + d;
    }

    public static BigInteger MultiplySimple(ulong x, ulong y)
    {
        var xHigh = x >> 32;
        var yHigh = y >> 32;
        var xTemp = x << 32;
        var yTemp = y << 32;
        var xLow = xTemp >> 32;
        var yLow = yTemp >> 32;

        var a = (BigInteger)xHigh * yHigh;
        var d = (BigInteger)xLow * yLow;
        var e = (BigInteger)(xHigh + xLow) * (yHigh + yLow) - a - d;

        var bigInt = (a << 64) + (e << 32) + d;
        return bigInt;
    }

    public static BigInteger MultiplyAdditively(ulong x, ulong y)
    {
        BigInteger result = 0;
        BigInteger Y = y;

        for (int radix = 0; radix < 64; radix++)
        {
            result += (((x >> radix) & 1) == 1) ? (Y << radix) : 0;
        }

        return result;
    }
    
    public static BigInteger SimdMultiply(ulong x, ulong y)
    {
        if (x == 0 || y == 0) return 0;
        
         unchecked
        {
            var xHigh = (uint)(x >> 32);
            var yHigh = (uint)(y >> 32);
            var xLow = (uint)x;
            var yLow = (uint)y;

            var oneLargeInput = xHigh >> 31 > 0 || yHigh >> 31 > 0; // if none of the inputs have 1st bit set
                                                                        // then no overflow will occur later

            var vectorX = Vector64.Create(xHigh, xLow);
            var vectorY = Vector64.Create(yHigh, yLow);
            var vectorZ = Vector128.Create(vectorX, vectorY);
            var product = AdvSimd.MultiplyWideningLower(vectorX, vectorY);
            var sum = AdvSimd.AddPairwiseWidening(vectorZ);

            var sumX = sum.GetElement(0);
            var sumY = sum.GetElement(1);
            var sumXcarry = sumX >> 32 != 0;
            var sumYcarry = sumY >> 32 != 0;
            var doubleCarry = sumXcarry & sumYcarry; // if both are true then we may get overflow later

            var sumProduct = sumX * sumY;
            var a = product.GetElement(0);
            var d = product.GetElement(1);
            var e = sumProduct - a - d;

            // build result as 4 uints like: | aHigh | aLow + eHigh | eLow + dHigh | dLow  |
            var dLow = (uint)d;
            var eLow = (uint)e;
            var dHigh = (d >> 32) + eLow;
            var aLow = (uint)a;
            var eHigh = (e >> 32) + aLow + (dHigh >> 32); // carry from eLow + dHigh
            var aHigh = (uint)((a >> 32) +  (eHigh >> 32)      // carry from aLow + eHigh
                + (doubleCarry && oneLargeInput ? 1UL : 0));   // adds 1 if overflow occurred in sumProduct
            var eH = (uint)eHigh;
            var dH = (uint)dHigh;

            //build byte array
            var arr = new byte[17];
            arr[16] = 0; // ensure value is treated as positive (biginteger is built from most to least significant byte )
            Buffer.BlockCopy(BitConverter.GetBytes(dLow), 0, arr, 0, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(dH), 0, arr, 4, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(eH), 0, arr, 8, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(aHigh), 0, arr, 12, 4);
            
            return new BigInteger(arr);
        }
    }
    
    public static string SimdMultiplyToString(ulong x, ulong y)
    {
        unchecked
        {
            if (x == 0 || y == 0) return "0";

            var xHigh = (uint)(x >> 32);
            var yHigh = (uint)(y >> 32);
            var xLow = (uint)x;
            var yLow = (uint)y;

            var oneLargeInput = xHigh >> 31 > 0 || yHigh >> 31 > 0; // if none of the inputs have 1st bit set
                                                                        // then no overflow will occur later

            var vectorX = Vector64.Create(xHigh, xLow);
            var vectorY = Vector64.Create(yHigh, yLow);
            var vectorZ = Vector128.Create(vectorX, vectorY);
            var product = AdvSimd.MultiplyWideningLower(vectorX, vectorY);
            var sum = AdvSimd.AddPairwiseWidening(vectorZ);

            var sumX = sum.GetElement(0);
            var sumY = sum.GetElement(1);
            var sumXcarry = sumX >> 32 != 0;
            var sumYcarry = sumY >> 32 != 0;
            var doubleCarry = sumXcarry & sumYcarry; // if both are true then we may get overflow later

            var sumProduct = sumX * sumY;
            var a = product.GetElement(0);
            var d = product.GetElement(1);
            var e = sumProduct - a - d;

            // build result as 4 uints like: | aHigh | aLow + eHigh | eLow + dHigh | dLow  |
            var dLow = (uint)d;
            var eLow = (uint)e;
            var dHigh = (d >> 32) + eLow;
            var aLow = (uint)a;
            var eHigh = (e >> 32) + aLow + (dHigh >> 32); // carry from eLow + dHigh
            var aHigh = (uint)((a >> 32) +  (eHigh >> 32)      // carry from aLow + eHigh
                + (doubleCarry && oneLargeInput ? 1UL : 0));   // adds 1 if overflow occurred in sumProduct
            var eH = (uint)eHigh;
            var dH = (uint)dHigh;
            
            var byte0 = aHigh;
            var byte1 = eH;
            var byte2 = dH;
            var byte3 = dLow;
            
            
            if (BitConverter.IsLittleEndian)
            {
                // bit hack for changing ints to big endian order
                byte0 = (aHigh & 0x000000FFU) << 24 | (aHigh & 0x0000FF00U) << 8 |
                            (aHigh & 0x00FF0000U) >> 8 | (aHigh & 0xFF000000U) >> 24;
                byte1 = (eH & 0x000000FFU) << 24 | (eH & 0x0000FF00U) << 8 |
                            (eH & 0x00FF0000U) >> 8 | (eH & 0xFF000000U) >> 24;
                byte2 = (dH & 0x000000FFU) << 24 | (dH & 0x0000FF00U) << 8 |
                            (dH & 0x00FF0000U) >> 8 | (dH & 0xFF000000U) >> 24;
                byte3 = (dLow & 0x000000FFU) << 24 | (dLow & 0x0000FF00U) << 8 |
                            (dLow & 0x00FF0000U) >> 8 | (dLow & 0xFF000000U) >> 24;
            }

            //build byte array
            var arr = new byte[16];
            Buffer.BlockCopy(BitConverter.GetBytes(byte0), 0, arr, 0, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(byte1), 0, arr, 4, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(byte2), 0, arr, 8, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(byte3), 0, arr, 12, 4);
            
            return Convert.ToHexString(arr);
        }
    }

    public static string SimdMultiplyToStringBuilder(ulong x, ulong y)
    {
        unchecked
        {
            if (x == 0 || y == 0) return "0";

            var xHigh = (uint)(x >> 32);
            var yHigh = (uint)(y >> 32);
            var xLow = (uint)x;
            var yLow = (uint)y;

            var oneLargeInput = xHigh >> 31 > 0 || yHigh >> 31 > 0; // if none of the inputs have 1st bit set
                                                                        // then no overflow will occur later

            var vectorX = Vector64.Create(xHigh, xLow);
            var vectorY = Vector64.Create(yHigh, yLow);
            var vectorZ = Vector128.Create(vectorX, vectorY);
            var product = AdvSimd.MultiplyWideningLower(vectorX, vectorY);
            var sum = AdvSimd.AddPairwiseWidening(vectorZ);

            var sumX = sum.GetElement(0);
            var sumY = sum.GetElement(1);
            var sumXcarry = sumX >> 32 != 0;
            var sumYcarry = sumY >> 32 != 0;
            var doubleCarry = sumXcarry & sumYcarry; // if both are true then we may get overflow later

            var sumProduct = sumX * sumY;
            var a = product.GetElement(0);
            var d = product.GetElement(1);
            var e = sumProduct - a - d;

            // build result as 4 uints like: | aHigh | aLow + eHigh | eLow + dHigh | dLow  |
            var dLow = (uint)d;
            var eLow = (uint)e;
            var dHigh = (d >> 32) + eLow;
            var aLow = (uint)a;
            var eHigh = (e >> 32) + aLow + (dHigh >> 32); // carry from eLow + dHigh
            var aHigh = (uint)((a >> 32) +  (eHigh >> 32)      // carry from aLow + eHigh
                + (doubleCarry && oneLargeInput ? 1UL : 0));   // adds 1 if overflow occurred in sumProduct
            var eH = (uint)eHigh;
            var dH = (uint)dHigh;

            //build string
            var sb = new StringBuilder();
            var ints = new List<uint> {aHigh, eH, dH, dLow };
            foreach (var integer in ints)
            {
                var bytes = BitConverter.GetBytes(integer);
                
                if (BitConverter.IsLittleEndian)
                {
                    for (int b = 3; b >= 0; b--)
                    {
                        sb.Append($"{bytes[b]:X2}");
                    }
                }
                else
                {
                    for (int b = 0; b < 4; b++)
                    {
                        sb.Append($"{bytes[b]:X2}");
                    }
                }
            }

            return sb.ToString();
        }
    }

    
}

[MemoryDiagnoser]
[RankColumn]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
public class KaratsubaBenchmarks
{
    private int[] Ints { get; set; }
    private long[] Longs1Bit { get; set; }
    private long[] Longs1Byte { get; set; }
    private long[] Longs { get; set; }
    private ulong[] ULongs { get; set; }
    private long[] LongInts { get; set; }
    private byte[] Bytes { get; set; }
    private byte[] Bytes1Bit { get; set; }
    private static byte[] byteArray = BitConverter.GetBytes(ulong.MaxValue);
    private static BitVector32 highBits = new BitVector32(int.MaxValue - 42);
    private static BitVector32 lowBits = new BitVector32(42);
    private static BitVector32.Section B0 = BitVector32.CreateSection(255);
    private static BitVector32.Section B1 = BitVector32.CreateSection(255, B0);
    private static BitVector32.Section B2 = BitVector32.CreateSection(255, B1);
    private static BitVector32.Section B3 = BitVector32.CreateSection(255, B2);

    private long mask = 0b_0100_0000_0000;
    private int intMask = 0b_0100_0000_0000;

    public KaratsubaBenchmarks()
    {
        var random = new Random();
        Ints = Enumerable.Range(1, 1000000).Select(_ => random.Next(0, Int32.MaxValue)).ToArray();
        Longs1Bit = Enumerable.Range(1, 1000000).Select(_ => (long)random.Next(0, 2)).ToArray();
        Longs1Byte = Enumerable.Range(1, 1000000).Select(_ => (long)random.Next(0, 256)).ToArray();
        Longs = Enumerable.Range(1, 1000000).Select(_ => (long)random.Next(0, Int32.MaxValue) << 20).ToArray();
        ULongs = Enumerable.Range(1, 1000000).Select(_ => ((ulong)random.Next(0, Int32.MaxValue) << 32) 
            + (ulong)random.Next(0, Int32.MaxValue)).ToArray();
        LongInts = Enumerable.Range(1, 1000000).Select(_ => (long)random.Next(0, Int32.MaxValue)).ToArray();
        Bytes = Enumerable.Range(1, 1000000).Select(_ => (byte)random.Next(0, 256)).ToArray();
        Bytes1Bit = Enumerable.Range(1, 1000000).Select(_ => (byte)random.Next(0, 2)).ToArray();
    }

    [Benchmark]
    public void EqualsLongTest()
    {
        for (int i = 0; i < Longs.Length; i++)
        {
            var operation = Longs[i] == 0;
        }
    }
    
    [Benchmark]
    public void LongBitShiftTest()
    {
        for (int i = 0; i < Ints.Length / 2; i++)
        {
            mask <<= 1;
            mask >>= 1;
        }
        
    }
    
    [Benchmark]
    public void IntBitShiftTest()
    {
        for (int i = 0; i < Ints.Length / 2; i++)
        {
            intMask <<= 1;
            intMask >>= 1;
        }
        
    }
    
    [Benchmark]
    public void ByteArrayTest()
    {
        for (int i = 0; i < Longs.Length / 8; i++)
        {
            byteArray[0]--;
            byteArray[0]++;
            byteArray[7]--;
            byteArray[7]++;
            byteArray[3] = 0;
            byteArray[3] = 0xff;
            byteArray[5] = 0x04;
            byteArray[5] = 0xf0;
        }
    }
    
    [Benchmark]
    public void ByteArrayEqualsTest()
    {
        for (int i = 0; i < Longs.Length / 4; i++)
        {
            var operation = byteArray[0] == 0x00;
            operation = byteArray[1] == 0x0f;
            operation = byteArray[5] == 0xff;
            operation = byteArray[7] == 0x00;
            
        }
    }
    
    [Benchmark]
    public void BitVectorTest()
    {
        for (int i = 0; i < Longs.Length / 8; i++)
        {
            highBits[B0]--;
            highBits[B0]++;
            lowBits[B3]--;
            lowBits[B3]++;
            highBits[B1] = 0;
            lowBits[B1] = 0xff;
            highBits[B2] = 0x04;
            lowBits[B2] = 0xf0;
        }
    }
     
    [Benchmark]
    public void BitVectorEqualsTest()
    {
        for (int i = 0; i < Longs.Length / 4; i++)
        {
            var operation = highBits[B0] == 0;
            operation = highBits[B1] == 0x0f;
            operation = lowBits[B2] == 0;
            operation = highBits[B3] == 0xff;
        }
    }
    
    [Benchmark]
    public void BitVector1BitEqualsTest()
    {
        var single = BitVector32.CreateSection(1, B0);
        for (int i = 0; i < Longs.Length / 4; i++)
        {
            var operation = highBits[single] == 0;
            operation = highBits[single] == 1;
            operation = lowBits[single] == 0;
            operation = lowBits[single] == 1;
        }
    }
    
    [Benchmark]
    public void BitVectorByteAssign()
    {
        for (int i = 0; i < Ints.Length; i++)
        {
            var operation = new BitVector32(Ints[i]);
        }
    }
    
    [Benchmark]
    public void AdditionTestInts()
    {
        for (int i = 0; i < Ints.Length / 2; i ++)
        {
            var a = Ints[i] + Ints[i + 1];
            var b = Ints[i + 1] + Ints[i];
        }
    }
 
    [Benchmark]
    public void MultiplicationTestInts()
    {
        for (int i = 0; i < Ints.Length / 2; i ++)
        {
            var a = Ints[i] * Ints[i + 1];
            var b = Ints[i + 1] * Ints[i];
        }
    }
    
    [Benchmark]
    public void SimdMultiplicationTestInts()
    {
        for (int i = 0; i < Ints.Length / 4; i ++)
        {
            var vectorX = Vector64.Create(Ints[i], Ints[i + 1]);
            var vectorY = Vector64.Create(Ints[i + 2], Ints[i + 3]);
            var product = AdvSimd.MultiplyWideningLower(vectorX, vectorY);
        }
    }
   
    [Benchmark]
    public void BigIntMultiplication()
    {
        for (int i = 0; i < ULongs.Length / 2; i++)
        {
            var operation = (BigInteger)ULongs[i] * ULongs[i + 1];
        }
    }
    
    [Benchmark]
    public void BigIntMultiplicationToString()
    {
        for (int i = 0; i < ULongs.Length / 2; i++)
        {
            var operation = ((BigInteger)ULongs[i] * ULongs[i + 1]).ToString("X");
        }
    }
    
    [Benchmark]
    public void KaratsubaTest()
    {
        for (int i = 0; i < ULongs.Length / 2; i++)
        {
            var operation = Karatsuba.Multiply(ULongs[i], ULongs[i + 1]);
        }
    }
    
    [Benchmark]
    public void SimpleKaratsubaTest()
    {
        for (int i = 0; i < ULongs.Length / 2; i++)
        {
            var operation = Karatsuba.MultiplySimple(ULongs[i], ULongs[i + 1]);
        }
    }
    
    [Benchmark]
    public void MultiplyAdditivelyTest()
    {
        for (int i = 0; i < ULongs.Length / 2; i++)
        {
            var operation = Karatsuba.MultiplyAdditively(ULongs[i], ULongs[i + 1]);
        }
    }
    
    [Benchmark]
    public void SimdMultiplyTest()
    {
        for (int i = 0; i < ULongs.Length / 2; i++)
        {
            var operation = Karatsuba.SimdMultiply(ULongs[i], ULongs[i + 1]);
        }
    }

    [Benchmark]
    public void SimdMutliplyToStringTest()
    {
        for (int i = 0; i < ULongs.Length / 2; i++)
        {
            var operation = Karatsuba.SimdMultiplyToString(ULongs[i], ULongs[i + 1]);
        }
    }

   [Benchmark]
    public void SimdMutliplyToStringBuilderTest()
    {
        for (int i = 0; i < ULongs.Length / 2; i++)
        {
            var operation = Karatsuba.SimdMultiplyToStringBuilder(ULongs[i], ULongs[i + 1]);
        }
    }

}