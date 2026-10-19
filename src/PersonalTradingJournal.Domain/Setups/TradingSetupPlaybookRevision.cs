namespace PersonalTradingJournal.Domain.Setups;

/// <summary>
/// Value carrier for the existing TradingSetup guidance and historical v1 captures.
/// Current persistence replaces one definition; Revision is its optimistic token, not a publishing workflow.
/// Captured values retain (TradingSetupId, Revision) identity. Archiving uses TradingSetup.IsActive.
/// </summary>
public sealed class TradingSetupPlaybookRevision
{
    private TradingSetupPlaybookRevision(TradingSetup setup, int revision, PlaybookGuidance guidance,
        DateTimeOffset recordedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(setup);
        ArgumentNullException.ThrowIfNull(guidance);
        if (!setup.IsActive) throw new InvalidOperationException("Reactivate the Setup before authoring Playbook guidance.");
        if (recordedAtUtc.Offset != TimeSpan.Zero || recordedAtUtc < setup.UpdatedAtUtc)
            throw new ArgumentException("The revision must use UTC and cannot predate its Setup state.", nameof(recordedAtUtc));
        TradingSetupId = setup.Id;
        SetupName = setup.Name;
        SetupDescription = setup.Description;
        Revision = revision;
        Guidance = guidance;
        RecordedAtUtc = recordedAtUtc;
    }

    public const int ContractVersion = 1;
    public Guid TradingSetupId { get; }
    public string SetupName { get; }
    public string? SetupDescription { get; }
    public int Revision { get; }
    public PlaybookGuidance Guidance { get; }
    public DateTimeOffset RecordedAtUtc { get; }

    public static TradingSetupPlaybookRevision Create(TradingSetup setup, PlaybookGuidance guidance,
        DateTimeOffset recordedAtUtc) => new(setup, 1, guidance, recordedAtUtc);

    /// <summary>Reads frozen metadata without consulting the current Setup's name or active state.</summary>
    public static TradingSetupPlaybookRevision Rehydrate(Guid setupId, int revision, string setupName,
        string? setupDescription, PlaybookGuidance guidance, DateTimeOffset recordedAtUtc)
    {
        if (revision < 1) throw new ArgumentOutOfRangeException(nameof(revision));
        return new(TradingSetup.Rehydrate(setupId, setupName, setupDescription, true,
            recordedAtUtc, recordedAtUtc), revision, guidance, recordedAtUtc);
    }

    public TradingSetupPlaybookRevision Revise(TradingSetup setup, int expectedRevision,
        PlaybookGuidance guidance, DateTimeOffset recordedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(setup);
        if (setup.Id != TradingSetupId) throw new ArgumentException("A Playbook cannot move to another Setup.", nameof(setup));
        if (expectedRevision != Revision) throw new InvalidOperationException("The Playbook revision changed; reload before editing.");
        if (recordedAtUtc < RecordedAtUtc) throw new ArgumentException("Revision time cannot move backwards.", nameof(recordedAtUtc));
        return new(setup, checked(Revision + 1), guidance, recordedAtUtc);
    }
}
