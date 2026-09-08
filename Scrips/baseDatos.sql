-- =====================================================================
-- SISTEMA DE INVENTARIO - SCRIPT CONSOLIDADO POSTGRESQL
-- Orden de creación respetando dependencias de Foreign Keys
-- =====================================================================

-- =====================================================================
-- NIVEL 1: TABLAS SIN DEPENDENCIAS (catálogos base)
-- =====================================================================

CREATE TABLE configuracion (
    configuracion_id BIGSERIAL PRIMARY KEY,
    clave VARCHAR(100) NOT NULL,
    valor VARCHAR(255) NOT NULL,
    descripcion VARCHAR(255),
    CONSTRAINT uq_configuracion_clave UNIQUE (clave)
);

CREATE TABLE pais (
    pais_id BIGSERIAL PRIMARY KEY,
    nombre VARCHAR(100) NOT NULL
);

CREATE TABLE tipo_documento (
    tipo_documento_id BIGSERIAL PRIMARY KEY,
    nombre VARCHAR(50) NOT NULL
);

CREATE TABLE tipo_iglesia (
    tipo_iglesia_id BIGSERIAL PRIMARY KEY,
    nombre VARCHAR(50) NOT NULL
);

CREATE TABLE tipo_salida (
    tipo_salida_id BIGSERIAL PRIMARY KEY,
    nombre VARCHAR(50) NOT NULL,
    activo BOOLEAN NOT NULL DEFAULT TRUE
);

CREATE TABLE tipo_entrada (
    tipo_entrada_id BIGSERIAL PRIMARY KEY,
    nombre VARCHAR(50) NOT NULL,
    activo BOOLEAN NOT NULL DEFAULT TRUE
);

CREATE TABLE tipo_impresion (
    tipo_impresion_id BIGSERIAL PRIMARY KEY,
    nombre VARCHAR(50) NOT NULL
);

CREATE TABLE tipo_media (
    tipo_media_id BIGSERIAL PRIMARY KEY,
    nombre VARCHAR(50) NOT NULL,
    activo BOOLEAN NOT NULL DEFAULT TRUE
);

CREATE TABLE departamento (
    departamento_id BIGSERIAL PRIMARY KEY,
    nombre VARCHAR(100) NOT NULL,
    activo BOOLEAN NOT NULL DEFAULT TRUE
);

CREATE TABLE sub_departamento (
    sub_departamento_id BIGSERIAL PRIMARY KEY,
    nombre VARCHAR(100) NOT NULL,
    activo BOOLEAN NOT NULL DEFAULT TRUE
);

CREATE TABLE mision (
    mision_id BIGSERIAL PRIMARY KEY,
    nombre VARCHAR(100) NOT NULL,
    activo BOOLEAN NOT NULL DEFAULT TRUE
);

CREATE TABLE provincia (
    provincia_id BIGSERIAL PRIMARY KEY,
    nombre VARCHAR(100) NOT NULL,
    activo BOOLEAN NOT NULL DEFAULT TRUE
);

CREATE TABLE periodo_almacen (
    periodo_almacen_id BIGSERIAL PRIMARY KEY,
    nombre VARCHAR(20) NOT NULL,
    esta_cerrado BOOLEAN NOT NULL DEFAULT FALSE,
    cerrado_en TIMESTAMP,
    cerrado_por BIGINT REFERENCES usuario_app(usuario_app_id)
);

CREATE TABLE almacen (
    almacen_id BIGSERIAL PRIMARY KEY,
    nombre VARCHAR(100) NOT NULL,
    direccion VARCHAR(255),
    activo BOOLEAN NOT NULL DEFAULT TRUE
);

CREATE TABLE permiso (
    permiso_id BIGSERIAL PRIMARY KEY,
    codigo VARCHAR(100) NOT NULL,
    nombre VARCHAR(100) NOT NULL,
    descripcion VARCHAR(255),
    modulo VARCHAR(50) NOT NULL,
    activo BOOLEAN NOT NULL DEFAULT TRUE,
    fecha_creacion TIMESTAMP NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_permiso_codigo UNIQUE (codigo)
);

