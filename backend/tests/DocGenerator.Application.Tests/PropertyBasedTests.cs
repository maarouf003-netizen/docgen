using System.Globalization;
using DocGenerator.Application.Common;
using DocGenerator.Application.DTOs;
using DocGenerator.Application.Services;
using DocGenerator.Domain.Entities;
using FsCheck;
using FsCheck.Fluent;

namespace DocGenerator.Application.Tests;

/// <summary>
/// P1-04 — اختبارات قائمة على الخصائص للدوال الحرجة (1000 حالة لكل خاصية):
/// تطبيع الأسماء idempotent، تواريخ round-trip، عدم رمي استثناءات غير متوقعة،
/// واحترام حدود الأطوال مع سلامة surrogate pairs.
/// </summary>
public class PropertyBasedTests
{
    private static void Check(Property property) =>
        FsCheck.Check.One(
            Config.QuickThrowOnFailure.WithMaxTest(1000),
            property);

    /// <summary>نص عربي/لاتيني مختلط يحوي تشكيلًا وتطويلًا وأرقامًا عربية ومسافات.</summary>
    private static Arbitrary<string> ArabicText()
    {
        var chars = Gen.Elements(
            'ا', 'أ', 'إ', 'آ', 'ى', 'ي', 'ة', 'ه', 'ـ', 'َ', 'ُ', 'ِ', 'ّ', 'ْ',
            ' ', '\t', '\n', 'A', 'z', '0', '٥', 'ؤ', 'ئ', 'ء', '،', 'م', 'ح');
        return Arb.From(chars.ArrayOf().Select(items => new string(items)));
    }

    private static string ToArabicDigits(string value) =>
        new(value.Select(c => c is >= '0' and <= '9' ? (char)('٠' + (c - '0')) : c).ToArray());

    private static bool HasUnpairedSurrogate(string value)
    {
        for (var i = 0; i < value.Length; i++)
        {
            if (char.IsHighSurrogate(value[i]))
            {
                if (i + 1 >= value.Length || !char.IsLowSurrogate(value[i + 1]))
                    return true;
                i++;
            }
            else if (char.IsLowSurrogate(value[i]))
            {
                return true;
            }
        }
        return false;
    }

    [Fact]
    public void ArabicNormalizer_IsIdempotent_AndCollapsesWhitespace()
    {
        Check(Prop.ForAll(ArabicText(), (string value) =>
        {
            var once = ArabicNameNormalizer.Normalize(value);
            var twice = ArabicNameNormalizer.Normalize(once);
            return once == twice
                && once == once.Trim()
                && !once.Contains("  ", StringComparison.Ordinal);
        }));
    }

    [Fact]
    public void ActionDateParser_RoundTripsDmyAndIsoFormats()
    {
        Check(Prop.ForAll<DateTime>((DateTime value) =>
        {
            var date = value.Date;
            var dmy = date.ToString("d/M/yyyy", CultureInfo.InvariantCulture);
            var iso = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            return ActionDateParser.TryParse(dmy) == date
                && ActionDateParser.TryParse(iso) == date
                && ActionDateParser.TryParse(ToArabicDigits(dmy)) == date;
        }));
    }

    [Fact]
    public void ActionDateParser_InvalidTextNeverThrows()
    {
        Check(Prop.ForAll<string>((string value) =>
        {
            try
            {
                _ = ActionDateParser.TryParse(value);
                return true;
            }
            catch
            {
                return false;
            }
        }));
    }

    [Fact]
    public void FreeDateParser_OnlyThrowsArgumentException_ForNonEmptyInvalidText()
    {
        Check(Prop.ForAll<string>((string value) =>
        {
            if (string.IsNullOrWhiteSpace(value))
                return FreeDateParser.Parse(value, "الحقل") is null;

            try
            {
                return FreeDateParser.Parse(value, "الحقل") is not null;
            }
            catch (ArgumentException ex)
            {
                return ex.Message.Contains("الحقل", StringComparison.Ordinal);
            }
            catch
            {
                return false;
            }
        }));
    }

    [Fact]
    public void FreeDateParser_ResponseRoundTrips()
    {
        Check(Prop.ForAll<DateTime>((DateTime value) =>
        {
            var text = FreeDateParser.ToResponse(value.Date);
            return text is not null
                && ActionDateParser.TryParse(text) == value.Date;
        }));
    }

    [Fact]
    public void NumberToWords_NeverThrows_AndAlwaysReturnsText()
    {
        Check(Prop.ForAll<long>((long value) =>
        {
            try
            {
                var words = NumberToWords.Convert(value);
                return !string.IsNullOrWhiteSpace(words);
            }
            catch
            {
                return false;
            }
        }));
    }

    [Fact]
    public void NumberToWords_HandlesLongExtremes()
    {
        foreach (var value in new[] { long.MinValue, long.MaxValue, -1L, 0L, 1L })
        {
            var words = NumberToWords.Convert(value);
            Assert.False(string.IsNullOrWhiteSpace(words));
        }

        Assert.StartsWith("ناقص", NumberToWords.Convert(long.MinValue), StringComparison.Ordinal);
    }

    [Fact]
    public void SearchTextTruncate_RespectsLimit_AndNeverSplitsSurrogates()
    {
        Check(Prop.ForAll<string>((string value) =>
        {
            var truncated = DocumentSearchTextBuilder.Truncate(value);
            return truncated.Length <= DocumentSearchTextBuilder.MaxLength
                && !HasUnpairedSurrogate(truncated)
                && DocumentSearchTextBuilder.Truncate(truncated) == truncated;
        }));
    }

    [Fact]
    public void SearchTextBuilder_OutputIsBoundedAndNormalized()
    {
        Check(Prop.ForAll(ArabicText(), (string value) =>
        {
            var document = new Document
            {
                BorrowerName = value,
                BorrowerFamily = value,
                Applicant = value,
                Court = value,
                FileNumber = value,
                ContractNumber = value,
            };

            var text = DocumentSearchTextBuilder.Build(document);
            return text.Length <= DocumentSearchTextBuilder.MaxLength
                && !HasUnpairedSurrogate(text)
                && ArabicNameNormalizer.Normalize(text) == text;
        }));
    }

    [Fact]
    public void ApplicantTextBuilder_RespectsLimit_AndNeverSplitsSurrogates()
    {
        Check(Prop.ForAll<string>((string value) =>
        {
            var text = ApplicantTextBuilder.Build(
            [
                new ApplicantPublicEntity
                {
                    Name = value,
                    Governorate = "دمشق",
                },
            ]);

            return text.Length <= ApplicantTextBuilder.MaxLength
                && !HasUnpairedSurrogate(text);
        }));
    }

    [Fact]
    public void InterventionNormalize_TrimsNaturalName_AndNeverThrowsForValidInput()
    {
        Check(Prop.ForAll(ArabicText(), (string value) =>
        {
            var name = value.Length > 60 ? value[..60] : value;
            if (string.IsNullOrWhiteSpace(name))
                name = "اسم";

            var request = new DocumentInterventionUpsertRequest
            {
                Nature = "natural",
                ReceiptDate = "1/8/2026",
                Name = $"  {name}  ",
            };

            try
            {
                var intervention = DocumentInterventionText.Normalize(request);
                return intervention.Name is not null
                    && intervention.Name == intervention.Name.Trim();
            }
            catch
            {
                return false;
            }
        }));
    }
}
