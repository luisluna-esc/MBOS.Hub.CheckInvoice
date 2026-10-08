-- =====================================================================
-- Datos de demostración para revisar los Reportes (2026-10-08).
--
-- Crea un almacén, productos, proveedores y pastores PROPIOS de la demo y sus movimientos
-- (agosto a octubre 2026): entradas, salidas a crédito, depósitos de Caja por producto y un
-- levantamiento de inventario. No toca datos reales: todo va en el almacén de la demo.
--
-- Cada fila creada se anota en demo_seed_registry; demoDataCleanup.sql borra exactamente eso.
-- Los catálogos que ya existen (departamentos, tipos, unidades) se reutilizan y no se borran.
-- Si la demo ya está cargada, el script se detiene sin hacer nada.
--
-- Mismo cálculo que la aplicación: la entrada actualiza stock y costo promedio, la salida sale
-- al costo promedio y descuenta stock, la cuenta por cobrar es la suma de las líneas de la
-- salida, y cada depósito se reparte por producto (payment_detail).
--
-- Correr:  sudo -u postgres psql -d inventrack -v ON_ERROR_STOP=1 -f demoData.sql
-- =====================================================================

BEGIN;

CREATE TABLE IF NOT EXISTS demo_seed_registry (
    table_name VARCHAR(50) NOT NULL,
    row_id BIGINT NOT NULL,
    created_at TIMESTAMP NOT NULL DEFAULT NOW()
);

DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM demo_seed_registry) THEN
        RAISE EXCEPTION 'La demo ya está cargada. Corre demoDataCleanup.sql antes de volver a cargarla.';
    END IF;
END $$;

-- ---------------------------------------------------------------------
-- Ayudantes (solo existen durante esta sesión)
-- ---------------------------------------------------------------------

CREATE FUNCTION pg_temp.reg(p_table TEXT, p_id BIGINT) RETURNS BIGINT LANGUAGE plpgsql AS $f$
BEGIN
    INSERT INTO demo_seed_registry (table_name, row_id) VALUES (p_table, p_id);
    RETURN p_id;
END $f$;

-- Catálogo simple (department, sub_department, media_type): reutiliza si ya existe el nombre.
CREATE FUNCTION pg_temp.catalog(p_table TEXT, p_name TEXT) RETURNS BIGINT LANGUAGE plpgsql AS $f$
DECLARE v_id BIGINT;
BEGIN
    EXECUTE format('SELECT %I FROM %I WHERE upper(name) = upper($1) ORDER BY 1 LIMIT 1', p_table || '_id', p_table)
        INTO v_id USING p_name;
    IF v_id IS NULL THEN
        EXECUTE format('INSERT INTO %I (name) VALUES ($1) RETURNING %I', p_table, p_table || '_id') INTO v_id USING p_name;
        PERFORM pg_temp.reg(p_table, v_id);
    END IF;
    RETURN v_id;
END $f$;

CREATE FUNCTION pg_temp.lookup(p_table TEXT, p_name TEXT) RETURNS BIGINT LANGUAGE plpgsql AS $f$
DECLARE v_id BIGINT;
BEGIN
    EXECUTE format('SELECT %I FROM %I WHERE upper(name) = upper($1) ORDER BY 1 LIMIT 1', p_table || '_id', p_table)
        INTO v_id USING p_name;
    RETURN v_id;
END $f$;

CREATE FUNCTION pg_temp.period(p_date DATE) RETURNS BIGINT LANGUAGE plpgsql AS $f$
DECLARE v_name TEXT := to_char(p_date, 'YYYY-MM'); v_id BIGINT;
BEGIN
    SELECT warehouse_period_id INTO v_id FROM warehouse_period WHERE name = v_name ORDER BY 1 LIMIT 1;
    IF v_id IS NULL THEN
        -- Se crea pero no se anota: períodos reales podrían usarlo después.
        INSERT INTO warehouse_period (name) VALUES (v_name) RETURNING warehouse_period_id INTO v_id;
    END IF;
    RETURN v_id;
END $f$;

