using Ferreteria.PuntoVenta.Models;

namespace Ferreteria.PuntoVenta.Helpers;

/// <summary>Tipo de restricción que se contrasta contra <c>Squema.sql</c>.</summary>
internal enum FieldConstraintKind
{
    MaxLength,
    Precision,
    Scale
}

/// <summary>
/// Vincula una constante de UI con su columna y propiedad EF correspondientes.
/// </summary>
[AttributeUsage(AttributeTargets.Field)]
internal sealed class FieldConstraintAttribute : Attribute
{
    /// <summary>Inicializa el vínculo de una constante con el esquema y el modelo.</summary>
    public FieldConstraintAttribute(
        string schema,
        string table,
        string column,
        FieldConstraintKind kind,
        Type entityType,
        string propertyName)
    {
        Schema = schema;
        Table = table;
        Column = column;
        Kind = kind;
        EntityType = entityType;
        PropertyName = propertyName;
    }

    /// <summary>Esquema PostgreSQL de la tabla.</summary>
    public string Schema { get; }

    /// <summary>Nombre de la tabla PostgreSQL.</summary>
    public string Table { get; }

    /// <summary>Nombre de la columna PostgreSQL.</summary>
    public string Column { get; }

    /// <summary>Tipo de restricción representado por la constante.</summary>
    public FieldConstraintKind Kind { get; }

    /// <summary>Tipo de entidad EF que contiene la propiedad.</summary>
    public Type EntityType { get; }

    /// <summary>Nombre de la propiedad CLR en la entidad EF.</summary>
    public string PropertyName { get; }
}

/// <summary>
/// Contratos de longitud y precisión de los campos editables del POS.
/// </summary>
/// <remarks>
/// Estas constantes son la única fuente que debe usar la UI para sus límites:
/// por ejemplo, un TextBox se enlaza con
/// <c>TextBox.MaxLength="{x:Static helpers:FieldConstraints.Products.DescriptionMaxLength}"</c>.
/// La prueba de deriva comprueba cada constante contra <c>Squema.sql</c> y el metamodelo EF.
/// </remarks>
public static class FieldConstraints
{
    /// <summary>Expone la precisión de apertura para el enlazado XAML.</summary>
    public static int CashSessionsOpeningAmountPrecision => CashSessions.OpeningAmountPrecision;

    /// <summary>Expone la escala de apertura para el enlazado XAML.</summary>
    public static int CashSessionsOpeningAmountScale => CashSessions.OpeningAmountScale;

    /// <summary>Expone la precisión del efectivo declarado para el enlazado XAML.</summary>
    public static int CashSessionsClosingDeclaredAmountPrecision => CashSessions.ClosingDeclaredAmountPrecision;

    /// <summary>Expone la escala del efectivo declarado para el enlazado XAML.</summary>
    public static int CashSessionsClosingDeclaredAmountScale => CashSessions.ClosingDeclaredAmountScale;

    /// <summary>Expone la longitud del código de caja para el enlazado XAML.</summary>
    public static int CashSessionsCashRegisterCodeMaxLength => CashSessions.CashRegisterCodeMaxLength;

    /// <summary>Expone la longitud máxima del nombre de cliente para XAML.</summary>
    public static int CustomerNameMaxLength => Customers.NameMaxLength;

    /// <summary>Expone la longitud máxima del DUI de cliente para XAML.</summary>
    public static int CustomerDuiMaxLength => Customers.DuiMaxLength;

    /// <summary>Expone la longitud máxima del NIT de cliente para XAML.</summary>
    public static int CustomerNitMaxLength => Customers.NitMaxLength;

    /// <summary>Expone la longitud máxima del NRC de cliente para XAML.</summary>
    public static int CustomerNrcMaxLength => Customers.NrcMaxLength;

    /// <summary>Expone la longitud máxima del teléfono de cliente para XAML.</summary>
    public static int CustomerPhoneMaxLength => Customers.PhoneMaxLength;

    /// <summary>Expone la longitud máxima del correo de cliente para XAML.</summary>
    public static int CustomerEmailMaxLength => Customers.EmailMaxLength;

    /// <summary>Expone la longitud máxima de la dirección de cliente para XAML.</summary>
    public static int CustomerAddressMaxLength => Customers.AddressMaxLength;

    /// <summary>Expone la precisión de cantidad de venta para XAML.</summary>
    public static int OrderDetailQuantityPrecision => OrderDetails.QuantityPrecision;

    /// <summary>Expone la escala de cantidad de venta para XAML.</summary>
    public static int OrderDetailQuantityScale => OrderDetails.QuantityScale;

    /// <summary>Restricciones del catálogo de productos.</summary>
    public static class Products
    {
        /// <summary>Longitud máxima del código.</summary>
        [FieldConstraint("public", "Products", "code", FieldConstraintKind.MaxLength, typeof(Product), nameof(Product.Code))]
        public const int CodeMaxLength = 30;

        /// <summary>Longitud máxima del código de barras.</summary>
        [FieldConstraint("public", "Products", "barcode", FieldConstraintKind.MaxLength, typeof(Product), nameof(Product.Barcode))]
        public const int BarcodeMaxLength = 50;

