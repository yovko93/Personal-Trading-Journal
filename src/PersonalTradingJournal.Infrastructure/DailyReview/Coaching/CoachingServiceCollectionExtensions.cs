using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PersonalTradingJournal.Application.DailyReview.Coaching;

namespace PersonalTradingJournal.Infrastructure.DailyReview.Coaching;

public static class CoachingServiceCollectionExtensions
{
    /// <summary>Registration is inert: no credential access or network request until GenerateAsync.
    /// Dedicated HttpClient has no logging/retry middleware and never follows redirects with evidence.</summary>
    public static IServiceCollection AddDailyCoaching(this IServiceCollection services)
    {
        services.AddSingleton(new OpenAiCoachingOptions());
        services.AddSingleton(new GroqCoachingOptions());
        services.AddSingleton(new CoachingGenerationOptions());
        services.AddSingleton<Transport>();
        services.TryAddSingleton<ICoachingCredentials, EnvironmentCredentials>();
        services.AddSingleton<OpenAiCoachingProvider>(s => new(s.GetRequiredService<Transport>().Client,
            s.GetRequiredService<OpenAiCoachingOptions>(),
            () => s.GetService<ICoachingConfiguration>() is { } configuration
                ? configuration.CredentialsFor(CoachingProviderKind.OpenAI).Resolve()
                : s.GetRequiredService<ICoachingCredentials>().Resolve()));
        services.AddSingleton<GroqCoachingProvider>(s => new(s.GetRequiredService<Transport>().Client,
            s.GetRequiredService<GroqCoachingOptions>(),
            () => s.GetService<ICoachingConfiguration>() is { } configuration
                ? configuration.CredentialsFor(CoachingProviderKind.Groq).Resolve()
                : Environment.GetEnvironmentVariable("GROQ_API_KEY")));
        services.AddSingleton<ICoachingProvider>(s => s.GetService<ICoachingConfiguration>() is { } configuration
            ? new SelectedCoachingProvider(configuration, s.GetRequiredService<OpenAiCoachingProvider>(), s.GetRequiredService<GroqCoachingProvider>())
            : s.GetRequiredService<OpenAiCoachingProvider>());
        services.AddTransient(s => new DailyCoachingGenerationService(s.GetRequiredService<ICoachingProvider>(),
            s.GetRequiredService<CoachingGenerationOptions>(), TimeProvider.System));
        services.AddTransient(s => new GenerateAndSaveCoachingService(s.GetRequiredService<DailyCoachingGenerationService>(),
            s.GetRequiredService<ICoachingAnalysisRepository>(), TimeProvider.System));
        return services;
    }

    private sealed class Transport : IDisposable
    {
        public HttpClient Client { get; } = new(new SocketsHttpHandler
        {
            AllowAutoRedirect = false, UseCookies = false, PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        }) { Timeout = Timeout.InfiniteTimeSpan };

        public void Dispose() => Client.Dispose();
    }

    private sealed class EnvironmentCredentials : ICoachingCredentials
    {
        public string? Resolve() => Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        public CoachingCredentialSource GetSource() => string.IsNullOrWhiteSpace(Resolve())
            ? CoachingCredentialSource.None : CoachingCredentialSource.Environment;
    }
}
