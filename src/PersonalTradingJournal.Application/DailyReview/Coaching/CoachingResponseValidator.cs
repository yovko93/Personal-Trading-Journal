using System.Text;
using System.Text.Json;

namespace PersonalTradingJournal.Application.DailyReview.Coaching;

/// <summary>Validates a bounded JSON response against the exact supplied packet. Passing checks
/// structure and source membership, not factual entailment, coaching quality or prompt safety.</summary>
public static class CoachingResponseValidator
{
    public static CoachingResponseValidation Validate(string json, CoachingEvidencePacket packet,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(packet);
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(json) || json.Length > CoachingContract.MaximumResponseBytes ||
            Encoding.UTF8.GetByteCount(json) > CoachingContract.MaximumResponseBytes)
            return Invalid("Response is empty or exceeds the v1 UTF-8 limit.");
        CoachingResponse? response;
        try
        {
            using var document = JsonDocument.Parse(json, new() { MaxDepth = 48 });
            if (HasDuplicateProperties(document.RootElement))
                return Invalid("Duplicate JSON property names are not allowed.");
            response = JsonSerializer.Deserialize<CoachingResponse>(json, CoachingContract.JsonOptions);
        }
        catch (JsonException) { return Invalid("Response must match the v1 JSON structure and enum names exactly."); }
        cancellationToken.ThrowIfCancellationRequested();
        if (response is null) return Invalid("Response must be an object.");
        var errors = new List<string>();
        if (response.ContractVersion != CoachingContract.Version) errors.Add("Unsupported contractVersion.");
        if (response.PacketId != packet.PacketId) errors.Add("packetId does not match the supplied evidence.");
        var sources = packet.Content.Sources.ToDictionary(s => s.Id, StringComparer.Ordinal);
        CheckStatement(response.DaySummary, "daySummary");
        CheckObservations(response.ExecutionObservations, "executionObservations");
        CheckObservations(response.BehaviorObservations, "behaviorObservations");
        CheckStatements(response.ImprovementSuggestions, "improvementSuggestions");
        CheckStatements(response.Uncertainties, "uncertainties");
        cancellationToken.ThrowIfCancellationRequested();
        if (errors.Count != 0) return new(false, null, errors.AsReadOnly());
        // Do not hand consumers mutable deserializer lists, even after successful validation.
        CoachingStatement Freeze(CoachingStatement item) => item with { SourceIds = Array.AsReadOnly(item.SourceIds.ToArray()) };
        CoachingObservation FreezeObservation(CoachingObservation item) => item with { SourceIds = Array.AsReadOnly(item.SourceIds.ToArray()) };
        return new(true, response with
        {
            DaySummary = Freeze(response.DaySummary),
            ExecutionObservations = Array.AsReadOnly(response.ExecutionObservations.Select(FreezeObservation).ToArray()),
            BehaviorObservations = Array.AsReadOnly(response.BehaviorObservations.Select(FreezeObservation).ToArray()),
            ImprovementSuggestions = Array.AsReadOnly(response.ImprovementSuggestions.Select(Freeze).ToArray()),
            Uncertainties = Array.AsReadOnly(response.Uncertainties.Select(Freeze).ToArray()),
        }, Array.Empty<string>());

        bool CheckItem(string? text, IReadOnlyList<string>? references, string path)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(text) || text.Length > CoachingContract.MaximumTextLength)
                errors.Add(path + ": text must be nonblank and at most 1000 UTF-16 characters.");
            if (references is null || references.Count is < 1 or > CoachingContract.MaximumCitationsPerItem)
            { errors.Add(path + ": supply 1–16 sourceIds."); return false; }
            var unique = new HashSet<string>(StringComparer.Ordinal);
            foreach (var id in references)
                if (id is null || !sources.ContainsKey(id) || !unique.Add(id))
                { errors.Add(path + ": sourceIds must be unique IDs from this packet."); return false; }
            return true;
        }
        void CheckStatement(CoachingStatement? item, string path)
        {
            if (item is null) errors.Add(path + ": required statement is missing.");
            else CheckItem(item.Text, item.SourceIds, path);
        }
        void CheckStatements(IReadOnlyList<CoachingStatement>? items, string path)
        {
            if (items is null || items.Count > CoachingContract.MaximumItemsPerSection)
            { errors.Add(path + ": supply an array with at most 12 items."); return; }
            for (int i = 0; i < items.Count; i++) CheckStatement(items[i], path + "[" + i + "]");
        }
        void CheckObservations(IReadOnlyList<CoachingObservation>? items, string path)
        {
            if (items is null || items.Count > CoachingContract.MaximumItemsPerSection)
            { errors.Add(path + ": supply an array with at most 12 items."); return; }
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var location = path + "[" + i + "]";
                if (item is null) { errors.Add(location + ": observation is missing."); continue; }
                if (!CheckItem(item.Text, item.SourceIds, location)) continue;
                bool appropriate = item.Basis switch
                {
                    CoachingObservationBasis.CalculatedFact => item.SourceIds.All(id => sources[id].Kind is
                        CoachingSourceKind.DayStatistics or CoachingSourceKind.CurrencyStatistics or CoachingSourceKind.AccountStatistics),
                    CoachingObservationBasis.RecordedTradeFact => item.SourceIds.All(id => sources[id].Kind is
                        CoachingSourceKind.Trade or CoachingSourceKind.Execution),
                    CoachingObservationBasis.UserWrittenJournalObservation => item.SourceIds.All(id => sources[id].Kind == CoachingSourceKind.Journal),
                    _ => false,
                };
                if (!appropriate) errors.Add(location + ": basis does not match the cited source kinds.");
            }
        }
    }

    private static bool HasDuplicateProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
                if (!names.Add(property.Name) || HasDuplicateProperties(property.Value)) return true;
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var child in element.EnumerateArray())
                if (HasDuplicateProperties(child)) return true;
        return false;
    }

    private static CoachingResponseValidation Invalid(string error) => new(false, null, Array.AsReadOnly(new[] { error }));
}