        /// <summary>Longitud máxima de la descripción.</summary>
        [FieldConstraint("public", "Products", "description", FieldConstraintKind.MaxLength, typeof(Product), nameof(Product.Description))]
        public const int DescriptionMaxLength = 200;

        /// <summary>Longitud máxima de la clase de rotación.</summary>
        [FieldConstraint("public", "Products", "RotationClass", FieldConstraintKind.MaxLength, typeof(Product), nameof(Product.RotationClass))]
        public const int RotationClassMaxLength = 10;

        /// <summary>Precisión del precio de venta.</summary>
        [FieldConstraint("public", "Products", "SalePrice", FieldConstraintKind.Precision, typeof(Product), nameof(Product.SalePrice))]
        public const int SalePricePrecision = 12;

        /// <summary>Escala del precio de venta.</summary>
        [FieldConstraint("public", "Products", "SalePrice", FieldConstraintKind.Scale, typeof(Product), nameof(Product.SalePrice))]
        public const int SalePriceScale = 2;

        /// <summary>Precisión del costo.</summary>
        [FieldConstraint("public", "Products", "CostPrice", FieldConstraintKind.Precision, typeof(Product), nameof(Product.CostPrice))]
        public const int CostPricePrecision = 12;

        /// <summary>Escala del costo.</summary>
        [FieldConstraint("public", "Products", "CostPrice", FieldConstraintKind.Scale, typeof(Product), nameof(Product.CostPrice))]
        public const int CostPriceScale = 4;

        /// <summary>Precisión del stock actual.</summary>
        [FieldConstraint("public", "Products", "CurrentStock", FieldConstraintKind.Precision, typeof(Product), nameof(Product.CurrentStock))]
        public const int CurrentStockPrecision = 12;

        /// <summary>Escala del stock actual.</summary>
        [FieldConstraint("public", "Products", "CurrentStock", FieldConstraintKind.Scale, typeof(Product), nameof(Product.CurrentStock))]
        public const int CurrentStockScale = 3;

        /// <summary>Precisión del stock mínimo.</summary>
        [FieldConstraint("public", "Products", "MinStock", FieldConstraintKind.Precision, typeof(Product), nameof(Product.MinStock))]
        public const int MinStockPrecision = 12;

        /// <summary>Escala del stock mínimo.</summary>
        [FieldConstraint("public", "Products", "MinStock", FieldConstraintKind.Scale, typeof(Product), nameof(Product.MinStock))]
        public const int MinStockScale = 3;

        /// <summary>Precisión del stock máximo.</summary>
        [FieldConstraint("public", "Products", "MaxStock", FieldConstraintKind.Precision, typeof(Product), nameof(Product.MaxStock))]
        public const int MaxStockPrecision = 12;

        /// <summary>Escala del stock máximo.</summary>
        [FieldConstraint("public", "Products", "MaxStock", FieldConstraintKind.Scale, typeof(Product), nameof(Product.MaxStock))]
        public const int MaxStockScale = 3;

        /// <summary>Precisión del punto de reorden.</summary>
        [FieldConstraint("public", "Products", "ReorderPoint", FieldConstraintKind.Precision, typeof(Product), nameof(Product.ReorderPoint))]
        public const int ReorderPointPrecision = 12;

        /// <summary>Escala del punto de reorden.</summary>
        [FieldConstraint("public", "Products", "ReorderPoint", FieldConstraintKind.Scale, typeof(Product), nameof(Product.ReorderPoint))]
        public const int ReorderPointScale = 3;
    }

    /// <summary>Restricciones de familias.</summary>
    public static class Families
    {
        /// <summary>Longitud máxima del código.</summary>
        [FieldConstraint("public", "Families", "code", FieldConstraintKind.MaxLength, typeof(Family), nameof(Family.Code))]
        public const int CodeMaxLength = 10;

        /// <summary>Longitud máxima del nombre.</summary>
        [FieldConstraint("public", "Families", "name", FieldConstraintKind.MaxLength, typeof(Family), nameof(Family.Name))]
        public const int NameMaxLength = 100;

        /// <summary>Longitud máxima de la descripción.</summary>
        [FieldConstraint("public", "Families", "description", FieldConstraintKind.MaxLength, typeof(Family), nameof(Family.Description))]
        public const int DescriptionMaxLength = 300;
    }

    /// <summary>Restricciones de subfamilias.</summary>
    public static class Subfamilies
    {
        /// <summary>Longitud máxima del código.</summary>
        [FieldConstraint("public", "Subfamilies", "code", FieldConstraintKind.MaxLength, typeof(Subfamily), nameof(Subfamily.Code))]
        public const int CodeMaxLength = 10;

        /// <summary>Longitud máxima del nombre.</summary>
        [FieldConstraint("public", "Subfamilies", "name", FieldConstraintKind.MaxLength, typeof(Subfamily), nameof(Subfamily.Name))]
        public const int NameMaxLength = 100;

