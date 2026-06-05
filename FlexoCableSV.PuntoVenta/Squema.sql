-- =============================================================================
-- FLEXOCABLE SV — Updated PostgreSQL Schema (English Structure, Spanish Values)
-- Version: 1.1.0 | Date: May 2026
-- Database: flexocable
-- ORM: Entity Framework Core + Npgsql
-- =============================================================================

-- Required extensions
CREATE EXTENSION IF NOT EXISTS "pgcrypto";    -- For bcrypt PIN hashing
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";   -- For DTE UUIDs

-- =============================================================================
-- SCHEMAS
-- =============================================================================
CREATE SCHEMA IF NOT EXISTS sales;
CREATE SCHEMA IF NOT EXISTS dte;
CREATE SCHEMA IF NOT EXISTS hr;
CREATE SCHEMA IF NOT EXISTS system;

-- =============================================================================
-- PUBLIC SCHEMA — Catalog & Inventory
-- =============================================================================

-- Measurement types
CREATE TABLE public."MeasurementTypes" (
    "Id"            SERIAL      PRIMARY KEY,
    "Code"          VARCHAR(10) NOT NULL UNIQUE,  -- METRO, PIEZA, KIT, PESO
    "Name"          VARCHAR(50) NOT NULL,
    "UnitLabel"    VARCHAR(20) NOT NULL,          -- "metros", "piezas", "kits", "kg"
    "Decimals"      SMALLINT    NOT NULL DEFAULT 0  -- 2=METRO, 3=PESO, 0=PIEZA/KIT
);

