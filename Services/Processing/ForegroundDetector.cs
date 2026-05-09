using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using KKTD_V3.Models;
using Microsoft.Extensions.Logging;

namespace KKTD_V3.Services.Processing;

/// <summary>
/// Schnelle Vordergrund-Detektion auf einem Mono8-Frame:
///   1. Otsu-Binarisierung (BinaryInv — Teile sind dunkel auf hellem Durchlicht-Hintergrund).
///   2. Morphologisches Opening 5×5 (Rauschpixel entfernen).
///   3. ConnectedComponentsWithStats → Bounding-Boxes + Pixel-Flächen.
///   4. Bahn-Zuordnung über Centroid-X anhand <see cref="LaneSettings"/>.
///
/// Ergebnis sind <see cref="DetectionResult"/> mit Verdict=Unknown — die Klassifikation
/// (Gut/Touching/Foreign) übernimmt später <see cref="AreaMatcher"/>+Pyramidensuche.
/// </summary>
public sealed class ForegroundDetector
{
    private readonly AppSettings _settings;
    private readonly ILogger<ForegroundDetector> _logger;

    public int MinAreaPx { get; set; } = 50;
    public int MorphKernel { get; set; } = 5;

    public ForegroundDetector(AppSettings settings, ILogger<ForegroundDetector> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public IReadOnlyList<DetectionResult> Detect(byte[] mono8, int width, int height)
    {
        if (mono8.Length != width * height) return Array.Empty<DetectionResult>();

        try
        {
            using var gray = new Mat(height, width, DepthType.Cv8U, 1);
            Marshal.Copy(mono8, 0, gray.DataPointer, mono8.Length);

            using var binary = new Mat();
            CvInvoke.Threshold(gray, binary, 0, 255,
                ThresholdType.Otsu | ThresholdType.BinaryInv);

            using var kernel = CvInvoke.GetStructuringElement(
                ElementShape.Ellipse, new Size(MorphKernel, MorphKernel), new Point(-1, -1));
            CvInvoke.MorphologyEx(binary, binary, MorphOp.Open, kernel,
                new Point(-1, -1), 1, BorderType.Default, default);

            using var labels = new Mat();
            using var stats = new Mat();
            using var centroids = new Mat();
            int labelCount = CvInvoke.ConnectedComponentsWithStats(
                binary, labels, stats, centroids, LineType.EightConnected, DepthType.Cv32S);

            if (labelCount <= 1) return Array.Empty<DetectionResult>();

            var statsData = (int[,])stats.GetData();
            var centroidData = (double[,])centroids.GetData();
            var results = new List<DetectionResult>(labelCount - 1);

            for (int i = 1; i < labelCount; i++)
            {
                int area = statsData[i, 4];
                if (area < MinAreaPx) continue;

                int x = statsData[i, 0];
                int y = statsData[i, 1];
                int w = statsData[i, 2];
                int h = statsData[i, 3];
                double cx = centroidData[i, 0];

                results.Add(new DetectionResult
                {
                    BoundingBox = new Rectangle(x, y, w, h),
                    AreaPx = area,
                    Lane = LaneOf(cx),
                    Verdict = DetectionVerdict.Unknown
                });
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ForegroundDetector.Detect fehlgeschlagen.");
            return Array.Empty<DetectionResult>();
        }
    }

    private int LaneOf(double centroidX)
    {
        var lanes = _settings.Lanes.Lanes;
        if (lanes is null) return 0;
        for (int k = 0; k < lanes.Length; k++)
        {
            var lane = lanes[k];
            if (lane is null) continue;
            if (centroidX >= lane.XPx && centroidX < lane.XPx + lane.WidthPx)
                return lane.Index;
        }
        return 0;
    }
}
