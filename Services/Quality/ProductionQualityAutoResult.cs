using System.Globalization;
using System.Text.RegularExpressions;

namespace JXHLJSApp.Services.Quality;

public static class ProductionQualityAutoResult
{
    public static bool ShouldQualify(bool isAcid, string? productDiameter, string? lowerTolerance,
        string? upperTolerance, string? actualDiameter, string? strengthRequirement,
        string? actualStrength, string? surfaceJudgment, string? coilDiameter, string? coilPitch)
    {
        // 此处按业务约定：表面不合格且强度在范围外，也是自动合格的必要条件。
        if (isAcid || surfaceJudgment != "不合格" || coilDiameter != "合格" || coilPitch != "合格" ||
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
        // 仅识别明确的上下限；要求缺失或无法识别时，不能自动判为合格。
        var match = Regex.Match(requirement?.Trim() ?? string.Empty,
            @"^(?<min>\d+(?:\.\d+)?)\s*(?:-|~|～|—|–|至|到)\s*(?<max>\d+(?:\.\d+)?)\s*(?:MPa)?$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return match.Success && TryNumber(match.Groups["min"].Value, out var min) &&
            TryNumber(match.Groups["max"].Value, out var max) && min <= max &&
            (strength < min || strength > max);
    }
}
