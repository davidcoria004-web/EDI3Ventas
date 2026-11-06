-- Validacion de la estructura requerida por el cliente del TP8; no modifica datos.
USE EDI3_TP8;
GO
DECLARE @columnas TABLE (Tabla SYSNAME, Columna SYSNAME);
INSERT INTO @columnas(Tabla,Columna) VALUES
(N'Cliente',N'ClienteID'),(N'Cliente',N'Activo'),
(N'Producto',N'ProductoID'),(N'Producto',N'PrecioUnitario'),(N'Producto',N'StockActual'),
(N'Pedido',N'PedidoID'),(N'Pedido',N'ClienteID'),(N'Pedido',N'Estado'),
(N'DetallePedido',N'PedidoID'),(N'DetallePedido',N'ProductoID'),
(N'DetallePedido',N'Cantidad'),(N'DetallePedido',N'PrecioUnitario'),
(N'Cotizacion',N'CotizacionID'),(N'Cotizacion',N'PedidoID'),
(N'Cotizacion',N'NumeroCotizacion'),(N'Cotizacion',N'Estado'),
(N'DetalleCotizacion',N'CotizacionID'),(N'DetalleCotizacion',N'Cantidad'),
(N'DetalleCotizacion',N'PrecioUnitario'),
(N'OrdenCompra',N'OrdenCompraID'),(N'OrdenCompra',N'PedidoID'),
(N'OrdenCompra',N'NumeroOC'),(N'OrdenCompra',N'Estado'),
(N'Factura',N'FacturaID'),(N'Factura',N'OrdenCompraID'),
(N'Factura',N'EstadoPago'),(N'Factura',N'ImporteTotal'),
(N'Pago',N'FacturaID'),(N'Pago',N'Importe'),
(N'Remito',N'RemitoID'),(N'Remito',N'OrdenCompraID'),
(N'Remito',N'NumeroRemito'),(N'Remito',N'Estado'),
(N'DetalleRemito',N'RemitoID'),(N'DetalleRemito',N'ProductoID'),
(N'DetalleRemito',N'Cantidad');
DECLARE @faltantes NVARCHAR(MAX);
SELECT @faltantes = STRING_AGG(CONVERT(NVARCHAR(MAX), c.Tabla + N'.' + c.Columna), N', ')
FROM @columnas c
WHERE NOT EXISTS (
    SELECT 1 FROM sys.columns x
    WHERE x.object_id = OBJECT_ID(N'dbo.' + c.Tabla) AND x.name = c.Columna
);
IF @faltantes IS NOT NULL
BEGIN
    SELECT @faltantes AS ColumnasFaltantes;
    THROW 51001, 'Incompatibilidad SQL: revise la lista ColumnasFaltantes.', 1;
END;
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.Producto') AND name = N'StockActual'
      AND system_type_id = TYPE_ID(N'decimal') AND precision = 12 AND scale = 3
) THROW 51002, 'Producto.StockActual debe ser decimal(12,3).', 1;
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.Producto') AND name = N'PrecioUnitario'
      AND system_type_id = TYPE_ID(N'decimal') AND precision = 12 AND scale = 2
) THROW 51003, 'Producto.PrecioUnitario debe ser decimal(12,2).', 1;
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.Factura')
      AND name = N'UX_Factura_Orden' AND is_unique = 1
) THROW 51004, 'Falta el indice unico de factura por OC.', 1;
SELECT N'Esquema compatible con la API /api/minoristas' AS Resultado;
GO
