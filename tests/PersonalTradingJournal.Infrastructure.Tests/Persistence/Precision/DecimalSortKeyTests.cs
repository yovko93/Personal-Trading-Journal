using Microsoft.Data.Sqlite;
using PersonalTradingJournal.Infrastructure.Persistence.Sorting;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.Precision;

public sealed class DecimalSortKeyTests
{
    public static TheoryData<decimal, decimal> OrderedPairs => new()
    {
        { -3300m, -10m },
        { -10m, -2m },
        { -2m, -1.25m },
        { -1.25m, -1.2m },
        { -1.2m, -0.01m },
        { -0.01m, 0m },
        { 0m, 0.001m },
        { 0.001m, 1m },
        { 1m, 1.2m },
        { 1.2m, 1.25m },
        { 1.25m, 2m },
        { 2m, 10m },
        { 10m, 1378.5m },
        { 1378.5m, 3300m },
        { decimal.MinValue, -3300m },
        { 3300m, decimal.MaxValue },
    };

    [Theory]
    [MemberData(nameof(OrderedPairs))]
    public void EncodePreservesNumericOrder(decimal lower, decimal higher)
    {
        string lowerKey = DecimalSortKey.Encode(lower);
        string higherKey = DecimalSortKey.Encode(higher);

        Assert.Equal(DecimalSortKey.Length, lowerKey.Length);
        Assert.Equal(DecimalSortKey.Length, higherKey.Length);
        Assert.True(StringComparer.Ordinal.Compare(lowerKey, higherKey) < 0);
    }

    [Theory]
    [InlineData("1.2", "1.20")]
    [InlineData("0", "0.0000000000000000000000000000")]
    [InlineData("-1.2", "-1.20")]
    public void EncodeUsesEqualKeysForEqualValues(string left, string right)
    {
        decimal leftValue = decimal.Parse(
            left,
            System.Globalization.CultureInfo.InvariantCulture);
        decimal rightValue = decimal.Parse(
            right,
            System.Globalization.CultureInfo.InvariantCulture);

        Assert.Equal(
            DecimalSortKey.Encode(leftValue),
            DecimalSortKey.Encode(rightValue));
    }

    [Fact]
    public async Task BinarySqliteOrderingMatchesDecimalOrderingForRepresentativeValues()
    {
        decimal[] values = CreateRepresentativeValues();
        decimal[] expected = values.Distinct().Order().ToArray();
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using (SqliteCommand create = connection.CreateCommand())
        {
            create.CommandText =
                "CREATE TABLE SortKeys (ValueIndex INTEGER, SortKey TEXT COLLATE BINARY);";
            _ = await create.ExecuteNonQueryAsync();
        }

        for (int index = 0; index < expected.Length; index++)
        {
            await using SqliteCommand insert = connection.CreateCommand();
            insert.CommandText =
                "INSERT INTO SortKeys (ValueIndex, SortKey) VALUES ($index, $key);";
            _ = insert.Parameters.AddWithValue("$index", index);
            _ = insert.Parameters.AddWithValue(
                "$key",
                DecimalSortKey.Encode(expected[index]));
            _ = await insert.ExecuteNonQueryAsync();
        }

        var actualIndexes = new List<int>();
        await using SqliteCommand select = connection.CreateCommand();
        select.CommandText =
            "SELECT ValueIndex FROM SortKeys ORDER BY SortKey COLLATE BINARY;";
        await using SqliteDataReader reader = await select.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            actualIndexes.Add(reader.GetInt32(0));
        }

        Assert.Equal(Enumerable.Range(0, expected.Length), actualIndexes);
    }

    private static decimal[] CreateRepresentativeValues()
    {
        var values = new List<decimal>
        {
            decimal.MinValue,
            -7922816251426433759354395033.5m,
            -3300m,
            -10m,
            -2m,
            -1.25m,
            -1.20m,
            -0.01m,
            -0.0000000000000000000000000001m,
            0m,
            0.0000000000000000000000000001m,
            0.001m,
            0.1m,
            0.2m,
            0.3m,
            1m,
            1.2m,
            1.20m,
            1.25m,
            2m,
            10m,
            1378.5m,
            29131.25m,
            29200.50m,
            3300m,
            7922816251426433759354395033.5m,
            decimal.MaxValue,
        };
        var random = new Random(1847);
        for (int index = 0; index < 500; index++)
        {
            values.Add(new decimal(
                random.Next(),
                random.Next(),
                random.Next(),
                random.Next(0, 2) == 1,
                (byte)random.Next(0, 29)));
        }

        return values.ToArray();
    }
}
