-- Rol de mínimo privilegio para la aplicación WPF POS.
-- Este archivo es aditivo/idempotente y NO se ejecuta automáticamente.
-- El operador debe establecer la contraseña fuera de este repositorio, por ejemplo:
--   psql -d ferreteria -U postgres
--   \password pos_app

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'pos_app') THEN
        CREATE ROLE pos_app LOGIN;
    END IF;
END
$$;

ALTER ROLE pos_app LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS;

GRANT CONNECT ON DATABASE ferreteria TO pos_app;
GRANT USAGE ON SCHEMA public, purchasing, sales, dte, hr, system TO pos_app;

-- Revocaciones explícitas: el POS no recibe DDL ni permisos sobre WebUsers o fiscal.
REVOKE ALL PRIVILEGES ON ALL TABLES IN SCHEMA public, purchasing, sales, dte, hr, system FROM pos_app;
REVOKE ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA public, purchasing, sales, dte, hr, system FROM pos_app;
REVOKE CREATE ON SCHEMA public, purchasing, sales, dte, hr, system FROM pos_app;
-- La revocación anterior cubre cualquier tabla existente en system, incluida
-- system."WebUsers" si está instalada; el POS no recibe permisos sobre ella.

-- Catálogo, clientes e inventario.
GRANT SELECT, INSERT, UPDATE ON TABLE
    public."MeasurementTypes",
    public."Families",
    public."Subfamilies",
    purchasing."Suppliers",
    public."Products",
    public."InventoryMovements",
    public."StockAlerts",
    public."SaleUnits",
    public."ProductSaleUnits",
    public."VolumeDiscounts",
    public."Customers"
TO pos_app;

-- Ventas, caja y devoluciones.
GRANT SELECT, INSERT, UPDATE ON TABLE
    sales."Orders",
    sales."OrderDetails",
    sales."CashSessions",
    sales."Payments",
    sales."Returns",
    sales."ReturnDetails",
    sales."CashMovements"
TO pos_app;

-- DTE y contingencia.
GRANT SELECT, INSERT, UPDATE ON TABLE
    dte."DteConfig",
    dte."DteIssued",
    dte."DteContingency"
TO pos_app;

-- Identidad POS y configuración de impresión/auditoría.
GRANT SELECT, INSERT, UPDATE ON TABLE
    hr."Departments",
    hr."Positions",
    hr."Employees",
    system."Settings",
    system."Printers",
    system."AuditLog"
TO pos_app;

-- No hay DELETE en los flujos POS actuales. Si aparece un borrado legítimo,
-- debe agregarse aquí de forma explícita y documentarse por servicio.
REVOKE DELETE ON ALL TABLES IN SCHEMA public, purchasing, sales, dte, hr, system FROM pos_app;
