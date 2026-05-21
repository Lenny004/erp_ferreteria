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
CREATE TABLE public.measurement_types (
    id            SERIAL      PRIMARY KEY,
    code          VARCHAR(10) NOT NULL UNIQUE,  -- METRO, PIEZA, KIT, PESO
    name          VARCHAR(50) NOT NULL,
    unit_label    VARCHAR(20) NOT NULL,          -- "metros", "piezas", "kits", "kg"
    decimals      SMALLINT    NOT NULL DEFAULT 0  -- 2=METRO, 3=PESO, 0=PIEZA/KIT
);

-- Product families
CREATE TABLE public.families (
    id          SERIAL       PRIMARY KEY,
    code        VARCHAR(5)   NOT NULL UNIQUE,  -- "01", "02", "FLV"
    name        VARCHAR(100) NOT NULL,
    description TEXT,
    is_active   BOOLEAN      NOT NULL DEFAULT TRUE,
    created_at  TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    updated_at  TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

-- Subfamilies
CREATE TABLE public.subfamilies (
    id          SERIAL       PRIMARY KEY,
    family_id   INTEGER      NOT NULL REFERENCES public.families(id),
    code        VARCHAR(10)  NOT NULL,
    name        VARCHAR(100) NOT NULL,
    is_active   BOOLEAN      NOT NULL DEFAULT TRUE,
    created_at  TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    updated_at  TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    UNIQUE (family_id, code)
);

-- Suppliers
CREATE TABLE public.suppliers (
    id          SERIAL       PRIMARY KEY,
    name        VARCHAR(150) NOT NULL,
    contact     VARCHAR(100),
    phone       VARCHAR(20),
    email       VARCHAR(100),
    is_active   BOOLEAN      NOT NULL DEFAULT TRUE,
    created_at  TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    updated_at  TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

-- Product catalog
CREATE TABLE public.products (
    id              SERIAL         PRIMARY KEY,
    code            VARCHAR(30)    NOT NULL UNIQUE,
    description     VARCHAR(200)   NOT NULL,
    family_id       INTEGER        NOT NULL REFERENCES public.families(id),
    subfamily_id    INTEGER        REFERENCES public.subfamilies(id),
    measurement_type_id INTEGER    NOT NULL REFERENCES public.measurement_types(id),
    sale_price      NUMERIC(12,2)  NOT NULL DEFAULT 0,
    cost_price      NUMERIC(12,2)  NOT NULL DEFAULT 0,
    current_stock   NUMERIC(12,3)  NOT NULL DEFAULT 0,
    min_stock       NUMERIC(12,3)  NOT NULL DEFAULT 0,
    supplier_id     INTEGER        REFERENCES public.suppliers(id),
    is_active       BOOLEAN        NOT NULL DEFAULT TRUE,
    created_at      TIMESTAMPTZ    NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ    NOT NULL DEFAULT NOW(),
    CONSTRAINT stock_not_negative CHECK (current_stock >= 0)
);

CREATE INDEX idx_products_code    ON public.products(code);
CREATE INDEX idx_products_family  ON public.products(family_id);
CREATE INDEX idx_products_active  ON public.products(is_active);

-- Inventory movements (immutable)
CREATE TABLE public.inventory_movements (
    id               BIGSERIAL      PRIMARY KEY,
    product_id       INTEGER        NOT NULL REFERENCES public.products(id),
    movement_type    VARCHAR(20)    NOT NULL,
    -- ENTRADA_COMPRA, ENTRADA_DEVOLUCION, SALIDA_VENTA,
    -- AJUSTE_SUMA, AJUSTE_RESTA
    quantity         NUMERIC(12,3)  NOT NULL,
    stock_before     NUMERIC(12,3)  NOT NULL,
    stock_after      NUMERIC(12,3)  NOT NULL,
    reason           VARCHAR(100),
    -- For AJUSTE: DAÑO, PÉRDIDA, INVENTARIO_FISICO, CORRECCIÓN
    -- For ENTRADA: COMPRA, DEVOLUCIÓN
    document_ref     VARCHAR(50),   -- Invoice number, order ID, etc.
    supplier_id      INTEGER        REFERENCES public.suppliers(id),
    employee_id      INTEGER        REFERENCES hr.employees(id), -- Who authorized adjustment
    created_at       TIMESTAMPTZ    NOT NULL DEFAULT NOW(),
    CONSTRAINT movement_type_valid CHECK (
        movement_type IN (
            'ENTRADA_COMPRA','ENTRADA_DEVOLUCION',
            'SALIDA_VENTA',
            'AJUSTE_SUMA','AJUSTE_RESTA'
        )
    )
);

CREATE INDEX idx_inv_product   ON public.inventory_movements(product_id);
CREATE INDEX idx_inv_type      ON public.inventory_movements(movement_type);
CREATE INDEX idx_inv_date      ON public.inventory_movements(created_at);

-- Low stock alerts (generated by trigger)
CREATE TABLE public.stock_alerts (
    id            BIGSERIAL      PRIMARY KEY,
    product_id    INTEGER        NOT NULL REFERENCES public.products(id),
    current_stock NUMERIC(12,3)  NOT NULL,
    min_stock     NUMERIC(12,3)  NOT NULL,
    is_resolved   BOOLEAN        NOT NULL DEFAULT FALSE,
    resolved_at   TIMESTAMPTZ,
    created_at    TIMESTAMPTZ    NOT NULL DEFAULT NOW()
);

-- Trigger: alert when stock ≤ minimum
CREATE OR REPLACE FUNCTION public.fn_stock_alert()
RETURNS TRIGGER AS $$
BEGIN
    IF NEW.current_stock <= NEW.min_stock
       AND NEW.current_stock != OLD.current_stock THEN
        INSERT INTO public.stock_alerts (product_id, current_stock, min_stock)
        VALUES (NEW.id, NEW.current_stock, NEW.min_stock);
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_stock_alert
AFTER UPDATE OF current_stock ON public.products
FOR EACH ROW EXECUTE FUNCTION public.fn_stock_alert();

-- Trigger: update timestamp on product modification
CREATE OR REPLACE FUNCTION public.fn_update_timestamp()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = NOW();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_product_timestamp
BEFORE UPDATE ON public.products
FOR EACH ROW EXECUTE FUNCTION public.fn_update_timestamp();

-- Apply timestamp trigger to all tables with updated_at
CREATE TRIGGER trg_family_timestamp
BEFORE UPDATE ON public.families
FOR EACH ROW EXECUTE FUNCTION public.fn_update_timestamp();

CREATE TRIGGER trg_subfamily_timestamp
BEFORE UPDATE ON public.subfamilies
FOR EACH ROW EXECUTE FUNCTION public.fn_update_timestamp();

CREATE TRIGGER trg_supplier_timestamp
BEFORE UPDATE ON public.suppliers
FOR EACH ROW EXECUTE FUNCTION public.fn_update_timestamp();


-- =============================================================================
-- SALES SCHEMA — Orders & Daily Operations
-- =============================================================================

-- Applications (transaction types)
CREATE TABLE sales.applications (
    id          SERIAL       PRIMARY KEY,
    code        VARCHAR(10)  NOT NULL UNIQUE,  -- VT-01, VT-02, VT-03, RP-01
    name        VARCHAR(100) NOT NULL,
    is_active   BOOLEAN      NOT NULL DEFAULT TRUE,
    created_at  TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    updated_at  TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE TRIGGER trg_application_timestamp
BEFORE UPDATE ON sales.applications
FOR EACH ROW EXECUTE FUNCTION public.fn_update_timestamp();

-- Confection orders (header) — Simplified statuses
CREATE TABLE sales.orders (
    id              BIGSERIAL     PRIMARY KEY,
    employee_id     INTEGER       NOT NULL REFERENCES hr.employees(id),
    application_id  INTEGER       NOT NULL REFERENCES sales.applications(id),
    customer_name   VARCHAR(150),
    order_date      DATE          NOT NULL DEFAULT CURRENT_DATE,
    order_time      TIME          NOT NULL DEFAULT CURRENT_TIME,
    status          VARCHAR(20)   NOT NULL DEFAULT 'PENDIENTE',
    -- PENDIENTE → COMPLETADA | CANCELADA
    subtotal        NUMERIC(12,2) NOT NULL DEFAULT 0,
    iva             NUMERIC(12,2) NOT NULL DEFAULT 0,   -- 13% calculated
    total           NUMERIC(12,2) NOT NULL DEFAULT 0,
    notes           TEXT,
    created_at      TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    CONSTRAINT order_status_valid CHECK (
        status IN ('PENDIENTE','COMPLETADA','CANCELADA')
    )
);

CREATE INDEX idx_orders_date      ON sales.orders(order_date);
CREATE INDEX idx_orders_status    ON sales.orders(status);
CREATE INDEX idx_orders_employee  ON sales.orders(employee_id);

CREATE TRIGGER trg_order_timestamp
BEFORE UPDATE ON sales.orders
FOR EACH ROW EXECUTE FUNCTION public.fn_update_timestamp();

-- Order details (items)
CREATE TABLE sales.order_details (
    id              BIGSERIAL     PRIMARY KEY,
    order_id        BIGINT        NOT NULL REFERENCES sales.orders(id) ON DELETE CASCADE,
    product_id      INTEGER       NOT NULL REFERENCES public.products(id),
    quantity        NUMERIC(12,3) NOT NULL,
    unit_price      NUMERIC(12,2) NOT NULL,
    subtotal        NUMERIC(12,2) NOT NULL,
    created_at      TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    CONSTRAINT quantity_positive CHECK (quantity > 0)
);

CREATE INDEX idx_detail_order ON sales.order_details(order_id);


-- =============================================================================
-- DTE SCHEMA — Electronic Invoicing
-- =============================================================================

-- DTE issuer configuration
CREATE TABLE dte.dte_config (
    id                    SERIAL       PRIMARY KEY,
    environment           CHAR(2)      NOT NULL,  -- '00' test, '01' production
    api_url               VARCHAR(200) NOT NULL,
    issuer_nit            VARCHAR(20)  NOT NULL,
    issuer_name           VARCHAR(200) NOT NULL,
    issuer_nrc            VARCHAR(20),
    activity_code         VARCHAR(10),
    activity_description  VARCHAR(200),
    address               TEXT,
    phone                 VARCHAR(20),
    email                 VARCHAR(100),
    certificate_path      VARCHAR(500),
    certificate_key       TEXT,         -- Encrypted at rest
    is_active             BOOLEAN      NOT NULL DEFAULT TRUE,
    created_at            TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    updated_at            TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    CONSTRAINT environment_valid CHECK (environment IN ('00','01'))
);

CREATE TRIGGER trg_dte_config_timestamp
BEFORE UPDATE ON dte.dte_config
FOR EACH ROW EXECUTE FUNCTION public.fn_update_timestamp();

-- Issued DTEs
CREATE TABLE dte.dte_issued (
    id                BIGSERIAL    PRIMARY KEY,
    order_id          BIGINT       NOT NULL REFERENCES sales.orders(id),
    dte_type          CHAR(2)      NOT NULL,  -- '01','03','05','06'
    control_number    VARCHAR(50)  NOT NULL UNIQUE,
    generation_code   UUID         NOT NULL UNIQUE DEFAULT uuid_generate_v4(),
    reception_stamp   VARCHAR(100),
    mh_status         VARCHAR(20)  NOT NULL DEFAULT 'PENDIENTE',
    -- PENDIENTE → PROCESADO | RECHAZADO | CONTINGENCIA
    json_sent         JSONB        NOT NULL,
    json_response     JSONB,
    payment_method    VARCHAR(20)  NOT NULL DEFAULT 'EFECTIVO',
    receiver_nit      VARCHAR(20),
    receiver_name     VARCHAR(200),
    environment       CHAR(2)      NOT NULL DEFAULT '01',
    sent_at           TIMESTAMPTZ,
    created_at        TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    reprints          SMALLINT     NOT NULL DEFAULT 0,
    CONSTRAINT dte_type_valid  CHECK (dte_type  IN ('01','03','05','06')),
    CONSTRAINT mh_status_valid CHECK (mh_status IN ('PENDIENTE','PROCESADO','RECHAZADO','CONTINGENCIA'))
);

CREATE INDEX idx_dte_order   ON dte.dte_issued(order_id);
CREATE INDEX idx_dte_status  ON dte.dte_issued(mh_status);
CREATE INDEX idx_dte_date    ON dte.dte_issued(created_at);

-- Contingency queue for automatic resend (every 15 min)
CREATE TABLE dte.dte_contingency (
    id              BIGSERIAL   PRIMARY KEY,
    dte_id          BIGINT      NOT NULL REFERENCES dte.dte_issued(id),
    attempts        SMALLINT    NOT NULL DEFAULT 0,
    last_error      TEXT,
    next_attempt_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    is_resolved     BOOLEAN     NOT NULL DEFAULT FALSE,
    resolved_at     TIMESTAMPTZ,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);


-- =============================================================================
-- HR SCHEMA — Employees & Payroll
-- =============================================================================

CREATE TABLE hr.departments (
    id          SERIAL       PRIMARY KEY,
    name        VARCHAR(100) NOT NULL UNIQUE,
    is_active   BOOLEAN      NOT NULL DEFAULT TRUE,
    created_at  TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    updated_at  TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE TRIGGER trg_department_timestamp
BEFORE UPDATE ON hr.departments
FOR EACH ROW EXECUTE FUNCTION public.fn_update_timestamp();

CREATE TABLE hr.positions (
    id             SERIAL       PRIMARY KEY,
    department_id  INTEGER      NOT NULL REFERENCES hr.departments(id),
    name           VARCHAR(100) NOT NULL,
    is_active      BOOLEAN      NOT NULL DEFAULT TRUE,
    created_at     TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    updated_at     TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE TRIGGER trg_position_timestamp
BEFORE UPDATE ON hr.positions
FOR EACH ROW EXECUTE FUNCTION public.fn_update_timestamp();

-- Employees (unified with technicians)
CREATE TABLE hr.employees (
    id              SERIAL        PRIMARY KEY,
    -- Identity
    first_name      VARCHAR(100)  NOT NULL,
    last_name       VARCHAR(100)  NOT NULL,
    dui             VARCHAR(15)   UNIQUE,
    nit             VARCHAR(20)   UNIQUE,
    isss_number     VARCHAR(20),
    nup             VARCHAR(20),
    -- Job
    position_id     INTEGER       REFERENCES hr.positions(id),
    hire_date       DATE          NOT NULL,
    termination_date DATE,
    base_salary     NUMERIC(10,2) NOT NULL,
    contract_type   VARCHAR(20)   NOT NULL DEFAULT 'PLANILLA',
    afp             VARCHAR(50),  -- "CRECER", "CONFIA"
    -- Contact
    phone           VARCHAR(20),
    alt_phone       VARCHAR(20),
    email           VARCHAR(100),
    address         TEXT,
    municipality    VARCHAR(100),
    -- Personal
    marital_status  VARCHAR(20),
    academic_level  VARCHAR(50),
    -- Emergency contact
    emergency_name  VARCHAR(100),
    emergency_phone VARCHAR(20),
    emergency_relationship VARCHAR(50),
    -- POS access
    pin_hash        TEXT,         -- bcrypt(PIN 4 digits, cost=12). NULL = cannot operate POS
    can_sell        BOOLEAN       NOT NULL DEFAULT FALSE,  -- Can process sales/confection
    can_cashier     BOOLEAN       NOT NULL DEFAULT FALSE,  -- Can operate cashier
    -- Status
    is_active       BOOLEAN       NOT NULL DEFAULT TRUE,
    created_at      TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    CONSTRAINT contract_valid CHECK (contract_type IN ('PLANILLA','HONORARIOS'))
);

CREATE TRIGGER trg_employee_timestamp
BEFORE UPDATE ON hr.employees
FOR EACH ROW EXECUTE FUNCTION public.fn_update_timestamp();

-- Monthly payroll (header)
CREATE TABLE hr.payroll (
    id                    SERIAL        PRIMARY KEY,
    period_month          SMALLINT      NOT NULL,
    period_year           SMALLINT      NOT NULL,
    status                VARCHAR(20)   NOT NULL DEFAULT 'BORRADOR',
    -- BORRADOR → APROBADA → PAGADA (immutable once PAGADA)
    total_salaries        NUMERIC(12,2) NOT NULL DEFAULT 0,
    total_isss_employee   NUMERIC(12,2) NOT NULL DEFAULT 0,
    total_afp_employee    NUMERIC(12,2) NOT NULL DEFAULT 0,
    total_isr             NUMERIC(12,2) NOT NULL DEFAULT 0,
    total_deductions      NUMERIC(12,2) NOT NULL DEFAULT 0,
    total_net             NUMERIC(12,2) NOT NULL DEFAULT 0,
    total_isss_employer   NUMERIC(12,2) NOT NULL DEFAULT 0,
    total_afp_employer    NUMERIC(12,2) NOT NULL DEFAULT 0,
    created_at            TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    updated_at            TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    approved_at           TIMESTAMPTZ,
    paid_at               TIMESTAMPTZ,
    UNIQUE (period_month, period_year),
    CONSTRAINT payroll_status_valid CHECK (status IN ('BORRADOR','APROBADA','PAGADA')),
    CONSTRAINT month_valid CHECK (period_month BETWEEN 1 AND 12)
);

CREATE TRIGGER trg_payroll_timestamp
BEFORE UPDATE ON hr.payroll
FOR EACH ROW EXECUTE FUNCTION public.fn_update_timestamp();

-- Payroll detail per employee
CREATE TABLE hr.payroll_details (
    id                   BIGSERIAL     PRIMARY KEY,
    payroll_id           INTEGER       NOT NULL REFERENCES hr.payroll(id),
    employee_id          INTEGER       NOT NULL REFERENCES hr.employees(id),
    -- Income
    base_salary          NUMERIC(10,2) NOT NULL,
    overtime_hours       NUMERIC(5,2)  NOT NULL DEFAULT 0,
    overtime_amount      NUMERIC(10,2) NOT NULL DEFAULT 0,  -- hours × (hourly_rate × 2)
    bonuses              NUMERIC(10,2) NOT NULL DEFAULT 0,
    total_income         NUMERIC(10,2) NOT NULL,
    -- Employee deductions
    isss_employee        NUMERIC(10,2) NOT NULL,  -- 3% of total_income
    afp_employee         NUMERIC(10,2) NOT NULL,  -- 7.25% of total_income
    isr                  NUMERIC(10,2) NOT NULL DEFAULT 0,
    other_deductions     NUMERIC(10,2) NOT NULL DEFAULT 0,
    total_deductions     NUMERIC(10,2) NOT NULL,
    -- Net pay
    net_salary           NUMERIC(10,2) NOT NULL,
    -- Employer cost (not deducted from employee)
    isss_employer        NUMERIC(10,2) NOT NULL,  -- 7.5%
    afp_employer         NUMERIC(10,2) NOT NULL,  -- 8.75%
    created_at           TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    UNIQUE (payroll_id, employee_id)
);


-- =============================================================================
-- SYSTEM SCHEMA — Settings, Printers & Audit
-- =============================================================================

-- General settings (key-value)
CREATE TABLE system.settings (
    key             VARCHAR(100) PRIMARY KEY,
    value           TEXT         NOT NULL,
    description     VARCHAR(200),
    updated_at      TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

-- Printers configuration
CREATE TABLE system.printers (
    id              SERIAL       PRIMARY KEY,
    name            VARCHAR(200) NOT NULL,  -- Windows device name
    connection_type VARCHAR(10)  NOT NULL DEFAULT 'USB',  -- USB, ETHERNET
    ip_address      VARCHAR(15),   -- Only for ETHERNET
    network_port    INTEGER,       -- Default 9100
    paper_width     SMALLINT     NOT NULL DEFAULT 80,  -- 80 or 58 mm
    is_default      BOOLEAN      NOT NULL DEFAULT FALSE,
    is_active       BOOLEAN      NOT NULL DEFAULT TRUE,
    created_at      TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

-- Ensure only one default printer at a time
CREATE UNIQUE INDEX idx_printer_default
    ON system.printers (is_default)
    WHERE is_default = TRUE;

CREATE TRIGGER trg_printer_timestamp
BEFORE UPDATE ON system.printers
FOR EACH ROW EXECUTE FUNCTION public.fn_update_timestamp();

-- WebApp users (for future web application)
CREATE TABLE system.web_users (
    id             SERIAL       PRIMARY KEY,
    username       VARCHAR(50)  NOT NULL UNIQUE,
    email          VARCHAR(100) NOT NULL UNIQUE,
    password_hash  TEXT         NOT NULL,  -- bcrypt(12 rounds)
    role           VARCHAR(20)  NOT NULL DEFAULT 'ADMIN',
    -- ADMIN, ACCOUNTANT, OWNER
    employee_id    INTEGER      REFERENCES hr.employees(id),  -- Link to HR employee
    is_active      BOOLEAN      NOT NULL DEFAULT TRUE,
    last_login_at  TIMESTAMPTZ,
    created_at     TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    updated_at     TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    CONSTRAINT role_valid CHECK (role IN ('ADMIN','ACCOUNTANT','OWNER'))
);

CREATE TRIGGER trg_web_user_timestamp
BEFORE UPDATE ON system.web_users
FOR EACH ROW EXECUTE FUNCTION public.fn_update_timestamp();

-- Action audit log
CREATE TABLE system.audit_log (
    id                BIGSERIAL    PRIMARY KEY,
    table_name        VARCHAR(100) NOT NULL,
    record_id         VARCHAR(50),
    action            VARCHAR(10)  NOT NULL,  -- INSERT, UPDATE, DELETE
    old_data          JSONB,
    new_data          JSONB,
    description       TEXT,
    ip_address        VARCHAR(45),
    created_at        TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_audit_table ON system.audit_log(table_name);
CREATE INDEX idx_audit_date   ON system.audit_log(created_at);


-- =============================================================================
-- INITIAL DATA (Seed)
-- =============================================================================

-- Measurement types
INSERT INTO public.measurement_types (code, name, unit_label, decimals) VALUES
    ('METRO', 'Metros lineales',     'metros', 2),
    ('PIEZA', 'Piezas / unidades',   'piezas', 0),
    ('KIT',   'Kits pre-armados',    'kits',   0),
    ('PESO',  'Kilogramos a granel', 'kg',     3);

-- 17 product families
INSERT INTO public.families (code, name, description) VALUES
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
INSERT INTO public.subfamilies (family_id, code, name)
SELECT id, 'AC', 'Acelerador' FROM public.families WHERE code = '02' UNION ALL
SELECT id, 'CC', 'Cambios'    FROM public.families WHERE code = '02' UNION ALL
SELECT id, 'EM', 'Embrague'   FROM public.families WHERE code = '02' UNION ALL
SELECT id, 'FR', 'Freno'      FROM public.families WHERE code = '02';

-- Subfamilies of Steel Cables (family 01)
INSERT INTO public.subfamilies (family_id, code, name)
SELECT id, 'Cga', 'Cable galvanizado acero' FROM public.families WHERE code = '01' UNION ALL
SELECT id, 'Cin', 'Cable inoxidable'        FROM public.families WHERE code = '01' UNION ALL
SELECT id, 'Cre', 'Cable recubierto PVC'    FROM public.families WHERE code = '01';

-- Sales applications
INSERT INTO sales.applications (code, name) VALUES
    ('VT-01', 'Venta Nueva'),
    ('VT-02', 'Reparación'),
    ('VT-03', 'Garantía'),
    ('RP-01', 'Reposición');

-- Departments and positions
INSERT INTO hr.departments (name) VALUES
    ('Producción'), ('Ventas'), ('Bodega'), ('Administración');

INSERT INTO hr.positions (department_id, name)
SELECT id, 'Técnico de Confección' FROM hr.departments WHERE name = 'Producción' UNION ALL
SELECT id, 'Vendedor'              FROM hr.departments WHERE name = 'Ventas'     UNION ALL
SELECT id, 'Bodeguero'             FROM hr.departments WHERE name = 'Bodega'     UNION ALL
SELECT id, 'Administrador'         FROM hr.departments WHERE name = 'Administración' UNION ALL
SELECT id, 'Gerente'               FROM hr.departments WHERE name = 'Administración';

-- System settings
INSERT INTO system.settings (key, value, description) VALUES
    ('iva_percentage',           '13',                          'IVA vigente en El Salvador (%)'),
    ('currency',                 'USD',                         'Moneda operativa'),
    ('session_timeout_minutes',  '30',                          'Minutos de inactividad antes de cerrar sesión'),
    ('business_name',            'FlexoCable El Salvador',      'Nombre para impresión en tickets'),
    ('business_nit',             '',                            'NIT del emisor para DTE — configurar antes de producción'),
    ('business_nrc',             '',                            'NRC del emisor para DTE'),
    ('business_address',         'San Salvador, El Salvador',   'Dirección para tickets'),
    ('business_phone',           '',                            'Teléfono para tickets'),
    ('ticket_footer_message',    'Gracias por su compra.',      'Mensaje al pie del ticket'),
    ('button_min_size_px',       '90',                          'Tamaño mínimo de botones táctiles'),
    ('font_base_size_pt',        '16',                          'Tamaño base de fuente en puntos');

-- Initial DTE configuration (test environment)
INSERT INTO dte.dte_config (environment, api_url, issuer_nit, issuer_name, is_active)
VALUES (
    '00',
    'https://apifacturatest.mh.gob.sv',
    '0000-000000-000-0',
    'FlexoCable El Salvador, S.A. de C.V.',
    TRUE
);

-- Initial web user (admin)
-- IMPORTANT: change password from WebApp before production
INSERT INTO system.web_users (username, email, password_hash, role)
VALUES (
    'admin',
    'admin@flexocable.com.sv',
    crypt('FlexoAdmin2026!', gen_salt('bf', 12)),
    'ADMIN'
);


-- =============================================================================
-- USEFUL VIEWS
-- =============================================================================

-- Products with stock status (for inventory table with colors)
CREATE OR REPLACE VIEW public.v_products_stock AS
SELECT
    p.id,
    p.code,
    p.description,
    f.name                                      AS family,
    sf.name                                     AS subfamily,
    mt.unit_label                               AS unit,
    mt.decimals,
    p.current_stock,
    p.min_stock,
    p.sale_price,
    CASE
        WHEN p.current_stock = 0                THEN 'AGOTADO'
        WHEN p.current_stock <= p.min_stock       THEN 'BAJO_MINIMO'
        ELSE                                            'OK'
    END                                         AS stock_status
FROM public.products p
JOIN  public.families      f  ON p.family_id        = f.id
LEFT JOIN public.subfamilies sf ON p.subfamily_id    = sf.id
JOIN  public.measurement_types mt ON p.measurement_type_id = mt.id
WHERE p.is_active = TRUE;

-- Today's sales with DTE status (for daily table)
CREATE OR REPLACE VIEW sales.v_sales_today AS
SELECT
    o.id,
    o.order_date,
    o.order_time,
    o.customer_name,
    o.total,
    o.status,
    e.first_name || ' ' || COALESCE(e.last_name,'') AS employee,
    a.name                                          AS application,
    d.reception_stamp,
    d.mh_status                                     AS dte_status,
    d.reprints
FROM sales.orders o
JOIN hr.employees      e ON o.employee_id    = e.id
JOIN sales.applications a ON o.application_id = a.id
LEFT JOIN dte.dte_issued d ON d.order_id      = o.id
WHERE o.order_date = CURRENT_DATE
ORDER BY o.order_time DESC;

-- Active stock alerts (for dashboard)
CREATE OR REPLACE VIEW public.v_active_alerts AS
SELECT
    al.id,
    al.created_at,
    p.code,
    p.description,
    al.current_stock,
    al.min_stock,
    CASE WHEN al.current_stock = 0 THEN 'AGOTADO' ELSE 'BAJO_MINIMO' END AS alert_type
FROM public.stock_alerts al
JOIN public.products p ON al.product_id = p.id
WHERE al.is_resolved = FALSE
ORDER BY al.created_at DESC;

-- Today's KPIs for WebApp dashboard
CREATE OR REPLACE VIEW sales.v_kpis_today AS
SELECT
    COUNT(*)                                     AS total_orders,
    COALESCE(SUM(o.total), 0)                    AS total_amount,
    COALESCE(AVG(o.total), 0)                  AS avg_ticket,
    COUNT(*) FILTER (WHERE d.mh_status = 'PROCESADO') AS dtes_sent,
    COUNT(*) FILTER (WHERE d.mh_status = 'CONTINGENCIA') AS in_contingency
FROM sales.orders o
LEFT JOIN dte.dte_issued d ON d.order_id = o.id
WHERE o.order_date = CURRENT_DATE
  AND o.status NOT IN ('CANCELADA');


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
--   [ ] Update business_nit and business_nrc in system.settings
--   [ ] Update dte.dte_config with real issuer data
--   [ ] Change DTE environment from '00' to '01'
--   [ ] Upload .p12 certificate to server
--   [ ] Change admin web user password
--   [ ] Load complete catalog of 500+ products (additional seed)
--   [ ] Create employees and assign PINs from the app
--   [ ] Configure automatic backup with pg_dump
-- =============================================================================