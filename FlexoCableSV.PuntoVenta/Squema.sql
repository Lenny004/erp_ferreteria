-- =============================================================================
-- FERRETERÍA ERP — Esquema PostgreSQL (estructura en inglés, valores en español)
-- Versión: 3.0.0-ferreteria | Motor: PostgreSQL 14+ | ORM: EF Core + Npgsql / Prisma
-- Convención de columnas: minúsculas para id/code/name/description/barcode/phone/
--   email/address/notes/key/value; PascalCase para el resto (alineado con Prisma).
-- =============================================================================

-- Extensiones necesarias
CREATE EXTENSION IF NOT EXISTS "pgcrypto";   -- gen_random_uuid() y crypt()/bcrypt

-- =============================================================================
-- ESQUEMAS
-- =============================================================================
CREATE SCHEMA IF NOT EXISTS public;
CREATE SCHEMA IF NOT EXISTS purchasing;
CREATE SCHEMA IF NOT EXISTS sales;
CREATE SCHEMA IF NOT EXISTS dte;
CREATE SCHEMA IF NOT EXISTS hr;
CREATE SCHEMA IF NOT EXISTS system;

-- =============================================================================
-- FUNCIONES / TRIGGERS COMPARTIDOS
-- =============================================================================

-- Actualiza "UpdatedAt" en cada modificación
CREATE OR REPLACE FUNCTION public.fn_update_timestamp()
RETURNS TRIGGER AS $$
BEGIN
    NEW."UpdatedAt" = NOW();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- =============================================================================
-- ESQUEMA public — Catálogo, unidades de venta e inventario
-- =============================================================================

-- Tipos de medida base (cómo se cuenta el stock de un producto)
CREATE TABLE public."MeasurementTypes" (
    "id"        UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "code"      VARCHAR(10)  NOT NULL UNIQUE,   -- UNIDAD, METRO, LIBRA, KILO, GALON, SACO...
    "name"      VARCHAR(50)  NOT NULL,
    "UnitLabel" VARCHAR(20)  NOT NULL,          -- "unidades", "metros", "libras", "kg"...
    "decimals"  SMALLINT     NOT NULL DEFAULT 0 -- 0 enteros, 2 decimales, 3 granel
);

