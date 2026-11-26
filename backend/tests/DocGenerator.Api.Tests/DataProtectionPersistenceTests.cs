using DocGenerator.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DocGenerator.Api.Tests;

/// <summary>
/// P6-01b: يثبت أن خيار الإنتاج `DataProtection:PersistToDatabase=true` يكتب مفاتيح
/// الحماية في جدول `DataProtectionKeys`، وأن الحماية/فك الحماية يعملان عبر القاعدة.
/// </summary>
public sealed class DataProtectionPersistenceTests
{
    private sealed class DatabaseBackedFactory : ApiFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseSetting("DataProtection:PersistToDatabase", "true");
        }
    }

    [PostgresFact]
    public async Task PersistToDatabase_WritesKeysAndRoundTripsPayload()
    {
        using var factory = new DatabaseBackedFactory();
        var protector = factory.Services
            .GetRequiredService<IDataProtectionProvider>()
            .CreateProtector("docgen-tests");

        var cipher = protector.Protect("محتوى اختباري");
        Assert.Equal("محتوى اختباري", protector.Unprotect(cipher));

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DocGeneratorPostgresDbContext>();
        Assert.True(await db.DataProtectionKeys.AnyAsync());
    }
}