        /// <summary>Longitud máxima de la descripción.</summary>
        [FieldConstraint("public", "Subfamilies", "description", FieldConstraintKind.MaxLength, typeof(Subfamily), nameof(Subfamily.Description))]
        public const int DescriptionMaxLength = 300;
    }

    /// <summary>Restricciones de unidades de medida.</summary>
    public static class MeasurementTypes
    {
        /// <summary>Longitud máxima del código.</summary>
        [FieldConstraint("public", "MeasurementTypes", "code", FieldConstraintKind.MaxLength, typeof(MeasurementType), nameof(MeasurementType.Code))]
        public const int CodeMaxLength = 10;

        /// <summary>Longitud máxima del nombre.</summary>
        [FieldConstraint("public", "MeasurementTypes", "name", FieldConstraintKind.MaxLength, typeof(MeasurementType), nameof(MeasurementType.Name))]
        public const int NameMaxLength = 50;

        /// <summary>Longitud máxima de la etiqueta de unidad.</summary>
        [FieldConstraint("public", "MeasurementTypes", "UnitLabel", FieldConstraintKind.MaxLength, typeof(MeasurementType), nameof(MeasurementType.UnitLabel))]
        public const int UnitLabelMaxLength = 20;
    }

    /// <summary>Restricciones de unidades de venta.</summary>
    public static class SaleUnits
    {
        /// <summary>Longitud máxima del código.</summary>
        [FieldConstraint("public", "SaleUnits", "code", FieldConstraintKind.MaxLength, typeof(SaleUnit), nameof(SaleUnit.Code))]
        public const int CodeMaxLength = 20;

        /// <summary>Longitud máxima del nombre.</summary>
        [FieldConstraint("public", "SaleUnits", "name", FieldConstraintKind.MaxLength, typeof(SaleUnit), nameof(SaleUnit.Name))]
        public const int NameMaxLength = 50;

        /// <summary>Longitud máxima de la abreviatura.</summary>
        [FieldConstraint("public", "SaleUnits", "Abbreviation", FieldConstraintKind.MaxLength, typeof(SaleUnit), nameof(SaleUnit.Abbreviation))]
        public const int AbbreviationMaxLength = 10;
    }

    /// <summary>Restricciones de clientes.</summary>
    public static class Customers
    {
        /// <summary>Longitud máxima del tipo de cliente.</summary>
        [FieldConstraint("public", "Customers", "CustomerType", FieldConstraintKind.MaxLength, typeof(Customer), nameof(Customer.CustomerType))]
        public const int CustomerTypeMaxLength = 5;

        /// <summary>Longitud máxima del nombre.</summary>
        [FieldConstraint("public", "Customers", "name", FieldConstraintKind.MaxLength, typeof(Customer), nameof(Customer.Name))]
        public const int NameMaxLength = 200;

        /// <summary>Longitud máxima del DUI.</summary>
        [FieldConstraint("public", "Customers", "Dui", FieldConstraintKind.MaxLength, typeof(Customer), nameof(Customer.Dui))]
        public const int DuiMaxLength = 15;

        /// <summary>Longitud máxima del NIT.</summary>
        [FieldConstraint("public", "Customers", "Nit", FieldConstraintKind.MaxLength, typeof(Customer), nameof(Customer.Nit))]
        public const int NitMaxLength = 20;

        /// <summary>Longitud máxima del NRC.</summary>
        [FieldConstraint("public", "Customers", "Nrc", FieldConstraintKind.MaxLength, typeof(Customer), nameof(Customer.Nrc))]
        public const int NrcMaxLength = 20;

        /// <summary>Longitud máxima del teléfono.</summary>
        [FieldConstraint("public", "Customers", "phone", FieldConstraintKind.MaxLength, typeof(Customer), nameof(Customer.Phone))]
        public const int PhoneMaxLength = 20;

        /// <summary>Longitud máxima del correo.</summary>
        [FieldConstraint("public", "Customers", "email", FieldConstraintKind.MaxLength, typeof(Customer), nameof(Customer.Email))]
        public const int EmailMaxLength = 100;

        /// <summary>Longitud máxima de la dirección.</summary>
        [FieldConstraint("public", "Customers", "address", FieldConstraintKind.MaxLength, typeof(Customer), nameof(Customer.Address))]
        public const int AddressMaxLength = 300;

        /// <summary>Longitud máxima del municipio.</summary>
        [FieldConstraint("public", "Customers", "municipality", FieldConstraintKind.MaxLength, typeof(Customer), nameof(Customer.Municipality))]
        public const int MunicipalityMaxLength = 100;

        /// <summary>Longitud máxima del departamento.</summary>
        [FieldConstraint("public", "Customers", "department", FieldConstraintKind.MaxLength, typeof(Customer), nameof(Customer.Department))]
        public const int DepartmentMaxLength = 50;
    }

    /// <summary>Restricciones de cabeceras de ventas.</summary>
    public static class Orders
    {
        /// <summary>Precisión del subtotal.</summary>
        [FieldConstraint("sales", "Orders", "subtotal", FieldConstraintKind.Precision, typeof(Order), nameof(Order.Subtotal))]
        public const int SubtotalPrecision = 12;

