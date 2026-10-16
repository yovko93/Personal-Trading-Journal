using System.Collections.Immutable;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PersonalTradingJournal.Application.Backups;
using PersonalTradingJournal.Application.Rules;
using PersonalTradingJournal.Domain.Rules;
using PersonalTradingJournal.Infrastructure.Backups;

namespace PersonalTradingJournal.Infrastructure.Tests.Persistence.Rules;

public sealed partial class TradingRulePersistenceTests
{
    [Fact]
    public async Task CompleteBackupPreflightOfflineRestoreAndRecoveryRetainRulesAndAssessments()
    {
        await using var source = await Fixture.Create(); var trade = await source.Seed();
        var automatic = await source.Rule(AutomaticDefinition());
        var checkedRun = await source.Get<IAutomaticRuleChecks>().RunAsync(TradingRuleAssessmentTarget.ForTrade(trade.Id, trade.TradingAccountId));
        Guid calculatedId = Assert.Single(checkedRun.AssessmentIds);
        var originalCalculation = (await source.Reader.ReadAssessmentAsync(calculatedId))!.Evaluation!;
        var rule = await source.Rule(new TradingRuleDefinition("Manual principle"));
        var assessment = Assess(rule, TradingRuleAssessmentTarget.ForTrade(trade.Id, trade.TradingAccountId), null,
            new TradingRuleEvidenceReference(TradingRuleEvidenceKind.Execution, trade.Executions[0].Id));
        Assert.True(await source.Assessments.TryAppendAsync(assessment, 1, null));
        Assert.True(await source.Rules.TryAppendAsync(rule.Archive(1, Now), 1));
        var ordered = await source.Rule(new TradingRuleDefinition("Ordered principle"));
        var catalog = await source.Reader.ReadCatalogAsync();
        Assert.Equal(RuleMoveStatus.Saved, await source.Get<ITradingRuleOrderStore>().MoveAsync(new(ordered.RuleId, automatic.RuleId, catalog.CatalogVersion)));
        string archive = Path.Combine(source.Root, "rules.ptjbackup");
        Assert.True((await source.Get<IBackupArchiveService>().CreateAsync(archive)).Succeeded);
        var preflight = await source.Get<IRestorePreflightService>().InspectAsync(archive, source.Root);
        Assert.True(preflight.Validated, preflight.ToString());
        await using var target = await Fixture.Create();
        var oldRule = await target.Rule(Definition(applicability: TradingRuleApplicability.NewYorkDay, name: "Recovery rule"));
        await target.Stop();
        var restored = await new JournalRestoreService(target.Paths).RestoreAsync(new(archive, preflight.Summary!.ArchiveSha256));
        Assert.True(restored.Succeeded, restored.ToString());
        Assert.True(File.Exists(restored.RecoveryArchivePath));
        Assert.True((await new RestorePreflightService().InspectAsync(restored.RecoveryArchivePath!, target.Root)).Validated);
        await target.Restart();
        Assert.Equal(new[] { ordered.RuleId, automatic.RuleId }, (await target.Reader.ReadCatalogAsync()).Items.Select(r => r.Revision.RuleId));
        Assert.Equal(automatic.Definition.AutomaticCriterion, (await target.Rules.GetLatestAsync(automatic.RuleId))!.Definition.AutomaticCriterion);
        var restoredCalculation = (await target.Reader.ReadAssessmentAsync(calculatedId))!.Evaluation!;
        Assert.Equal(originalCalculation.Calculation, restoredCalculation.Calculation);
        Assert.Equal(originalCalculation.ExecutionIds, restoredCalculation.ExecutionIds);
        Assert.Equal(1, (await target.Get<IAutomaticRuleChecks>().RunAsync(TradingRuleAssessmentTarget.ForTrade(trade.Id, trade.TradingAccountId))).Unchanged);
        Assert.Equal(new[] { 2, 1 }, (await target.Reader.ReadRevisionsAsync(rule.RuleId)).Select(r => r.Revision.Revision));
        Assert.Empty((await target.Rules.GetLatestAsync(rule.RuleId))!.Definition.RequiredEvidence);
        Assert.Empty((await target.Rules.GetLatestAsync(rule.RuleId))!.Definition.Criterion);
        Assert.Empty((await target.Reader.ReadAssessmentAsync(assessment.Id))!.Assessment.RuleRevision.Definition.Criterion);
        var saved = (await target.Reader.ReadAssessmentAsync(assessment.Id))!;
        Assert.Equal(assessment.Explanation, saved.Assessment.Explanation);
        Assert.Equal(TradingRuleSourceState.Available, saved.TargetState);
        Assert.Equal(TradingRuleSourceState.Available, Assert.Single(saved.Evidence).State);
        Assert.Null(await target.Rules.GetLatestAsync(oldRule.RuleId));
        // The recovery archive must also round-trip through the same offline service.
        string recovery = restored.RecoveryArchivePath!;
        var check = await new RestorePreflightService().InspectAsync(recovery, target.Root);
        await target.Stop();
        var recovered = await new JournalRestoreService(target.Paths).RestoreAsync(new(recovery, check.Summary!.ArchiveSha256));
        Assert.True(recovered.Succeeded, recovered.ToString()); await target.Restart();
        Assert.Equal(oldRule.RuleId, (await target.Rules.GetLatestAsync(oldRule.RuleId))!.RuleId);
        Assert.Null(await target.Reader.ReadAssessmentAsync(assessment.Id));
    }