CREATE FUNCTION pg_temp.product(p_code TEXT) RETURNS BIGINT LANGUAGE sql AS $f$
    SELECT p.product_id FROM product p
    JOIN demo_seed_registry r ON r.table_name = 'product' AND r.row_id = p.product_id
    WHERE p.code = p_code
$f$;

CREATE FUNCTION pg_temp.client(p_email TEXT) RETURNS BIGINT LANGUAGE sql AS $f$
    SELECT c.client_id FROM client c JOIN party pa ON pa.party_id = c.party_id WHERE lower(pa.email) = lower(p_email)
$f$;

-- Entrada: líneas como ARRAY[[código, cantidad, costo unitario], ...] (texto).
CREATE FUNCTION pg_temp.receipt(p_warehouse BIGINT, p_supplier BIGINT, p_date DATE, p_invoice TEXT, p_lines TEXT[][])
RETURNS BIGINT LANGUAGE plpgsql AS $f$
DECLARE
    v_id BIGINT; v_total NUMERIC := 0; i INT;
    v_product BIGINT; v_qty NUMERIC; v_cost NUMERIC; v_stock stock%ROWTYPE;
    v_user BIGINT := (SELECT min(app_user_id) FROM app_user);
BEGIN
    FOR i IN 1 .. array_length(p_lines, 1) LOOP
        v_total := v_total + round(p_lines[i][2]::NUMERIC * p_lines[i][3]::NUMERIC, 2);
    END LOOP;

    INSERT INTO receipt (supplier_id, tax_id, warehouse_id, warehouse_period_id, receipt_type_id, invoice_number,
                         description, issue_date, invoice_total, created_at, created_by)
    VALUES (p_supplier, (SELECT pa.tax_id FROM supplier s JOIN party pa USING (party_id) WHERE s.supplier_id = p_supplier),
            p_warehouse, pg_temp.period(p_date), pg_temp.lookup('receipt_type', 'Compras con Factura'), p_invoice,
            'Datos de demostración', p_date, v_total, p_date + TIME '10:00' + INTERVAL '4 hours', v_user)
    RETURNING receipt_id INTO v_id;
    PERFORM pg_temp.reg('receipt', v_id);

    FOR i IN 1 .. array_length(p_lines, 1) LOOP
        v_product := pg_temp.product(p_lines[i][1]);
        v_qty := p_lines[i][2]::NUMERIC;
        v_cost := p_lines[i][3]::NUMERIC;
        INSERT INTO receipt_detail (receipt_id, product_id, quantity, unit_cost, total_cost)
        VALUES (v_id, v_product, v_qty, v_cost, round(v_qty * v_cost, 2));

        SELECT * INTO v_stock FROM stock WHERE warehouse_id = p_warehouse AND product_id = v_product;
        IF NOT FOUND THEN
            INSERT INTO stock (warehouse_id, product_id, quantity, average_cost) VALUES (p_warehouse, v_product, v_qty, v_cost);
        ELSE
            UPDATE stock
            SET average_cost = (quantity * average_cost + v_qty * v_cost) / (quantity + v_qty),
                quantity = quantity + v_qty
            WHERE stock_id = v_stock.stock_id;
        END IF;
    END LOOP;
    RETURN v_id;
END $f$;

-- Salida al costo promedio del stock; si p_due_date no es NULL se envía a Cuentas por Cobrar.
-- Líneas como ARRAY[[código, cantidad], ...].
CREATE FUNCTION pg_temp.issue(p_warehouse BIGINT, p_client BIGINT, p_date DATE, p_lines TEXT[][], p_due_date DATE)
RETURNS BIGINT LANGUAGE plpgsql AS $f$
DECLARE
    v_id BIGINT; v_ar BIGINT; v_total NUMERIC := 0; i INT;
    v_product BIGINT; v_qty NUMERIC; v_cost NUMERIC;
    v_user BIGINT := (SELECT min(app_user_id) FROM app_user);
