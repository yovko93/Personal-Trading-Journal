using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using PersonalTradingJournal.Application.DailyReview.Coaching;
using PersonalTradingJournal.Desktop.Settings;
using PersonalTradingJournal.Desktop.Tests.DailyReview;
using PersonalTradingJournal.Desktop.Tests.TestDoubles;
using PersonalTradingJournal.Desktop.ViewModels.Settings;
using PersonalTradingJournal.Infrastructure.DailyReview.Coaching;
using PersonalTradingJournal.Infrastructure.Storage;

namespace PersonalTradingJournal.Desktop.Tests.Settings;

public sealed class CoachingCredentialsTests
{
    [Fact]
    public void SaveReplaceRestartRemoveAndFallbackNeverExposePlaintextAtRest()
    {
        using var f = new SecretFixture();
        Assert.Equal(CoachingCredentialSource.None, f.Store.GetSource());
        f.Environment = "synthetic-environment";
        Assert.Equal(CoachingCredentialSource.Environment, f.Store.GetSource());
        Assert.True(f.Store.Resolve() == f.Environment);
        Assert.True(f.Store.Save("synthetic-first"));
        Assert.Equal(CoachingCredentialSource.Saved, f.Store.GetSource());
        Assert.True(f.Store.Resolve() == "synthetic-first");
        Assert.True(f.Store.Save("synthetic-replacement"));
        var restarted = new ProtectedCoachingCredentials(f.Path, () => f.Environment);
        Assert.True(restarted.Resolve() == "synthetic-replacement");
        foreach (var file in Directory.GetFiles(f.Root, "*", SearchOption.AllDirectories))
        {
            byte[] bytes = File.ReadAllBytes(file);
            bool containsPlaintext = Encoding.UTF8.GetString(bytes).Contains("synthetic-")
                || Encoding.Unicode.GetString(bytes).Contains("synthetic-");
            Assert.False(containsPlaintext); // Never put file contents into assertion diagnostics.
        }
        Assert.Single(Directory.GetFiles(f.Root, "*", SearchOption.AllDirectories));
        Assert.False(f.Path.StartsWith(f.Paths.DataDirectory + System.IO.Path.DirectorySeparatorChar));
        Assert.True(restarted.Remove());
        Assert.Equal(CoachingCredentialSource.Environment, restarted.GetSource());
        f.Environment = null;
        Assert.Equal(CoachingCredentialSource.None, restarted.GetSource());
        Assert.True(restarted.Remove());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(40)]
    [InlineData(17000)]
    public void CorruptStorageBlocksFallbackAndCanBeReplacedOrRemoved(int size)
    {
        using var f = new SecretFixture(); f.Environment = "synthetic-fallback";
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(f.Path)!);
        File.WriteAllBytes(f.Path, new byte[size]);
        Assert.Equal(CoachingCredentialSource.Unreadable, f.Store.GetSource());
        var error = Assert.Throws<CoachingCredentialException>(() => f.Store.Resolve());
        Assert.Null(error.InnerException);
        Assert.DoesNotContain("synthetic", error.Message);
        Assert.True(f.Store.Save("synthetic-repaired"));
        Assert.Equal(CoachingCredentialSource.Saved, f.Store.GetSource());
        Assert.True(f.Store.Remove());
        Assert.Equal(CoachingCredentialSource.Environment, f.Store.GetSource());
    }

    [Theory]
    [InlineData("")]
    [InlineData("has spaces")]
    [InlineData("line\nbreak")]
    public void InvalidReplacementLeavesExistingKeyIntact(string invalid)
    {
        using var f = new SecretFixture();
        Assert.True(f.Store.Save("synthetic-existing"));
        Assert.False(f.Store.Save(invalid));
        Assert.True(f.Store.Resolve() == "synthetic-existing");
        Assert.False(f.Store.Save(new string('x', 4097)));
    }

    [Fact]
    public void StorageFailureIsSanitizedAndSettingsDoesNotClaimSuccess()
    {
        using var f = new SecretFixture();
        Directory.CreateDirectory(f.Path); // A directory cannot be replaced by the credential file.
        using var vm = Settings(f.Store);
        vm.SaveKey("synthetic-never-in-error");
        Assert.Contains("not saved", vm.CredentialMessage);
        Assert.DoesNotContain("synthetic", vm.CredentialMessage);
        Assert.Equal(CoachingCredentialSource.Unreadable, vm.CredentialSource);
        vm.RemoveKeyCommand.Execute(null);
        Assert.Contains("could not be removed", vm.CredentialMessage);
        Assert.Empty(Directory.GetFiles(f.Root, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public void FailedReplacementPreservesPreviousEncryptedFileAndCleansTemporary()
    {
        using var f = new SecretFixture();
        Assert.True(f.Store.Save("synthetic-existing"));
        using (var held = new FileStream(f.Path, FileMode.Open, FileAccess.Read, FileShare.Read))
            Assert.False(f.Store.Save("synthetic-new"));
        Assert.True(f.Store.Resolve() == "synthetic-existing");
        Assert.Single(Directory.GetFiles(f.Root, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task SettingsNeverCallsProviderAndSameProviderUsesRotatedKeyImmediately()
    {
        using var f = new SecretFixture();
        using var handler = new CaptureHandler();
        using var client = new HttpClient(handler);
        var provider = new OpenAiCoachingProvider(client, new(), f.Store.Resolve);
        var packet = CoachingEvidencePacketBuilder.Build(ReviewFixture.Evidence(new(ReviewFixture.Day))).Packet!;
        using var vm = Settings(f.Store);
        vm.RefreshCredentialStatus(); vm.SaveKey("synthetic-first"); vm.SaveKey("synthetic-second");
        Assert.Equal(0, handler.Calls);
        Assert.Equal(CoachingCredentialSource.Saved, vm.CredentialSource);
        Assert.DoesNotContain("synthetic", vm.CredentialStatus + vm.CredentialMessage);
        handler.Expected = "synthetic-second";
        await provider.GenerateAsync(packet, CancellationToken.None);
        Assert.True(handler.Matched);
        vm.SaveKey("synthetic-third"); handler.Expected = "synthetic-third";
        await provider.GenerateAsync(packet, CancellationToken.None);
        Assert.True(handler.Matched);
        f.Environment = "synthetic-fallback";
        vm.RemoveKeyCommand.Execute(null);
        Assert.Contains("environment fallback", vm.CredentialStatus);
        Assert.Equal(2, handler.Calls);
        handler.Expected = f.Environment;
        await provider.GenerateAsync(packet, CancellationToken.None);
        Assert.True(handler.Matched);
        File.WriteAllBytes(f.Path, [0]);
        var failed = await provider.GenerateAsync(packet, CancellationToken.None);
        Assert.Equal(CoachingGenerationStatus.MissingCredentials, failed.Status);
        Assert.Equal(3, handler.Calls); // Corrupt saved key sends nothing, including no fallback request.
    }

    internal static SettingsViewModel Settings(ProtectedCoachingCredentials store) =>
        new(new FakeThemeService(), new FakeDesktopSettingsStore(), NullLogger<SettingsViewModel>.Instance, store);

    private sealed class CaptureHandler : HttpMessageHandler
    {
        internal string? Expected; internal bool Matched; internal int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++; Matched = request.Headers.Authorization?.Parameter == Expected;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized));
        }
    }

    internal sealed class SecretFixture : IDisposable
    {
        internal string Root { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ptj-secret-test-" + Guid.NewGuid().ToString("N"));
        internal LocalApplicationPaths Paths { get; }
        internal string Path => Paths.CoachingCredentialsPath;
        internal string? Environment;
        internal ProtectedCoachingCredentials Store { get; }
        internal SecretFixture() { Paths = new(Root); Store = new(Path, () => Environment); }
        public void Dispose() { if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true); }
    }
}