CREATE TABLE embarque (
    embarque_id BIGSERIAL PRIMARY KEY,
    numero INT NOT NULL,
    fecha DATE NOT NULL,
    descripcion VARCHAR(255)
);

-- =====================================================================
-- NIVEL 2: USUARIO (auto-referenciada)
-- =====================================================================

CREATE TABLE usuario (
    usuario_id BIGSERIAL PRIMARY KEY,
    nombre VARCHAR(100) NOT NULL,
    apellido VARCHAR(100) NOT NULL,
    correo VARCHAR(150) NOT NULL,
    nombre_usuario VARCHAR(50) NOT NULL,
    hash_contrasena VARCHAR(255) NOT NULL,
    ultimo_inicio_sesion TIMESTAMP,
    activo BOOLEAN NOT NULL DEFAULT TRUE,
    fecha_creacion TIMESTAMP NOT NULL DEFAULT NOW(),
    usuario_creacion BIGINT,
    fecha_modificacion TIMESTAMP,
    usuario_modificacion BIGINT,
    CONSTRAINT uq_usuario_correo UNIQUE (correo),
    CONSTRAINT uq_usuario_nombre_usuario UNIQUE (nombre_usuario),
    CONSTRAINT fk_usuario_creacion FOREIGN KEY (usuario_creacion) REFERENCES usuario(usuario_id),
    CONSTRAINT fk_usuario_modificacion FOREIGN KEY (usuario_modificacion) REFERENCES usuario(usuario_id)
);

-- =====================================================================
-- NIVEL 3: dependen de usuario
-- =====================================================================

CREATE TABLE rol (
    rol_id BIGSERIAL PRIMARY KEY,
    nombre VARCHAR(50) NOT NULL,
    descripcion VARCHAR(255),
    activo BOOLEAN NOT NULL DEFAULT TRUE,
    fecha_creacion TIMESTAMP NOT NULL DEFAULT NOW(),
    usuario_creacion BIGINT,
    fecha_modificacion TIMESTAMP,
    usuario_modificacion BIGINT,
    CONSTRAINT uq_rol_nombre UNIQUE (nombre),
    CONSTRAINT fk_rol_usuario_creacion FOREIGN KEY (usuario_creacion) REFERENCES usuario(usuario_id),
    CONSTRAINT fk_rol_usuario_modificacion FOREIGN KEY (usuario_modificacion) REFERENCES usuario(usuario_id)
);

CREATE TABLE refresh_token (
    refresh_token_id BIGSERIAL PRIMARY KEY,
    usuario_id BIGINT NOT NULL,
    token VARCHAR(500) NOT NULL,
    fecha_expiracion TIMESTAMP NOT NULL,
    revocado BOOLEAN NOT NULL DEFAULT FALSE,
    fecha_creacion TIMESTAMP NOT NULL DEFAULT NOW(),
    creado_ip VARCHAR(50),
    CONSTRAINT fk_refresh_token_usuario FOREIGN KEY (usuario_id) REFERENCES usuario(usuario_id) ON DELETE CASCADE,
    CONSTRAINT uq_refresh_token_token UNIQUE (token)
);

CREATE TABLE distrito (
    distrito_id BIGSERIAL PRIMARY KEY,
    codigo VARCHAR(20),
    nombre_distrito VARCHAR(100) NOT NULL,
    mision_id BIGINT,
    provincia_id BIGINT,
    usuario_id BIGINT,
    fecha_registro TIMESTAMP NOT NULL DEFAULT NOW(),
    activo BOOLEAN NOT NULL DEFAULT TRUE,
    CONSTRAINT fk_distrito_mision FOREIGN KEY (mision_id) REFERENCES mision(mision_id),
    CONSTRAINT fk_distrito_provincia FOREIGN KEY (provincia_id) REFERENCES provincia(provincia_id),
    CONSTRAINT fk_distrito_usuario FOREIGN KEY (usuario_id) REFERENCES usuario(usuario_id)
);

CREATE TABLE auditoria (
    auditoria_id BIGSERIAL PRIMARY KEY,
    usuario_id BIGINT,
    nombre_tabla VARCHAR(100) NOT NULL,
    registro_id BIGINT,
    fecha TIMESTAMP NOT NULL DEFAULT NOW(),
    accion VARCHAR(20) NOT NULL,
    CONSTRAINT fk_auditoria_usuario FOREIGN KEY (usuario_id) REFERENCES usuario(usuario_id)
);

