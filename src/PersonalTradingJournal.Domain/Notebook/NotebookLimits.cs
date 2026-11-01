using System.Text;

namespace PersonalTradingJournal.Domain.Notebook;

public static class NotebookLimits
{
    public const int Title = 200, FolderName = 128, TagName = 64, Caption = 2000, AlternativeText = 500;
    public const int DocumentBytes = 1024 * 1024, PlainText = 200_000, Nodes = 4096, Depth = 16, ListDepth = 3;
    public const int Tags = 32, Links = 100, Images = 32, Url = 2048, Search = 256;
    public const int ImageBytes = 8 * 1024 * 1024, ImageDimension = 8192, ImagePixels = 16_000_000;

    public static Guid Id(Guid value, string parameter)
    {
        if (value == Guid.Empty) throw new ArgumentException("An identity is required.", parameter);
        return value;
    }

    public static Guid? OptionalId(Guid? value, string parameter) =>
        value is { } id ? Id(id, parameter) : null;

    public static string Text(string value, int maximum, string parameter, bool required = false)
    {
        ArgumentNullException.ThrowIfNull(value, parameter);
        if (value.Length > maximum) throw new ArgumentOutOfRangeException(parameter, $"Maximum length is {maximum}.");
        if (required && string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Meaningful text is required.", parameter);
        for (int i = 0; i < value.Length; i++)
        {
            if (value[i] == '\0') throw new ArgumentException("NUL is not supported.", parameter);
            if (!char.IsSurrogate(value[i])) continue;
            if (!char.IsHighSurrogate(value[i]) || i + 1 == value.Length || !char.IsLowSurrogate(value[++i]))
                throw new ArgumentException("Valid Unicode is required.", parameter);
        }
        return value;
    }

    public static string Name(string value, int maximum)
    {
        ArgumentNullException.ThrowIfNull(value);
        Text(value, int.MaxValue, nameof(value), true);
        return Text(value.Trim().Normalize(NormalizationForm.FormC), maximum, nameof(value), true);
    }

    public static string NoteTitle(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return Text(value.Trim(), Title, nameof(value), true);
    }

    internal static IReadOnlyList<T> Copy<T>(IEnumerable<T> values, int maximum, string parameter)
    {
        ArgumentNullException.ThrowIfNull(values, parameter);
        var copy = values.Take(maximum + 1).ToArray();
        if (copy.Length > maximum || copy.Any(x => x is null))
            throw new ArgumentException($"Supply at most {maximum} non-null entries.", parameter);
        return Array.AsReadOnly(copy);
    }
}
