namespace PersonalTradingJournal.Domain.Setups;

/// <summary>
/// An owned extension of the existing TradingSetup, identified by (TradingSetupId, Revision).
/// Editing produces a new immutable value; archiving uses TradingSetup.IsActive, not a second catalog.
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
