using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FlexoCableSV.PuntoVenta.Models;
public class Order
{
    [Key]
    public long Id { get; set; }
    public int EmployeeId { get; set; }
    public int ApplicationId { get; set; }

    [MaxLength(150)]
    public string? CustomerName { get; set; }
    public DateTime OrderDate { get; set; } = DateTime.UtcNow.Date;
    public TimeSpan OrderTime { get; set; } = DateTime.UtcNow.TimeOfDay;

    [Required, MaxLength(20)]
    public string Status { get; set; } = "PENDIENTE";

    [Column(TypeName = "numeric(12,2)")]
    public decimal Subtotal { get; set; } = 0;

    [Column(TypeName = "numeric(12,2)")]
    public decimal Iva { get; set; } = 0;

    [Column(TypeName = "numeric(12,2)")]
    public decimal Total { get; set; } = 0;
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    [ForeignKey("EmployeeId")]
    public Employee Employee { get; set; } = null!;

    [ForeignKey("ApplicationId")]
    public Application Application { get; set; } = null!;

    public ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();
    public ICollection<DteIssued> DteIssued { get; set; } = new List<DteIssued>();
}