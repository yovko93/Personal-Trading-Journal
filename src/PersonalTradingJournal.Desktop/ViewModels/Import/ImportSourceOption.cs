using PersonalTradingJournal.Application.Imports;

namespace PersonalTradingJournal.Desktop.ViewModels.Import;

public sealed record ImportSourceOption(ImportCsvFormat Format, string Name);