        /// <summary>Escala del subtotal.</summary>
        [FieldConstraint("sales", "Orders", "subtotal", FieldConstraintKind.Scale, typeof(Order), nameof(Order.Subtotal))]
        public const int SubtotalScale = 2;

        /// <summary>Precisión del IVA.</summary>
        [FieldConstraint("sales", "Orders", "TaxAmount", FieldConstraintKind.Precision, typeof(Order), nameof(Order.TaxAmount))]
        public const int TaxAmountPrecision = 12;

        /// <summary>Escala del IVA.</summary>
        [FieldConstraint("sales", "Orders", "TaxAmount", FieldConstraintKind.Scale, typeof(Order), nameof(Order.TaxAmount))]
        public const int TaxAmountScale = 2;

        /// <summary>Precisión del descuento.</summary>
        [FieldConstraint("sales", "Orders", "DiscountAmount", FieldConstraintKind.Precision, typeof(Order), nameof(Order.DiscountAmount))]
        public const int DiscountAmountPrecision = 12;

        /// <summary>Escala del descuento.</summary>
        [FieldConstraint("sales", "Orders", "DiscountAmount", FieldConstraintKind.Scale, typeof(Order), nameof(Order.DiscountAmount))]
        public const int DiscountAmountScale = 2;

        /// <summary>Precisión del total.</summary>
        [FieldConstraint("sales", "Orders", "total", FieldConstraintKind.Precision, typeof(Order), nameof(Order.Total))]
        public const int TotalPrecision = 12;

        /// <summary>Escala del total.</summary>
        [FieldConstraint("sales", "Orders", "total", FieldConstraintKind.Scale, typeof(Order), nameof(Order.Total))]
        public const int TotalScale = 2;
    }

    /// <summary>Restricciones de líneas de venta.</summary>
    public static class OrderDetails
    {
        /// <summary>Precisión de la cantidad vendida.</summary>
        [FieldConstraint("sales", "OrderDetails", "quantity", FieldConstraintKind.Precision, typeof(OrderDetail), nameof(OrderDetail.Quantity))]
        public const int QuantityPrecision = 12;

        /// <summary>Escala de la cantidad vendida.</summary>
        [FieldConstraint("sales", "OrderDetails", "quantity", FieldConstraintKind.Scale, typeof(OrderDetail), nameof(OrderDetail.Quantity))]
        public const int QuantityScale = 3;

        /// <summary>Precisión de unidades por presentación.</summary>
        [FieldConstraint("sales", "OrderDetails", "UnitsPerPackage", FieldConstraintKind.Precision, typeof(OrderDetail), nameof(OrderDetail.UnitsPerPackage))]
        public const int UnitsPerPackagePrecision = 12;

        /// <summary>Escala de unidades por presentación.</summary>
        [FieldConstraint("sales", "OrderDetails", "UnitsPerPackage", FieldConstraintKind.Scale, typeof(OrderDetail), nameof(OrderDetail.UnitsPerPackage))]
        public const int UnitsPerPackageScale = 3;

        /// <summary>Precisión del precio unitario.</summary>
        [FieldConstraint("sales", "OrderDetails", "UnitPrice", FieldConstraintKind.Precision, typeof(OrderDetail), nameof(OrderDetail.UnitPrice))]
        public const int UnitPricePrecision = 12;

        /// <summary>Escala del precio unitario.</summary>
        [FieldConstraint("sales", "OrderDetails", "UnitPrice", FieldConstraintKind.Scale, typeof(OrderDetail), nameof(OrderDetail.UnitPrice))]
        public const int UnitPriceScale = 2;

        /// <summary>Precisión del costo unitario.</summary>
        [FieldConstraint("sales", "OrderDetails", "UnitCost", FieldConstraintKind.Precision, typeof(OrderDetail), nameof(OrderDetail.UnitCost))]
        public const int UnitCostPrecision = 12;

        /// <summary>Escala del costo unitario.</summary>
        [FieldConstraint("sales", "OrderDetails", "UnitCost", FieldConstraintKind.Scale, typeof(OrderDetail), nameof(OrderDetail.UnitCost))]
        public const int UnitCostScale = 4;

        /// <summary>Precisión del descuento de línea.</summary>
        [FieldConstraint("sales", "OrderDetails", "DiscountAmount", FieldConstraintKind.Precision, typeof(OrderDetail), nameof(OrderDetail.DiscountAmount))]
        public const int DiscountAmountPrecision = 12;

        /// <summary>Escala del descuento de línea.</summary>
        [FieldConstraint("sales", "OrderDetails", "DiscountAmount", FieldConstraintKind.Scale, typeof(OrderDetail), nameof(OrderDetail.DiscountAmount))]
        public const int DiscountAmountScale = 2;

        /// <summary>Precisión del subtotal de línea.</summary>
        [FieldConstraint("sales", "OrderDetails", "subtotal", FieldConstraintKind.Precision, typeof(OrderDetail), nameof(OrderDetail.Subtotal))]
        public const int SubtotalPrecision = 12;

