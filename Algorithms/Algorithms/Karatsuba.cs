using System.Collections;
using System.Numerics;
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
    
}

[MemoryDiagnoser]
[RankColumn]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
public class KaratsubaResearch
{
    public int[] Ints { get; set; }
    public long[] Longs1Bit { get; set; }
    public long[] Longs1Byte { get; set; }
    public long[] Longs { get; set; }
    public ulong[] ULongs { get; set; }
    public long[] LongInts { get; set; }
    public byte[] Bytes { get; set; }
    public byte[] Bytes1Bit { get; set; }
   
    public long mask = 0b_0100_0000_0000;
    public int intMask = 0b_0100_0000_0000;

    public KaratsubaResearch()
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
    public void MultiplicationTestLongInts()
    {
        for (int i = 0; i < LongInts.Length; i += 2)
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
    public void SimpleKaratsuba()
    {
        for (int i = 0; i < ULongs.Length / 2; i++)
        {
            var operation = Karatsuba.MultiplySimple(ULongs[i], ULongs[i + 1]);
        }
    }

    [Benchmark]
    public void BigIntMultiplication()
    {
        for (int i = 0; i < ULongs.Length / 2; i++)
        {
            var operation = (BigInteger)ULongs[i] * (BigInteger)ULongs[i + 1];
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
    public void MultiplyAdditivelyTest()
    {
        for (int i = 0; i < ULongs.Length / 2; i++)
        {
            var operation = Karatsuba.MultiplyAdditively(ULongs[i], ULongs[i + 1]);
        }
    }
}