CREATE INDEX idx_auditoria_usuario ON auditoria(usuario_id);
CREATE INDEX idx_auditoria_nombre_tabla ON auditoria(nombre_tabla);

-- =====================================================================
-- NIVEL 4: relaciones usuario-rol-permiso, menu, iglesia
-- =====================================================================

CREATE TABLE usuario_rol (
    usuario_rol_id BIGSERIAL PRIMARY KEY,
    usuario_id BIGINT NOT NULL,
    rol_id BIGINT NOT NULL,
    fecha_creacion TIMESTAMP NOT NULL DEFAULT NOW(),
    usuario_creacion BIGINT,
    CONSTRAINT fk_usuario_rol_usuario FOREIGN KEY (usuario_id) REFERENCES usuario(usuario_id) ON DELETE CASCADE,
    CONSTRAINT fk_usuario_rol_rol FOREIGN KEY (rol_id) REFERENCES rol(rol_id) ON DELETE CASCADE,
    CONSTRAINT fk_usuario_rol_creacion FOREIGN KEY (usuario_creacion) REFERENCES usuario(usuario_id),
    CONSTRAINT uq_usuario_rol UNIQUE (usuario_id, rol_id)
);

CREATE INDEX idx_usuario_rol_usuario ON usuario_rol(usuario_id);
CREATE INDEX idx_usuario_rol_rol ON usuario_rol(rol_id);

CREATE TABLE rol_permiso (
    rol_permiso_id BIGSERIAL PRIMARY KEY,
    rol_id BIGINT NOT NULL,
    permiso_id BIGINT NOT NULL,
    fecha_creacion TIMESTAMP NOT NULL DEFAULT NOW(),
    usuario_creacion BIGINT,
    CONSTRAINT fk_rol_permiso_rol FOREIGN KEY (rol_id) REFERENCES rol(rol_id) ON DELETE CASCADE,
    CONSTRAINT fk_rol_permiso_permiso FOREIGN KEY (permiso_id) REFERENCES permiso(permiso_id) ON DELETE CASCADE,
    CONSTRAINT fk_rol_permiso_creacion FOREIGN KEY (usuario_creacion) REFERENCES usuario(usuario_id),
    CONSTRAINT uq_rol_permiso UNIQUE (rol_id, permiso_id)
);

CREATE INDEX idx_rol_permiso_rol ON rol_permiso(rol_id);
CREATE INDEX idx_rol_permiso_permiso ON rol_permiso(permiso_id);

CREATE TABLE menu (
    menu_id BIGSERIAL PRIMARY KEY,
    nombre VARCHAR(100) NOT NULL,
    ruta VARCHAR(150),
    icono VARCHAR(50),
    menu_padre_id BIGINT,
    orden INT NOT NULL DEFAULT 0,
    activo BOOLEAN NOT NULL DEFAULT TRUE,
    permiso_id BIGINT,
    CONSTRAINT fk_menu_padre FOREIGN KEY (menu_padre_id) REFERENCES menu(menu_id) ON DELETE CASCADE,
    CONSTRAINT fk_menu_permiso FOREIGN KEY (permiso_id) REFERENCES permiso(permiso_id)
);

CREATE INDEX idx_menu_padre ON menu(menu_padre_id);
CREATE INDEX idx_menu_permiso ON menu(permiso_id);

CREATE TABLE iglesia (
    iglesia_id BIGSERIAL PRIMARY KEY,
    codigo VARCHAR(20),
    nombre_iglesia VARCHAR(150) NOT NULL,
    distrito_id BIGINT,
    tipo_iglesia_id BIGINT,
    activo BOOLEAN NOT NULL DEFAULT TRUE,
    CONSTRAINT fk_iglesia_distrito FOREIGN KEY (distrito_id) REFERENCES distrito(distrito_id),
    CONSTRAINT fk_iglesia_tipo_iglesia FOREIGN KEY (tipo_iglesia_id) REFERENCES tipo_iglesia(tipo_iglesia_id)
);

