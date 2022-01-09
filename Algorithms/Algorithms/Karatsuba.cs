using System.Collections;
using System.Collections.Specialized;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;

namespace Algorithms;

public static class Karatsuba
{
    private static byte[]? _X { get; set; }
    private static byte[]? _Y { get; set; }
    private static byte[]? _result { get; set; }

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
        var e = (BigInteger)MultiplyRecursive((xHigh + xLow), (yHigh + yLow), 18) - a - d;

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
        var e = MultiplyRecursive((xHigh + xLow), (yHigh + yLow), (significantBits + 2)) - a - d;

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
        BigInteger Y = (BigInteger)y;

        for (int radix = 0; radix < 64; radix++)
        {
            result += (((x >> radix) & 1) == 1) ? (Y << radix) : 0;
        }

        return result;
    }

    public static BigInteger SimdMultiply(ulong x, ulong y)
    {
        var xHigh = (uint)(x >> 32);
        var yHigh = (uint)(y >> 32);
        var xLow = (uint)x;
        var yLow = (uint)y;
    
        var vectorX = Vector64.Create(xHigh, xLow);
        var vectorY = Vector64.Create(yHigh, yLow);
        var vectorZ = Vector128.Create(vectorX, vectorY);
        var product = AdvSimd.MultiplyWideningLower(vectorX, vectorY);
        var sum = AdvSimd.AddPairwiseWidening(vectorZ);
        var sumX = sum.GetElement(0);
        var sumY = sum.GetElement(1);
        
        var sumProduct = (BigInteger)sum.GetElement(0) * sum.GetElement(1);
        var check = sum.GetElement(0) < uint.MaxValue;
        var a = (BigInteger)product.GetElement(0);
        var d = product.GetElement(1);
        var e = sumProduct - a - d;

        return (a << 64) + (e << 32) + d;
    }
    
    public static string SimdMultiplyToString(ulong x, ulong y)
    {
        if (x == 0 || y == 0) return "0";
        
        var xHigh = (uint)(x >> 32);
        var yHigh = (uint)(y >> 32);
        var xLow = (uint)x;
        var yLow = (uint)y;

        var oneLargeInput = (x >> 63) > 0 || (y >> 63) > 0; // check if at least 1 of the inputs has the 1st bit set
    
        var vectorX = Vector64.Create(xHigh, xLow);
        var vectorY = Vector64.Create(yHigh, yLow);
        var vectorZ = Vector128.Create(vectorX, vectorY);
        var product = AdvSimd.MultiplyWideningLower(vectorX, vectorY);
        var sum = AdvSimd.AddPairwiseWidening(vectorZ);
        
        var sumXcarry = (sum.GetElement(0) >> 32) != 0 ;
        var sumYcarry = (sum.GetElement(1) >> 32) != 0;
        var doubleCarry = sumXcarry & sumYcarry; // if both are true then sumProduct should be += ( 1 << 64 )
        
        var sumProduct = (ulong)sum.GetElement(0) * sum.GetElement(1);
        var a = product.GetElement(0);
        var d = product.GetElement(1);
        var e = sumProduct - a - d;
        
        return (((BigInteger)(a + (doubleCarry && oneLargeInput ? 1UL << 32 : 0))  << 64) 
                + ((BigInteger)e << 32) + d ).ToString("n0");
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
    public void BitVector1BitTest()
    {
        var single = BitVector32.CreateSection(1, B0);
        for (int i = 0; i < Longs.Length / 8; i++)
        {
            highBits[single]--;
            highBits[single]++;
            lowBits[single]--;
            lowBits[single]++;
            highBits[single] = 0;
            highBits[single] = 1;
            lowBits[single] = 0;
            lowBits[single] = 1;
        }
    }
    
    [Benchmark]
    public void BitVector1BoolTest()
    {
        for (int i = 0; i < Longs.Length / 8; i++)
        {
            highBits[0] = true;
            lowBits[2] = true;
             highBits[7] = false;
            lowBits[10] = false;
            highBits[15] = true;
            lowBits[20] = true;
             highBits[31] = false;
            lowBits[30] = false;
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
    public void SimdAdditionTestInts()
    {
        for (int i = 0; i < Ints.Length / 4; i ++)
        {
            var vectorX = Vector64.Create(Ints[i], Ints[i + 1]);
            var vectorY = Vector64.Create(Ints[i + 2], Ints[i + 3]);
            var product = AdvSimd.AddWideningLower(vectorX, vectorY);
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
    public void MultiplicationTestLongInts()
    {
        for (int i = 0; i < LongInts.Length / 2; i ++)
        {
            var a = LongInts[i] * LongInts[i + 1];
            var b = LongInts[i + 1] * LongInts[i];
        }
    }
    
    [Benchmark]
    public void MultiplicationTestULongs()
    {
        for (int i = 0; i < ULongs.Length / 2; i++)
        {
            var operation = ULongs[i] * ULongs[i + 1];
        }
    }
    
    [Benchmark]
    public void MultiplicationTest1BitLongs()
    {
        for (int i = 0; i < Longs1Bit.Length / 2; i++)
        {
            var operation = Longs1Bit[i] * Longs1Bit[i + 1];
        }
    }
    
    [Benchmark]
    public void MultiplicationTest1ByteLongs()
    {
        for (int i = 0; i < Longs1Byte.Length / 2; i++)
        {
            var operation = Longs1Byte[i] * Longs1Byte[i + 1];
        }
    }
    
    [Benchmark]
    public void MultiplicationTestBytes()
    {
        for (int i = 0; i < Bytes.Length / 2; i++)
        {
            var operation = Bytes[i] * Bytes[i + 1];
        }
    }
    
    [Benchmark]
    public void MultiplicationTestBytes1Bit()
    {
        for (int i = 0; i < Bytes1Bit.Length / 2; i++)
        {
            var operation = Bytes1Bit[i] * Bytes1Bit[i + 1];
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
            var operation = ((BigInteger)ULongs[i] * ULongs[i + 1]).ToString("n0");
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
    public void SimpleKaratsuba()
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
}