using PersonalTradingJournal.Application.Instruments;
using PersonalTradingJournal.Domain.Instruments;
using PersonalTradingJournal.Domain.Trades;

namespace PersonalTradingJournal.Application.Imports.Topstep;

public enum TopstepInstrumentResolutionStatus { Blocked, ExistingInstrument, ProposedCreation }

/// <summary>Explicit verification of an exact source contract against immutable catalog facts, not just an ID.</summary>
public sealed record TopstepInstrumentVerification(string SourceContract, InstrumentListItem Instrument);

public sealed record TopstepInstrumentCreationProposal(
    string CanonicalSymbol, string DisplayName, AssetClass AssetClass, string Exchange,
    string Currency, decimal TickSize, decimal TickValue, decimal PointValue, string MetadataSource);

public sealed class TopstepInstrumentResolution
{
    internal TopstepInstrumentResolution(string sourceContract, TopstepContractIdentity? identity,
        TopstepInstrumentResolutionStatus status, IEnumerable<InstrumentListItem> matches,
        TopstepInstrumentCreationProposal? proposal, TradePricingSnapshot? pricing, string? verificationSource)
    {
        SourceContract = sourceContract;
        Identity = identity;
        Status = status;
        MatchingInstruments = Array.AsReadOnly(matches.ToArray());
        CreationProposal = proposal;
        Pricing = pricing;
        VerificationSource = verificationSource;
    }

    public string SourceContract { get; }
    public TopstepContractIdentity? Identity { get; }
    public TopstepInstrumentResolutionStatus Status { get; }
    /// <summary>Full matching-set snapshot, including inactive/invalid matches. Recheck cardinality and facts at confirmation.</summary>
    public IReadOnlyList<InstrumentListItem> MatchingInstruments { get; }
    public InstrumentListItem? ExistingInstrument => Status == TopstepInstrumentResolutionStatus.ExistingInstrument ? MatchingInstruments.Single() : null;
    public TopstepInstrumentCreationProposal? CreationProposal { get; }
    public TradePricingSnapshot? Pricing { get; }
    public string? VerificationSource { get; }
}
