/**
 * حارس تغطية تجزئة اختبارات Api (مكمل لشبكات subhead):
 * مهمة backend في CI تشغّل Api.Tests على 5 دفعات (shards) بدل التشغيل الموحد —
 * لأن ~14 مصنع WebApplicationFactory متوازيًا يعلّق انتظار الدخول إلى الأبد
 * (مشخّص بـ blame-hang على جهازين: الأنصاف خضراء، الموحد أحمر بلا أي إكمال).
 * هذا الحارس يضمن أن كل فئة `*Tests.cs` مغطاة بدفعة واحدة على الأقل — فأي فئة
 * جديدة تُنسى خارج الدفعات تُفشل CI بدل أن تُسقَط بصمت.
 *
 * عند إضافة فئة اختبار: أضف اسمها (أو بادئة تغطيها) لإحدى القوائم أدناه
 * وفي `.github/workflows/ci.yml` (نفس الدفعة) — القائمتان متطابقتان عمدًا.
 */
import { readdirSync } from 'node:fs';
import { join } from 'node:path';

const ROOT = join(import.meta.dirname, '..');
const TEST_DIR = join(ROOT, 'backend', 'tests', 'DocGenerator.Api.Tests');

// مقاطع `FullyQualifiedName~X` نفسها المستعملة في ci.yml (مطابقة تضمينية).
const shards = {
  'shard-1': [
    'RF007AuditScopeTests',
    'ExecutionCircuits',
    'DocumentsExportIntegrationTests',
    'AppealForwardFlowTests',
    'TransferIntegrationTests',
    'TransferBranchGuardTests',
    'RF004AuthzTests',
    'SubHeadRolePermissionsTests',
  ],
  'shard-2a': [
    'DocumentsIntegrationTests',
    'DelegationsIntegrationTests',
    'DelegationPendingCountBranchGuardTests',
    'AlertsIntegrationTests',
    'EntityRegistry',
  ],
  'shard-2b': [
    'DocumentScopeIsolationTests',
    'DelegationScopeIsolationTests',
    'CorrespondenceIntegrationTests',
    'ReviewCorrespondenceScopeTests',
  ],
  'shard-3': [
    'StatisticsBranchGuardTests',
    'StatisticsHeadScopeIntegrationTests',
    'StatsScopeTests',
    'ManagerStatsIntegrationTests',
    'SectionsIntegrationTests',
    'UserManagementIntegrationTests',
    'UserBranchInvariantTests',
    'BranchManagementIntegrationTests',
    'AuthIntegrationTests',
    'AuthControllerBranchRequiredTests',
    'AuditIntegrationTests',
    'AppSuggestionsIntegrationTests',
    'PersonalRemindersIntegrationTests',
    'WordGenerationIntegrationTests',
    'ClientErrorsIntegrationTests',
  ],
  'shard-4': [
    'RF001',
    'RF003AuthzProofTests',
    'RF005SecurityLoggingTests',
    'RF006PostgresFactoryTests',
    'RF008SessionLifetimeTests',
    'RF009NumberingTests',
    'RF010ConcurrencyTests',
    'RF011IdempotencyTests',
    'RF016HealthTests',
    'RF017SilentWriteAuditTests',
    'RF018ReadAuditTests',
    'RF019AppendOnlyTests',
    'ClaimsPrincipalExtensionsTests',
    'DatabaseInitializerTests',
    'EnricherMiddlewareTests',
    'EntityManagerPortalGuardTests',
    'ExceptionHandlerIntegrationTests',
    'ForwardedHeadersOptionsTests',
    'GlobalExceptionHandlerTests',
    'JwtSecretValidationTests',
    'LockoutIntegrationTests',
    'LogSanitizerTests',
    'MetaControllerTests',
    'MissingRoleClaimTests',
    'PostgresConnectionStringTests',
    'RateLimitingIntegrationTests',
    'SecurityHeadersIntegrationTests',
    'StaleRoleRevocationTests',
    'SwaggerIntegrationTests',
    'TokenRevocationIntegrationTests',
  ],
};

const classes = readdirSync(TEST_DIR)
  .filter((f) => f.endsWith('Tests.cs'))
  .map((f) => f.replace(/\.cs$/, ''));

const uncovered = classes.filter(
  (c) => !Object.values(shards).flat().some((s) => c.includes(s)),
);

if (uncovered.length > 0) {
  console.error(
    `api-shards: ${uncovered.length} فئة بلا دفعة (أضفها لإحدى الدفعات في هذا الملف وفي ci.yml):\n- ${uncovered.join('\n- ')}`,
  );
  process.exit(1);
}
console.log(`api-shards: مغطاة — ${classes.length} فئة في 5 دفعات.`);
