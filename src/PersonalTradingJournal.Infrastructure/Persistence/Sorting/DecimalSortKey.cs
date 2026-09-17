using System.Globalization;
using System.Numerics;

namespace PersonalTradingJournal.Infrastructure.Persistence.Sorting;

/// <summary>
/// Encodes a <see cref="decimal"/> as a fixed-width text key whose ordinal order
/// is identical to the decimal's numeric order.
/// </summary>
public static class DecimalSortKey
{
    public const int Length = 58;

    private const int DecimalScale = 28;
    private const int MagnitudeWidth = Length - 1;
    private static readonly BigInteger[] PowersOfTen = BuildPowersOfTen();
    private static readonly BigInteger MaximumScaledMagnitude =
        ((BigInteger.One << 96) - BigInteger.One) * PowersOfTen[DecimalScale];

    public static string Encode(decimal value)
    {
        int[] bits = decimal.GetBits(value);
        var magnitude = new BigInteger((uint)bits[0]) |
            (new BigInteger((uint)bits[1]) << 32) |
            (new BigInteger((uint)bits[2]) << 64);
        int scale = (bits[3] >> 16) & 0xFF;
        BigInteger scaledMagnitude =
            magnitude * PowersOfTen[DecimalScale - scale];
        bool isNegative = (bits[3] & int.MinValue) != 0 && magnitude != 0;
        BigInteger orderedMagnitude = isNegative
            ? MaximumScaledMagnitude - scaledMagnitude
            : scaledMagnitude;

        return string.Concat(
            isNegative ? "0" : "1",
            orderedMagnitude.ToString(
                $"D{MagnitudeWidth}",
                CultureInfo.InvariantCulture));
    }

    private static BigInteger[] BuildPowersOfTen()
    {
        var values = new BigInteger[DecimalScale + 1];
        values[0] = BigInteger.One;
        for (int index = 1; index < values.Length; index++)
        {
            values[index] = values[index - 1] * 10;
        }

        return values;
    }
}
