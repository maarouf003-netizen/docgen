namespace DocGenerator.Benchmarks;

/// <summary>
/// فحص ذاتي بلا قاعدة بيانات: يثبت صحة حساب `p50`/`p95` (الاستيفاء الخطي) ومنطق
/// العتبات قبل الاعتماد على أي رقم. يُشغَّل بـ`--self-test` في بوابات التطوير.
/// </summary>
public static class SelfTest
{
    public static int Run()
    {
        var failures = new List<string>();

        Check(failures, "p50 رباعي", MeasurementRunner.Percentile([1, 2, 3, 4], 50), 2.5);
        Check(failures, "p95 رباعي", MeasurementRunner.Percentile([1, 2, 3, 4], 95), 3.85);
        Check(failures, "p50 مفرد", MeasurementRunner.Percentile([10], 50), 10);
        Check(failures, "p0", MeasurementRunner.Percentile([1, 2, 3], 0), 1);
        Check(failures, "p100", MeasurementRunner.Percentile([1, 2, 3], 100), 3);
        Check(failures, "p50 خماسي", MeasurementRunner.Percentile([1, 2, 3, 4, 5], 50), 3);

        CheckThrows(failures, "عيّنة فارغة", () => MeasurementRunner.Percentile([], 50));

        if (failures.Count == 0)
        {
            Console.WriteLine("self-test: ok (percentiles + guards)");
            return 0;
        }

        foreach (var failure in failures)
            Console.Error.WriteLine("self-test: FAIL — " + failure);
        return 1;
    }

    private static void Check(List<string> failures, string name, double actual, double expected)
    {
        if (Math.Abs(actual - expected) > 0.0001)
            failures.Add($"{name}: متوقع {expected}، الناتج {actual}");
    }

    private static void CheckThrows(List<string> failures, string name, Action action)
    {
        try
        {
            action();
            failures.Add($"{name}: لم يرمِ الاستثناء المتوقع");
        }
        catch (ArgumentException)
        {
            // متوقع.
        }
    }
}