CREATE INDEX idx_iglesia_distrito ON iglesia(distrito_id);

-- =====================================================================
-- NIVEL 5: cliente, proveedor, producto
-- =====================================================================

CREATE TABLE cliente (
    cliente_id BIGSERIAL PRIMARY KEY,
    usuario_id BIGINT,
    tipo_documento_id BIGINT,
    nit VARCHAR(20),
    nombre VARCHAR(150) NOT NULL,
    correo VARCHAR(150),
    celular VARCHAR(20),
    distrito_id BIGINT,
    iglesia_id BIGINT,
    activo BOOLEAN NOT NULL DEFAULT TRUE,
    complemento VARCHAR(10),
    CONSTRAINT fk_cliente_usuario FOREIGN KEY (usuario_id) REFERENCES usuario(usuario_id),
    CONSTRAINT fk_cliente_tipo_documento FOREIGN KEY (tipo_documento_id) REFERENCES tipo_documento(tipo_documento_id),
    CONSTRAINT fk_cliente_distrito FOREIGN KEY (distrito_id) REFERENCES distrito(distrito_id),
    CONSTRAINT fk_cliente_iglesia FOREIGN KEY (iglesia_id) REFERENCES iglesia(iglesia_id),
    CONSTRAINT uq_cliente_nit UNIQUE (nit)
);

CREATE INDEX idx_cliente_distrito ON cliente(distrito_id);
CREATE INDEX idx_cliente_iglesia ON cliente(iglesia_id);

CREATE TABLE proveedor (
    proveedor_id BIGSERIAL PRIMARY KEY,
    codigo VARCHAR(20),
    razon_social VARCHAR(150),
    nombre VARCHAR(150) NOT NULL,
    nit VARCHAR(20),
    pais_id BIGINT,
    direccion VARCHAR(255),
    telefono VARCHAR(20),
    celular VARCHAR(20),
    correo VARCHAR(150),
    observacion VARCHAR(255),
    activo BOOLEAN NOT NULL DEFAULT TRUE,
    CONSTRAINT fk_proveedor_pais FOREIGN KEY (pais_id) REFERENCES pais(pais_id)
);

CREATE TABLE producto (
    producto_id BIGSERIAL PRIMARY KEY,
    codigo VARCHAR(20),
    nombre VARCHAR(200) NOT NULL,
    departamento_id BIGINT,
    sub_departamento_id BIGINT,
    tipo_media_id BIGINT,
    unidad_medida VARCHAR(20),
    precio NUMERIC(12,2),
    stock_minimo NUMERIC(12,2),
    stock_maximo NUMERIC(12,2),
    activo BOOLEAN NOT NULL DEFAULT TRUE,
    CONSTRAINT fk_producto_departamento FOREIGN KEY (departamento_id) REFERENCES departamento(departamento_id),
    CONSTRAINT fk_producto_sub_departamento FOREIGN KEY (sub_departamento_id) REFERENCES sub_departamento(sub_departamento_id),
    CONSTRAINT fk_producto_tipo_media FOREIGN KEY (tipo_media_id) REFERENCES tipo_media(tipo_media_id)
);

CREATE INDEX idx_producto_departamento ON producto(departamento_id);
CREATE INDEX idx_producto_sub_departamento ON producto(sub_departamento_id);
CREATE INDEX idx_producto_codigo ON producto(codigo);

-- =====================================================================
-- NIVEL 6: transaccionales (cabeceras) y relaciones de nivel medio
-- =====================================================================

CREATE TABLE cliente_distrito_historial (
    cliente_distrito_historial_id BIGSERIAL PRIMARY KEY,
    cliente_id BIGINT NOT NULL,
    distrito_id BIGINT NOT NULL,
    fecha_inicio DATE NOT NULL,
    fecha_fin DATE,
    CONSTRAINT fk_cdh_cliente FOREIGN KEY (cliente_id) REFERENCES cliente(cliente_id),
    CONSTRAINT fk_cdh_distrito FOREIGN KEY (distrito_id) REFERENCES distrito(distrito_id)
);

CREATE INDEX idx_cdh_cliente ON cliente_distrito_historial(cliente_id);
CREATE INDEX idx_cdh_distrito ON cliente_distrito_historial(distrito_id);

