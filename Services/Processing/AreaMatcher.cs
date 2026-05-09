using System.Collections.Generic;
using System.Linq;
using KKTD_V3.Models;

namespace KKTD_V3.Services.Processing;

/// <summary>
/// Flächenbasierte Vorauswahl: anhand bekannter Patterns entscheiden, ob
/// ein Segment ein Kandidat ist, möglicherweise zwei berührende Teile darstellt
/// oder direkt ein Fremdteil ist (siehe §9 Schritt 4-6).
/// </summary>
public sealed class AreaMatcher
{
    public AreaMatchResult Classify(int areaPx, ArticleData article)
    {
        if (article.Patterns.Count == 0)
            return new AreaMatchResult(AreaVerdict.NoPatternData, null, null);

        int globalMin = article.Patterns.Min(p => p.AreaMinPx);
        int globalMax = article.Patterns.Max(p => p.AreaMaxPx);

        if (areaPx < globalMin)
            return new AreaMatchResult(AreaVerdict.ForeignPart_TooSmall, null, null);

        if (areaPx > globalMax)
        {
            // Berührung möglich? Fläche ≈ Summe zweier bekannter Patterns?
            foreach (var p1 in article.Patterns)
            {
                foreach (var p2 in article.Patterns)
                {
                    int sumMin = p1.AreaMinPx + p2.AreaMinPx;
                    int sumMax = p1.AreaMaxPx + p2.AreaMaxPx;
                    if (areaPx >= sumMin && areaPx <= sumMax)
                        return new AreaMatchResult(AreaVerdict.PossibleTouching, p1, p2);
                }
            }
            return new AreaMatchResult(AreaVerdict.ForeignPart_TooLarge, null, null);
        }

        var match = article.Patterns.FirstOrDefault(p => areaPx >= p.AreaMinPx && areaPx <= p.AreaMaxPx);
        if (match is null)
            return new AreaMatchResult(AreaVerdict.ForeignPart_NoPatternMatch, null, null);

        return new AreaMatchResult(AreaVerdict.CandidateForTemplateMatch, match, null);
    }
}

public enum AreaVerdict
{
    NoPatternData,
    ForeignPart_TooSmall,
    ForeignPart_TooLarge,
    ForeignPart_NoPatternMatch,
    CandidateForTemplateMatch,
    PossibleTouching
}

public sealed record AreaMatchResult(AreaVerdict Verdict, PatternData? Pattern, PatternData? SecondPattern);
