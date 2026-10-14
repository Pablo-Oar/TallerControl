using Microsoft.EntityFrameworkCore;
using TallerControl.Application.Common;
using TallerControl.Application.Tenancy;
using TallerControl.Domain.Tenancy;
using TallerControl.Infrastructure.Persistence;

namespace TallerControl.Infrastructure.Tenancy;

/// <summary>
/// Numeración correlativa con <c>SELECT … FOR UPDATE</c> sobre <c>document_sequences</c>. Al tomar el
/// bloqueo de la fila, dos documentos del mismo tipo y sucursal se numeran en serie; al ejecutarse
/// dentro de la transacción del documento, un rollback devuelve el número y no deja huecos.
/// </summary>
public sealed class DocumentNumberGenerator : IDocumentNumberGenerator
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IBranchContext _branch;

    public DocumentNumberGenerator(AppDbContext db, ITenantContext tenant, IBranchContext branch)
    {
        _db = db;
        _tenant = tenant;
        _branch = branch;
    }

    public async Task<int> NextAsync(DocumentKind kind, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenant.TenantId
            ?? throw new InvalidOperationException("No hay taller en la sesión.");

        // MVP: cuando no hay sucursal en la sesión (selector no implementado aún), se usa la principal.
        var branchId = _branch.BranchId
            ?? await _db.Set<Branch>()
                .AsNoTracking()
                .Where(b => b.TenantId == tenantId && b.IsMain)
                .Select(b => (Guid?)b.Id)
                .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("No hay sucursal principal configurada para el taller.");

        if (_db.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                "La numeración debe ejecutarse dentro de la transacción del documento; " +
                "fuera de una transacción el número no se devolvería ante un error.");
        }

        var text = EnumText.ToSnakeCase(kind.ToString());

        // 1) Garantiza que la fila exista (no pisa una existente).
        await _db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO document_sequences (tenant_id, branch_id, kind, next_value)
            VALUES ({tenantId}, {branchId}, {text}, 1)
            ON CONFLICT (tenant_id, branch_id, kind) DO NOTHING
            """,
            cancellationToken);

        // 2) Bloquea la fila y lee el valor vigente.
        var current = await _db.Database.SqlQuery<int>(
            $"""
            SELECT next_value AS "Value" FROM document_sequences
            WHERE tenant_id = {tenantId} AND branch_id = {branchId} AND kind = {text}
            FOR UPDATE
            """)
            .SingleAsync(cancellationToken);

        // 3) Avanza el contador; el bloqueo se mantiene hasta el commit o rollback.
        await _db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE document_sequences SET next_value = next_value + 1
            WHERE tenant_id = {tenantId} AND branch_id = {branchId} AND kind = {text}
            """,
            cancellationToken);

        return current;
    }
}