    [Fact]
    public async Task ExportVersionEightRetainsExactRuleAssessmentReferencesAndAllExistingTables()
    {
        await using var f = await Fixture.Create(); var trade = await f.Seed();
        var rule = await f.Rule(AutomaticDefinition(account: trade.TradingAccountId));
        var manual = await f.Rule(new TradingRuleDefinition("Manual principle"));
        var catalog = await f.Reader.ReadCatalogAsync();
        Assert.Equal(RuleMoveStatus.Saved, await f.Get<ITradingRuleOrderStore>().MoveAsync(new(manual.RuleId, rule.RuleId, catalog.CatalogVersion)));
        var a = Assess(rule, TradingRuleAssessmentTarget.ForTrade(trade.Id, trade.TradingAccountId), null,
            new TradingRuleEvidenceReference(TradingRuleEvidenceKind.Trade, trade.Id));
        Assert.True(await f.Assessments.TryAppendAsync(a, 1, null));
        string output = Path.Combine(f.Root, "export");
        var result = await f.Get<IPortableExportService>().CreateAsync(output);
        Assert.True(result.Succeeded, result.ToString());
        using var json = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(output, PortableExportContract.DataFile)));
        Assert.Equal(9, json.RootElement.GetProperty("version").GetInt32());
        var tables = json.RootElement.GetProperty("tables");
        var positions = tables.GetProperty("tradingRulePositions").EnumerateArray().ToArray();
        Assert.Equal(new[] { manual.RuleId, rule.RuleId }, positions.Select(p => p.GetProperty("ruleId").GetGuid()));
        Assert.Equal(new long[] { 0, 1 }, positions.Select(p => p.GetProperty("position").GetInt64()));
        Assert.Equal((await f.Reader.ReadCatalogAsync()).CatalogVersion, Assert.Single(tables.GetProperty("tradingRuleCatalogState").EnumerateArray()).GetProperty("version").GetInt64());
        var revisions = tables.GetProperty("tradingRuleRevisions").EnumerateArray().ToArray();
        Assert.Equal(2, revisions.Length);
        var revision = revisions.Single(r => r.GetProperty("ruleId").GetGuid() == rule.RuleId);
        var manualRevision = revisions.Single(r => r.GetProperty("ruleId").GetGuid() == manual.RuleId);
        Assert.Equal("", manualRevision.GetProperty("requiredEvidence").GetString());
        Assert.Equal("", manualRevision.GetProperty("criterion").GetString());
        Assert.Equal(JsonValueKind.Null, manualRevision.GetProperty("automaticCriterionJson").ValueKind);
        Assert.Equal(rule.RuleId, revision.GetProperty("ruleId").GetGuid());
        Assert.True(revision.GetProperty("isGlobal").GetBoolean());
        Assert.Equal(1, revision.GetProperty("checkCapability").GetInt32());
        Assert.Equal(JsonValueKind.Null, revision.GetProperty("accountId").ValueKind);
        Assert.Equal(rule.Definition.Criterion, revision.GetProperty("criterion").GetString());
        var assessment = Assert.Single(tables.GetProperty("tradingRuleAssessments").EnumerateArray());
        Assert.Equal(a.Id, assessment.GetProperty("id").GetGuid());
        Assert.Equal(trade.Id, assessment.GetProperty("tradeId").GetGuid());
        Assert.Equal(JsonValueKind.Null, assessment.GetProperty("newYorkDate").ValueKind);
        await using var db = f.Context();
        Assert.Equal((await db.TradingRuleAssessments.SingleAsync()).EvidenceJson, assessment.GetProperty("evidenceJson").GetString());
        Assert.Equal(JsonValueKind.Null, tables.GetProperty("executions")[0].GetProperty("commission").ValueKind);
        Assert.Equal(4, Directory.GetFiles(output, "*.csv").Length);
        Assert.False(json.RootElement.GetProperty("restorableBackup").GetBoolean());
        Assert.NotNull(revision.GetProperty("automaticCriterionJson").GetString());
        Assert.Equal(JsonValueKind.Null, assessment.GetProperty("evaluationJson").ValueKind);
        Assert.Equal(JsonValueKind.Null, assessment.GetProperty("evaluationFingerprint").ValueKind);
    }

    [Fact]
    public async Task PreviousSchemaArchiveIsExplicitlyRejectedAndInstalledDataUnchanged()
    {
        await using var f = await Fixture.Create(PreviousSchema); var trade = await f.Seed();
        string snapshot = Path.Combine(f.Root, "previous.db"), archive = Path.Combine(f.Root, "previous.ptjbackup");
        ImmutableArray<string> migrations;
        await using (var db = f.Context())
        {
            migrations = (await db.Database.GetAppliedMigrationsAsync()).ToImmutableArray();
            await db.Database.OpenConnectionAsync();
            using var target = new SqliteConnection($"Data Source={snapshot};Pooling=False"); target.Open();
            ((SqliteConnection)db.Database.GetDbConnection()).BackupDatabase(target);
        }
        byte[] bytes = await File.ReadAllBytesAsync(snapshot);
        var manifest = new BackupManifest(BackupArchiveContract.Format, 1, "1.0.0", Now, new("sqlite", migrations), "SHA-256",
            [new(BackupArchiveContract.DatabasePath, BackupFileKind.Database, bytes.Length, Convert.ToHexStringLower(SHA256.HashData(bytes)))]);
        using (var zip = ZipFile.Open(archive, ZipArchiveMode.Create))
        {
            using (var entry = zip.CreateEntry(BackupArchiveContract.DatabasePath).Open()) entry.Write(bytes);
            using var metadata = zip.CreateEntry(BackupArchiveContract.ManifestPath).Open();
            JsonSerializer.Serialize(metadata, manifest, BackupArchiveContract.JsonOptions);
        }
        var result = await new RestorePreflightService().InspectAsync(archive, f.Root);
        Assert.False(result.Validated); Assert.Equal(BackupSchemaCompatibility.RequiresStagedMigration, result.Compatibility);
        Assert.Equal(BackupValidationCode.UnsupportedDatabaseSchema, result.Failure);
        await using var installed = f.Context();
        Assert.Equal(migrations.ToArray(), (await installed.Database.GetAppliedMigrationsAsync()).ToArray());
        Assert.Equal(trade.Id, (await installed.Trades.SingleAsync()).Id);
        Assert.Empty(Directory.GetDirectories(f.Root, ".preflight-*.partial"));
    }
}