        /// <summary>Escala del subtotal de línea.</summary>
        [FieldConstraint("sales", "OrderDetails", "subtotal", FieldConstraintKind.Scale, typeof(OrderDetail), nameof(OrderDetail.Subtotal))]
        public const int SubtotalScale = 2;

        /// <summary>Longitud máxima de la nota de línea.</summary>
        [FieldConstraint("sales", "OrderDetails", "notes", FieldConstraintKind.MaxLength, typeof(OrderDetail), nameof(OrderDetail.Notes))]
        public const int NotesMaxLength = 300;
    }

    /// <summary>Restricciones de pagos de ventas.</summary>
    public static class Payments
    {
        /// <summary>Longitud máxima del método de pago.</summary>
        [FieldConstraint("sales", "Payments", "method", FieldConstraintKind.MaxLength, typeof(Payment), nameof(Payment.Method))]
        public const int MethodMaxLength = 20;

        /// <summary>Precisión del monto cobrado.</summary>
        [FieldConstraint("sales", "Payments", "amount", FieldConstraintKind.Precision, typeof(Payment), nameof(Payment.Amount))]
        public const int AmountPrecision = 12;

        /// <summary>Escala del monto cobrado.</summary>
        [FieldConstraint("sales", "Payments", "amount", FieldConstraintKind.Scale, typeof(Payment), nameof(Payment.Amount))]
        public const int AmountScale = 2;

        /// <summary>Longitud máxima de la referencia de pago.</summary>
        [FieldConstraint("sales", "Payments", "reference", FieldConstraintKind.MaxLength, typeof(Payment), nameof(Payment.Reference))]
        public const int ReferenceMaxLength = 100;
    }

    /// <summary>Restricciones de proveedores.</summary>
    public static class Suppliers
    {
        /// <summary>Longitud máxima de la razón social.</summary>
        [FieldConstraint("purchasing", "Suppliers", "name", FieldConstraintKind.MaxLength, typeof(Supplier), nameof(Supplier.Name))]
        public const int NameMaxLength = 200;

        /// <summary>Longitud máxima del nombre comercial.</summary>
        [FieldConstraint("purchasing", "Suppliers", "TradeName", FieldConstraintKind.MaxLength, typeof(Supplier), nameof(Supplier.TradeName))]
        public const int TradeNameMaxLength = 200;

        /// <summary>Longitud máxima del NIT.</summary>
        [FieldConstraint("purchasing", "Suppliers", "Nit", FieldConstraintKind.MaxLength, typeof(Supplier), nameof(Supplier.Nit))]
        public const int NitMaxLength = 20;

        /// <summary>Longitud máxima del NRC.</summary>
        [FieldConstraint("purchasing", "Suppliers", "Nrc", FieldConstraintKind.MaxLength, typeof(Supplier), nameof(Supplier.Nrc))]
        public const int NrcMaxLength = 20;

        /// <summary>Longitud máxima del contacto.</summary>
        [FieldConstraint("purchasing", "Suppliers", "ContactName", FieldConstraintKind.MaxLength, typeof(Supplier), nameof(Supplier.ContactName))]
        public const int ContactNameMaxLength = 150;

        /// <summary>Longitud máxima del teléfono.</summary>
        [FieldConstraint("purchasing", "Suppliers", "phone", FieldConstraintKind.MaxLength, typeof(Supplier), nameof(Supplier.Phone))]
        public const int PhoneMaxLength = 20;

        /// <summary>Longitud máxima del correo.</summary>
        [FieldConstraint("purchasing", "Suppliers", "email", FieldConstraintKind.MaxLength, typeof(Supplier), nameof(Supplier.Email))]
        public const int EmailMaxLength = 100;

        /// <summary>Longitud máxima de la dirección.</summary>
        [FieldConstraint("purchasing", "Suppliers", "address", FieldConstraintKind.MaxLength, typeof(Supplier), nameof(Supplier.Address))]
        public const int AddressMaxLength = 300;

        /// <summary>Longitud máxima del municipio.</summary>
        [FieldConstraint("purchasing", "Suppliers", "municipality", FieldConstraintKind.MaxLength, typeof(Supplier), nameof(Supplier.Municipality))]
        public const int MunicipalityMaxLength = 100;

        /// <summary>Longitud máxima del departamento.</summary>
        [FieldConstraint("purchasing", "Suppliers", "department", FieldConstraintKind.MaxLength, typeof(Supplier), nameof(Supplier.Department))]
        public const int DepartmentMaxLength = 50;

        /// <summary>Longitud máxima del país.</summary>
        [FieldConstraint("purchasing", "Suppliers", "country", FieldConstraintKind.MaxLength, typeof(Supplier), nameof(Supplier.Country))]
        public const int CountryMaxLength = 5;
    }