CREATE TABLE entrada (
    entrada_id BIGSERIAL PRIMARY KEY,
    proveedor_id BIGINT,
    nit VARCHAR(20),
    almacen_id BIGINT,
    periodo_almacen_id BIGINT,
    tipo_entrada_id BIGINT,
    factura VARCHAR(50),
    descripcion VARCHAR(255),
    fecha_emision TIMESTAMP NOT NULL DEFAULT NOW(),
    total_factura NUMERIC(12,2),
    CONSTRAINT fk_entrada_proveedor FOREIGN KEY (proveedor_id) REFERENCES proveedor(proveedor_id),
    CONSTRAINT fk_entrada_almacen FOREIGN KEY (almacen_id) REFERENCES almacen(almacen_id),
    CONSTRAINT fk_entrada_periodo_almacen FOREIGN KEY (periodo_almacen_id) REFERENCES periodo_almacen(periodo_almacen_id),
    CONSTRAINT fk_entrada_tipo_entrada FOREIGN KEY (tipo_entrada_id) REFERENCES tipo_entrada(tipo_entrada_id)
);

CREATE INDEX idx_entrada_almacen ON entrada(almacen_id);
CREATE INDEX idx_entrada_fecha ON entrada(fecha_emision);
CREATE INDEX idx_entrada_proveedor ON entrada(proveedor_id);

CREATE TABLE salida (
    salida_id BIGSERIAL PRIMARY KEY,
    tipo_salida_id BIGINT,
    almacen_id BIGINT,
    periodo_almacen_id BIGINT,
    cliente_id BIGINT,
    complemento VARCHAR(10),
    fecha_emision TIMESTAMP NOT NULL DEFAULT NOW(),
    tipo_impresion_id BIGINT,
    descripcion VARCHAR(255),
    CONSTRAINT fk_salida_tipo_salida FOREIGN KEY (tipo_salida_id) REFERENCES tipo_salida(tipo_salida_id),
    CONSTRAINT fk_salida_almacen FOREIGN KEY (almacen_id) REFERENCES almacen(almacen_id),
    CONSTRAINT fk_salida_periodo_almacen FOREIGN KEY (periodo_almacen_id) REFERENCES periodo_almacen(periodo_almacen_id),
    CONSTRAINT fk_salida_cliente FOREIGN KEY (cliente_id) REFERENCES cliente(cliente_id),
    CONSTRAINT fk_salida_tipo_impresion FOREIGN KEY (tipo_impresion_id) REFERENCES tipo_impresion(tipo_impresion_id)
);

CREATE INDEX idx_salida_almacen ON salida(almacen_id);
CREATE INDEX idx_salida_fecha ON salida(fecha_emision);
CREATE INDEX idx_salida_cliente ON salida(cliente_id);

CREATE TABLE transferencia (
    transferencia_id BIGSERIAL PRIMARY KEY,
    almacen_origen_id BIGINT,
    almacen_destino_id BIGINT,
    usuario_emisor_id BIGINT,
    usuario_receptor_id BIGINT,
    fecha TIMESTAMP NOT NULL DEFAULT NOW(),
    observacion TEXT,
    CONSTRAINT fk_transferencia_almacen_origen FOREIGN KEY (almacen_origen_id) REFERENCES almacen(almacen_id),
    CONSTRAINT fk_transferencia_almacen_destino FOREIGN KEY (almacen_destino_id) REFERENCES almacen(almacen_id),
    CONSTRAINT fk_transferencia_usuario_emisor FOREIGN KEY (usuario_emisor_id) REFERENCES usuario(usuario_id),
    CONSTRAINT fk_transferencia_usuario_receptor FOREIGN KEY (usuario_receptor_id) REFERENCES usuario(usuario_id)
);

CREATE INDEX idx_transferencia_fecha ON transferencia(fecha);

