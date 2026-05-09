using System.Collections.Generic;
using System.Threading.Tasks;
using KKTD_V3.Models;
using Microsoft.Extensions.Logging;

namespace KKTD_V3.Services.Processing;

/// <summary>
/// Hauptpipeline: Flat-Field → Otsu → Morphologie → Segmentierung pro Bahn →
/// Flächen-Vorauswahl → Pyramidensuche → feine Konturprüfung → Verdict.
/// </summary>
public sealed class ImageProcessingService
{
    private readonly FlatFieldService _flatField;
    private readonly AreaMatcher _areaMatcher;
    private readonly PyramidMatcher _pyramidMatcher;
    private readonly ContourMatcher _contourMatcher;
    private readonly AppSettings _settings;
    private readonly ILogger<ImageProcessingService> _logger;

    public ImageProcessingService(
        FlatFieldService flatField,
        AreaMatcher areaMatcher,
        PyramidMatcher pyramidMatcher,
        ContourMatcher contourMatcher,
        AppSettings settings,
        ILogger<ImageProcessingService> logger)
    {
        _flatField = flatField;
        _areaMatcher = areaMatcher;
        _pyramidMatcher = pyramidMatcher;
        _contourMatcher = contourMatcher;
        _settings = settings;
        _logger = logger;
    }

    public Task<IReadOnlyList<DetectionResult>> ProcessAsync(
        byte[] mono8,
        int width,
        int height,
        ArticleData article,
        double encoderAtCaptureMm)
    {
        // TODO: vollständige CUDA-Pipeline implementieren — siehe §9 KKTD_ProjectMarkdown_V3.md.
        IReadOnlyList<DetectionResult> empty = System.Array.Empty<DetectionResult>();
        return Task.FromResult(empty);
    }
}
