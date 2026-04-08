namespace WindowsUtilityPack.Services.QrCode;

/// <summary>
/// Lightweight report describing potential scannability risk.
/// </summary>
public sealed class QrScannabilityReport
{
    /// <summary>True when no high-risk issues were detected.</summary>
    public bool IsLikelyScannable { get; init; }

    /// <summary>Human-readable warnings for risky combinations.</summary>
    public required IReadOnlyList<string> Warnings { get; init; }
}
