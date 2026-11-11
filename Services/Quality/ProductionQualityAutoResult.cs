using System.Globalization;
using System.Text.RegularExpressions;

namespace JXHLJSApp.Services.Quality;

public static class ProductionQualityAutoResult
{
    public static bool ShouldQualify(bool isAcid, string? productDiameter, string? lowerTolerance,
        string? upperTolerance, string? actualDiameter, string? strengthRequirement,
        string? actualStrength, string? surfaceJudgment, string? coilDiameter, string? coilPitch,
        bool isHeatTreatment = false)
    {
        // 热处理只判断直径和强度；其他非酸洗工序保留表面、圈径和圈距条件。
        if (isAcid ||
            (!isHeatTreatment && (surfaceJudgment != "不合格" || coilDiameter != "合格" || coilPitch != "合格")) ||
            !TryNumber(productDiameter, out var product) || !TryNumber(lowerTolerance, out var lower) ||
            !TryNumber(upperTolerance, out var upper) || !TryNumber(actualDiameter, out var actual) ||
            !TryNumber(actualStrength, out var strength) || product <= 0 || actual <= 0 || strength < 0)
        {
            return false;
        }

        // 与详情卡片一致，下公差按负偏差、上公差按正偏差处理。
        try
        {
            return actual >= product - Math.Abs(lower) && actual <= product + Math.Abs(upper) &&
                IsStrengthOutsideRange(strengthRequirement, strength);
        }
        catch (OverflowException)
        {
            return false;
        }
    }

    private static bool TryNumber(string? text, out decimal value) =>
        decimal.TryParse(text?.Trim().Replace('＋', '+').Replace('－', '-'),
            NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture, out value);

    private static bool IsStrengthOutsideRange(string? requirement, decimal strength)
    {
        var text = requirement?.Trim() ?? string.Empty;
        // 明确上下限优先使用原区间，不额外扩展。
        var match = Regex.Match(text,
            @"^(?<min>\d+(?:\.\d+)?)\s*(?:-|~|～|—|–|至|到)\s*(?<max>\d+(?:\.\d+)?)\s*(?:MPa)?$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (match.Success)
        {
            return TryNumber(match.Groups["min"].Value, out var min) &&
                TryNumber(match.Groups["max"].Value, out var max) && min <= max &&
                (strength < min || strength > max);
        }

        // 单值或“数字左右”按中心值 ±110；完整匹配，避免误识别单边要求。
        var centerMatch = Regex.Match(text,
            @"^(?<center>\d+(?:\.\d+)?)\s*(?:左右\s*)?(?:MPa)?$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!centerMatch.Success || !TryNumber(centerMatch.Groups["center"].Value, out var center))
        {
            return false;
        }

        var lower = center - 110m;
        var upper = center + 110m;
        return strength < lower || strength > upper;
    }
}
