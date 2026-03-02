namespace Aegis.Api.Models;

/// <summary>
/// The three-pronged confidence score combining similarity, experience, and LLM certainty.
/// </summary>
public class ConfidenceResult
{
    /// <summary>How closely the request matches past tickets in the Vector DB (0-100).</summary>
    public int SimilarityScore { get; set; }

    /// <summary>Based on historical resolution: were similar issues resolved by AI or escalated? (0-100).</summary>
    public int HistoricalSuccessRate { get; set; }

    /// <summary>LLM's self-assessed certainty that it has enough info to act (0-100).</summary>
    public int LlmCertainty { get; set; }

    /// <summary>Weighted final score (0-100).</summary>
    public int FinalScore => (int)(SimilarityScore * 0.35 + HistoricalSuccessRate * 0.40 + LlmCertainty * 0.25);

    /// <summary>The tier this score maps to.</summary>
    public string Tier => FinalScore switch
    {
        >= 80 => "Tier1_AutoRemediate",
        >= 50 => "Tier2_HumanInTheLoop",
        _ => "Tier3_Escalate"
    };
}
