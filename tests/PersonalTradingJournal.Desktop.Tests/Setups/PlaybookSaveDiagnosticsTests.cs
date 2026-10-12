using System.Text.Json;
using PersonalTradingJournal.Desktop.ViewModels.Setups;

namespace PersonalTradingJournal.Desktop.Tests.Setups;

public sealed class PlaybookSaveDiagnosticsTests
{
    [Fact]
    public async Task OptInRecordsActualCommandAndVerifiedReadbackWithoutAuthoredTextOrPaths()
    {
        string root = Path.Combine(Path.GetTempPath(), "ptj-playbook-diagnostic-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var disabled = new PlaybookSaveDiagnostics(root, Path.Combine(root, "journal.db"), false);
            var f = new PlaybookFixture(diagnostics: disabled); await f.OpenAsync(); f.Edit("PRIVATE guidance");
            await f.Vm.SaveCommand.ExecuteAsync(null); Assert.Empty(Directory.GetFiles(root));
            var diagnostics = new PlaybookSaveDiagnostics(root, Path.Combine(root, "journal.db"), true);
            f = new PlaybookFixture(diagnostics: diagnostics); await f.OpenAsync(); f.Edit("PRIVATE guidance");
            await f.Vm.SaveCommand.ExecuteAsync(null);
            string file = Assert.Single(Directory.GetFiles(root));
            string text = File.ReadAllText(file);
            Assert.DoesNotContain("PRIVATE", text); Assert.DoesNotContain(root, text); Assert.DoesNotContain("NQ", text);
            Assert.Contains("BinarySha256", text); Assert.Contains("DatabasePathFingerprint", text);
            var events = File.ReadAllLines(file).Skip(1).Select(line => JsonDocument.Parse(line).RootElement.GetProperty("Data")).ToArray();
            Assert.Equal(new[] { "Invoked", "WriteStarted", "Committed", "Readback", "Reloaded", "Finished" },
                events.Select(e => e.GetProperty("Phase").GetString()));
            Assert.All(events, e => Assert.Equal(f.Setup.Id, e.GetProperty("SetupId").GetGuid()));
            Assert.Equal("Verified", events[^1].GetProperty("Result").GetString());
            Assert.False(events[^1].GetProperty("Editing").GetBoolean());
            for (int i = 0; i < 1000; i++) diagnostics.Record(new(Guid.Empty, PlaybookSavePhase.Invoked,
                PlaybookSaveResult.Pending, null, 0, null, null, false, false, 0));
            Assert.Equal(PlaybookSaveDiagnostics.MaximumEvents, File.ReadAllLines(file).Length);
            Assert.True(new FileInfo(file).Length <= PlaybookSaveDiagnostics.MaximumBytes);
        }
        finally { Directory.Delete(root, true); }
    }
}