    /// <summary>Restricciones de empleados y usuarios POS.</summary>
    public static class Employees
    {
        /// <summary>Longitud máxima del nombre.</summary>
        [FieldConstraint("hr", "Employees", "FirstName", FieldConstraintKind.MaxLength, typeof(Employee), nameof(Employee.FirstName))]
        public const int FirstNameMaxLength = 100;

        /// <summary>Longitud máxima del apellido.</summary>
        [FieldConstraint("hr", "Employees", "LastName", FieldConstraintKind.MaxLength, typeof(Employee), nameof(Employee.LastName))]
        public const int LastNameMaxLength = 100;

        /// <summary>Longitud máxima del DUI.</summary>
        [FieldConstraint("hr", "Employees", "Dui", FieldConstraintKind.MaxLength, typeof(Employee), nameof(Employee.Dui))]
        public const int DuiMaxLength = 15;

        /// <summary>Longitud máxima del NIT.</summary>
        [FieldConstraint("hr", "Employees", "Nit", FieldConstraintKind.MaxLength, typeof(Employee), nameof(Employee.Nit))]
        public const int NitMaxLength = 20;

        /// <summary>Longitud máxima del NUP.</summary>
        [FieldConstraint("hr", "Employees", "Nup", FieldConstraintKind.MaxLength, typeof(Employee), nameof(Employee.Nup))]
        public const int NupMaxLength = 20;

        /// <summary>Longitud máxima del número ISSS.</summary>
        [FieldConstraint("hr", "Employees", "IsssNumber", FieldConstraintKind.MaxLength, typeof(Employee), nameof(Employee.IsssNumber))]
        public const int IsssNumberMaxLength = 20;

        /// <summary>Longitud máxima del tipo de contrato.</summary>
        [FieldConstraint("hr", "Employees", "ContractType", FieldConstraintKind.MaxLength, typeof(Employee), nameof(Employee.ContractType))]
        public const int ContractTypeMaxLength = 20;

        /// <summary>Longitud máxima del tipo de salario.</summary>
        [FieldConstraint("hr", "Employees", "SalaryType", FieldConstraintKind.MaxLength, typeof(Employee), nameof(Employee.SalaryType))]
        public const int SalaryTypeMaxLength = 20;

        /// <summary>Longitud máxima del género.</summary>
        [FieldConstraint("hr", "Employees", "Gender", FieldConstraintKind.MaxLength, typeof(Employee), nameof(Employee.Gender))]
        public const int GenderMaxLength = 20;

        /// <summary>Longitud máxima de la nacionalidad.</summary>
        [FieldConstraint("hr", "Employees", "Nationality", FieldConstraintKind.MaxLength, typeof(Employee), nameof(Employee.Nationality))]
        public const int NationalityMaxLength = 20;

        /// <summary>Longitud máxima del pasaporte.</summary>
        [FieldConstraint("hr", "Employees", "PassportNumber", FieldConstraintKind.MaxLength, typeof(Employee), nameof(Employee.PassportNumber))]
        public const int PassportNumberMaxLength = 30;

        /// <summary>Longitud máxima del estado civil.</summary>
        [FieldConstraint("hr", "Employees", "MaritalStatus", FieldConstraintKind.MaxLength, typeof(Employee), nameof(Employee.MaritalStatus))]
        public const int MaritalStatusMaxLength = 20;

        /// <summary>Longitud máxima del nivel académico.</summary>
        [FieldConstraint("hr", "Employees", "AcademicLevel", FieldConstraintKind.MaxLength, typeof(Employee), nameof(Employee.AcademicLevel))]
        public const int AcademicLevelMaxLength = 50;

        /// <summary>Longitud máxima del departamento salvadoreño.</summary>
        [FieldConstraint("hr", "Employees", "DepartmentSv", FieldConstraintKind.MaxLength, typeof(Employee), nameof(Employee.DepartmentSv))]
        public const int DepartmentSvMaxLength = 30;

        /// <summary>Longitud máxima del teléfono.</summary>
        [FieldConstraint("hr", "Employees", "phone", FieldConstraintKind.MaxLength, typeof(Employee), nameof(Employee.Phone))]
        public const int PhoneMaxLength = 20;

        /// <summary>Longitud máxima del correo.</summary>
        [FieldConstraint("hr", "Employees", "email", FieldConstraintKind.MaxLength, typeof(Employee), nameof(Employee.Email))]
        public const int EmailMaxLength = 100;

        /// <summary>Longitud máxima de la institución AFP.</summary>
        [FieldConstraint("hr", "Employees", "AfpInstitution", FieldConstraintKind.MaxLength, typeof(Employee), nameof(Employee.AfpInstitution))]
        public const int AfpInstitutionMaxLength = 20;

        /// <summary>Longitud máxima del canal de pago.</summary>
        [FieldConstraint("hr", "Employees", "PaymentChannel", FieldConstraintKind.MaxLength, typeof(Employee), nameof(Employee.PaymentChannel))]
        public const int PaymentChannelMaxLength = 30;

        /// <summary>Longitud máxima del motivo de terminación.</summary>
        [FieldConstraint("hr", "Employees", "TerminationReason", FieldConstraintKind.MaxLength, typeof(Employee), nameof(Employee.TerminationReason))]
        public const int TerminationReasonMaxLength = 40;

