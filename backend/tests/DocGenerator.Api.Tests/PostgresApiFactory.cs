namespace DocGenerator.Api.Tests;

/// <summary>
/// غلاف توافقي لمصنع التكامل على <c>PostgreSQL</c>: منذ `P1-08/8b` صار
/// <see cref="ApiFactory"/> نفسه Postgres-only (قاعدة فريدة + هجرات + <c>Respawn</c>)،
/// ويبقى هذا النوع كي لا تتغيّر أسماء المجموعات/الأصناف القائمة
/// (<see cref="PostgresApiTestCollection"/>).
/// </summary>
public sealed class PostgresApiFactory : ApiFactory
{
}
