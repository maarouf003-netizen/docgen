namespace DocGenerator.Benchmarks;

/// <summary>توقيع البيانات: بصمة `MD5` للمستندات + عدّ الجداول المرتبطة.</summary>
public sealed record DataSignature(string Fingerprint, IReadOnlyDictionary<string, long> TableCounts);
