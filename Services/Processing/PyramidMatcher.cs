using System.Collections.Generic;
using KKTD_V3.Models;
using Microsoft.Extensions.Logging;

namespace KKTD_V3.Services.Processing;

/// <summary>
/// Pyramidensuche: erst grob auf 25%-Skala, dann fein im Vollbild —
/// CUDA Template-Matching pro Bahn/Zone-Template.
/// </summary>
public sealed class PyramidMatcher
{
    private readonly ILogger<PyramidMatcher> _logger;

    public PyramidMatcher(ILogger<PyramidMatcher> logger)
    {
        _logger = logger;
    }

    public IReadOnlyList<TemplateCandidate> FindCandidates(
        byte[] segmentMono8, int width, int height,
        int lane, int zone, ArticleData article)
    {
        // TODO: GpuMat hochladen, Resize 0.25, MatchTemplate (TM_CCOEFF_NORMED),
        //       lokale Maxima ≥ ContourMatchThresholdCoarse als Kandidaten zurückgeben.
        return System.Array.Empty<TemplateCandidate>();
    }
}

public sealed record TemplateCandidate(string TemplateId, int X, int Y, double Score);