CREATE TABLE stock (
    stock_id BIGSERIAL PRIMARY KEY,
    almacen_id BIGINT NOT NULL,
    producto_id BIGINT NOT NULL,
    cantidad NUMERIC(12,2) NOT NULL DEFAULT 0,
    costo_promedio NUMERIC(12,4) NOT NULL DEFAULT 0,
    CONSTRAINT fk_stock_almacen FOREIGN KEY (almacen_id) REFERENCES almacen(almacen_id),
    CONSTRAINT fk_stock_producto FOREIGN KEY (producto_id) REFERENCES producto(producto_id),
    CONSTRAINT uq_stock_almacen_producto UNIQUE (almacen_id, producto_id)
);

CREATE INDEX idx_stock_producto ON stock(producto_id);

CREATE TABLE deposito (
    deposito_id BIGSERIAL PRIMARY KEY,
    cliente_id BIGINT,
    producto_id BIGINT,
    embarque_id BIGINT,
    numero_recibo VARCHAR(20),
    fecha DATE NOT NULL DEFAULT CURRENT_DATE,
    monto NUMERIC(12,2) NOT NULL,
    observacion VARCHAR(255),
    CONSTRAINT fk_deposito_cliente FOREIGN KEY (cliente_id) REFERENCES cliente(cliente_id),
    CONSTRAINT fk_deposito_producto FOREIGN KEY (producto_id) REFERENCES producto(producto_id),
    CONSTRAINT fk_deposito_embarque FOREIGN KEY (embarque_id) REFERENCES embarque(embarque_id)
);

CREATE INDEX idx_deposito_cliente ON deposito(cliente_id);

CREATE TABLE descuento (
    descuento_id BIGSERIAL PRIMARY KEY,
    cliente_id BIGINT,
    origen_tabla VARCHAR(50) NOT NULL,
    origen_id BIGINT NOT NULL,
    monto NUMERIC(12,2) NOT NULL,
    descripcion VARCHAR(255),
    fecha DATE NOT NULL DEFAULT CURRENT_DATE,
    estado VARCHAR(20) NOT NULL DEFAULT 'pendiente',
    CONSTRAINT fk_descuento_cliente FOREIGN KEY (cliente_id) REFERENCES cliente(cliente_id)
);

CREATE INDEX idx_descuento_cliente ON descuento(cliente_id);
CREATE INDEX idx_descuento_estado ON descuento(estado);

CREATE TABLE levantamiento_inventario (
    levantamiento_inventario_id BIGSERIAL PRIMARY KEY,
    almacen_id BIGINT NOT NULL,
    levantamiento_origen_id BIGINT,
    fecha_inicio DATE NOT NULL,
    fecha_fin DATE NOT NULL,
    fecha_conteo TIMESTAMP NOT NULL DEFAULT NOW(),
    estado VARCHAR(20) NOT NULL DEFAULT 'cerrado',
    usuario_creacion BIGINT,
    fecha_creacion TIMESTAMP NOT NULL DEFAULT NOW(),
    usuario_cierre BIGINT,
    fecha_cierre TIMESTAMP,
    CONSTRAINT fk_levantamiento_almacen FOREIGN KEY (almacen_id) REFERENCES almacen(almacen_id),
    CONSTRAINT fk_levantamiento_origen FOREIGN KEY (levantamiento_origen_id) REFERENCES levantamiento_inventario(levantamiento_inventario_id),
    CONSTRAINT fk_levantamiento_usuario_creacion FOREIGN KEY (usuario_creacion) REFERENCES usuario(usuario_id),
    CONSTRAINT fk_levantamiento_usuario_cierre FOREIGN KEY (usuario_cierre) REFERENCES usuario(usuario_id)
);

CREATE INDEX idx_levantamiento_almacen ON levantamiento_inventario(almacen_id);

-- =====================================================================
-- NIVEL 7: detalles (dependen de las cabeceras del nivel 6)
-- =====================================================================

CREATE TABLE entrada_detalle (
    entrada_detalle_id BIGSERIAL PRIMARY KEY,
    entrada_id BIGINT NOT NULL,
    producto_id BIGINT NOT NULL,
    cantidad NUMERIC(12,2) NOT NULL,
    costo_unitario NUMERIC(12,2) NOT NULL,
    costo_total NUMERIC(12,2) NOT NULL,
    orden_trabajo VARCHAR(50),
    detalle VARCHAR(255),
    CONSTRAINT fk_entrada_detalle_entrada FOREIGN KEY (entrada_id) REFERENCES entrada(entrada_id) ON DELETE CASCADE,
    CONSTRAINT fk_entrada_detalle_producto FOREIGN KEY (producto_id) REFERENCES producto(producto_id)
);