        /// <summary>Precisión del salario base.</summary>
        [FieldConstraint("hr", "Employees", "BaseSalary", FieldConstraintKind.Precision, typeof(Employee), nameof(Employee.BaseSalary))]
        public const int BaseSalaryPrecision = 10;

        /// <summary>Escala del salario base.</summary>
        [FieldConstraint("hr", "Employees", "BaseSalary", FieldConstraintKind.Scale, typeof(Employee), nameof(Employee.BaseSalary))]
        public const int BaseSalaryScale = 2;

        /// <summary>Precisión del bono predeterminado.</summary>
        [FieldConstraint("hr", "Employees", "DefaultBonus", FieldConstraintKind.Precision, typeof(Employee), nameof(Employee.DefaultBonus))]
        public const int DefaultBonusPrecision = 10;

        /// <summary>Escala del bono predeterminado.</summary>
        [FieldConstraint("hr", "Employees", "DefaultBonus", FieldConstraintKind.Scale, typeof(Employee), nameof(Employee.DefaultBonus))]
        public const int DefaultBonusScale = 2;

        /// <summary>Precisión de viáticos predeterminados.</summary>
        [FieldConstraint("hr", "Employees", "DefaultViaticos", FieldConstraintKind.Precision, typeof(Employee), nameof(Employee.DefaultViaticos))]
        public const int DefaultViaticosPrecision = 10;

        /// <summary>Escala de viáticos predeterminados.</summary>
        [FieldConstraint("hr", "Employees", "DefaultViaticos", FieldConstraintKind.Scale, typeof(Employee), nameof(Employee.DefaultViaticos))]
        public const int DefaultViaticosScale = 2;
    }

    /// <summary>Restricciones de movimientos de inventario.</summary>
    public static class InventoryMovements
    {
        /// <summary>Longitud máxima del tipo de movimiento.</summary>
        [FieldConstraint("public", "InventoryMovements", "MovementType", FieldConstraintKind.MaxLength, typeof(InventoryMovement), nameof(InventoryMovement.MovementType))]
        public const int MovementTypeMaxLength = 30;

        /// <summary>Longitud máxima del motivo.</summary>
        [FieldConstraint("public", "InventoryMovements", "reason", FieldConstraintKind.MaxLength, typeof(InventoryMovement), nameof(InventoryMovement.Reason))]
        public const int ReasonMaxLength = 300;

        /// <summary>Precisión de la cantidad.</summary>
        [FieldConstraint("public", "InventoryMovements", "quantity", FieldConstraintKind.Precision, typeof(InventoryMovement), nameof(InventoryMovement.Quantity))]
        public const int QuantityPrecision = 12;

        /// <summary>Escala de la cantidad.</summary>
        [FieldConstraint("public", "InventoryMovements", "quantity", FieldConstraintKind.Scale, typeof(InventoryMovement), nameof(InventoryMovement.Quantity))]
        public const int QuantityScale = 3;

        /// <summary>Precisión del costo unitario.</summary>
        [FieldConstraint("public", "InventoryMovements", "UnitCost", FieldConstraintKind.Precision, typeof(InventoryMovement), nameof(InventoryMovement.UnitCost))]
        public const int UnitCostPrecision = 12;

        /// <summary>Escala del costo unitario.</summary>
        [FieldConstraint("public", "InventoryMovements", "UnitCost", FieldConstraintKind.Scale, typeof(InventoryMovement), nameof(InventoryMovement.UnitCost))]
        public const int UnitCostScale = 4;

        /// <summary>Precisión del costo total.</summary>
        [FieldConstraint("public", "InventoryMovements", "TotalCost", FieldConstraintKind.Precision, typeof(InventoryMovement), nameof(InventoryMovement.TotalCost))]
        public const int TotalCostPrecision = 12;

        /// <summary>Escala del costo total.</summary>
        [FieldConstraint("public", "InventoryMovements", "TotalCost", FieldConstraintKind.Scale, typeof(InventoryMovement), nameof(InventoryMovement.TotalCost))]
        public const int TotalCostScale = 4;

        /// <summary>Precisión del stock anterior.</summary>
        [FieldConstraint("public", "InventoryMovements", "StockBefore", FieldConstraintKind.Precision, typeof(InventoryMovement), nameof(InventoryMovement.StockBefore))]
        public const int StockBeforePrecision = 12;

        /// <summary>Escala del stock anterior.</summary>
        [FieldConstraint("public", "InventoryMovements", "StockBefore", FieldConstraintKind.Scale, typeof(InventoryMovement), nameof(InventoryMovement.StockBefore))]
        public const int StockBeforeScale = 3;

        /// <summary>Precisión del stock posterior.</summary>
        [FieldConstraint("public", "InventoryMovements", "StockAfter", FieldConstraintKind.Precision, typeof(InventoryMovement), nameof(InventoryMovement.StockAfter))]
        public const int StockAfterPrecision = 12;

