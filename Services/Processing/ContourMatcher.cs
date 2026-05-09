using System.Collections.Generic;
using KKTD_V3.Models;
using Microsoft.Extensions.Logging;

namespace KKTD_V3.Services.Processing;

/// <summary>
/// Feine Konturprüfung der Pyramidensuche-Kandidaten.
/// Score ≥ 90% → Gutteil, 70-90% → Grenzteil, &lt; 70% → Fremdteil.
/// </summary>
public sealed class ContourMatcher
{
    private readonly ILogger<ContourMatcher> _logger;

    public ContourMatcher(ILogger<ContourMatcher> logger)
    {
        _logger = logger;
    }

    public DetectionVerdict Verify(
        TemplateCandidate candidate,
        ArticleData article,
        out double finalScore)
    {
        // TODO: Volles Template aus Datei laden, GPU MatchTemplate im Voll-ROI,
        //       feinster Score → DetectionVerdict ableiten.
        finalScore = candidate.Score;
        var fine = article.Detection.ContourMatchThresholdFine;
        var coarse = article.Detection.ContourMatchThresholdCoarse;

        if (finalScore >= fine) return DetectionVerdict.GoodPart;
        if (finalScore >= coarse) return DetectionVerdict.BorderlinePart;
        return DetectionVerdict.ForeignPart;
    }
}