CREATE INDEX idx_entrada_detalle_entrada ON entrada_detalle(entrada_id);
CREATE INDEX idx_entrada_detalle_producto ON entrada_detalle(producto_id);

CREATE TABLE salida_detalle (
    salida_detalle_id BIGSERIAL PRIMARY KEY,
    salida_id BIGINT NOT NULL,
    producto_id BIGINT NOT NULL,
    cantidad NUMERIC(12,2) NOT NULL,
    costo_unitario NUMERIC(12,2) NOT NULL,
    costo_total NUMERIC(12,2) NOT NULL,
    CONSTRAINT fk_salida_detalle_salida FOREIGN KEY (salida_id) REFERENCES salida(salida_id) ON DELETE CASCADE,
    CONSTRAINT fk_salida_detalle_producto FOREIGN KEY (producto_id) REFERENCES producto(producto_id)
);

CREATE INDEX idx_salida_detalle_salida ON salida_detalle(salida_id);
CREATE INDEX idx_salida_detalle_producto ON salida_detalle(producto_id);

CREATE TABLE transferencia_detalle (
    transferencia_detalle_id BIGSERIAL PRIMARY KEY,
    transferencia_id BIGINT NOT NULL,
    producto_id BIGINT NOT NULL,
    cantidad NUMERIC(12,2) NOT NULL,
    precio_unitario NUMERIC(12,2),
    total_pvp NUMERIC(12,2),
    CONSTRAINT fk_transferencia_detalle_transferencia FOREIGN KEY (transferencia_id) REFERENCES transferencia(transferencia_id) ON DELETE CASCADE,
    CONSTRAINT fk_transferencia_detalle_producto FOREIGN KEY (producto_id) REFERENCES producto(producto_id)
);

CREATE INDEX idx_transferencia_detalle_transferencia ON transferencia_detalle(transferencia_id);

CREATE TABLE cuenta_por_cobrar (
    cuenta_por_cobrar_id BIGSERIAL PRIMARY KEY,
    salida_id BIGINT,
    cliente_id BIGINT,
    monto_total NUMERIC(12,2) NOT NULL,
    saldo_pendiente NUMERIC(12,2) NOT NULL,
    tipo_pago VARCHAR(20) NOT NULL,
    fecha_limite DATE,
    estado VARCHAR(20) NOT NULL DEFAULT 'pendiente',
    fecha_creacion TIMESTAMP NOT NULL DEFAULT NOW(),
    usuario_creacion BIGINT,
    CONSTRAINT fk_cxc_salida FOREIGN KEY (salida_id) REFERENCES salida(salida_id),
    CONSTRAINT fk_cxc_cliente FOREIGN KEY (cliente_id) REFERENCES cliente(cliente_id),
    CONSTRAINT fk_cxc_usuario_creacion FOREIGN KEY (usuario_creacion) REFERENCES usuario(usuario_id)
);

CREATE INDEX idx_cxc_cliente ON cuenta_por_cobrar(cliente_id);
CREATE INDEX idx_cxc_estado ON cuenta_por_cobrar(estado);

CREATE TABLE levantamiento_inventario_detalle (
    levantamiento_inventario_detalle_id BIGSERIAL PRIMARY KEY,
    levantamiento_inventario_id BIGINT NOT NULL,
    producto_id BIGINT NOT NULL,
    cantidad_sistema NUMERIC(12,2) NOT NULL,
    valor_unitario NUMERIC(12,4),
    cantidad_fisica NUMERIC(12,2),
    diferencia NUMERIC(12,2),
    observacion VARCHAR(255),
    CONSTRAINT fk_levantamiento_detalle_cabecera FOREIGN KEY (levantamiento_inventario_id) REFERENCES levantamiento_inventario(levantamiento_inventario_id) ON DELETE CASCADE,
    CONSTRAINT fk_levantamiento_detalle_producto FOREIGN KEY (producto_id) REFERENCES producto(producto_id)
);

