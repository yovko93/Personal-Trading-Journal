using Microsoft.Extensions.Logging.Abstractions;
using PersonalTradingJournal.Application.DailyReview.Coaching;
using PersonalTradingJournal.Desktop.Settings;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Settings;

namespace PersonalTradingJournal.Desktop.Tests.Settings;

public sealed class CoachingProviderSelectionTests
{
    [Theory]
    [InlineData(false, CoachingProviderKind.Groq)]
    [InlineData(true, CoachingProviderKind.OpenAI)]
    public void InitialSelectionPreservesExistingUsersAndSurvivesRestart(bool existing, CoachingProviderKind expected)
    {
        using var f = new CoachingCredentialsTests.SecretFixture();
        var groq = new ProtectedCoachingCredentials(f.Paths.GroqCredentialsPath, () => null);
        var config = new CoachingConfiguration(f.Paths.CoachingProviderPath, f.Store, groq, existing);
        Assert.Equal(expected, config.SelectedProvider);
        Assert.True(config.Select(CoachingProviderKind.Groq));
        Assert.Equal(CoachingProviderKind.Groq, new CoachingConfiguration(f.Paths.CoachingProviderPath, f.Store, groq, true).SelectedProvider);
        Assert.False(f.Paths.GroqCredentialsPath.StartsWith(f.Paths.DataDirectory + Path.DirectorySeparatorChar));
    }

    [Fact]
    public void KeysAreIndependentAndSettingsSwitchSaveReplaceRemoveNeverExposesStoredValue()
    {
        using var f = new CoachingCredentialsTests.SecretFixture();
        f.Environment = "synthetic-openai-env";
        var groq = new ProtectedCoachingCredentials(f.Paths.GroqCredentialsPath, () => "synthetic-groq-env");
        var config = new CoachingConfiguration(f.Paths.CoachingProviderPath, f.Store, groq, false);
        Assert.Equal(CoachingProviderKind.OpenAI, config.SelectedProvider); // Existing environment configuration.
        using var vm = Settings(config);
        vm.SaveKey("synthetic-openai-saved");
        vm.SelectedProvider = CoachingProviderKind.Groq;
        Assert.Contains("GROQ_API_KEY", vm.CredentialStatus);
        vm.SaveKey("synthetic-groq-first"); vm.SaveKey("synthetic-groq-replaced");
        Assert.True(config.Resolve() == "synthetic-groq-replaced");
        Assert.True(config.CredentialsFor(CoachingProviderKind.OpenAI).Resolve() == "synthetic-openai-saved");
        Assert.DoesNotContain("synthetic", vm.CredentialMessage + vm.CredentialStatus);
        Assert.DoesNotContain("synthetic", System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(f.Paths.GroqCredentialsPath)));
        vm.RemoveKeyCommand.Execute(null);
        Assert.Contains("GROQ_API_KEY", vm.CredentialStatus);
        Assert.True(config.Resolve() == "synthetic-groq-env");
        vm.SelectedProvider = CoachingProviderKind.OpenAI;
        Assert.Equal(CoachingCredentialSource.Saved, vm.CredentialSource);
        vm.RemoveKeyCommand.Execute(null);
        Assert.Contains("OPENAI_API_KEY", vm.CredentialStatus);
        Assert.True(config.Resolve() == "synthetic-openai-env");
    }

    [Fact]
    public void CorruptGroqKeyCannotUseOpenAiKeyAndUnknownChoiceRequiresExplicitRecovery()
    {
        using var f = new CoachingCredentialsTests.SecretFixture();
        var groq = new ProtectedCoachingCredentials(f.Paths.GroqCredentialsPath, () => "synthetic-groq-env");
        var config = new CoachingConfiguration(f.Paths.CoachingProviderPath, f.Store, groq, false);
        Assert.True(f.Store.Save("synthetic-openai"));
        File.WriteAllBytes(f.Paths.GroqCredentialsPath, [0]);
        Assert.Equal(CoachingCredentialSource.Unreadable, config.GetSource());
        Assert.Throws<CoachingCredentialException>(() => config.Resolve());
        File.WriteAllText(f.Paths.CoachingProviderPath, "unknown-provider");
        var restarted = new CoachingConfiguration(f.Paths.CoachingProviderPath, f.Store, groq, true);
        Assert.Equal(CoachingProviderKind.Unavailable, restarted.SelectedProvider);
        Assert.Throws<CoachingCredentialException>(() => restarted.Resolve());
        Assert.True(restarted.Select(CoachingProviderKind.OpenAI));
        Assert.True(restarted.Resolve() == "synthetic-openai");
    }

    [Fact]
    public void FailedProviderWriteKeepsOriginalSelectionAndKeys()
    {
        using var f = new CoachingCredentialsTests.SecretFixture();
        var groq = new ProtectedCoachingCredentials(f.Paths.GroqCredentialsPath, () => null);
        var config = new CoachingConfiguration(f.Paths.CoachingProviderPath, f.Store, groq, false);
        using var held = new FileStream(f.Paths.CoachingProviderPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var vm = Settings(config);
        vm.SelectedProvider = CoachingProviderKind.OpenAI;
        Assert.Equal(CoachingProviderKind.Groq, vm.SelectedProvider);
        Assert.Contains("could not be saved", vm.CredentialMessage);
    }

    internal static SettingsViewModel Settings(CoachingConfiguration configuration) => new(
        new FakeThemeService(), new FakeDesktopSettingsStore(), NullLogger<SettingsViewModel>.Instance, configuration: configuration);
}
