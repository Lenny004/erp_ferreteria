using System.Text.Json;
using Ferreteria.PuntoVenta.Data;
using Ferreteria.PuntoVenta.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Ferreteria.PuntoVenta.Services;

public sealed class AuditService(
    IServiceScopeFactory scopeFactory,
    ILogger<AuditService> logger) : IAuditService
{
    public Task RecordLoginAsync(Employee employee, string module, CancellationToken cancellationToken = default)
    {
        return RecordSessionEventAsync("LOGIN", employee, module, cancellationToken);
    }

    public Task RecordLogoutAsync(Employee employee, string? module, CancellationToken cancellationToken = default)
    {
        return RecordSessionEventAsync("LOGOUT", employee, module, cancellationToken);
    }

    public async Task RecordChangeAsync(
        string action,
        string tableName,
        string recordId,
        object? oldData,
        object? newData,
        Guid? userId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();

            db.AuditLogs.Add(new AuditLog
            {
                Id = Guid.NewGuid(),
                TableName = tableName,
                RecordId = recordId,
                Action = action,
                UserId = userId,
                OldData = oldData is null ? null : JsonSerializer.Serialize(oldData),
                NewData = newData is null ? null : JsonSerializer.Serialize(newData),
                CreatedAt = DateTime.UtcNow
            });

            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "No se pudo registrar auditoria {Action} en {Table}/{RecordId}", action, tableName, recordId);
        }
    }

    private async Task RecordSessionEventAsync(
        string action,
        Employee employee,
        string? module,
        CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<FerreteriaDbContext>();

            db.AuditLogs.Add(new AuditLog
            {
                Id = Guid.NewGuid(),
                TableName = "hr.Employees",
                RecordId = employee.Id.ToString(),
                Action = action,
                UserId = employee.Id,
                NewData = JsonSerializer.Serialize(new
                {
                    employeeId = employee.Id,
                    employeeName = $"{employee.FirstName} {employee.LastName}",
                    module,
                    source = "WPF_POS"
                }),
                CreatedAt = DateTime.UtcNow
            });

            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "No se pudo registrar auditoria de sesion {Action} para empleado {EmployeeId}", action, employee.Id);
        }
    }
}