BEGIN
    INSERT INTO issue (issue_type_id, warehouse_id, warehouse_period_id, client_id, issue_date, print_type_id,
                       description, created_at, created_by)
    VALUES (pg_temp.lookup('issue_type', 'Salida Normal'), p_warehouse, pg_temp.period(p_date), p_client, p_date,
            pg_temp.lookup('print_type', 'Impresion Hoja'), 'Datos de demostración',
            p_date + TIME '15:00' + INTERVAL '4 hours', v_user)
    RETURNING issue_id INTO v_id;
    PERFORM pg_temp.reg('issue', v_id);

    FOR i IN 1 .. array_length(p_lines, 1) LOOP
        v_product := pg_temp.product(p_lines[i][1]);
        v_qty := p_lines[i][2]::NUMERIC;
        SELECT round(average_cost, 2) INTO v_cost FROM stock WHERE warehouse_id = p_warehouse AND product_id = v_product;
        IF v_cost IS NULL OR (SELECT quantity FROM stock WHERE warehouse_id = p_warehouse AND product_id = v_product) < v_qty THEN
            RAISE EXCEPTION 'Stock insuficiente de % para la salida del %', p_lines[i][1], p_date;
        END IF;
        INSERT INTO issue_detail (issue_id, product_id, quantity, unit_cost, total_cost)
        VALUES (v_id, v_product, v_qty, v_cost, round(v_qty * v_cost, 2));
        v_total := v_total + round(v_qty * v_cost, 2);
        UPDATE stock SET quantity = quantity - v_qty WHERE warehouse_id = p_warehouse AND product_id = v_product;
    END LOOP;

    IF p_due_date IS NOT NULL THEN
        INSERT INTO account_receivable (issue_id, client_id, total_amount, outstanding_balance, payment_type,
                                        payment_detail, due_date, status, created_at, created_by)
        VALUES (v_id, p_client, v_total, v_total, 'credit', 'Datos de demostración', p_due_date, 'pending',
                p_date + TIME '15:00' + INTERVAL '4 hours', v_user)
        RETURNING account_receivable_id INTO v_ar;
        PERFORM pg_temp.reg('account_receivable', v_ar);
    END IF;
    RETURN v_id;
END $f$;

-- Depósito de Caja repartido por producto. Líneas como ARRAY[[código, monto], ...].
CREATE FUNCTION pg_temp.deposit(p_issue BIGINT, p_date DATE, p_method TEXT, p_lines TEXT[][])
RETURNS BIGINT LANGUAGE plpgsql AS $f$
DECLARE
    v_ar account_receivable%ROWTYPE; v_payment BIGINT; v_amount NUMERIC := 0; i INT; v_detail issue_detail%ROWTYPE;
    v_user BIGINT := (SELECT min(app_user_id) FROM app_user);
BEGIN
    SELECT * INTO v_ar FROM account_receivable WHERE issue_id = p_issue;
    FOR i IN 1 .. array_length(p_lines, 1) LOOP
        v_amount := v_amount + p_lines[i][2]::NUMERIC;
    END LOOP;

    INSERT INTO payment (account_receivable_id, amount, payment_date, payment_method, notes, created_by)
    VALUES (v_ar.account_receivable_id, v_amount, p_date + TIME '11:30' + INTERVAL '4 hours', p_method,
            'Datos de demostración', v_user)
    RETURNING payment_id INTO v_payment;
    PERFORM pg_temp.reg('payment', v_payment);

    FOR i IN 1 .. array_length(p_lines, 1) LOOP
        SELECT d.* INTO v_detail FROM issue_detail d
        WHERE d.issue_id = p_issue AND d.product_id = pg_temp.product(p_lines[i][1]);
        INSERT INTO payment_detail (payment_id, issue_detail_id, product_id, amount)
        VALUES (v_payment, v_detail.issue_detail_id, v_detail.product_id, p_lines[i][2]::NUMERIC);
    END LOOP;

    UPDATE account_receivable
    SET outstanding_balance = GREATEST(outstanding_balance - v_amount, 0),
        status = CASE WHEN outstanding_balance - v_amount <= 0 THEN 'paid' ELSE 'pending' END
    WHERE account_receivable_id = v_ar.account_receivable_id;
    RETURN v_payment;
END $f$;

-- Lo que falta pagar de un producto de una salida (para pagarlo completo).
CREATE FUNCTION pg_temp.remaining(p_issue BIGINT, p_code TEXT) RETURNS TEXT LANGUAGE sql AS $f$
    SELECT (d.total_cost - COALESCE((SELECT sum(pd.amount) FROM payment_detail pd WHERE pd.issue_detail_id = d.issue_detail_id), 0))::TEXT
    FROM issue_detail d WHERE d.issue_id = p_issue AND d.product_id = pg_temp.product(p_code)
