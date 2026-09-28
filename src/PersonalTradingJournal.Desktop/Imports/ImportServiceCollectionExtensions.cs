using Microsoft.Extensions.DependencyInjection;
using PersonalTradingJournal.Application.Imports;
using PersonalTradingJournal.Application.Imports.Topstep;
using PersonalTradingJournal.Infrastructure.Imports.Csv;
using PersonalTradingJournal.Infrastructure.Imports.Topstep;

namespace PersonalTradingJournal.Desktop.Imports;

public static class ImportServiceCollectionExtensions
{
    public static IServiceCollection AddTopstepDesktopImport(this IServiceCollection services)
    {
        services.AddTransient<IImportCsvFormatDetector, ImportCsvFormatDetector>();
        services.AddTransient<ITopstepCsvParser, TopstepCsvParser>();
        services.AddTransient<ITopstepTradeCandidateReconstructor, TopstepTradeCandidateReconstructor>();
        services.AddTransient<TopstepReferencePreparationService>();
        services.AddTransient<TopstepImportPreviewBuilder>();
        services.AddTransient<ImportTopstepTradesUseCase>();
        return services;
    }
}
