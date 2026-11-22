using System.Text;
using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;

namespace DocGenerator.Application.Services;

/// <summary>
/// يبني سياق قالب docx من بيانات المستند الحيّة (المكافئ لـ prepare_docxtpl_context + build_document_context).
/// </summary>
public partial class DocumentContextBuilder : IDocumentContextBuilder
{
    // خط تعبئة يدوي يُستخدم عند غياب قيمة حقل مؤرّخ يُضمَن خطيًا (كتاريخ الحجز) ليبقى موضع الكتابة ظاهرًا.
    private const string HandwritingFillLine = "…………………………………………";

    private readonly IRepository<Document> _documents;
    private readonly TimeProvider _clock;
    private readonly TimeZoneInfo _timeZone;

    public DocumentContextBuilder(IRepository<Document> documents, TimeProvider clock, TimeZoneInfo timeZone)
    {
        _documents = documents;
        _clock = clock;
        _timeZone = timeZone;
    }
}