$f$;

-- ---------------------------------------------------------------------
-- Datos
-- ---------------------------------------------------------------------
DO $$
DECLARE
    v_wh BIGINT; v_dep_books BIGINT; v_dep_music BIGINT; v_dep_health BIGINT;
    v_sub_bibles BIGINT; v_sub_lessons BIGINT; v_sub_hymns BIGINT; v_sub_first_aid BIGINT;
    v_book BIGINT; v_magazine BIGINT; v_dvd BIGINT; v_booklet BIGINT;
    v_party BIGINT; v_supplier1 BIGINT; v_supplier2 BIGINT; v_id BIGINT;
    v_ci BIGINT := pg_temp.lookup('document_type', 'Cedula de Identidad');
    v_country BIGINT := pg_temp.lookup('country', 'Bolivia');
    i1 BIGINT; i2 BIGINT; i3 BIGINT; i4 BIGINT; i5 BIGINT; i6 BIGINT;
    v_count BIGINT; v_user BIGINT := (SELECT min(app_user_id) FROM app_user);
    r RECORD;
BEGIN
    -- Almacén propio de la demo
    INSERT INTO warehouse (name, address, is_active)
    VALUES ('ALMACEN DEMO REPORTES', 'Datos de demostración', TRUE) RETURNING warehouse_id INTO v_wh;
    PERFORM pg_temp.reg('warehouse', v_wh);

    -- Catálogos (se reutilizan si ya existen)
    v_dep_books := pg_temp.catalog('department', 'LIBROS - REVISTAS Y FOLLETOS');
    v_dep_music := pg_temp.catalog('department', 'MUSICA');
    v_dep_health := pg_temp.catalog('department', 'SALUD');
    v_sub_bibles := pg_temp.catalog('sub_department', 'BIBLIAS');
    v_sub_lessons := pg_temp.catalog('sub_department', 'LECCIONES');
    v_sub_hymns := pg_temp.catalog('sub_department', 'HIMNARIOS');
    v_sub_first_aid := pg_temp.catalog('sub_department', 'PRIMEROS AUXILIOS');
    v_book := pg_temp.catalog('media_type', 'Libro');
    v_magazine := pg_temp.catalog('media_type', 'Revista');
    v_dvd := pg_temp.catalog('media_type', 'DVD');
    v_booklet := pg_temp.catalog('media_type', 'Folleto');

    -- 8 productos
    FOR r IN SELECT * FROM (VALUES
        ('90001', 'Biblia Reina Valera 1960', v_dep_books, v_sub_bibles, v_book),
        ('90002', 'Biblia de Estudio Andrews', v_dep_books, v_sub_bibles, v_book),
        ('90003', 'Lección de Escuela Sabática Adultos', v_dep_books, v_sub_lessons, v_magazine),
        ('90004', 'Lección de Escuela Sabática Jóvenes', v_dep_books, v_sub_lessons, v_magazine),
        ('90005', 'Himnario Adventista', v_dep_music, v_sub_hymns, v_book),
        ('90006', 'DVD Coros de Alabanza', v_dep_music, v_sub_hymns, v_dvd),
        ('90007', 'Botiquín de Primeros Auxilios', v_dep_health, v_sub_first_aid, v_booklet),
        ('90008', 'Guía de Salud Familiar', v_dep_health, v_sub_first_aid, v_booklet)
    ) AS t(code, name, dep, sub, media) LOOP
        INSERT INTO product (code, name, department_id, sub_department_id, media_type_id, is_active)
        VALUES (r.code, r.name, r.dep, r.sub, r.media, TRUE) RETURNING product_id INTO v_id;
        PERFORM pg_temp.reg('product', v_id);
    END LOOP;

    -- 2 proveedores
    INSERT INTO party (name, tax_id, email, mobile_phone)
    VALUES ('Editorial Demo Esperanza', '9988000101', 'ventas@demo-esperanza.bo', '70000101') RETURNING party_id INTO v_party;
    PERFORM pg_temp.reg('party', v_party);
    INSERT INTO supplier (party_id, code, legal_name, country_id, address, is_active)
    VALUES (v_party, 'DEMO-P1', 'Editorial Demo Esperanza S.R.L.', v_country, 'Datos de demostración', TRUE)
    RETURNING supplier_id INTO v_supplier1;
    PERFORM pg_temp.reg('supplier', v_supplier1);

    INSERT INTO party (name, tax_id, email, mobile_phone)
    VALUES ('Distribuidora Demo Salud y Vida', '9988000102', 'ventas@demo-saludyvida.bo', '70000102') RETURNING party_id INTO v_party;
    PERFORM pg_temp.reg('party', v_party);
    INSERT INTO supplier (party_id, code, legal_name, country_id, address, is_active)
    VALUES (v_party, 'DEMO-P2', 'Distribuidora Demo Salud y Vida Ltda.', v_country, 'Datos de demostración', TRUE)
    RETURNING supplier_id INTO v_supplier2;
    PERFORM pg_temp.reg('supplier', v_supplier2);

    -- 4 pastores
    FOR r IN SELECT * FROM (VALUES
        ('Pr. Juan Mamani Quispe', '9977000201', 'pr.juan.mamani@demo-inventrack.bo', '71000201'),
        ('Pr. Carlos Choque Flores', '9977000202', 'pr.carlos.choque@demo-inventrack.bo', '71000202'),
        ('Pra. María Condori Apaza', '9977000203', 'pra.maria.condori@demo-inventrack.bo', '71000203'),
        ('Pr. Luis Ticona Huanca', '9977000204', 'pr.luis.ticona@demo-inventrack.bo', '71000204')
    ) AS t(name, tax_id, email, phone) LOOP
        INSERT INTO party (name, tax_id, email, mobile_phone) VALUES (r.name, r.tax_id, r.email, r.phone)
        RETURNING party_id INTO v_party;
        PERFORM pg_temp.reg('party', v_party);
        INSERT INTO client (party_id, document_type_id, is_active, is_pastor) VALUES (v_party, v_ci, TRUE, TRUE)
        RETURNING client_id INTO v_id;
        PERFORM pg_temp.reg('client', v_id);
    END LOOP;

    -- Entradas
    PERFORM pg_temp.receipt(v_wh, v_supplier1, DATE '2026-08-04', 'DEMO-F-1001', ARRAY[
        ['90001', '120', '45.00'], ['90002', '30', '180.00'], ['90003', '200', '12.00'],
        ['90004', '150', '12.00'], ['90005', '80', '55.00'], ['90006', '40', '35.00']]);
    PERFORM pg_temp.receipt(v_wh, v_supplier2, DATE '2026-08-06', 'DEMO-F-2001', ARRAY[
        ['90007', '25', '120.00'], ['90008', '60', '25.00']]);
    PERFORM pg_temp.receipt(v_wh, v_supplier1, DATE '2026-09-02', 'DEMO-F-1002', ARRAY[
        ['90001', '60', '48.00'], ['90003', '150', '12.50'], ['90004', '100', '12.50']]);
    -- Entrada de octubre: el panel de Inicio muestra los ingresos del mes en curso.
    PERFORM pg_temp.receipt(v_wh, v_supplier2, DATE '2026-10-05', 'DEMO-F-2002', ARRAY[
        ['90007', '10', '125.00'], ['90008', '40', '24.00']]);

    -- Salidas (5 a crédito, 1 de contado)
    i1 := pg_temp.issue(v_wh, pg_temp.client('pr.juan.mamani@demo-inventrack.bo'), DATE '2026-08-12',
        ARRAY[['90001', '15'], ['90003', '40']], DATE '2026-09-30');
    i2 := pg_temp.issue(v_wh, pg_temp.client('pr.carlos.choque@demo-inventrack.bo'), DATE '2026-08-20',
        ARRAY[['90005', '10'], ['90006', '6']], DATE '2026-09-20');
    i3 := pg_temp.issue(v_wh, pg_temp.client('pra.maria.condori@demo-inventrack.bo'), DATE '2026-09-10',
        ARRAY[['90002', '4'], ['90007', '3'], ['90008', '10']], DATE '2026-11-30');
    i4 := pg_temp.issue(v_wh, pg_temp.client('pr.juan.mamani@demo-inventrack.bo'), DATE '2026-09-24',
        ARRAY[['90004', '60']], DATE '2026-12-15');
    i5 := pg_temp.issue(v_wh, pg_temp.client('pr.luis.ticona@demo-inventrack.bo'), DATE '2026-10-03',
        ARRAY[['90001', '20'], ['90005', '12'], ['90006', '5']], DATE '2026-12-31');
    i6 := pg_temp.issue(v_wh, pg_temp.client('pr.carlos.choque@demo-inventrack.bo'), DATE '2026-09-15',
        ARRAY[['90008', '8']], NULL);

    -- Depósitos de Caja, por producto
    -- i1: un depósito parcial y la fecha límite ya pasó -> "Pago retrasado"
    PERFORM pg_temp.deposit(i1, DATE '2026-09-05', 'cash', ARRAY[['90001', '300.00'], ['90003', '200.00']]);
    -- i2: dos depósitos que la pagan completa -> "Pagada"
    PERFORM pg_temp.deposit(i2, DATE '2026-09-01', 'transfer', ARRAY[['90005', pg_temp.remaining(i2, '90005')]]);
    PERFORM pg_temp.deposit(i2, DATE '2026-09-18', 'cash', ARRAY[['90006', pg_temp.remaining(i2, '90006')]]);
    -- i3: parcial, todavía en plazo -> "Pendiente"
    PERFORM pg_temp.deposit(i3, DATE '2026-09-28', 'transfer', ARRAY[['90002', '400.00'], ['90008', pg_temp.remaining(i3, '90008')]]);
    -- i5: un depósito en octubre -> "Pendiente"
    PERFORM pg_temp.deposit(i5, DATE '2026-10-06', 'cash', ARRAY[['90001', '450.00'], ['90006', pg_temp.remaining(i5, '90006')]]);

    -- Levantamiento de inventario de septiembre (cuadra con el sistema, sin diferencias)
    INSERT INTO inventory_count (warehouse_id, start_date, end_date, count_date, status, created_by, created_at, closed_by, closed_at)
    VALUES (v_wh, DATE '2026-09-01', DATE '2026-09-30', TIMESTAMP '2026-09-30 22:00', 'closed', v_user,
            TIMESTAMP '2026-09-30 22:00', v_user, TIMESTAMP '2026-09-30 22:00')
    RETURNING inventory_count_id INTO v_count;
    PERFORM pg_temp.reg('inventory_count', v_count);

    -- Stock al 30/09: se deshacen los movimientos de octubre (la salida i5 y la entrada de octubre).
    INSERT INTO inventory_count_detail (inventory_count_id, product_id, system_quantity, unit_value, physical_quantity, difference, notes)
    SELECT v_count, s.product_id,
           s.quantity + COALESCE((SELECT sum(d.quantity) FROM issue_detail d WHERE d.issue_id = i5 AND d.product_id = s.product_id), 0)
                      - COALESCE((SELECT sum(d.quantity) FROM receipt_detail d JOIN receipt rc USING (receipt_id)
                                  WHERE rc.warehouse_id = v_wh AND rc.issue_date >= DATE '2026-10-01' AND d.product_id = s.product_id), 0),
           s.average_cost,
           s.quantity + COALESCE((SELECT sum(d.quantity) FROM issue_detail d WHERE d.issue_id = i5 AND d.product_id = s.product_id), 0)
                      - COALESCE((SELECT sum(d.quantity) FROM receipt_detail d JOIN receipt rc USING (receipt_id)
                                  WHERE rc.warehouse_id = v_wh AND rc.issue_date >= DATE '2026-10-01' AND d.product_id = s.product_id), 0),
           0, 'Conteo de demostración'
    FROM stock s WHERE s.warehouse_id = v_wh;
END $$;

-- Resumen de lo cargado
SELECT table_name AS tabla, count(*) AS filas FROM demo_seed_registry GROUP BY table_name ORDER BY table_name;

COMMIT;
