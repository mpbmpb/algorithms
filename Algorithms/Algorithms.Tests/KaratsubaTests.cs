using System.Numerics;
using Algorithms;

namespace algorithms.Tests;

public class KaratsubaTests
{
    [Theory]
    [InlineData(18446744073709551615, 18446744073709551615, "340,282,366,920,938,463,426,481,119,284,349,108,225")]
    [InlineData(18446744073709, 18446744073709, "340,282,366,920,918,112,425,016,681" )]
    [InlineData(9, 3, "27" )]
    [InlineData(424242424242424242, 2021, "857,393,939,393,939,393,082" )]
    public void Multiply_gives_correct_result(ulong x, ulong y, string expected)
    {
        var result = Karatsuba.Multiply(x, y).ToString("n0");

        result.Should().Match(expected);
    }
    
    [Theory]
    [InlineData(18446744073709551615, 18446744073709551615, "340,282,366,920,938,463,426,481,119,284,349,108,225")]
    [InlineData(18446744073709, 18446744073709, "340,282,366,920,918,112,425,016,681" )]
    [InlineData(9, 3, "27" )]
    [InlineData(424242424242424242, 2021, "857,393,939,393,939,393,082" )]
    public void MultiplySimple_gives_correct_result(ulong x, ulong y, string expected)
    {
        var result = Karatsuba.MultiplySimple(x, y).ToString("n0");

        result.Should().Match(expected);
    }
    
    [Theory]
    [InlineData(18446744073709551615, 18446744073709551615, "340,282,366,920,938,463,426,481,119,284,349,108,225")]
    [InlineData(18446744073709, 18446744073709, "340,282,366,920,918,112,425,016,681" )]
    [InlineData(9, 3, "27" )]
    [InlineData(424242424242424242, 2021, "857,393,939,393,939,393,082" )]
    public void MultiplyAdditively_gives_correct_result(ulong x, ulong y, string expected)
    {
        var result = Karatsuba.MultiplyAdditively(x, y).ToString("n0");

        result.Should().Match(expected);
    }
    
    [Theory]
    [InlineData(18446744073709551615, 18446744073709551615, "340,282,366,920,938,463,426,481,119,284,349,108,225")]
    [InlineData(18446744073709, 18446744073709, "340,282,366,920,918,112,425,016,681" )]
    [InlineData(9, 3, "27" )]
    [InlineData(424242424242424242, 2021, "857,393,939,393,939,393,082" )]
    public void SimdMultiply_gives_correct_result(ulong x, ulong y, string expected)
    {
        var result = Karatsuba.SimdMultiply(x, y).ToString("n0");

        result.Should().Match(expected);
    }
    
}