        /// <summary>Escala del stock posterior.</summary>
        [FieldConstraint("public", "InventoryMovements", "StockAfter", FieldConstraintKind.Scale, typeof(InventoryMovement), nameof(InventoryMovement.StockAfter))]
        public const int StockAfterScale = 3;
    }

    /// <summary>Restricciones de sesiones de caja.</summary>
    public static class CashSessions
    {
        /// <summary>Longitud máxima del código de caja.</summary>
        [FieldConstraint("sales", "CashSessions", "CashRegisterCode", FieldConstraintKind.MaxLength, typeof(CashSession), nameof(CashSession.CashRegisterCode))]
        public const int CashRegisterCodeMaxLength = 50;

        /// <summary>Longitud máxima del estado.</summary>
        [FieldConstraint("sales", "CashSessions", "status", FieldConstraintKind.MaxLength, typeof(CashSession), nameof(CashSession.Status))]
        public const int StatusMaxLength = 20;

        /// <summary>Precisión del fondo inicial.</summary>
        [FieldConstraint("sales", "CashSessions", "OpeningAmount", FieldConstraintKind.Precision, typeof(CashSession), nameof(CashSession.OpeningAmount))]
        public const int OpeningAmountPrecision = 12;

        /// <summary>Escala del fondo inicial.</summary>
        [FieldConstraint("sales", "CashSessions", "OpeningAmount", FieldConstraintKind.Scale, typeof(CashSession), nameof(CashSession.OpeningAmount))]
        public const int OpeningAmountScale = 2;

        /// <summary>Precisión del efectivo declarado.</summary>
        [FieldConstraint("sales", "CashSessions", "ClosingDeclaredAmount", FieldConstraintKind.Precision, typeof(CashSession), nameof(CashSession.ClosingDeclaredAmount))]
        public const int ClosingDeclaredAmountPrecision = 12;

        /// <summary>Escala del efectivo declarado.</summary>
        [FieldConstraint("sales", "CashSessions", "ClosingDeclaredAmount", FieldConstraintKind.Scale, typeof(CashSession), nameof(CashSession.ClosingDeclaredAmount))]
        public const int ClosingDeclaredAmountScale = 2;

        /// <summary>Precisión del efectivo esperado.</summary>
        [FieldConstraint("sales", "CashSessions", "ClosingExpectedAmount", FieldConstraintKind.Precision, typeof(CashSession), nameof(CashSession.ClosingExpectedAmount))]
        public const int ClosingExpectedAmountPrecision = 12;

        /// <summary>Escala del efectivo esperado.</summary>
        [FieldConstraint("sales", "CashSessions", "ClosingExpectedAmount", FieldConstraintKind.Scale, typeof(CashSession), nameof(CashSession.ClosingExpectedAmount))]
        public const int ClosingExpectedAmountScale = 2;

        /// <summary>Precisión de la diferencia.</summary>
        [FieldConstraint("sales", "CashSessions", "difference", FieldConstraintKind.Precision, typeof(CashSession), nameof(CashSession.Difference))]
        public const int DifferencePrecision = 12;

        /// <summary>Escala de la diferencia.</summary>
        [FieldConstraint("sales", "CashSessions", "difference", FieldConstraintKind.Scale, typeof(CashSession), nameof(CashSession.Difference))]
        public const int DifferenceScale = 2;
    }

    /// <summary>Restricciones de movimientos de efectivo.</summary>
    public static class CashMovements
    {
        /// <summary>Longitud máxima del tipo de movimiento.</summary>
        [FieldConstraint("sales", "CashMovements", "MovementType", FieldConstraintKind.MaxLength, typeof(CashMovement), nameof(CashMovement.MovementType))]
        public const int MovementTypeMaxLength = 30;

        /// <summary>Longitud máxima del motivo.</summary>
        [FieldConstraint("sales", "CashMovements", "reason", FieldConstraintKind.MaxLength, typeof(CashMovement), nameof(CashMovement.Reason))]
        public const int ReasonMaxLength = 300;

        /// <summary>Precisión del importe.</summary>
        [FieldConstraint("sales", "CashMovements", "amount", FieldConstraintKind.Precision, typeof(CashMovement), nameof(CashMovement.Amount))]
        public const int AmountPrecision = 12;

        /// <summary>Escala del importe.</summary>
        [FieldConstraint("sales", "CashMovements", "amount", FieldConstraintKind.Scale, typeof(CashMovement), nameof(CashMovement.Amount))]
        public const int AmountScale = 2;
    }

    /// <summary>Restricciones de configuración global.</summary>
    public static class Settings
    {
        /// <summary>Longitud máxima de la clave.</summary>
        [FieldConstraint("system", "Settings", "Key", FieldConstraintKind.MaxLength, typeof(Setting), nameof(Setting.Key))]
        public const int KeyMaxLength = 100;

        /// <summary>Longitud máxima de la descripción.</summary>
        [FieldConstraint("system", "Settings", "Description", FieldConstraintKind.MaxLength, typeof(Setting), nameof(Setting.Description))]
        public const int DescriptionMaxLength = 300;
    }
}
