using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Application.DTOs;
using DocGenerator.Domain.Entities;
using DocGenerator.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DocGenerator.Infrastructure.Persistence;

/// <summary>
/// استعلامات تنبيهات رئيس القسم: قوائم المستلم/الفرع وعدّاد غير المقروء ومستلمو التعميم.
/// </summary>
public partial class HeadAlertRepository : Repository<HeadAlert>, IHeadAlertRepository
{
    public HeadAlertRepository(DocGeneratorDbContext db) : base(db) { }
}
