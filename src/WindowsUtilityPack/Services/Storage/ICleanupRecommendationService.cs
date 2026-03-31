using WindowsUtilityPack.Models;

namespace WindowsUtilityPack.Services.Storage;

/// <summary>
/// Produces cleanup recommendations from a scan tree.
///
/// Safety model:
///   - Recommendations are categorised by risk (Low/Medium/High).
///   - No files are deleted by this service — it only analyses and suggests.
///   - Users must explicitly review and confirm before any action is taken.
///   - Identifications are transparent: each recommendation includes a Rationale.
/// </summary>
public interface ICleanupRecommendationService
{
    /// <summary>
    /// Analyses the scan tree and produces actionable cleanup recommendations.
    /// </summary>
    /// <param name="root">Root node of the completed scan tree.</param>
    /// <param name="duplicates">Optional already-computed duplicate groups.</param>
    /// <param name="cancellationToken">Token to cancel the analysis.</param>
    /// <returns>Recommendations sorted by potential savings descending.</returns>
    Task<IReadOnlyList<CleanupRecommendation>> AnalyseAsync(
        StorageItem root,
        IReadOnlyList<DuplicateGroup>? duplicates,
        CancellationToken cancellationToken);
}
