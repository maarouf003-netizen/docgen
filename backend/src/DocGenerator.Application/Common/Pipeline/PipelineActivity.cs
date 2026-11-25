using System.Diagnostics;
using DocGenerator.Application.Common.Observability;

namespace DocGenerator.Application.Common.Pipeline;

/// <summary>
/// `P5-01` — غلاف نشاط موحّد حول تنفيذ معالج واحد. لا يغيّر النتيجة ولا الاستثناء:
/// يعيد نفس القيمة ويعيد رمي نفس الاستثناء بعد وسم النشاط بالفشل.
/// </summary>
internal static class PipelineActivity
{
    /// <summary>هل يوجد مستمع حاليًا؟ عند `false` لا يُنشأ نشاط ولا آلة حالة إضافية.</summary>
    public static bool IsEnabled => ApplicationDiagnostics.ActivitySource.HasListeners();

    public static async Task<TResult> RunAsync<TRequest, TResult>(
        object handler,
        Func<Task<TResult>> next)
    {
        using var activity = ApplicationDiagnostics.ActivitySource.StartActivity(
            typeof(TRequest).Name, ActivityKind.Internal);
        activity?.SetTag(ApplicationDiagnostics.RequestTypeTag, typeof(TRequest).Name);
        activity?.SetTag(ApplicationDiagnostics.HandlerTag, handler.GetType().Name);

        try
        {
            var result = await next();
            activity?.SetTag(ApplicationDiagnostics.OutcomeTag, ApplicationDiagnostics.SuccessOutcome);
            activity?.SetStatus(ActivityStatusCode.Ok);
            return result;
        }
        catch
        {
            // لا يُسجَّل نص الاستثناء في وسم/حدث: قد يحمل بيانات طلب (PII).
            activity?.SetTag(ApplicationDiagnostics.OutcomeTag, ApplicationDiagnostics.ExceptionOutcome);
            activity?.SetStatus(ActivityStatusCode.Error);
            throw;
        }
    }
}
