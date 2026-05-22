using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FlexoCableSV.PuntoVenta.Models
{
    // =============================================================================
    // PUBLIC SCHEMA — Catalog & Inventory
    // =============================================================================

    [Table("measurement_types", Schema = "public")]
    public class MeasurementType
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(10)]
        public string Code { get; set; } = string.Empty;

        [Required, MaxLength(50)]
        public string Name { get; set; } = string.Empty;

        [Required, MaxLength(20)]
        public string UnitLabel { get; set; } = string.Empty;

        public short Decimals { get; set; } = 0;
    }

    [Table("families", Schema = "public")]
    public class Family
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(5)]
        public string Code { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public ICollection<Subfamily> Subfamilies { get; set; } = new List<Subfamily>();
        public ICollection<Product> Products { get; set; } = new List<Product>();
    }

    [Table("subfamilies", Schema = "public")]
    public class Subfamily
    {
        [Key]
        public int Id { get; set; }

        public int FamilyId { get; set; }

        [Required, MaxLength(10)]
        public string Code { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        [ForeignKey("FamilyId")]
        public Family Family { get; set; } = null!;
        public ICollection<Product> Products { get; set; } = new List<Product>();
    }

    [Table("suppliers", Schema = "public")]
    public class Supplier
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(150)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? Contact { get; set; }

        [MaxLength(20)]
        public string? Phone { get; set; }

        [MaxLength(100)]
        public string? Email { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public ICollection<Product> Products { get; set; } = new List<Product>();
        public ICollection<InventoryMovement> InventoryMovements { get; set; } = new List<InventoryMovement>();
    }

    [Table("products", Schema = "public")]
    public class Product
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(30)]
        public string Code { get; set; } = string.Empty;

        [Required, MaxLength(200)]
        public string Description { get; set; } = string.Empty;

        public int FamilyId { get; set; }

        public int? SubfamilyId { get; set; }

        public int MeasurementTypeId { get; set; }

        [Column(TypeName = "numeric(12,2)")]
        public decimal SalePrice { get; set; } = 0;

        [Column(TypeName = "numeric(12,2)")]
        public decimal CostPrice { get; set; } = 0;

        [Column(TypeName = "numeric(12,3)")]
        public decimal CurrentStock { get; set; } = 0;

        [Column(TypeName = "numeric(12,3)")]
        public decimal MinStock { get; set; } = 0;

        public int? SupplierId { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        [ForeignKey("FamilyId")]
        public Family Family { get; set; } = null!;

        [ForeignKey("SubfamilyId")]
        public Subfamily? Subfamily { get; set; }

        [ForeignKey("MeasurementTypeId")]
        public MeasurementType MeasurementType { get; set; } = null!;

        [ForeignKey("SupplierId")]
        public Supplier? Supplier { get; set; }

        public ICollection<InventoryMovement> InventoryMovements { get; set; } = new List<InventoryMovement>();
        public ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();
        public ICollection<StockAlert> StockAlerts { get; set; } = new List<StockAlert>();
    }

    [Table("inventory_movements", Schema = "public")]
    public class InventoryMovement
    {
        [Key]
        public long Id { get; set; }

        public int ProductId { get; set; }

        [Required, MaxLength(20)]
        public string MovementType { get; set; } = string.Empty;

        [Column(TypeName = "numeric(12,3)")]
        public decimal Quantity { get; set; }

        [Column(TypeName = "numeric(12,3)")]
        public decimal StockBefore { get; set; }

        [Column(TypeName = "numeric(12,3)")]
        public decimal StockAfter { get; set; }

        [MaxLength(100)]
        public string? Reason { get; set; }

        [MaxLength(50)]
        public string? DocumentRef { get; set; }

        public int? SupplierId { get; set; }

        public int? EmployeeId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        [ForeignKey("ProductId")]
        public Product Product { get; set; } = null!;

        [ForeignKey("SupplierId")]
        public Supplier? Supplier { get; set; }

        [ForeignKey("EmployeeId")]
        public Employee? Employee { get; set; }
    }

    [Table("stock_alerts", Schema = "public")]
    public class StockAlert
    {
        [Key]
        public long Id { get; set; }

        public int ProductId { get; set; }

        [Column(TypeName = "numeric(12,3)")]
        public decimal CurrentStock { get; set; }

        [Column(TypeName = "numeric(12,3)")]
        public decimal MinStock { get; set; }

        public bool IsResolved { get; set; } = false;

        public DateTime? ResolvedAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        [ForeignKey("ProductId")]
        public Product Product { get; set; } = null!;
    }


    // =============================================================================
    // SALES SCHEMA — Orders & Operations
    // =============================================================================

    [Table("applications", Schema = "sales")]
    public class Application
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(10)]
        public string Code { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public ICollection<Order> Orders { get; set; } = new List<Order>();
    }

    [Table("orders", Schema = "sales")]
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

    [Table("order_details", Schema = "sales")]
    public class OrderDetail
    {
        [Key]
        public long Id { get; set; }

        public long OrderId { get; set; }

        public int ProductId { get; set; }

        [Column(TypeName = "numeric(12,3)")]
        public decimal Quantity { get; set; }

        [Column(TypeName = "numeric(12,2)")]
        public decimal UnitPrice { get; set; }

        [Column(TypeName = "numeric(12,2)")]
        public decimal Subtotal { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        [ForeignKey("OrderId")]
        public Order Order { get; set; } = null!;

        [ForeignKey("ProductId")]
        public Product Product { get; set; } = null!;
    }


    // =============================================================================
    // DTE SCHEMA — Electronic Invoicing
    // =============================================================================

    [Table("dte_config", Schema = "dte")]
    public class DteConfig
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(2)]
        public string Environment { get; set; } = "00";

        [Required, MaxLength(200)]
        public string ApiUrl { get; set; } = string.Empty;

        [Required, MaxLength(20)]
        public string IssuerNit { get; set; } = string.Empty;

        [Required, MaxLength(200)]
        public string IssuerName { get; set; } = string.Empty;

        [MaxLength(20)]
        public string? IssuerNrc { get; set; }

        [MaxLength(10)]
        public string? ActivityCode { get; set; }

        [MaxLength(200)]
        public string? ActivityDescription { get; set; }

        public string? Address { get; set; }

        [MaxLength(20)]
        public string? Phone { get; set; }

        [MaxLength(100)]
        public string? Email { get; set; }

        [MaxLength(500)]
        public string? CertificatePath { get; set; }

        public string? CertificateKey { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    [Table("dte_issued", Schema = "dte")]
    public class DteIssued
    {
        [Key]
        public long Id { get; set; }

        public long OrderId { get; set; }

        [Required, MaxLength(2)]
        public string DteType { get; set; } = string.Empty;

        [Required, MaxLength(50)]
        public string ControlNumber { get; set; } = string.Empty;

        public Guid GenerationCode { get; set; } = Guid.NewGuid();

        [MaxLength(100)]
        public string? ReceptionStamp { get; set; }

        [Required, MaxLength(20)]
        public string MhStatus { get; set; } = "PENDIENTE";

        [Required, Column(TypeName = "jsonb")]
        public string JsonSent { get; set; } = "{}";

        [Column(TypeName = "jsonb")]
        public string? JsonResponse { get; set; }

        [Required, MaxLength(20)]
        public string PaymentMethod { get; set; } = "EFECTIVO";

        [MaxLength(20)]
        public string? ReceiverNit { get; set; }

        [MaxLength(200)]
        public string? ReceiverName { get; set; }

        [Required, MaxLength(2)]
        public string Environment { get; set; } = "01";

        public DateTime? SentAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public short Reprints { get; set; } = 0;

        // Navigation
        [ForeignKey("OrderId")]
        public Order Order { get; set; } = null!;
        public DteContingency? Contingency { get; set; }
    }

    [Table("dte_contingency", Schema = "dte")]
    public class DteContingency
    {
        [Key]
        public long Id { get; set; }

        public long DteId { get; set; }

        public short Attempts { get; set; } = 0;

        public string? LastError { get; set; }

        public DateTime NextAttemptAt { get; set; } = DateTime.UtcNow;

        public bool IsResolved { get; set; } = false;

        public DateTime? ResolvedAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        [ForeignKey("DteId")]
        public DteIssued DteIssued { get; set; } = null!;
    }


    // =============================================================================
    // HR SCHEMA — Employees & Payroll
    // =============================================================================

    [Table("departments", Schema = "hr")]
    public class Department
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public ICollection<Position> Positions { get; set; } = new List<Position>();
    }

    [Table("positions", Schema = "hr")]
    public class Position
    {
        [Key]
        public int Id { get; set; }

        public int DepartmentId { get; set; }

        [Required, MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        [ForeignKey("DepartmentId")]
        public Department Department { get; set; } = null!;
        public ICollection<Employee> Employees { get; set; } = new List<Employee>();
    }

    [Table("employees", Schema = "hr")]
    public class Employee
    {
        [Key]
        public int Id { get; set; }

        // Identity
        [Required, MaxLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string LastName { get; set; } = string.Empty;

        [MaxLength(15)]
        public string? Dui { get; set; }

        [MaxLength(20)]
        public string? Nit { get; set; }

        [MaxLength(20)]
        public string? IsssNumber { get; set; }

        [MaxLength(20)]
        public string? Nup { get; set; }

        // Job
        public int? PositionId { get; set; }

        public DateTime HireDate { get; set; }

        public DateTime? TerminationDate { get; set; }

        [Column(TypeName = "numeric(10,2)")]
        public decimal BaseSalary { get; set; }

        [Required, MaxLength(20)]
        public string ContractType { get; set; } = "PLANILLA";

        [MaxLength(50)]
        public string? Afp { get; set; }

        // Contact
        [MaxLength(20)]
        public string? Phone { get; set; }

        [MaxLength(20)]
        public string? AltPhone { get; set; }

        [MaxLength(100)]
        public string? Email { get; set; }

        public string? Address { get; set; }

        [MaxLength(100)]
        public string? Municipality { get; set; }

        // Personal
        [MaxLength(20)]
        public string? MaritalStatus { get; set; }

        [MaxLength(50)]
        public string? AcademicLevel { get; set; }

        // Emergency contact
        [MaxLength(100)]
        public string? EmergencyName { get; set; }

        [MaxLength(20)]
        public string? EmergencyPhone { get; set; }

        [MaxLength(50)]
        public string? EmergencyRelationship { get; set; }

        // POS access (replaces technicians table)
        public string? PinHash { get; set; }

        public bool CanSell { get; set; } = false;

        public bool CanCashier { get; set; } = false;

        // Status
        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        [ForeignKey("PositionId")]
        public Position? Position { get; set; }

        public ICollection<Order> Orders { get; set; } = new List<Order>();
        public ICollection<InventoryMovement> InventoryMovements { get; set; } = new List<InventoryMovement>();
        public ICollection<PayrollDetail> PayrollDetails { get; set; } = new List<PayrollDetail>();
        public ICollection<WebUser> WebUsers { get; set; } = new List<WebUser>();
    }

    [Table("payroll", Schema = "hr")]
    public class Payroll
    {
        [Key]
        public int Id { get; set; }

        public short PeriodMonth { get; set; }

        public short PeriodYear { get; set; }

        [Required, MaxLength(20)]
        public string Status { get; set; } = "BORRADOR";

        [Column(TypeName = "numeric(12,2)")]
        public decimal TotalSalaries { get; set; } = 0;

        [Column(TypeName = "numeric(12,2)")]
        public decimal TotalIsssEmployee { get; set; } = 0;

        [Column(TypeName = "numeric(12,2)")]
        public decimal TotalAfpEmployee { get; set; } = 0;

        [Column(TypeName = "numeric(12,2)")]
        public decimal TotalIsr { get; set; } = 0;

        [Column(TypeName = "numeric(12,2)")]
        public decimal TotalDeductions { get; set; } = 0;

        [Column(TypeName = "numeric(12,2)")]
        public decimal TotalNet { get; set; } = 0;

        [Column(TypeName = "numeric(12,2)")]
        public decimal TotalIsssEmployer { get; set; } = 0;

        [Column(TypeName = "numeric(12,2)")]
        public decimal TotalAfpEmployer { get; set; } = 0;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? ApprovedAt { get; set; }

        public DateTime? PaidAt { get; set; }

        // Navigation
        public ICollection<PayrollDetail> PayrollDetails { get; set; } = new List<PayrollDetail>();
    }

    [Table("payroll_details", Schema = "hr")]
    public class PayrollDetail
    {
        [Key]
        public long Id { get; set; }

        public int PayrollId { get; set; }

        public int EmployeeId { get; set; }

        // Income
        [Column(TypeName = "numeric(10,2)")]
        public decimal BaseSalary { get; set; }

        [Column(TypeName = "numeric(5,2)")]
        public decimal OvertimeHours { get; set; } = 0;

        [Column(TypeName = "numeric(10,2)")]
        public decimal OvertimeAmount { get; set; } = 0;

        [Column(TypeName = "numeric(10,2)")]
        public decimal Bonuses { get; set; } = 0;

        [Column(TypeName = "numeric(10,2)")]
        public decimal TotalIncome { get; set; }

        // Deductions
        [Column(TypeName = "numeric(10,2)")]
        public decimal IsssEmployee { get; set; }

        [Column(TypeName = "numeric(10,2)")]
        public decimal AfpEmployee { get; set; }

        [Column(TypeName = "numeric(10,2)")]
        public decimal Isr { get; set; } = 0;

        [Column(TypeName = "numeric(10,2)")]
        public decimal OtherDeductions { get; set; } = 0;

        [Column(TypeName = "numeric(10,2)")]
        public decimal TotalDeductions { get; set; }

        // Net
        [Column(TypeName = "numeric(10,2)")]
        public decimal NetSalary { get; set; }

        // Employer cost
        [Column(TypeName = "numeric(10,2)")]
        public decimal IsssEmployer { get; set; }

        [Column(TypeName = "numeric(10,2)")]
        public decimal AfpEmployer { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        [ForeignKey("PayrollId")]
        public Payroll Payroll { get; set; } = null!;

        [ForeignKey("EmployeeId")]
        public Employee Employee { get; set; } = null!;
    }


    // =============================================================================
    // SYSTEM SCHEMA — Settings, Printers, Web Users & Audit
    // =============================================================================

    [Table("settings", Schema = "system")]
    public class Setting
    {
        [Key]
        [MaxLength(100)]
        public string Key { get; set; } = string.Empty;

        [Required]
        public string Value { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? Description { get; set; }

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    [Table("printers", Schema = "system")]
    public class Printer
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [Required, MaxLength(10)]
        public string ConnectionType { get; set; } = "USB";

        [MaxLength(15)]
        public string? IpAddress { get; set; }

        public int? NetworkPort { get; set; }

        public short PaperWidth { get; set; } = 80;

        public bool IsDefault { get; set; } = false;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    [Table("web_users", Schema = "system")]
    public class WebUser
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(50)]
        public string Username { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        [Required, MaxLength(20)]
        public string Role { get; set; } = "ADMIN";

        public int? EmployeeId { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime? LastLoginAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        [ForeignKey("EmployeeId")]
        public Employee? Employee { get; set; }
    }

    [Table("audit_log", Schema = "system")]
    public class AuditLog
    {
        [Key]
        public long Id { get; set; }

        [Required, MaxLength(100)]
        public string TableName { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? RecordId { get; set; }

        [Required, MaxLength(10)]
        public string Action { get; set; } = string.Empty;

        [Column(TypeName = "jsonb")]
        public string? OldData { get; set; }

        [Column(TypeName = "jsonb")]
        public string? NewData { get; set; }

        public string? Description { get; set; }

        [MaxLength(45)]
        public string? IpAddress { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