-- Product families
CREATE TABLE public."Families" (
    "Id"          SERIAL       PRIMARY KEY,
    "Code"        VARCHAR(5)   NOT NULL UNIQUE,  -- "01", "02", "FLV"
    "Name"        VARCHAR(100) NOT NULL,
    "Description" TEXT,
    "IsActive"   BOOLEAN      NOT NULL DEFAULT TRUE,
    "CreatedAt"  TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    "UpdatedAt"  TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

-- Subfamilies
CREATE TABLE public."Subfamilies" (
    "Id"          SERIAL       PRIMARY KEY,
    "FamilyId"   INTEGER      NOT NULL REFERENCES public."Families"("Id"),
    "Code"        VARCHAR(10)  NOT NULL,
    "Name"        VARCHAR(100) NOT NULL,
    "IsActive"   BOOLEAN      NOT NULL DEFAULT TRUE,
    "CreatedAt"  TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    "UpdatedAt"  TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    UNIQUE ("FamilyId", "Code")
);

-- Suppliers
CREATE TABLE public."Suppliers" (
    "Id"          SERIAL       PRIMARY KEY,
    "Name"        VARCHAR(150) NOT NULL,
    "Contact"     VARCHAR(100),
    "Phone"       VARCHAR(20),
    "Email"       VARCHAR(100),
    "IsActive"   BOOLEAN      NOT NULL DEFAULT TRUE,
    "CreatedAt"  TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    "UpdatedAt"  TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

-- Product catalog
CREATE TABLE public."Products" (
    "Id"              SERIAL         PRIMARY KEY,
    "Code"            VARCHAR(30)    NOT NULL UNIQUE,
    "Description"     VARCHAR(200)   NOT NULL,
    "FamilyId"       INTEGER        NOT NULL REFERENCES public."Families"("Id"),
    "SubfamilyId"    INTEGER        REFERENCES public."Subfamilies"("Id"),
    "MeasurementTypeId" INTEGER    NOT NULL REFERENCES public."MeasurementTypes"("Id"),
    "SalePrice"      NUMERIC(12,2)  NOT NULL DEFAULT 0,
    "CostPrice"      NUMERIC(12,2)  NOT NULL DEFAULT 0,
    "CurrentStock"   NUMERIC(12,3)  NOT NULL DEFAULT 0,
    "MinStock"       NUMERIC(12,3)  NOT NULL DEFAULT 0,
    "SupplierId"     INTEGER        REFERENCES public."Suppliers"("Id"),
    "IsActive"       BOOLEAN        NOT NULL DEFAULT TRUE,
    "CreatedAt"      TIMESTAMPTZ    NOT NULL DEFAULT NOW(),
    "UpdatedAt"      TIMESTAMPTZ    NOT NULL DEFAULT NOW(),
    CONSTRAINT "StockNotNegative" CHECK ("CurrentStock" >= 0)
);

CREATE INDEX "IdxProductsCode"    ON public."Products"("Code");
CREATE INDEX "IdxProductsFamily"  ON public."Products"("FamilyId");
CREATE INDEX "IdxProductsActive"  ON public."Products"("IsActive");

-- Inventory movements (immutable)
CREATE TABLE public."InventoryMovements" (
    "Id"               BIGSERIAL      PRIMARY KEY,
    "ProductId"       INTEGER        NOT NULL REFERENCES public."Products"("Id"),
    "MovementType"    VARCHAR(20)    NOT NULL,
    -- ENTRADA_COMPRA, ENTRADA_DEVOLUCION, SALIDA_VENTA,
    -- AJUSTE_SUMA, AJUSTE_RESTA
    "Quantity"         NUMERIC(12,3)  NOT NULL,
    "StockBefore"     NUMERIC(12,3)  NOT NULL,
    "StockAfter"      NUMERIC(12,3)  NOT NULL,
    "Reason"           VARCHAR(100),
    -- For AJUSTE: DAÑO, PÉRDIDA, INVENTARIO_FISICO, CORRECCIÓN
    -- For ENTRADA: COMPRA, DEVOLUCIÓN
    "DocumentRef"     VARCHAR(50),   -- Invoice number, order ID, etc.
    "SupplierId"      INTEGER        REFERENCES public."Suppliers"("Id"),
    "EmployeeId"      INTEGER        REFERENCES hr."Employees"("Id"), -- Who authorized adjustment
    "CreatedAt"       TIMESTAMPTZ    NOT NULL DEFAULT NOW(),
    CONSTRAINT "MovementTypeValid" CHECK (
        "MovementType" IN (
            'ENTRADA_COMPRA','ENTRADA_DEVOLUCION',
            'SALIDA_VENTA',
            'AJUSTE_SUMA','AJUSTE_RESTA'
        )
    )
);

CREATE INDEX "IdxInvProduct"   ON public."InventoryMovements"("ProductId");
CREATE INDEX "IdxInvType"      ON public."InventoryMovements"("MovementType");
CREATE INDEX "IdxInvDate"      ON public."InventoryMovements"("CreatedAt");

-- Low stock alerts (generated by trigger)
CREATE TABLE public."StockAlerts" (
    "Id"            BIGSERIAL      PRIMARY KEY,
    "ProductId"    INTEGER        NOT NULL REFERENCES public."Products"("Id"),
    "CurrentStock" NUMERIC(12,3)  NOT NULL,
    "MinStock"     NUMERIC(12,3)  NOT NULL,
    "IsResolved"   BOOLEAN        NOT NULL DEFAULT FALSE,
    "ResolvedAt"   TIMESTAMPTZ,
    "CreatedAt"    TIMESTAMPTZ    NOT NULL DEFAULT NOW()
);

-- Trigger: alert when stock ≤ minimum (skip if unresolved alert already exists)
CREATE OR REPLACE FUNCTION public.fn_stock_alert()
RETURNS TRIGGER AS $$
BEGIN
    IF NEW."CurrentStock" <= NEW."MinStock"
       AND NEW."CurrentStock" != OLD."CurrentStock"
       AND NOT EXISTS (
           SELECT 1 FROM public."StockAlerts"
           WHERE "ProductId" = NEW."Id" AND "IsResolved" = FALSE
       ) THEN
        INSERT INTO public."StockAlerts" ("ProductId", "CurrentStock", "MinStock")
        VALUES (NEW."Id", NEW."CurrentStock", NEW."MinStock");
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE INDEX IF NOT EXISTS "IdxStockAlertsUnresolved"
    ON public."StockAlerts"("ProductId") WHERE "IsResolved" = FALSE;

CREATE TRIGGER "TrgStockAlert"
AFTER UPDATE OF "CurrentStock" ON public."Products"
FOR EACH ROW EXECUTE FUNCTION public.fn_stock_alert();

-- Trigger: update timestamp on product modification
CREATE OR REPLACE FUNCTION public.fn_update_timestamp()
RETURNS TRIGGER AS $$
BEGIN
    NEW."UpdatedAt" = NOW();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER "TrgProductTimestamp"
BEFORE UPDATE ON public."Products"
FOR EACH ROW EXECUTE FUNCTION public.fn_update_timestamp();

-- Apply timestamp trigger to all tables with "UpdatedAt"
CREATE TRIGGER "TrgFamilyTimestamp"
BEFORE UPDATE ON public."Families"
FOR EACH ROW EXECUTE FUNCTION public.fn_update_timestamp();

CREATE TRIGGER "TrgSubfamilyTimestamp"
BEFORE UPDATE ON public."Subfamilies"
FOR EACH ROW EXECUTE FUNCTION public.fn_update_timestamp();

CREATE TRIGGER "TrgSupplierTimestamp"
BEFORE UPDATE ON public."Suppliers"
FOR EACH ROW EXECUTE FUNCTION public.fn_update_timestamp();


-- =============================================================================
-- SALES SCHEMA — Orders & Daily Operations
-- =============================================================================

-- Applications (transaction types)
CREATE TABLE sales."Applications" (
    "Id"          SERIAL       PRIMARY KEY,
    "Code"        VARCHAR(10)  NOT NULL UNIQUE,  -- VT-01, VT-02, VT-03, RP-01
    "Name"        VARCHAR(100) NOT NULL,
    "IsActive"   BOOLEAN      NOT NULL DEFAULT TRUE,
    "CreatedAt"  TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    "UpdatedAt"  TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE TRIGGER "TrgApplicationTimestamp"
BEFORE UPDATE ON sales."Applications"
FOR EACH ROW EXECUTE FUNCTION public.fn_update_timestamp();

-- Confection orders (header) — Simplified statuses
CREATE TABLE sales."Orders" (
    "Id"              BIGSERIAL     PRIMARY KEY,
    "EmployeeId"     INTEGER       NOT NULL REFERENCES hr."Employees"("Id"),
    "ApplicationId"  INTEGER       NOT NULL REFERENCES sales."Applications"("Id"),
    "CustomerName"   VARCHAR(150),
    "OrderDate"      DATE          NOT NULL DEFAULT CURRENT_DATE,
    "OrderTime"      TIME          NOT NULL DEFAULT CURRENT_TIME,
    "Status"          VARCHAR(20)   NOT NULL DEFAULT 'PENDIENTE',
    -- PENDIENTE → COMPLETADA | CANCELADA
    "Subtotal"      NUMERIC(12,2) NOT NULL DEFAULT 0,
    "Iva"             NUMERIC(12,2) NOT NULL DEFAULT 0,   -- 13% calculated
    "Total"           NUMERIC(12,2) NOT NULL DEFAULT 0,
    "Notes"           TEXT,
    "CreatedAt"      TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    "UpdatedAt"      TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    CONSTRAINT "OrderStatusValid" CHECK (
        "Status" IN ('PENDIENTE','COMPLETADA','CANCELADA')
    )
);

CREATE INDEX "IdxOrdersDate"      ON sales."Orders"("OrderDate");
CREATE INDEX "IdxOrdersStatus"    ON sales."Orders"("Status");
CREATE INDEX "IdxOrdersEmployee"  ON sales."Orders"("EmployeeId");

CREATE TRIGGER "TrgOrderTimestamp"
BEFORE UPDATE ON sales."Orders"
FOR EACH ROW EXECUTE FUNCTION public.fn_update_timestamp();

-- Order details (items)
CREATE TABLE sales."OrderDetails" (
    "Id"              BIGSERIAL     PRIMARY KEY,
    "OrderId"        BIGINT        NOT NULL REFERENCES sales."Orders"("Id") ON DELETE CASCADE,
    "ProductId"      INTEGER       NOT NULL REFERENCES public."Products"("Id"),
    "Quantity"        NUMERIC(12,3) NOT NULL,
    "UnitPrice"      NUMERIC(12,2) NOT NULL,
    "Subtotal"      NUMERIC(12,2) NOT NULL,
    "CreatedAt"      TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    CONSTRAINT "QuantityPositive" CHECK ("Quantity" > 0)
);

CREATE INDEX "IdxDetailOrder" ON sales."OrderDetails"("OrderId");


-- =============================================================================
-- DTE SCHEMA — Electronic Invoicing
-- =============================================================================

-- DTE issuer configuration
CREATE TABLE dte."DteConfig" (
    "Id"                    SERIAL       PRIMARY KEY,
    "Environment"         CHAR(2)      NOT NULL,  -- '00' test, '01' production
    "ApiUrl"               VARCHAR(200) NOT NULL,
    "IssuerNit"            VARCHAR(20)  NOT NULL,
    "IssuerName"           VARCHAR(200) NOT NULL,
    "IssuerNrc"            VARCHAR(9),
    "ActivityCode"         VARCHAR(10),
    "ActivityDescription"  VARCHAR(200),
    "Address"               TEXT,
    "Phone"                 VARCHAR(20),
    "Email"                 VARCHAR(100),
    "CertificatePath"      VARCHAR(500),
    "CertificateKey"       TEXT,         -- Encrypted at rest
    "IsActive"             BOOLEAN      NOT NULL DEFAULT TRUE,
    "CreatedAt"            TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    "UpdatedAt"            TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    CONSTRAINT "EnvironmentValid" CHECK ("Environment" IN ('00','01'))
);

CREATE TRIGGER "TrgDteConfigTimestamp"
BEFORE UPDATE ON dte."DteConfig"
FOR EACH ROW EXECUTE FUNCTION public.fn_update_timestamp();

-- Issued DTEs
CREATE TABLE dte."DteIssued" (
    "Id"                BIGSERIAL    PRIMARY KEY,
    "OrderId"          BIGINT       NOT NULL REFERENCES sales."Orders"("Id"),
    "DteType"          CHAR(2)      NOT NULL,  -- '01','03','05','06'
    "ControlNumber"    VARCHAR(50)  NOT NULL UNIQUE,
    "GenerationCode"   UUID         NOT NULL UNIQUE DEFAULT "UuidGenerateV4"(),
    "ReceptionStamp"   VARCHAR(100),
    "MhStatus"         VARCHAR(20)  NOT NULL DEFAULT 'PENDIENTE',
    -- PENDIENTE → PROCESADO | RECHAZADO | CONTINGENCIA
    "JsonSent"         JSONB        NOT NULL,
    "JsonResponse"     JSONB,
    "PaymentMethod"    VARCHAR(20)  NOT NULL DEFAULT 'EFECTIVO',
    "ReceiverNit"      VARCHAR(20),
    "ReceiverName"     VARCHAR(200),
    "Environment"     CHAR(2)      NOT NULL DEFAULT '01',
    "SentAt"           TIMESTAMPTZ,
    "CreatedAt"        TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    "Reprints"          SMALLINT     NOT NULL DEFAULT 0,
    CONSTRAINT "DteTypeValid"  CHECK ("DteType"  IN ('01','03','05','06')),
    CONSTRAINT "MhStatusValid" CHECK ("MhStatus" IN ('PENDIENTE','PROCESADO','RECHAZADO','CONTINGENCIA'))
);

CREATE INDEX "IdxDteOrder"   ON dte."DteIssued"("OrderId");
CREATE INDEX "IdxDteStatus"  ON dte."DteIssued"("MhStatus");
CREATE INDEX "IdxDteDate"    ON dte."DteIssued"("CreatedAt");

-- Contingency queue for automatic resend (every 15 min)
CREATE TABLE dte."DteContingency" (
    "Id"              BIGSERIAL   PRIMARY KEY,
    "DteId"          BIGINT      NOT NULL REFERENCES dte."DteIssued"("Id"),
    "Attempts"        SMALLINT    NOT NULL DEFAULT 0,
    "LastError"      TEXT,
    "NextAttemptAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "IsResolved"     BOOLEAN     NOT NULL DEFAULT FALSE,
    "ResolvedAt"     TIMESTAMPTZ,
    "CreatedAt"      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);


-- =============================================================================
-- HR SCHEMA — Employees & Payroll
-- =============================================================================

CREATE TABLE hr."Departments" (
    "Id"          SERIAL       PRIMARY KEY,
    "Name"        VARCHAR(100) NOT NULL UNIQUE,
    "IsActive"   BOOLEAN      NOT NULL DEFAULT TRUE,
    "CreatedAt"  TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    "UpdatedAt"  TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE TRIGGER "TrgDepartmentTimestamp"
BEFORE UPDATE ON hr."Departments"
FOR EACH ROW EXECUTE FUNCTION public.fn_update_timestamp();

CREATE TABLE hr."Positions" (
    "Id"             SERIAL       PRIMARY KEY,
    "DepartmentId"  INTEGER      NOT NULL REFERENCES hr."Departments"("Id"),
    "Name"           VARCHAR(100) NOT NULL,
    "IsActive"      BOOLEAN      NOT NULL DEFAULT TRUE,
    "CreatedAt"     TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    "UpdatedAt"     TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE TRIGGER "TrgPositionTimestamp"
BEFORE UPDATE ON hr."Positions"
FOR EACH ROW EXECUTE FUNCTION public.fn_update_timestamp();

-- Employees (unified with technicians)
CREATE TABLE hr."Employees" (
    "Id"              SERIAL        PRIMARY KEY,
    -- Identity
    "FirstName"      VARCHAR(100)  NOT NULL,
    "LastName"       VARCHAR(100)  NOT NULL,
    "Dui"             VARCHAR(15)   UNIQUE,
    "Nit"             VARCHAR(20)   UNIQUE,
    "IsssNumber"     VARCHAR(20),
    "Nup"             VARCHAR(20),
    -- Job
    "PositionId"     INTEGER       REFERENCES hr."Positions"("Id"),
    "HireDate"       DATE          NOT NULL,
    "TerminationDate" DATE,
    "BaseSalary"     NUMERIC(10,2) NOT NULL,
    "ContractType"   VARCHAR(20)   NOT NULL DEFAULT 'PLANILLA',
    "Afp"             VARCHAR(50),  -- "CRECER", "CONFIA"
    -- Contact
    "Phone"           VARCHAR(20),
    "AltPhone"       VARCHAR(20),
    "Email"           VARCHAR(100),
    "Address"         TEXT,
    "Municipality"    VARCHAR(100),
    -- Personal
    "MaritalStatus"  VARCHAR(20),
    "AcademicLevel"  VARCHAR(50),
    -- Emergency contact
    "EmergencyName"  VARCHAR(100),
    "EmergencyPhone" VARCHAR(20),
    "EmergencyRelationship" VARCHAR(50),
    -- POS access
    "PinHash"        TEXT,         -- bcrypt(PIN 4 digits, cost=12). NULL = cannot operate POS
    "CanSell"        BOOLEAN       NOT NULL DEFAULT FALSE,  -- Can process sales/confection
    "CanCashier"     BOOLEAN       NOT NULL DEFAULT FALSE,  -- Can operate cashier
    -- Status
    "IsActive"       BOOLEAN       NOT NULL DEFAULT TRUE,
    "CreatedAt"      TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    "UpdatedAt"      TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    CONSTRAINT "ContractValid" CHECK ("ContractType" IN ('PLANILLA','HONORARIOS'))
);

CREATE TRIGGER "TrgEmployeeTimestamp"
BEFORE UPDATE ON hr."Employees"
FOR EACH ROW EXECUTE FUNCTION public.fn_update_timestamp();

-- Monthly payroll (header)
CREATE TABLE hr."Payroll" (
    "Id"                    SERIAL        PRIMARY KEY,
    "PeriodMonth"          SMALLINT      NOT NULL,
    "PeriodYear"           SMALLINT      NOT NULL,
    "Status"                VARCHAR(20)   NOT NULL DEFAULT 'BORRADOR',
    -- BORRADOR → APROBADA → PAGADA (immutable once PAGADA)
    "TotalSalaries"        NUMERIC(12,2) NOT NULL DEFAULT 0,
    "TotalIsssEmployee"   NUMERIC(12,2) NOT NULL DEFAULT 0,
    "TotalAfpEmployee"    NUMERIC(12,2) NOT NULL DEFAULT 0,
    "TotalIsr"             NUMERIC(12,2) NOT NULL DEFAULT 0,
    "TotalDeductions"      NUMERIC(12,2) NOT NULL DEFAULT 0,
    "TotalNet"             NUMERIC(12,2) NOT NULL DEFAULT 0,
    "TotalIsssEmployer"   NUMERIC(12,2) NOT NULL DEFAULT 0,
    "TotalAfpEmployer"    NUMERIC(12,2) NOT NULL DEFAULT 0,
    "CreatedAt"            TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    "UpdatedAt"            TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    "ApprovedAt"           TIMESTAMPTZ,
    "PaidAt"               TIMESTAMPTZ,
    UNIQUE ("PeriodMonth", "PeriodYear"),
    CONSTRAINT "PayrollStatusValid" CHECK ("Status" IN ('BORRADOR','APROBADA','PAGADA')),
    CONSTRAINT "MonthValid" CHECK ("PeriodMonth" BETWEEN 1 AND 12)
);

CREATE TRIGGER "TrgPayrollTimestamp"
BEFORE UPDATE ON hr."Payroll"
FOR EACH ROW EXECUTE FUNCTION public.fn_update_timestamp();

-- Payroll detail per employee
CREATE TABLE hr."PayrollDetails" (
    "Id"                   BIGSERIAL     PRIMARY KEY,
    "PayrollId"           INTEGER       NOT NULL REFERENCES hr."Payroll"("Id"),
    "EmployeeId"          INTEGER       NOT NULL REFERENCES hr."Employees"("Id"),
    -- Income
    "BaseSalary"          NUMERIC(10,2) NOT NULL,
    "OvertimeHours"       NUMERIC(5,2)  NOT NULL DEFAULT 0,
    "OvertimeAmount"      NUMERIC(10,2) NOT NULL DEFAULT 0,  -- hours × ("HourlyRate" × 2)
    "Bonuses"            NUMERIC(10,2) NOT NULL DEFAULT 0,
    "TotalIncome"         NUMERIC(10,2) NOT NULL,
    -- Employee deductions
    "IsssEmployee"        NUMERIC(10,2) NOT NULL,  -- 3% of "TotalIncome"
    "AfpEmployee"         NUMERIC(10,2) NOT NULL,  -- 7.25% of "TotalIncome"
    "Isr"                NUMERIC(10,2) NOT NULL DEFAULT 0,
    "OtherDeductions"     NUMERIC(10,2) NOT NULL DEFAULT 0,
    "TotalDeductions"     NUMERIC(10,2) NOT NULL,
    -- Net pay
    "NetSalary"           NUMERIC(10,2) NOT NULL,
    -- Employer cost (not deducted from employee)
    "IsssEmployer"        NUMERIC(10,2) NOT NULL,  -- 7.5%
    "AfpEmployer"         NUMERIC(10,2) NOT NULL,  -- 8.75%
    "CreatedAt"           TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    UNIQUE ("PayrollId", "EmployeeId")
);


-- =============================================================================
-- SYSTEM SCHEMA — Settings, Printers & Audit
-- =============================================================================

-- General settings ("Key"-"Value")
CREATE TABLE system."Settings" (
    "Key"             VARCHAR(100) PRIMARY KEY,
    "Value"           TEXT         NOT NULL,
    "Description"     VARCHAR(200),
    "UpdatedAt"      TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

-- Printers configuration
CREATE TABLE system."Printers" (
    "Id"              SERIAL       PRIMARY KEY,
    "Name"            VARCHAR(200) NOT NULL,  -- Windows device "Name"
    "ConnectionType" VARCHAR(10)  NOT NULL DEFAULT 'USB',  -- USB, ETHERNET
    "IpAddress"      VARCHAR(15),   -- Only for ETHERNET
    "NetworkPort"    INTEGER,       -- Default 9100
    "PaperWidth"     SMALLINT     NOT NULL DEFAULT 80,  -- 80 or 58 mm
    "IsDefault"      BOOLEAN      NOT NULL DEFAULT FALSE,
    "IsActive"       BOOLEAN      NOT NULL DEFAULT TRUE,
    "CreatedAt"      TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    "UpdatedAt"      TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

-- Ensure only one default printer at a time
CREATE UNIQUE INDEX "IdxPrinterDefault"
    ON system."Printers" ("IsDefault")
    WHERE "IsDefault" = TRUE;

CREATE TRIGGER "TrgPrinterTimestamp"
BEFORE UPDATE ON system."Printers"
FOR EACH ROW EXECUTE FUNCTION public.fn_update_timestamp();

-- WebApp users (for future web application)
CREATE TABLE system."WebUsers" (
    "Id"             SERIAL       PRIMARY KEY,
    "Username"       VARCHAR(50)  NOT NULL UNIQUE,
    "Email"          VARCHAR(100) NOT NULL UNIQUE,
    "PasswordHash"  TEXT         NOT NULL,  -- bcrypt(12 rounds)
    "Role"           VARCHAR(20)  NOT NULL DEFAULT 'ADMIN',
    -- ADMIN, ACCOUNTANT, OWNER
    "EmployeeId"    INTEGER      REFERENCES hr."Employees"("Id"),  -- Link to HR employee
    "IsActive"      BOOLEAN      NOT NULL DEFAULT TRUE,
    "LastLoginAt"  TIMESTAMPTZ,
    "CreatedAt"     TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    "UpdatedAt"     TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    CONSTRAINT "RoleValid" CHECK ("Role" IN ('ADMIN','ACCOUNTANT','OWNER'))
);

CREATE TRIGGER "TrgWebUserTimestamp"
BEFORE UPDATE ON system."WebUsers"
FOR EACH ROW EXECUTE FUNCTION public.fn_update_timestamp();

-- Action audit log
CREATE TABLE system."AuditLog" (
    "Id"                BIGSERIAL    PRIMARY KEY,
    "TableName"        VARCHAR(100) NOT NULL,
    "RecordId"         VARCHAR(50),
    "Action"          VARCHAR(10)  NOT NULL,  -- INSERT, UPDATE, DELETE
    "OldData"          JSONB,
    "NewData"          JSONB,
    "Description"       TEXT,
    "IpAddress"        VARCHAR(45),
    "CreatedAt"        TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE INDEX "IdxAuditTable" ON system."AuditLog"("TableName");
CREATE INDEX "IdxAuditDate"   ON system."AuditLog"("CreatedAt");


-- =============================================================================
-- INITIAL DATA (Seed)
-- =============================================================================

-- Measurement types
INSERT INTO public."MeasurementTypes" ("Code", "Name", "UnitLabel", "Decimals") VALUES
    ('METRO', 'Metros lineales',     'metros', 2),
    ('PIEZA', 'Piezas / unidades',   'piezas', 0),
    ('KIT',   'Kits pre-armados',    'kits',   0),
    ('PESO',  'Kilogramos a granel', 'kg',     3);

-- 17 product families
INSERT INTO public."Families" ("Code", "Name", "Description") VALUES
    ('01',  'Cables de Acero',     'Cables galvanizados e inoxidables 7x7, 7x19'),
    ('02',  'Boquillas',           'Boquillas acelerador, cambios, embrague, freno'),
    ('03',  'Piezas en Caucho',    'Guardapolvos, bujes, empaques, soportes'),
    ('04',  'Flejes y Pines',      'Flejes de retención, pines de control'),
    ('05',  'Horquillas',          'Horquillas metálicas para cables'),
    ('06',  'Pasacables',          'Pasacables plásticos, deslizadores'),
    ('07',  'Platinas',            'Platinas graduación, soportes, conectores'),
    ('08',  'Resortes',            'Resortes compresión para frenos y cambios'),
    ('09',  'Terminales',          'Terminales martillo, ojo, tornillo, colombina'),
    ('10',  'Tuercas',             'Tuercas ajuste, velocímetro, plásticas'),
    ('11',  'Tubos y Anillos',     'Tubos metálicos, bujes, anillos retención'),
    ('12',  'Manijas',             'Manijas acelerador, freno de mano, apertura'),
    ('13',  'Troqueles y Kits',    'Troqueles grafadores, kits pre-armados'),
    ('FLV', 'Flexoindustrial VLD', 'Very Light Duty - trabajo muy liviano'),
    ('FLL', 'Flexoindustrial LD',  'Light Duty - trabajo liviano'),
    ('FLM', 'Flexoindustrial MD',  'Medium Duty - trabajo medio'),
    ('FLH', 'Flexoindustrial HD',  'Heavy Duty - trabajo pesado');

-- Subfamilies of Boquillas (family 02)
INSERT INTO public."Subfamilies" ("FamilyId", "Code", "Name")
SELECT "Id", 'AC', 'Acelerador' FROM public."Families" WHERE "Code" = '02' UNION ALL
SELECT "Id", 'CC', 'Cambios'    FROM public."Families" WHERE "Code" = '02' UNION ALL
SELECT "Id", 'EM', 'Embrague'   FROM public."Families" WHERE "Code" = '02' UNION ALL
SELECT "Id", 'FR', 'Freno'      FROM public."Families" WHERE "Code" = '02';

-- Subfamilies of Steel Cables (family 01)
INSERT INTO public."Subfamilies" ("FamilyId", "Code", "Name")
SELECT "Id", 'Cga', 'Cable galvanizado acero' FROM public."Families" WHERE "Code" = '01' UNION ALL
SELECT "Id", 'Cin', 'Cable inoxidable'        FROM public."Families" WHERE "Code" = '01' UNION ALL
SELECT "Id", 'Cre', 'Cable recubierto PVC'    FROM public."Families" WHERE "Code" = '01';

-- Sales applications
INSERT INTO sales."Applications" ("Code", "Name") VALUES
    ('VT-01', 'Venta Nueva'),
    ('VT-02', 'Reparación'),
    ('VT-03', 'Garantía'),
    ('RP-01', 'Reposición');

-- Departments and positions
INSERT INTO hr."Departments" ("Name") VALUES
    ('Producción'), ('Ventas'), ('Bodega'), ('Administración');

INSERT INTO hr."Positions" ("DepartmentId", "Name")
SELECT "Id", 'Técnico de Confección' FROM hr."Departments" WHERE "Name" = 'Producción' UNION ALL
SELECT "Id", 'Vendedor'              FROM hr."Departments" WHERE "Name" = 'Ventas'     UNION ALL
SELECT "Id", 'Bodeguero'             FROM hr."Departments" WHERE "Name" = 'Bodega'     UNION ALL
SELECT "Id", 'Administrador'         FROM hr."Departments" WHERE "Name" = 'Administración' UNION ALL
SELECT "Id", 'Gerente'               FROM hr."Departments" WHERE "Name" = 'Administración';

-- System settings
INSERT INTO system."Settings" ("Key", "Value", "Description") VALUES
    ('"IvaPercentage"',           '13',                          'IVA vigente en El Salvador (%)'),
    ('currency',                 'USD',                         'Moneda operativa'),
    ('"SessionTimeoutMinutes"',  '30',                          'Minutos de inactividad antes de cerrar sesión'),
    ('"BusinessName"',            'FlexoCable El Salvador',      'Nombre para impresión en tickets'),
    ('"BusinessNit"',             '',                            'NIT del emisor para DTE — configurar antes de producción'),
    ('"BusinessNrc"',             '',                            'NRC del emisor para DTE'),
    ('"BusinessAddress"',         'San Salvador, El Salvador',   'Dirección para tickets'),
    ('"BusinessPhone"',           '',                            'Teléfono para tickets'),
    ('"TicketFooterMessage"',    'Gracias por su compra.',      'Mensaje al pie del ticket'),
    ('"ButtonMinSizePx"',       '90',                          'Tamaño mínimo de botones táctiles'),
    ('"FontBaseSizePt"',        '16',                          'Tamaño base de fuente en puntos');

-- Initial DTE configuration (test environment)
INSERT INTO dte."DteConfig" ("Environment", "ApiUrl", "IssuerNit", "IssuerName", "IsActive")
VALUES (
    '00',
    'https://apifacturatest.mh.gob.sv',
    '0000-000000-000-0',
    'FlexoCable El Salvador, S.A. de C.V.',
    TRUE
);

-- Initial web user (admin)
-- IMPORTANT: change password from WebApp before production
INSERT INTO system."WebUsers" ("Username", "Email", "PasswordHash", "Role")
VALUES (
    'admin',
    'admin@flexocable.com.sv',
    crypt('FlexoAdmin2026!', gen_salt('bf', 12)),
    'ADMIN'
);


-- =============================================================================
-- USEFUL VIEWS
-- =============================================================================

-- Products with stock "Status" (for inventory table with colors)
CREATE OR REPLACE VIEW public."VProductsStock" AS
SELECT
    p."Id",
    p."Code",
    p."Description",
    f."Name"                                      AS family,
    sf."Name"                                     AS subfamily,
    mt."UnitLabel"                               AS unit,
    mt."Decimals",
    p."CurrentStock",
    p."MinStock",
    p."SalePrice",
    CASE
        WHEN p."CurrentStock" = 0                THEN 'AGOTADO'
        WHEN p."CurrentStock" <= p."MinStock"       THEN 'BAJO_MINIMO'
        ELSE                                            'OK'
    END                                         AS "StockStatus"
FROM public."Products" p
JOIN  public."Families"      f  ON p."FamilyId"        = f."Id"
LEFT JOIN public."Subfamilies" sf ON p."SubfamilyId"    = sf."Id"
JOIN  public."MeasurementTypes" mt ON p."MeasurementTypeId" = mt."Id"
WHERE p."IsActive" = TRUE;

-- Today's sales with DTE "Status" (for daily table)
CREATE OR REPLACE VIEW sales."VSalesToday" AS
SELECT
    o."Id",
    o."OrderDate",
    o."OrderTime",
    o."CustomerName",
    o."Total",
    o."Status",
    e."FirstName" || ' ' || COALESCE(e."LastName",'') AS employee,
    a."Name"                                          AS application,
    d."ReceptionStamp",
    d."MhStatus"                                     AS "DteStatus",
    d."Reprints"
FROM sales."Orders" o
JOIN hr."Employees"      e ON o."EmployeeId"    = e."Id"
JOIN sales."Applications" a ON o."ApplicationId" = a."Id"
LEFT JOIN dte."DteIssued" d ON d."OrderId"      = o."Id"
WHERE o."OrderDate" = CURRENT_DATE
ORDER BY o."OrderTime" DESC;

-- Active stock alerts (for dashboard)
CREATE OR REPLACE VIEW public."VActiveAlerts" AS
SELECT
    al."Id",
    al."CreatedAt",
    p."Code",
    p."Description",
    al."CurrentStock",
    al."MinStock",
    CASE WHEN al."CurrentStock" = 0 THEN 'AGOTADO' ELSE 'BAJO_MINIMO' END AS "AlertType"
FROM public."StockAlerts" al
JOIN public."Products" p ON al."ProductId" = p."Id"
WHERE al."IsResolved" = FALSE
ORDER BY al."CreatedAt" DESC;

-- Today's KPIs for WebApp dashboard
CREATE OR REPLACE VIEW sales."VKpisToday" AS
SELECT
    COUNT(*)                                     AS "TotalOrders",
    COALESCE(SUM(o."Total"), 0)                    AS "TotalAmount",
    COALESCE(AVG(o."Total"), 0)                  AS "AvgTicket",
    COUNT(*) FILTER (WHERE d."MhStatus" = 'PROCESADO') AS "DtesSent",
    COUNT(*) FILTER (WHERE d."MhStatus" = 'CONTINGENCIA') AS "InContingency"
FROM sales."Orders" o
LEFT JOIN dte."DteIssued" d ON d."OrderId" = o."Id"
WHERE o."OrderDate" = CURRENT_DATE
  AND o."Status" NOT IN ('CANCELADA');


-- =============================================================================
-- PERMISSIONS
-- =============================================================================

GRANT ALL PRIVILEGES ON ALL TABLES    IN SCHEMA public  TO flexo_user;
GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA public  TO flexo_user;
GRANT ALL PRIVILEGES ON ALL TABLES    IN SCHEMA sales   TO flexo_user;
GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA sales   TO flexo_user;
GRANT ALL PRIVILEGES ON ALL TABLES    IN SCHEMA dte     TO flexo_user;
GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA dte     TO flexo_user;
GRANT ALL PRIVILEGES ON ALL TABLES    IN SCHEMA hr      TO flexo_user;
GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA hr      TO flexo_user;
GRANT ALL PRIVILEGES ON ALL TABLES    IN SCHEMA system  TO flexo_user;
GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA system  TO flexo_user;


-- =============================================================================
-- END OF SCHEMA — FlexoCable SV v1.1.0
-- =============================================================================
-- Production Checklist:
--   [ ] Update "BusinessNit" and "BusinessNrc" in system."Settings"
--   [ ] Update dte."DteConfig" with real issuer data
--   [ ] Change DTE environment from '00' to '01'
--   [ ] Upload .p12 certificate to server
--   [ ] Change admin web user password
--   [ ] Load complete catalog of 500+ products (additional seed)
--   [ ] Create employees and assign PINs from the app
--   [ ] Configure automatic backup with "PgDump"
-- =============================================================================