CREATE INDEX idx_levantamiento_detalle_cabecera ON levantamiento_inventario_detalle(levantamiento_inventario_id);

-- =====================================================================
-- NIVEL 8: cuota (depende de cuenta_por_cobrar)
-- =====================================================================

CREATE TABLE cuota (
    cuota_id BIGSERIAL PRIMARY KEY,
    cuenta_por_cobrar_id BIGINT NOT NULL,
    numero_cuota INT NOT NULL,
    monto_cuota NUMERIC(12,2) NOT NULL,
    fecha_vencimiento DATE NOT NULL,
    estado VARCHAR(20) NOT NULL DEFAULT 'pendiente',
    CONSTRAINT fk_cuota_cxc FOREIGN KEY (cuenta_por_cobrar_id) REFERENCES cuenta_por_cobrar(cuenta_por_cobrar_id) ON DELETE CASCADE
);

CREATE INDEX idx_cuota_cxc ON cuota(cuenta_por_cobrar_id);

-- =====================================================================
-- NIVEL 9: pago (depende de cuenta_por_cobrar y cuota)
-- =====================================================================

CREATE TABLE pago (
    pago_id BIGSERIAL PRIMARY KEY,
    cuenta_por_cobrar_id BIGINT NOT NULL,
    cuota_id BIGINT,
    monto NUMERIC(12,2) NOT NULL,
    fecha_pago TIMESTAMP NOT NULL DEFAULT NOW(),
    metodo_pago VARCHAR(30),
    observacion VARCHAR(255),
    usuario_creacion BIGINT,
    CONSTRAINT fk_pago_cxc FOREIGN KEY (cuenta_por_cobrar_id) REFERENCES cuenta_por_cobrar(cuenta_por_cobrar_id),
    CONSTRAINT fk_pago_cuota FOREIGN KEY (cuota_id) REFERENCES cuota(cuota_id),
    CONSTRAINT fk_pago_usuario_creacion FOREIGN KEY (usuario_creacion) REFERENCES usuario(usuario_id)
);

CREATE INDEX idx_pago_cxc ON pago(cuenta_por_cobrar_id);

-- =====================================================================
-- NIVEL 10: solicitud_cambio (flujo de aprobación de edición/eliminación
-- para las cabeceras de movimientos inmutables: entrada, salida, transferencia)
-- =====================================================================

CREATE TABLE solicitud_cambio (
    solicitud_cambio_id BIGSERIAL PRIMARY KEY,
    nombre_tabla VARCHAR(50) NOT NULL,
    registro_id BIGINT NOT NULL,
    accion VARCHAR(20) NOT NULL,
    datos_actuales JSONB,
    datos_propuestos JSONB,
    motivo VARCHAR(255) NOT NULL,
    estado VARCHAR(20) NOT NULL DEFAULT 'pendiente',
    solicitado_por BIGINT NOT NULL,
    solicitado_en TIMESTAMP NOT NULL DEFAULT NOW(),
    revisado_por BIGINT,
    revisado_en TIMESTAMP,
    notas_revision VARCHAR(255),
    CONSTRAINT fk_solicitud_cambio_solicitado_por FOREIGN KEY (solicitado_por) REFERENCES usuario(usuario_id),
    CONSTRAINT fk_solicitud_cambio_revisado_por FOREIGN KEY (revisado_por) REFERENCES usuario(usuario_id),
    CONSTRAINT chk_solicitud_cambio_tabla CHECK (nombre_tabla IN ('entrada', 'salida', 'transferencia')),
    CONSTRAINT chk_solicitud_cambio_accion CHECK (accion IN ('editar', 'eliminar'))
);

CREATE INDEX idx_solicitud_cambio_estado ON solicitud_cambio(estado);
CREATE INDEX idx_solicitud_cambio_tabla_registro ON solicitud_cambio(nombre_tabla, registro_id);

-- =====================================================================
-- DATOS SEMILLA (seed)
-- =====================================================================

INSERT INTO configuracion (clave, valor, descripcion)
VALUES ('cuenta_contable_descuento', '1135005', 'Código de cuenta contable para descuentos de libros misioneros');

-- =====================================================================
-- FIN DEL SCRIPT
-- =====================================================================