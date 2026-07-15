using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ferreteria.PuntoVenta.Models;

/// <summary>Bitácora de cambios y sesión. Tabla <c>system.AuditLogs</c>.</summary>
public class AuditLog
{
    [Key]
    public Guid Id { get; set; }

    /// <summary>Tabla lógica afectada (ej. <c>public.Products</c>, <c>hr.Employees</c>).</summary>
    [Required, MaxLength(100)]
    public string TableName { get; set; } = string.Empty;

    /// <summary>Id del registro afectado (como texto).</summary>
    [Required, MaxLength(50)]
    public string RecordId { get; set; } = string.Empty;

    /// <summary>INSERT, UPDATE, DELETE, LOGIN, LOGOUT, PIN_CHANGE.</summary>
    [Required, MaxLength(10)]
    public string Action { get; set; } = string.Empty;

    /// <summary>JSON del estado anterior. No debe incluir PIN ni secretos.</summary>
    [Column(TypeName = "jsonb")]
    public string? OldData { get; set; }

    /// <summary>JSON del estado nuevo. No debe incluir PIN ni secretos.</summary>
    [Column(TypeName = "jsonb")]
    public string? NewData { get; set; }

    /// <summary>Id del <see cref="Employee"/> que ejecutó la acción.</summary>
    public Guid? UserId { get; set; }

    [MaxLength(45)]
    public string? IpAddress { get; set; }

    [MaxLength(300)]
    public string? UserAgent { get; set; }

    [Column(TypeName = "timestamptz")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