-- Categorías de producto (familias)
CREATE TABLE public."Families" (
    "id"          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "code"        VARCHAR(10)  NOT NULL UNIQUE,
    "name"        VARCHAR(100) NOT NULL,
    "description" VARCHAR(300),
    "IsActive"    BOOLEAN      NOT NULL DEFAULT TRUE,
    "CreatedAt"   TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    "UpdatedAt"   TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE TRIGGER "TrgFamilyTimestamp"
BEFORE UPDATE ON public."Families"
FOR EACH ROW EXECUTE FUNCTION public.fn_update_timestamp();

-- Subcategorías (subfamilias)
CREATE TABLE public."Subfamilies" (
    "id"          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "FamilyId"    UUID         NOT NULL REFERENCES public."Families"("id"),
    "code"        VARCHAR(10)  NOT NULL,
    "name"        VARCHAR(100) NOT NULL,
    "description" VARCHAR(300),
    "IsActive"    BOOLEAN      NOT NULL DEFAULT TRUE,
    "CreatedAt"   TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    "UpdatedAt"   TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    UNIQUE ("FamilyId", "code")
);

CREATE INDEX "IdxSubfamiliesFamily" ON public."Subfamilies"("FamilyId");

CREATE TRIGGER "TrgSubfamilyTimestamp"
BEFORE UPDATE ON public."Subfamilies"
FOR EACH ROW EXECUTE FUNCTION public.fn_update_timestamp();

-- Unidades de venta / presentaciones (unidad, docena, caja, bulto, saco...)
CREATE TABLE public."SaleUnits" (
    "id"           UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "code"         VARCHAR(20)  NOT NULL UNIQUE,  -- UNIDAD, DOCENA, CAJA, BULTO...
    "name"         VARCHAR(50)  NOT NULL,
    "Abbreviation" VARCHAR(10)  NOT NULL,
    "IsActive"     BOOLEAN      NOT NULL DEFAULT TRUE,
    "CreatedAt"    TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    "UpdatedAt"    TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE TRIGGER "TrgSaleUnitTimestamp"
BEFORE UPDATE ON public."SaleUnits"
FOR EACH ROW EXECUTE FUNCTION public.fn_update_timestamp();

-- Clientes (datos fiscales básicos — sin cuentas por cobrar)
CREATE TABLE public."Customers" (
    "id"           UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "CustomerType" VARCHAR(5)   NOT NULL DEFAULT 'CF',  -- CF consumidor final | CCF crédito fiscal
    "name"         VARCHAR(200) NOT NULL,
    "Dui"          VARCHAR(15)  UNIQUE,
    "Nit"          VARCHAR(20)  UNIQUE,
    "Nrc"          VARCHAR(20)  UNIQUE,
    "phone"        VARCHAR(20),
    "email"        VARCHAR(100),
    "address"      VARCHAR(300),
    "municipality" VARCHAR(100),
    "department"   VARCHAR(50),
    "IsActive"     BOOLEAN      NOT NULL DEFAULT TRUE,
    "CreatedAt"    TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    "UpdatedAt"    TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    CONSTRAINT "CustomerTypeValid" CHECK ("CustomerType" IN ('CF','CCF'))
);

CREATE INDEX "IdxCustomersNit" ON public."Customers"("Nit");

CREATE TRIGGER "TrgCustomerTimestamp"
BEFORE UPDATE ON public."Customers"
FOR EACH ROW EXECUTE FUNCTION public.fn_update_timestamp();

-- Proveedores (referenciado por Products; catálogo completo en purchasing.Suppliers)
CREATE TABLE purchasing."Suppliers" (
    "id"           UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "name"         VARCHAR(200) NOT NULL,
    "TradeName"    VARCHAR(200),
    "Nit"          VARCHAR(20)  UNIQUE,
    "Nrc"          VARCHAR(20)  UNIQUE,
    "ContactName"  VARCHAR(150),
    "phone"        VARCHAR(20),
    "email"        VARCHAR(100),
    "address"      VARCHAR(300),
    "municipality" VARCHAR(100),
    "department"   VARCHAR(50),
    "country"      VARCHAR(5)   NOT NULL DEFAULT 'SV',
    "CreditDays"   INTEGER      NOT NULL DEFAULT 0,
    "IsActive"     BOOLEAN      NOT NULL DEFAULT TRUE,
    "notes"        TEXT,
    "CreatedAt"    TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    "UpdatedAt"    TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE INDEX "IdxSuppliersNit" ON purchasing."Suppliers"("Nit");

CREATE TRIGGER "TrgSupplierTimestamp"
BEFORE UPDATE ON purchasing."Suppliers"
FOR EACH ROW EXECUTE FUNCTION public.fn_update_timestamp();

-- Catálogo de productos de ferretería
CREATE TABLE public."Products" (
    "id"                UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "code"              VARCHAR(30)   NOT NULL UNIQUE,
    "barcode"           VARCHAR(50)   UNIQUE,
    "description"       VARCHAR(200)  NOT NULL,
    "FamilyId"          UUID          NOT NULL REFERENCES public."Families"("id"),
    "SubfamilyId"       UUID          REFERENCES public."Subfamilies"("id"),
    "MeasurementTypeId" UUID          NOT NULL REFERENCES public."MeasurementTypes"("id"),
    "SupplierId"        UUID          REFERENCES purchasing."Suppliers"("id"),
    "SalePrice"         NUMERIC(12,2) NOT NULL DEFAULT 0,
    "CostPrice"         NUMERIC(12,4) NOT NULL DEFAULT 0,
    "CurrentStock"      NUMERIC(12,3) NOT NULL DEFAULT 0,
    "MinStock"          NUMERIC(12,3) NOT NULL DEFAULT 0,
    "MaxStock"          NUMERIC(12,3),
    "ReorderPoint"      NUMERIC(12,3),
    -- Clasificación de rotación (ABC): ALTA | MEDIA | BAJA | NULA
    "RotationClass"     VARCHAR(10),
    "IsActive"          BOOLEAN       NOT NULL DEFAULT TRUE,
    "notes"             TEXT,
    "CreatedAt"         TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    "UpdatedAt"         TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    CONSTRAINT "StockNotNegative"   CHECK ("CurrentStock" >= 0),
    CONSTRAINT "RotationClassValid" CHECK ("RotationClass" IS NULL OR "RotationClass" IN ('ALTA','MEDIA','BAJA','NULA'))
);

CREATE INDEX "IdxProductsFamily"    ON public."Products"("FamilyId");
CREATE INDEX "IdxProductsSubfamily" ON public."Products"("SubfamilyId");
CREATE INDEX "IdxProductsSupplier"  ON public."Products"("SupplierId");
CREATE INDEX "IdxProductsActive"    ON public."Products"("IsActive");

CREATE TRIGGER "TrgProductTimestamp"
BEFORE UPDATE ON public."Products"
FOR EACH ROW EXECUTE FUNCTION public.fn_update_timestamp();

-- Presentaciones de venta por producto con factor de conversión y precio propio
-- Ej: un producto que se cuenta por UNIDAD puede venderse por DOCENA (12) o CAJA (100).
CREATE TABLE public."ProductSaleUnits" (
    "id"              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "ProductId"       UUID          NOT NULL REFERENCES public."Products"("id") ON DELETE CASCADE,
    "SaleUnitId"      UUID          NOT NULL REFERENCES public."SaleUnits"("id"),
    -- Cuántas unidades base descuenta del stock esta presentación (UNIDAD=1, DOCENA=12)
    "UnitsPerPackage" NUMERIC(12,3) NOT NULL DEFAULT 1,
    "SalePrice"       NUMERIC(12,2) NOT NULL DEFAULT 0,
    "barcode"         VARCHAR(50),
    "IsDefault"       BOOLEAN       NOT NULL DEFAULT FALSE,
    "IsActive"        BOOLEAN       NOT NULL DEFAULT TRUE,
    "CreatedAt"       TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    "UpdatedAt"       TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    UNIQUE ("ProductId", "SaleUnitId"),
    CONSTRAINT "UnitsPerPackagePositive" CHECK ("UnitsPerPackage" > 0)
);

CREATE INDEX "IdxProductSaleUnitsProduct" ON public."ProductSaleUnits"("ProductId");

CREATE TRIGGER "TrgProductSaleUnitTimestamp"
BEFORE UPDATE ON public."ProductSaleUnits"
FOR EACH ROW EXECUTE FUNCTION public.fn_update_timestamp();

-- Descuentos por volumen (por producto o por categoría)
CREATE TABLE public."VolumeDiscounts" (
    "id"              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "ProductId"       UUID          REFERENCES public."Products"("id") ON DELETE CASCADE,
    "FamilyId"        UUID          REFERENCES public."Families"("id"),
    "MinQuantity"     NUMERIC(12,3) NOT NULL,
    "DiscountPercent" NUMERIC(5,2),
    "FixedUnitPrice"  NUMERIC(12,2),
    "StartDate"       DATE,
    "EndDate"         DATE,
    "IsActive"        BOOLEAN       NOT NULL DEFAULT TRUE,
    "CreatedAt"       TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    "UpdatedAt"       TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    CONSTRAINT "VolumeDiscountTarget" CHECK ("ProductId" IS NOT NULL OR "FamilyId" IS NOT NULL),
    CONSTRAINT "VolumeDiscountMinQty" CHECK ("MinQuantity" > 0),
    CONSTRAINT "VolumeDiscountValue"  CHECK ("DiscountPercent" IS NOT NULL OR "FixedUnitPrice" IS NOT NULL)
);

CREATE INDEX "IdxVolumeDiscountsProduct" ON public."VolumeDiscounts"("ProductId");
CREATE INDEX "IdxVolumeDiscountsFamily"  ON public."VolumeDiscounts"("FamilyId");

CREATE TRIGGER "TrgVolumeDiscountTimestamp"
BEFORE UPDATE ON public."VolumeDiscounts"
FOR EACH ROW EXECUTE FUNCTION public.fn_update_timestamp();

-- Kardex de inventario (inmutable): entradas, salidas y ajustes
CREATE TABLE public."InventoryMovements" (
    "id"              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "ProductId"       UUID          NOT NULL REFERENCES public."Products"("id"),
    -- ENTRADA_COMPRA | ENTRADA_DEVOLUCION | SALIDA_VENTA | AJUSTE_ENTRADA | AJUSTE_SALIDA
    "MovementType"    VARCHAR(30)   NOT NULL,
    "quantity"        NUMERIC(12,3) NOT NULL,
    "UnitCost"        NUMERIC(12,4) NOT NULL DEFAULT 0,
    "TotalCost"       NUMERIC(12,4) NOT NULL DEFAULT 0,
    "StockBefore"     NUMERIC(12,3) NOT NULL,
    "StockAfter"      NUMERIC(12,3) NOT NULL,
    "OrderId"         UUID,
    "PurchaseOrderId" UUID,
    "EmployeeId"      UUID,
    "reason"          VARCHAR(300),
    "CreatedAt"       TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    CONSTRAINT "MovementTypeValid" CHECK (
        "MovementType" IN (
            'ENTRADA_COMPRA','ENTRADA_DEVOLUCION',
            'SALIDA_VENTA','AJUSTE_ENTRADA','AJUSTE_SALIDA'
        )
    )
);

CREATE INDEX "IdxInvMovProductDate" ON public."InventoryMovements"("ProductId","CreatedAt");
CREATE INDEX "IdxInvMovType"        ON public."InventoryMovements"("MovementType");
CREATE INDEX "IdxInvMovOrder"       ON public."InventoryMovements"("OrderId");
CREATE INDEX "IdxInvMovPO"          ON public."InventoryMovements"("PurchaseOrderId");

-- Alertas de stock bajo (generadas por trigger)
CREATE TABLE public."StockAlerts" (
    "id"           UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "ProductId"    UUID          NOT NULL REFERENCES public."Products"("id"),
    "CurrentStock" NUMERIC(12,3) NOT NULL,
    "MinStock"     NUMERIC(12,3) NOT NULL,
    "IsResolved"   BOOLEAN       NOT NULL DEFAULT FALSE,
    "ResolvedAt"   TIMESTAMPTZ,
    "CreatedAt"    TIMESTAMPTZ   NOT NULL DEFAULT NOW()
);

CREATE INDEX "IdxStockAlertsProduct" ON public."StockAlerts"("ProductId");
CREATE INDEX "IdxStockAlertsUnresolved"
    ON public."StockAlerts"("ProductId") WHERE "IsResolved" = FALSE;

-- Trigger: crea alerta cuando el stock cae al mínimo (evita duplicados sin resolver)
CREATE OR REPLACE FUNCTION public.fn_stock_alert()
RETURNS TRIGGER AS $$
BEGIN
    IF NEW."CurrentStock" <= NEW."MinStock"
       AND NEW."CurrentStock" != OLD."CurrentStock"
       AND NOT EXISTS (
           SELECT 1 FROM public."StockAlerts"
           WHERE "ProductId" = NEW."id" AND "IsResolved" = FALSE
       ) THEN
        INSERT INTO public."StockAlerts" ("ProductId", "CurrentStock", "MinStock")
        VALUES (NEW."id", NEW."CurrentStock", NEW."MinStock");
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER "TrgStockAlert"
AFTER UPDATE OF "CurrentStock" ON public."Products"
FOR EACH ROW EXECUTE FUNCTION public.fn_stock_alert();

-- =============================================================================
-- ESQUEMA purchasing — Compras a proveedores
-- =============================================================================

CREATE TABLE purchasing."PurchaseOrders" (
    "id"                UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "SupplierId"        UUID          NOT NULL REFERENCES purchasing."Suppliers"("id"),
    "EmployeeId"        UUID          NOT NULL,
    "SupplierDocNumber" VARCHAR(50),
    "SupplierDocType"   VARCHAR(10),   -- CCF | FAC | OTRO
    "status"            VARCHAR(20)   NOT NULL DEFAULT 'BORRADOR', -- BORRADOR|CONFIRMADA|RECIBIDA|CANCELADA
    "subtotal"          NUMERIC(12,2) NOT NULL DEFAULT 0,
    "TaxAmount"         NUMERIC(12,2) NOT NULL DEFAULT 0,
    "total"             NUMERIC(12,2) NOT NULL DEFAULT 0,
    "notes"             TEXT,
    "ExpectedDate"      DATE,
    "ReceivedAt"        TIMESTAMPTZ,
    "ReceivedById"      UUID,
    "CreatedAt"         TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    "UpdatedAt"         TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    CONSTRAINT "PurchaseStatusValid" CHECK ("status" IN ('BORRADOR','CONFIRMADA','RECIBIDA','CANCELADA'))
);

CREATE INDEX "IdxPurchaseOrdersSupplier" ON purchasing."PurchaseOrders"("SupplierId");
CREATE INDEX "IdxPurchaseOrdersStatus"   ON purchasing."PurchaseOrders"("status");

CREATE TRIGGER "TrgPurchaseOrderTimestamp"
BEFORE UPDATE ON purchasing."PurchaseOrders"
FOR EACH ROW EXECUTE FUNCTION public.fn_update_timestamp();

CREATE TABLE purchasing."PurchaseOrderDetails" (
    "id"              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "PurchaseOrderId" UUID          NOT NULL REFERENCES purchasing."PurchaseOrders"("id") ON DELETE CASCADE,
    "ProductId"       UUID          NOT NULL REFERENCES public."Products"("id"),
    "quantity"        NUMERIC(12,3) NOT NULL,
    "UnitCost"        NUMERIC(12,4) NOT NULL,
    "TaxRate"         NUMERIC(5,4)  NOT NULL DEFAULT 0.13,
    "subtotal"        NUMERIC(12,2) NOT NULL,
    "total"           NUMERIC(12,2) NOT NULL,
    "notes"           VARCHAR(300),
    CONSTRAINT "PODetailQtyPositive" CHECK ("quantity" > 0)
);

CREATE INDEX "IdxPODetailsOrder"   ON purchasing."PurchaseOrderDetails"("PurchaseOrderId");
CREATE INDEX "IdxPODetailsProduct" ON purchasing."PurchaseOrderDetails"("ProductId");

ALTER TABLE public."InventoryMovements"
    ADD CONSTRAINT "FKInventoryMovementsPurchaseOrder"
    FOREIGN KEY ("PurchaseOrderId") REFERENCES purchasing."PurchaseOrders"("id");

-- =============================================================================
-- ESQUEMA sales — Ventas / facturación y caja
-- =============================================================================

CREATE TABLE sales."CashSessions" (
    "id"                    UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "EmployeeId"            UUID          NOT NULL,
    "CashRegisterCode"      VARCHAR(50)   NOT NULL DEFAULT 'CAJA-01',
    "OpenedAt"              TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    "ClosedAt"              TIMESTAMPTZ,
    "OpeningAmount"         NUMERIC(12,2) NOT NULL DEFAULT 0,
    "ClosingDeclaredAmount" NUMERIC(12,2),
    "ClosingExpectedAmount" NUMERIC(12,2),
    "difference"            NUMERIC(12,2),
    "status"                VARCHAR(20)   NOT NULL DEFAULT 'ABIERTA', -- ABIERTA|CERRADA|CANCELADA
    "notes"                 TEXT,
    "CreatedAt"             TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    "UpdatedAt"             TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    CONSTRAINT "CashSessionStatusValid" CHECK ("status" IN ('ABIERTA','CERRADA','CANCELADA'))
);

CREATE UNIQUE INDEX "IdxCashSessionOpen"
    ON sales."CashSessions"("EmployeeId","CashRegisterCode")
    WHERE "status" = 'ABIERTA';
CREATE INDEX "IdxCashSessionStatus" ON sales."CashSessions"("status");
CREATE INDEX "IdxCashSessionOpened" ON sales."CashSessions"("OpenedAt");

CREATE TRIGGER "TrgCashSessionTimestamp"
BEFORE UPDATE ON sales."CashSessions"
FOR EACH ROW EXECUTE FUNCTION public.fn_update_timestamp();

-- Facturas / ventas (cabecera)
CREATE TABLE sales."Orders" (
    "id"              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "EmployeeId"      UUID          NOT NULL,
    "CashSessionId"   UUID          REFERENCES sales."CashSessions"("id"),
    "CustomerId"      UUID          REFERENCES public."Customers"("id"),
    "OrderType"       VARCHAR(20)   NOT NULL DEFAULT 'VENTA_CAJA', -- VENTA_CAJA | COTIZACION
    "ClientRequestId" UUID          NOT NULL DEFAULT gen_random_uuid(),
    "status"          VARCHAR(20)   NOT NULL DEFAULT 'PENDIENTE',  -- PENDIENTE|COMPLETADA|CANCELADA
    "subtotal"        NUMERIC(12,2) NOT NULL DEFAULT 0,
    "TaxAmount"       NUMERIC(12,2) NOT NULL DEFAULT 0,
    "DiscountAmount"  NUMERIC(12,2) NOT NULL DEFAULT 0,
    "total"           NUMERIC(12,2) NOT NULL DEFAULT 0,
    "notes"           TEXT,
    "CreatedAt"       TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    "UpdatedAt"       TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    CONSTRAINT "OrderStatusValid" CHECK ("status" IN ('PENDIENTE','COMPLETADA','CANCELADA'))
);

CREATE UNIQUE INDEX "IdxOrdersClientRequest" ON sales."Orders"("ClientRequestId");
CREATE INDEX "IdxOrdersEmployee"    ON sales."Orders"("EmployeeId");
CREATE INDEX "IdxOrdersCashSession" ON sales."Orders"("CashSessionId");
CREATE INDEX "IdxOrdersStatus"      ON sales."Orders"("status");
CREATE INDEX "IdxOrdersCreatedAt"   ON sales."Orders"("CreatedAt");

CREATE TRIGGER "TrgOrderTimestamp"
BEFORE UPDATE ON sales."Orders"
FOR EACH ROW EXECUTE FUNCTION public.fn_update_timestamp();

ALTER TABLE public."InventoryMovements"
    ADD CONSTRAINT "FKInventoryMovementsOrder"
    FOREIGN KEY ("OrderId") REFERENCES sales."Orders"("id");

-- Detalle de factura (líneas)
CREATE TABLE sales."OrderDetails" (
    "id"             UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "OrderId"        UUID          NOT NULL REFERENCES sales."Orders"("id") ON DELETE CASCADE,
    "ProductId"      UUID          NOT NULL REFERENCES public."Products"("id"),
    "SaleUnitId"     UUID          REFERENCES public."SaleUnits"("id"),
    -- Cantidad en la presentación vendida (ej: 2 cajas) y factor a unidades base
    "quantity"       NUMERIC(12,3) NOT NULL,
    "UnitsPerPackage" NUMERIC(12,3) NOT NULL DEFAULT 1,
    "UnitPrice"      NUMERIC(12,2) NOT NULL,
    "UnitCost"       NUMERIC(12,4) NOT NULL DEFAULT 0,
    "DiscountAmount" NUMERIC(12,2) NOT NULL DEFAULT 0,
    "subtotal"       NUMERIC(12,2) NOT NULL,
    "notes"          VARCHAR(300),
    "CreatedAt"      TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    CONSTRAINT "OrderDetailQtyPositive" CHECK ("quantity" > 0)
);

CREATE INDEX "IdxOrderDetailsOrder"   ON sales."OrderDetails"("OrderId");
CREATE INDEX "IdxOrderDetailsProduct" ON sales."OrderDetails"("ProductId");

-- Pagos
CREATE TABLE sales."Payments" (
    "id"            UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "OrderId"       UUID          NOT NULL REFERENCES sales."Orders"("id") ON DELETE CASCADE,
    "CashSessionId" UUID          REFERENCES sales."CashSessions"("id"),
    "method"        VARCHAR(20)   NOT NULL, -- EFECTIVO|TARJETA|TRANSFERENCIA|OTRO
    "amount"        NUMERIC(12,2) NOT NULL,
    "reference"     VARCHAR(100),
    "CreatedAt"     TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    CONSTRAINT "PaymentMethodValid"  CHECK ("method" IN ('EFECTIVO','TARJETA','TRANSFERENCIA','OTRO')),
    CONSTRAINT "PaymentAmountPositive" CHECK ("amount" > 0)
);

CREATE INDEX "IdxPaymentsOrder"   ON sales."Payments"("OrderId");
CREATE INDEX "IdxPaymentsSession" ON sales."Payments"("CashSessionId");

-- =============================================================================
-- ESQUEMA dte — Facturación electrónica (opcional, alineado con Hacienda SV)
-- =============================================================================

CREATE TABLE dte."DteConfig" (
    "id"                 UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "EmisorNit"          VARCHAR(20)  NOT NULL,
    "EmisorNrc"          VARCHAR(20)  NOT NULL,
    "EmisorName"         VARCHAR(250) NOT NULL,
    "EmisorTradeName"    VARCHAR(250),
    "ActividadEconomica" VARCHAR(10)  NOT NULL,
    "AddressLine"        VARCHAR(300) NOT NULL,
    "Municipality"       VARCHAR(100) NOT NULL,
    "Department"         VARCHAR(50)  NOT NULL,
    "phone"              VARCHAR(20),
    "email"              VARCHAR(100),
    "ambiente"           VARCHAR(5)   NOT NULL DEFAULT '00',
    "IsActive"           BOOLEAN      NOT NULL DEFAULT TRUE,
    "CreatedAt"          TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    "UpdatedAt"          TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE TRIGGER "TrgDteConfigTimestamp"
BEFORE UPDATE ON dte."DteConfig"
FOR EACH ROW EXECUTE FUNCTION public.fn_update_timestamp();

CREATE TABLE dte."DteIssued" (
    "id"             UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "OrderId"        UUID          REFERENCES sales."Orders"("id"),
    "DteType"        VARCHAR(5)    NOT NULL,   -- 01 FAC | 03 CCF | 05 Nota Crédito
    "GenerationCode" UUID          NOT NULL UNIQUE DEFAULT gen_random_uuid(),
    "ControlNumber"  VARCHAR(40)   NOT NULL UNIQUE,
    "MhStatus"       VARCHAR(20)   NOT NULL DEFAULT 'PENDIENTE',
    "MhResponse"     TEXT,
    "MhSello"        TEXT,
    "RelatedDteId"   UUID          REFERENCES dte."DteIssued"("id"),
    "ambiente"       VARCHAR(5)    NOT NULL DEFAULT '00',
    "JsonPayload"    TEXT,
    "PdfUrl"         VARCHAR(500),
    "TotalExenta"    NUMERIC(12,2) NOT NULL DEFAULT 0,
    "TotalGravada"   NUMERIC(12,2) NOT NULL DEFAULT 0,
    "TotalIva"       NUMERIC(12,2) NOT NULL DEFAULT 0,
    "TotalPagar"     NUMERIC(12,2) NOT NULL DEFAULT 0,
    "reprints"       INTEGER       NOT NULL DEFAULT 0,
    "IssuedAt"       TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    "ProcessedAt"    TIMESTAMPTZ,
    "CreatedAt"      TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    CONSTRAINT "MhStatusValid" CHECK ("MhStatus" IN ('PENDIENTE','PROCESADO','RECHAZADO','CONTINGENCIA'))
);

CREATE INDEX "IdxDteIssuedOrder"  ON dte."DteIssued"("OrderId");
CREATE INDEX "IdxDteIssuedStatus" ON dte."DteIssued"("MhStatus");
CREATE INDEX "IdxDteIssuedType"   ON dte."DteIssued"("DteType");
CREATE INDEX "IdxDteIssuedAt"     ON dte."DteIssued"("IssuedAt");

CREATE TABLE dte."DteContingency" (
    "id"           UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "DteId"        UUID          NOT NULL UNIQUE REFERENCES dte."DteIssued"("id"),
    "AttemptCount" INTEGER       NOT NULL DEFAULT 0,
    "LastError"    TEXT,
    "NextRetryAt"  TIMESTAMPTZ,
    "ResolvedAt"   TIMESTAMPTZ,
    "CreatedAt"    TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    "UpdatedAt"    TIMESTAMPTZ   NOT NULL DEFAULT NOW()
);

CREATE INDEX "IdxContingencyRetry" ON dte."DteContingency"("NextRetryAt");

-- =============================================================================
-- ESQUEMA hr — Recursos Humanos básico (usuarios / empleados)
-- =============================================================================

CREATE TABLE hr."Departments" (
    "id"          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "name"        VARCHAR(100) NOT NULL UNIQUE,
    "ParentId"    UUID         REFERENCES hr."Departments"("id"),
    "description" VARCHAR(300),
    "IsActive"    BOOLEAN      NOT NULL DEFAULT TRUE,
    "CreatedAt"   TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    "UpdatedAt"   TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE INDEX "IdxDepartmentsParent" ON hr."Departments"("ParentId");

CREATE TRIGGER "TrgDepartmentTimestamp"
BEFORE UPDATE ON hr."Departments"
FOR EACH ROW EXECUTE FUNCTION public.fn_update_timestamp();

CREATE TABLE hr."Positions" (
    "id"           UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "DepartmentId" UUID         NOT NULL REFERENCES hr."Departments"("id"),
    "name"         VARCHAR(100) NOT NULL,
    "description"  VARCHAR(500),
    "IsActive"     BOOLEAN      NOT NULL DEFAULT TRUE,
    "CreatedAt"    TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    "UpdatedAt"    TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    UNIQUE ("DepartmentId","name")
);

CREATE TRIGGER "TrgPositionTimestamp"
BEFORE UPDATE ON hr."Positions"
FOR EACH ROW EXECUTE FUNCTION public.fn_update_timestamp();

-- Empleados / usuarios del punto de venta (cargo, PIN, permisos)
CREATE TABLE hr."Employees" (
    "id"                   UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "FirstName"            VARCHAR(100)  NOT NULL,
    "LastName"             VARCHAR(100)  NOT NULL,
    "Dui"                  VARCHAR(15)   UNIQUE,
    "Nit"                  VARCHAR(20)   UNIQUE,
    "Nup"                  VARCHAR(20)   UNIQUE,
    "IsssNumber"           VARCHAR(20)   UNIQUE,
    "PositionId"           UUID          REFERENCES hr."Positions"("id"),
    "DepartmentId"         UUID          REFERENCES hr."Departments"("id"),
    "DirectSupervisorId"   UUID          REFERENCES hr."Employees"("id"),
    "HireDate"             DATE          NOT NULL,
    "ContractEndDate"      DATE,
    "TerminationDate"      DATE,
    "BaseSalary"           NUMERIC(10,2) NOT NULL DEFAULT 0,
    "ContractType"         VARCHAR(20)   NOT NULL DEFAULT 'PLAZO_FIJO',
    "SalaryType"           VARCHAR(20)   NOT NULL DEFAULT 'MENSUAL',
    "DefaultBonus"         NUMERIC(10,2) NOT NULL DEFAULT 0,
    "DefaultViaticos"      NUMERIC(10,2) NOT NULL DEFAULT 0,
    "BirthDate"            DATE,
    "Gender"               VARCHAR(20),
    "Nationality"          VARCHAR(20)   DEFAULT 'SALVADOREÑA',
    "PassportNumber"       VARCHAR(30),
    "MaritalStatus"        VARCHAR(20),
    "AcademicLevel"        VARCHAR(50),
    "DepartmentSv"         VARCHAR(30),
    "DependentsDescription" TEXT,
    "phone"                VARCHAR(20),
    "email"                VARCHAR(100),
    "address"              TEXT,
    "AfpInstitution"       VARCHAR(20),
    "AfpEnrollmentDate"    DATE,
    "IsssEnrolled"         BOOLEAN       NOT NULL DEFAULT TRUE,
    "IsssEnrollmentDate"   DATE,
    "PaymentChannel"       VARCHAR(30)   DEFAULT 'DEPOSITO_BANCARIO',
    -- Acceso punto de venta
    "PinHash"              TEXT,
    "PinUpdatedAt"         TIMESTAMPTZ,
    "AttendanceEnabled"    BOOLEAN       NOT NULL DEFAULT TRUE,
    "CanSell"              BOOLEAN       NOT NULL DEFAULT FALSE,  -- acceso a Inventario
    "CanCashier"           BOOLEAN       NOT NULL DEFAULT FALSE,  -- acceso a Caja
    "OnProbation"          BOOLEAN       NOT NULL DEFAULT FALSE,
    "ProbationEndDate"     DATE,
    "ProbationCompletedAt" TIMESTAMPTZ,
    "TerminationReason"    VARCHAR(40),
    "TerminationNotes"     TEXT,
    "IsActive"             BOOLEAN       NOT NULL DEFAULT TRUE,
    "CreatedAt"            TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    "UpdatedAt"            TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    CONSTRAINT "ContractValid" CHECK ("ContractType" IN ('TIEMPO_PARCIAL','PLAZO_FIJO','HONORARIOS','PASANTE')),
    CONSTRAINT "SalaryTypeValid" CHECK ("SalaryType" IN ('MENSUAL','QUINCENAL','SEMANAL'))
);

CREATE INDEX "IdxEmployeesDepartment"  ON hr."Employees"("DepartmentId");
CREATE INDEX "IdxEmployeesSupervisor"  ON hr."Employees"("DirectSupervisorId");
CREATE INDEX "IdxEmployeesContractType" ON hr."Employees"("ContractType");

CREATE TRIGGER "TrgEmployeeTimestamp"
BEFORE UPDATE ON hr."Employees"
FOR EACH ROW EXECUTE FUNCTION public.fn_update_timestamp();

-- FKs de EmployeeId (declaradas tras crear hr.Employees)
ALTER TABLE public."InventoryMovements"
    ADD CONSTRAINT "FKInventoryMovementsEmployee"
    FOREIGN KEY ("EmployeeId") REFERENCES hr."Employees"("id");
ALTER TABLE sales."Orders"
    ADD CONSTRAINT "FKOrdersEmployee"
    FOREIGN KEY ("EmployeeId") REFERENCES hr."Employees"("id");
ALTER TABLE sales."CashSessions"
    ADD CONSTRAINT "FKCashSessionsEmployee"
    FOREIGN KEY ("EmployeeId") REFERENCES hr."Employees"("id");
ALTER TABLE purchasing."PurchaseOrders"
    ADD CONSTRAINT "FKPurchaseOrdersEmployee"
    FOREIGN KEY ("EmployeeId") REFERENCES hr."Employees"("id");

-- =============================================================================
-- ESQUEMA system — Configuración, seguridad y auditoría
-- =============================================================================

CREATE TABLE system."Settings" (
    "Key"         VARCHAR(100) PRIMARY KEY,
    "Value"       TEXT         NOT NULL,
    "Description" VARCHAR(300),
    "IsPublic"    BOOLEAN      NOT NULL DEFAULT FALSE,
    "UpdatedAt"   TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE TABLE system."Printers" (
    "Id"             UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "Name"           VARCHAR(200) NOT NULL,
    "ConnectionType" VARCHAR(10)  NOT NULL DEFAULT 'USB',
    "IpAddress"      VARCHAR(15),
    "NetworkPort"    INTEGER,
    "PaperWidth"     SMALLINT     NOT NULL DEFAULT 80,
    "IsDefault"      BOOLEAN      NOT NULL DEFAULT FALSE,
    "IsActive"       BOOLEAN      NOT NULL DEFAULT TRUE,
    "CreatedAt"      TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    "UpdatedAt"      TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE UNIQUE INDEX "IdxPrinterDefault"
    ON system."Printers"("IsDefault") WHERE "IsDefault" = TRUE;

CREATE TRIGGER "TrgPrinterTimestamp"
BEFORE UPDATE ON system."Printers"
FOR EACH ROW EXECUTE FUNCTION public.fn_update_timestamp();

-- Usuarios del panel administrativo web (no se usan en la caja WPF)
CREATE TABLE system."WebUsers" (
    "id"           UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "Username"     VARCHAR(50)  NOT NULL UNIQUE,
    "Email"        VARCHAR(100) NOT NULL UNIQUE,
    "PasswordHash" TEXT         NOT NULL,
    "Role"         VARCHAR(20)  NOT NULL DEFAULT 'ADMIN',
    "EmployeeId"   UUID         REFERENCES hr."Employees"("id"),
    "IsActive"     BOOLEAN      NOT NULL DEFAULT TRUE,
    "LastLoginAt"  TIMESTAMPTZ,
    "CreatedAt"    TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    "UpdatedAt"    TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    CONSTRAINT "RoleValid" CHECK ("Role" IN ('ADMIN','ACCOUNTANT','OWNER'))
);

CREATE TRIGGER "TrgWebUserTimestamp"
BEFORE UPDATE ON system."WebUsers"
FOR EACH ROW EXECUTE FUNCTION public.fn_update_timestamp();

-- Bitácora de auditoría para cambios críticos
CREATE TABLE system."AuditLog" (
    "id"        UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "TableName" VARCHAR(100) NOT NULL,
    "RecordId"  VARCHAR(50)  NOT NULL,
    "action"    VARCHAR(10)  NOT NULL,  -- INSERT | UPDATE | DELETE | LOGIN | LOGOUT
    "OldData"   JSONB,
    "NewData"   JSONB,
    "UserId"    UUID,
    "IpAddress" VARCHAR(45),
    "UserAgent" VARCHAR(300),
    "CreatedAt" TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE INDEX "IdxAuditLogRecord"    ON system."AuditLog"("TableName","RecordId");
CREATE INDEX "IdxAuditLogUser"      ON system."AuditLog"("UserId");
CREATE INDEX "IdxAuditLogCreatedAt" ON system."AuditLog"("CreatedAt");

-- =============================================================================
-- VISTAS ÚTILES
-- =============================================================================

-- Productos con estado de stock (para tabla de inventario con colores)
CREATE OR REPLACE VIEW public."VProductsStock" AS
SELECT
    p."id",
    p."code",
    p."description",
    f."name"      AS family,
    sf."name"     AS subfamily,
    mt."UnitLabel" AS unit,
    mt."decimals",
    p."CurrentStock",
    p."MinStock",
    p."SalePrice",
    p."RotationClass",
    CASE
        WHEN p."CurrentStock" = 0            THEN 'AGOTADO'
        WHEN p."CurrentStock" <= p."MinStock" THEN 'BAJO_MINIMO'
        ELSE 'OK'
    END AS "StockStatus"
FROM public."Products" p
JOIN public."Families" f ON p."FamilyId" = f."id"
LEFT JOIN public."Subfamilies" sf ON p."SubfamilyId" = sf."id"
JOIN public."MeasurementTypes" mt ON p."MeasurementTypeId" = mt."id"
WHERE p."IsActive" = TRUE;

-- Rotación de stock: unidades vendidas en los últimos 30 días por producto
CREATE OR REPLACE VIEW public."VProductRotation" AS
SELECT
    p."id",
    p."code",
    p."description",
    p."CurrentStock",
    COALESCE(SUM(CASE WHEN m."MovementType" = 'SALIDA_VENTA'
                      AND m."CreatedAt" >= NOW() - INTERVAL '30 days'
                 THEN m."quantity" ELSE 0 END), 0) AS "SoldLast30Days",
    CASE
        WHEN COALESCE(SUM(CASE WHEN m."MovementType" = 'SALIDA_VENTA'
                               AND m."CreatedAt" >= NOW() - INTERVAL '30 days'
                          THEN m."quantity" ELSE 0 END), 0) = 0 THEN 'NULA'
        WHEN COALESCE(SUM(CASE WHEN m."MovementType" = 'SALIDA_VENTA'
                               AND m."CreatedAt" >= NOW() - INTERVAL '30 days'
                          THEN m."quantity" ELSE 0 END), 0) >= 50 THEN 'ALTA'
        WHEN COALESCE(SUM(CASE WHEN m."MovementType" = 'SALIDA_VENTA'
                               AND m."CreatedAt" >= NOW() - INTERVAL '30 days'
                          THEN m."quantity" ELSE 0 END), 0) >= 10 THEN 'MEDIA'
        ELSE 'BAJA'
    END AS "RotationClassComputed"
FROM public."Products" p
LEFT JOIN public."InventoryMovements" m ON m."ProductId" = p."id"
WHERE p."IsActive" = TRUE
GROUP BY p."id", p."code", p."description", p."CurrentStock";

-- Alertas de stock activas
CREATE OR REPLACE VIEW public."VActiveAlerts" AS
SELECT
    al."id",
    al."CreatedAt",
    p."code",
    p."description",
    al."CurrentStock",
    al."MinStock",
    CASE WHEN al."CurrentStock" = 0 THEN 'AGOTADO' ELSE 'BAJO_MINIMO' END AS "AlertType"
FROM public."StockAlerts" al
JOIN public."Products" p ON al."ProductId" = p."id"
WHERE al."IsResolved" = FALSE
ORDER BY al."CreatedAt" DESC;

-- KPIs de ventas del día
CREATE OR REPLACE VIEW sales."VKpisToday" AS
SELECT
    COUNT(*)                       AS "TotalOrders",
    COALESCE(SUM(o."total"), 0)    AS "TotalAmount",
    COALESCE(AVG(o."total"), 0)    AS "AvgTicket"
FROM sales."Orders" o
WHERE o."CreatedAt"::date = CURRENT_DATE
  AND o."status" = 'COMPLETADA';

-- =============================================================================
-- DATOS INICIALES (Seed) — Catálogo de ferretería
-- =============================================================================

-- Tipos de medida base
INSERT INTO public."MeasurementTypes" ("code","name","UnitLabel","decimals") VALUES
    ('UNIDAD', 'Unidad',            'unidades', 0),
    ('METRO',  'Metros lineales',   'metros',   2),
    ('LIBRA',  'Libras',            'lb',       2),
    ('KILO',   'Kilogramos',        'kg',       3),
    ('QUINTAL','Quintales',         'qq',       2),
    ('GALON',  'Galones',           'gal',      2),
    ('LITRO',  'Litros',            'L',        2),
    ('SACO',   'Sacos',             'sacos',    0),
    ('ROLLO',  'Rollos',            'rollos',   0),
    ('JUEGO',  'Juegos / sets',     'juegos',   0),
    ('PAR',    'Pares',             'pares',    0)
ON CONFLICT ("code") DO UPDATE SET
    "name" = EXCLUDED."name", "UnitLabel" = EXCLUDED."UnitLabel", "decimals" = EXCLUDED."decimals";

-- Unidades de venta / presentaciones
INSERT INTO public."SaleUnits" ("code","name","Abbreviation") VALUES
    ('UNIDAD',       'Unidad',        'u'),
    ('MEDIA_DOCENA', 'Media docena',  '½doc'),
    ('DOCENA',       'Docena',        'doc'),
    ('PAR',          'Par',           'par'),
    ('CIENTO',       'Ciento',        'cto'),
    ('MILLAR',       'Millar',        'mil'),
    ('CAJA',         'Caja',          'caja'),
    ('BULTO',        'Bulto',         'bul'),
    ('SACO',         'Saco',          'saco'),
    ('ROLLO',        'Rollo',         'rollo'),
    ('JUEGO',        'Juego',         'jgo')
ON CONFLICT ("code") DO UPDATE SET
    "name" = EXCLUDED."name", "Abbreviation" = EXCLUDED."Abbreviation", "IsActive" = TRUE;

-- Categorías de ferretería
INSERT INTO public."Families" ("code","name","description") VALUES
    ('CL', 'Clavos y Tornillería',   'Clavos, tornillos, pernos, tuercas y arandelas'),
    ('CE', 'Cemento y Construcción', 'Cemento, cal, arena, grava y aditivos'),
    ('BL', 'Bloques y Adoquines',    'Bloques de concreto, ladrillos y adoquines'),
    ('HM', 'Herramientas Manuales',  'Palas, martillos, alicates, llaves, destornilladores'),
    ('HE', 'Herramientas Eléctricas','Taladros, esmeriles, sierras, lijadoras'),
    ('MQ', 'Maquinaria',             'Mezcladoras, compresores, plantas eléctricas'),
    ('PN', 'Pinturas y Solventes',   'Pinturas, thinner, brochas, rodillos'),
    ('IL', 'Iluminación',            'Lámparas, focos LED, reflectores'),
    ('EL', 'Material Eléctrico',     'Cables, cintas, cajas, breakers, tomacorrientes'),
    ('FO', 'Fontanería',             'Tuberías PVC, llaves, codos, pegamento'),
    ('AD', 'Adhesivos y Selladores', 'Silicón, pegamentos, cintas'),
    ('SE', 'Seguridad y Protección', 'Guantes, cascos, lentes, mascarillas'),
    ('FE', 'Ferretería General',     'Artículos varios de ferretería')
ON CONFLICT ("code") DO UPDATE SET
    "name" = EXCLUDED."name", "description" = EXCLUDED."description", "IsActive" = TRUE;

-- Subcategorías de Clavos y Tornillería
INSERT INTO public."Subfamilies" ("FamilyId","code","name")
SELECT "id",'CLA','Clavos'    FROM public."Families" WHERE "code"='CL' UNION ALL
SELECT "id",'TOR','Tornillos' FROM public."Families" WHERE "code"='CL' UNION ALL
SELECT "id",'PER','Pernos'    FROM public."Families" WHERE "code"='CL' UNION ALL
SELECT "id",'TUE','Tuercas y arandelas' FROM public."Families" WHERE "code"='CL'
ON CONFLICT ("FamilyId","code") DO UPDATE SET "name" = EXCLUDED."name", "IsActive" = TRUE;

-- Proveedores demo
INSERT INTO purchasing."Suppliers" ("name","TradeName","ContactName","phone","email","country")
SELECT 'Distribuidora El Constructor S.A. de C.V.','El Constructor','Ana Ramírez','2222-1111','ventas@elconstructor.com','SV'
WHERE NOT EXISTS (SELECT 1 FROM purchasing."Suppliers" WHERE "name"='Distribuidora El Constructor S.A. de C.V.');
INSERT INTO purchasing."Suppliers" ("name","TradeName","ContactName","phone","email","country")
SELECT 'Importadora Ferretera Centroamericana','ImporFerca','Luis Meléndez','2222-2222','compras@imporferca.com','SV'
WHERE NOT EXISTS (SELECT 1 FROM purchasing."Suppliers" WHERE "name"='Importadora Ferretera Centroamericana');

-- Productos de ferretería (usa funciones auxiliares por código de familia/medida)
INSERT INTO public."Products" ("code","description","FamilyId","SubfamilyId","MeasurementTypeId","SalePrice","CostPrice","CurrentStock","MinStock","MaxStock")
SELECT v.code, v.description,
       (SELECT f2."id" FROM public."Families" f2 WHERE f2."code"=v.fam),
       (SELECT sf."id" FROM public."Subfamilies" sf JOIN public."Families" f ON sf."FamilyId"=f."id" WHERE f."code"=v.fam AND sf."code"=v.sub),
       (SELECT mt2."id" FROM public."MeasurementTypes" mt2 WHERE mt2."code"=v.mt),
       v.price, v.cost, v.stock, v.minst, v.maxst
FROM (VALUES
    ('CL-CLA-001','Clavo de acero 2 pulgadas','CL','CLA','LIBRA', 0.90, 0.55, 250, 40, 800),
    ('CL-CLA-002','Clavo de acero 3 pulgadas','CL','CLA','LIBRA', 0.95, 0.58, 180, 40, 800),
    ('CL-TOR-001','Tornillo goloso 1 pulgada (caja 100)','CL','TOR','UNIDAD', 4.50, 2.80, 60, 10, 200),
    ('CL-PER-001','Perno hexagonal 1/2 x 4','CL','PER','UNIDAD', 0.75, 0.42, 300, 50, 1000),
    ('CE-CEM-001','Cemento gris Portland 42.5 kg','CE',NULL,'SACO', 9.75, 8.10, 120, 20, 500),
    ('CE-CEM-002','Cal hidratada 25 kg','CE',NULL,'SACO', 4.25, 3.10, 80, 15, 300),
    ('BL-BLO-001','Bloque de concreto 15x20x40','BL',NULL,'UNIDAD', 0.65, 0.45, 1500, 200, 5000),
    ('BL-BLO-002','Ladrillo de obra rojo','BL',NULL,'UNIDAD', 0.28, 0.18, 4000, 500, 10000),
    ('HM-PAL-001','Pala cuadrada mango de madera','HM',NULL,'UNIDAD', 12.50, 8.00, 35, 5, 120),
    ('HM-MAR-001','Martillo de uña 16 oz','HM',NULL,'UNIDAD', 7.90, 4.50, 40, 8, 100),
    ('HM-LLA-001','Juego de llaves combinadas 10 pzas','HM',NULL,'JUEGO', 24.00, 15.50, 18, 4, 60),
    ('HE-TAL-001','Taladro percutor 1/2 pulgada 650W','HE',NULL,'UNIDAD', 58.00, 41.00, 12, 3, 40),
    ('HE-ESM-001','Esmeril angular 4-1/2 pulgadas','HE',NULL,'UNIDAD', 42.00, 29.00, 10, 2, 30),
    ('MQ-MEZ-001','Mezcladora de concreto 1 saco','MQ',NULL,'UNIDAD', 850.00, 690.00, 3, 1, 8),
    ('PN-PIN-001','Pintura látex blanca (galón)','PN',NULL,'GALON', 18.50, 12.00, 60, 10, 200),
    ('PN-PIN-002','Pintura anticorrosiva roja (bote 1/4)','PN',NULL,'UNIDAD', 6.75, 4.20, 45, 8, 150),
    ('PN-BRO-001','Brocha de 3 pulgadas','PN',NULL,'UNIDAD', 2.30, 1.20, 90, 15, 300),
    ('IL-LAM-001','Lámpara LED tipo panel 18W','IL',NULL,'UNIDAD', 8.90, 5.40, 70, 12, 250),
    ('IL-FOC-001','Foco LED 9W luz blanca','IL',NULL,'UNIDAD', 1.85, 1.00, 200, 30, 600),
    ('EL-CAB-001','Cable THHN #12 (metro)','EL',NULL,'METRO', 0.55, 0.34, 1200, 150, 5000),
    ('EL-CIN-001','Cinta aislante 3M negra','EL',NULL,'UNIDAD', 1.25, 0.70, 150, 25, 500),
    ('FO-TUB-001','Tubo PVC 1/2 pulgada x 6m','FO',NULL,'UNIDAD', 3.40, 2.10, 100, 20, 400),
    ('FO-PEG-001','Pegamento PVC 118 ml','FO',NULL,'UNIDAD', 3.15, 1.90, 55, 10, 200),
    ('AD-SIL-001','Silicón transparente cartucho','AD',NULL,'UNIDAD', 2.95, 1.65, 80, 15, 300),
    ('SE-GUA-001','Guantes de trabajo de cuero','SE',NULL,'PAR', 3.50, 2.00, 60, 10, 200),
    ('SE-CAS-001','Casco de seguridad amarillo','SE',NULL,'UNIDAD', 6.20, 3.80, 30, 6, 100)
) AS v(code, description, fam, sub, mt, price, cost, stock, minst, maxst)
WHERE NOT EXISTS (SELECT 1 FROM public."Products" p WHERE p."code" = v.code);

-- Presentación de venta por defecto (UNIDAD 1:1) para todos los productos sembrados
INSERT INTO public."ProductSaleUnits" ("ProductId","SaleUnitId","UnitsPerPackage","SalePrice","IsDefault")
SELECT p."id", su."id", 1, p."SalePrice", TRUE
FROM public."Products" p
JOIN public."SaleUnits" su ON su."code" = 'UNIDAD'
WHERE NOT EXISTS (
    SELECT 1 FROM public."ProductSaleUnits" psu WHERE psu."ProductId" = p."id" AND psu."SaleUnitId" = su."id"
);

-- Presentación por DOCENA (12x) para focos LED, con precio de docena
INSERT INTO public."ProductSaleUnits" ("ProductId","SaleUnitId","UnitsPerPackage","SalePrice","IsDefault")
SELECT p."id", su."id", 12, ROUND(p."SalePrice" * 12 * 0.92, 2), FALSE
FROM public."Products" p
JOIN public."SaleUnits" su ON su."code" = 'DOCENA'
WHERE p."code" IN ('IL-FOC-001','CL-CLA-001')
  AND NOT EXISTS (SELECT 1 FROM public."ProductSaleUnits" psu WHERE psu."ProductId"=p."id" AND psu."SaleUnitId"=su."id");

-- Descuentos por volumen de ejemplo
INSERT INTO public."VolumeDiscounts" ("ProductId","MinQuantity","DiscountPercent")
SELECT p."id", 10, 5.00 FROM public."Products" p WHERE p."code" = 'CE-CEM-001'
  AND NOT EXISTS (SELECT 1 FROM public."VolumeDiscounts" vd WHERE vd."ProductId"=p."id" AND vd."MinQuantity"=10);
INSERT INTO public."VolumeDiscounts" ("ProductId","MinQuantity","FixedUnitPrice")
SELECT p."id", 100, 0.55 FROM public."Products" p WHERE p."code" = 'BL-BLO-001'
  AND NOT EXISTS (SELECT 1 FROM public."VolumeDiscounts" vd WHERE vd."ProductId"=p."id" AND vd."MinQuantity"=100);

-- Departamentos y puestos
INSERT INTO hr."Departments" ("name") VALUES
    ('Ventas'), ('Bodega'), ('Administración')
ON CONFLICT ("name") DO UPDATE SET "IsActive" = TRUE;

INSERT INTO hr."Positions" ("DepartmentId","name")
SELECT "id",'Cajero'        FROM hr."Departments" WHERE "name"='Ventas'         UNION ALL
SELECT "id",'Vendedor'      FROM hr."Departments" WHERE "name"='Ventas'         UNION ALL
SELECT "id",'Bodeguero'     FROM hr."Departments" WHERE "name"='Bodega'         UNION ALL
SELECT "id",'Encargado de Inventario' FROM hr."Departments" WHERE "name"='Bodega' UNION ALL
SELECT "id",'Administrador' FROM hr."Departments" WHERE "name"='Administración'
ON CONFLICT ("DepartmentId","name") DO UPDATE SET "IsActive" = TRUE;

-- Empleados demo (PIN bcrypt — solo desarrollo, cambiar en producción)
-- Administrador: PIN 1234, acceso a Caja e Inventario
INSERT INTO hr."Employees" ("FirstName","LastName","Dui","PositionId","DepartmentId","HireDate","BaseSalary","ContractType","SalaryType","PinHash","CanSell","CanCashier")
SELECT 'Administrador','Sistema','00000001-0', p."id", d."id", CURRENT_DATE, 800.00,'PLAZO_FIJO','MENSUAL', crypt('1234', gen_salt('bf',12)), TRUE, TRUE
FROM hr."Positions" p JOIN hr."Departments" d ON d."name"='Administración'
WHERE p."name"='Administrador'
  AND NOT EXISTS (SELECT 1 FROM hr."Employees" WHERE "Dui"='00000001-0');

-- Cajero: PIN 0000, solo Caja
INSERT INTO hr."Employees" ("FirstName","LastName","Dui","PositionId","DepartmentId","HireDate","BaseSalary","ContractType","SalaryType","PinHash","CanSell","CanCashier")
SELECT 'Carlos','Cajero','00000002-0', p."id", d."id", CURRENT_DATE, 450.00,'PLAZO_FIJO','QUINCENAL', crypt('0000', gen_salt('bf',12)), FALSE, TRUE
FROM hr."Positions" p JOIN hr."Departments" d ON d."name"='Ventas'
WHERE p."name"='Cajero'
  AND NOT EXISTS (SELECT 1 FROM hr."Employees" WHERE "Dui"='00000002-0');

-- Bodeguero: PIN 5678, solo Inventario
INSERT INTO hr."Employees" ("FirstName","LastName","Dui","PositionId","DepartmentId","HireDate","BaseSalary","ContractType","SalaryType","PinHash","CanSell","CanCashier")
SELECT 'María','Bodega','00000003-0', p."id", d."id", CURRENT_DATE, 500.00,'PLAZO_FIJO','QUINCENAL', crypt('5678', gen_salt('bf',12)), TRUE, FALSE
FROM hr."Positions" p JOIN hr."Departments" d ON d."name"='Bodega'
WHERE p."name"='Encargado de Inventario'
  AND NOT EXISTS (SELECT 1 FROM hr."Employees" WHERE "Dui"='00000003-0');

-- Cliente por defecto (Consumidor Final)
INSERT INTO public."Customers" ("CustomerType","name")
SELECT 'CF','Consumidor Final'
WHERE NOT EXISTS (SELECT 1 FROM public."Customers" WHERE "name"='Consumidor Final');

-- Configuración general
INSERT INTO system."Settings" ("Key","Value","Description") VALUES
    ('IvaPercentage',         '13',                       'IVA vigente en El Salvador (%)'),
    ('Currency',              'USD',                      'Moneda operativa'),
    ('SessionTimeoutMinutes', '30',                       'Minutos de inactividad antes de cerrar sesión'),
    ('BusinessName',          'Mini Ferretería',          'Nombre para impresión en recibos'),
    ('BusinessAddress',       'San Salvador, El Salvador','Dirección para recibos'),
    ('BusinessPhone',         '',                         'Teléfono para recibos'),
    ('TicketFooterMessage',   'Gracias por su compra.',   'Mensaje al pie del recibo'),
    ('DefaultCustomerName',   'Consumidor Final',         'Cliente por defecto en ventas de mostrador')
ON CONFLICT ("Key") DO UPDATE SET
    "Value" = EXCLUDED."Value", "Description" = EXCLUDED."Description", "UpdatedAt" = NOW();

-- Usuario web administrador (cambiar contraseña antes de producción)
INSERT INTO system."WebUsers" ("Username","Email","PasswordHash","Role")
VALUES ('admin','admin@ferreteria.local', crypt('Ferreteria2026!', gen_salt('bf',12)), 'ADMIN')
ON CONFLICT ("Username") DO NOTHING;

-- =============================================================================
-- PERMISOS
-- =============================================================================
DO $$
DECLARE
    v_role text := 'ferreteria_user';
    v_schema text;
BEGIN
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = v_role) THEN
        FOREACH v_schema IN ARRAY ARRAY['public','purchasing','sales','dte','hr','system'] LOOP
            EXECUTE format('GRANT ALL PRIVILEGES ON ALL TABLES    IN SCHEMA %I TO %I', v_schema, v_role);
            EXECUTE format('GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA %I TO %I', v_schema, v_role);
        END LOOP;
    END IF;
END $$;

-- =============================================================================
-- FIN DEL ESQUEMA — Ferretería ERP v3.0.0
-- =============================================================================
