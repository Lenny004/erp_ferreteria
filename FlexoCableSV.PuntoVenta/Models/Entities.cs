using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FlexoCableSV.PuntoVenta.Models
{
    // ── public schema — catálogo e inventario ────────────────────────────────

    [Table("measurement_types", Schema = "public")]
    public class MeasurementType
    {
        public int Id { get; set; }
        public string Code { get; set; } = null!;
        public string Name { get; set; } = null!;
        public string UnitLabel { get; set; } = null!;
        public short Decimals { get; set; }
    }

    [Table("families", Schema = "public")]
    public class Family
    {
        public int Id { get; set; }
        public string Code { get; set; } = null!;
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    [Table("subfamilies", Schema = "public")]
    public class Subfamily
    {
        public int Id { get; set; }
        public int FamilyId { get; set; }
        public string Code { get; set; } = null!;
        public string Name { get; set; } = null!;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public Family Family { get; set; } = null!;
    }

    [Table("suppliers", Schema = "public")]
    public class Supplier
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public string? Contact { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    [Table("products", Schema = "public")]
    public class Product
    {
        public int Id { get; set; }
        public string Code { get; set; } = null!;
        public string Description { get; set; } = null!;
        public int FamilyId { get; set; }
        public int? SubfamilyId { get; set; }
        public int MeasurementTypeId { get; set; }
        public decimal SalePrice { get; set; }
        public decimal CostPrice { get; set; }
        public decimal CurrentStock { get; set; }
        public decimal MinStock { get; set; }
        public int? SupplierId { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public Family Family { get; set; } = null!;
        public Subfamily? Subfamily { get; set; }
        public MeasurementType MeasurementType { get; set; } = null!;
        public Supplier? Supplier { get; set; }
    }

    [Table("inventory_movements", Schema = "public")]
    public class InventoryMovement
    {
        public long Id { get; set; }
        public int ProductId { get; set; }
        public string MovementType { get; set; } = null!;
        public decimal Quantity { get; set; }
        public decimal StockBefore { get; set; }
        public decimal StockAfter { get; set; }
        public string? Reason { get; set; }
        public string? DocumentRef { get; set; }
        public int? SupplierId { get; set; }
        public int? EmployeeId { get; set; }
        public DateTime CreatedAt { get; set; }

        public Product Product { get; set; } = null!;
    }

    [Table("stock_alerts", Schema = "public")]
    public class StockAlert
    {
        public long Id { get; set; }
        public int ProductId { get; set; }
        public decimal CurrentStock { get; set; }
        public decimal MinStock { get; set; }
        public bool IsResolved { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public DateTime CreatedAt { get; set; }

        public Product Product { get; set; } = null!;
    }

    // ── sales schema — órdenes ───────────────────────────────────────────────

    [Table("applications", Schema = "sales")]
    public class Application
    {
        public int Id { get; set; }
        public string Code { get; set; } = null!;
        public string Name { get; set; } = null!;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    [Table("orders", Schema = "sales")]
    public class Order
    {
        public long Id { get; set; }
        public int EmployeeId { get; set; }
        public int ApplicationId { get; set; }
        public string? CustomerName { get; set; }
        public DateOnly OrderDate { get; set; }
        public TimeOnly OrderTime { get; set; }
        public string Status { get; set; } = "PENDIENTE";
        public decimal Subtotal { get; set; }
        public decimal Iva { get; set; }
        public decimal Total { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public Employee Employee { get; set; } = null!;
        public Application Application { get; set; } = null!;
        public ICollection<OrderDetail> Details { get; set; } = [];
    }

    [Table("order_details", Schema = "sales")]
    public class OrderDetail
    {
        public long Id { get; set; }
        public long OrderId { get; set; }
        public int ProductId { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Subtotal { get; set; }
        public DateTime CreatedAt { get; set; }

        public Order Order { get; set; } = null!;
        public Product Product { get; set; } = null!;
    }

    // ── dte schema — facturación electrónica ─────────────────────────────────

    [Table("dte_config", Schema = "dte")]
    public class DteConfig
    {
        public int Id { get; set; }
        public string Environment { get; set; } = null!;
        public string ApiUrl { get; set; } = null!;
        public string IssuerNit { get; set; } = null!;
        public string IssuerName { get; set; } = null!;
        public string? IssuerNrc { get; set; }
        public string? ActivityCode { get; set; }
        public string? ActivityDescription { get; set; }
        public string? Address { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? CertificatePath { get; set; }
        public string? CertificateKey { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    [Table("dte_issued", Schema = "dte")]
    public class DteIssued
    {
        public long Id { get; set; }
        public long OrderId { get; set; }
        public string DteType { get; set; } = null!;
        public string ControlNumber { get; set; } = null!;
        public Guid GenerationCode { get; set; }
        public string? ReceptionStamp { get; set; }
        public string MhStatus { get; set; } = "PENDIENTE";
        public string JsonSent { get; set; } = null!;
        public string? JsonResponse { get; set; }
        public string PaymentMethod { get; set; } = "EFECTIVO";
        public string? ReceiverNit { get; set; }
        public string? ReceiverName { get; set; }
        public string Environment { get; set; } = "01";
        public DateTime? SentAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public short Reprints { get; set; }

        public Order Order { get; set; } = null!;
    }

    [Table("dte_contingency", Schema = "dte")]
    public class DteContingency
    {
        public long Id { get; set; }
        public long DteId { get; set; }
        public short Attempts { get; set; }
        public string? LastError { get; set; }
        public DateTime NextAttemptAt { get; set; }
        public bool IsResolved { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public DateTime CreatedAt { get; set; }

        public DteIssued Dte { get; set; } = null!;
    }

    // ── hr schema — empleados (incluye técnicos con PIN) ─────────────────────

    [Table("departments", Schema = "hr")]
    public class Department
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    [Table("positions", Schema = "hr")]
    public class Position
    {
        public int Id { get; set; }
        public int DepartmentId { get; set; }
        public string Name { get; set; } = null!;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public Department Department { get; set; } = null!;
    }

    [Table("employees", Schema = "hr")]
    public class Employee
    {
        public int Id { get; set; }
        public string FirstName { get; set; } = null!;
        public string LastName { get; set; } = null!;
        public string? Dui { get; set; }
        public string? Nit { get; set; }
        public string? IsssNumber { get; set; }
        public string? Nup { get; set; }
        public int? PositionId { get; set; }
        public DateOnly HireDate { get; set; }
        public DateOnly? TerminationDate { get; set; }
        public decimal BaseSalary { get; set; }
        public string ContractType { get; set; } = "PLANILLA";
        public string? Afp { get; set; }
        public string? Phone { get; set; }
        public string? AltPhone { get; set; }
        public string? Email { get; set; }
        public string? Address { get; set; }
        public string? Municipality { get; set; }
        public string? MaritalStatus { get; set; }
        public string? AcademicLevel { get; set; }
        public string? EmergencyName { get; set; }
        public string? EmergencyPhone { get; set; }
        public string? EmergencyRelationship { get; set; }
        public string? PinHash { get; set; }
        public bool CanSell { get; set; }
        public bool CanCashier { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public Position? Position { get; set; }

        [NotMapped]
        public string FullName => $"{FirstName} {LastName}";
    }

    [Table("payroll", Schema = "hr")]
    public class Payroll
    {
        public int Id { get; set; }
        public short PeriodMonth { get; set; }
        public short PeriodYear { get; set; }
        public string Status { get; set; } = "BORRADOR";
        public decimal TotalSalaries { get; set; }
        public decimal TotalIsssEmployee { get; set; }
        public decimal TotalAfpEmployee { get; set; }
        public decimal TotalIsr { get; set; }
        public decimal TotalDeductions { get; set; }
        public decimal TotalNet { get; set; }
        public decimal TotalIsssEmployer { get; set; }
        public decimal TotalAfpEmployer { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public DateTime? PaidAt { get; set; }

        public ICollection<PayrollDetail> Details { get; set; } = [];
    }

    [Table("payroll_details", Schema = "hr")]
    public class PayrollDetail
    {
        public long Id { get; set; }
        public int PayrollId { get; set; }
        public int EmployeeId { get; set; }
        public decimal BaseSalary { get; set; }
        public decimal OvertimeHours { get; set; }
        public decimal OvertimeAmount { get; set; }
        public decimal Bonuses { get; set; }
        public decimal TotalIncome { get; set; }
        public decimal IsssEmployee { get; set; }
        public decimal AfpEmployee { get; set; }
        public decimal Isr { get; set; }
        public decimal OtherDeductions { get; set; }
        public decimal TotalDeductions { get; set; }
        public decimal NetSalary { get; set; }
        public decimal IsssEmployer { get; set; }
        public decimal AfpEmployer { get; set; }
        public DateTime CreatedAt { get; set; }

        public Payroll Payroll { get; set; } = null!;
        public Employee Employee { get; set; } = null!;
    }

    // ── system schema — configuración y auditoría ────────────────────────────

    [Table("settings", Schema = "system")]
    public class Setting
    {
        [Key]
        public string Key { get; set; } = null!;
        public string Value { get; set; } = null!;
        public string? Description { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    [Table("printers", Schema = "system")]
    public class Printer
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public string ConnectionType { get; set; } = "USB";
        public string? IpAddress { get; set; }
        public int? NetworkPort { get; set; }
        public short PaperWidth { get; set; } = 80;
        public bool IsDefault { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    [Table("web_users", Schema = "system")]
    public class WebUser
    {
        public int Id { get; set; }
        public string Username { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string PasswordHash { get; set; } = null!;
        public string Role { get; set; } = "ADMIN";
        public int? EmployeeId { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime? LastLoginAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public Employee? Employee { get; set; }
    }

    [Table("audit_log", Schema = "system")]
    public class AuditLog
    {
        public long Id { get; set; }
        public string TableName { get; set; } = null!;
        public string? RecordId { get; set; }
        public string Action { get; set; } = null!;
        public string? OldData { get; set; }
        public string? NewData { get; set; }
        public string? Description { get; set; }
        public string? IpAddress { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
