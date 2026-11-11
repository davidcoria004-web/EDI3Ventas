USE EDI3_TP8;
GO
SET XACT_ABORT ON;
BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.Pedido', N'U') IS NOT NULL
       AND COL_LENGTH(N'dbo.OrdenCompra', 'PedidoID') IS NOT NULL
    BEGIN
        IF COL_LENGTH(N'dbo.OrdenCompra', 'ClienteID') IS NULL
            ALTER TABLE dbo.OrdenCompra ADD ClienteID INT NULL;

        IF COL_LENGTH(N'dbo.OrdenCompra', 'FechaEntregaSolicitada') IS NULL
            ALTER TABLE dbo.OrdenCompra ADD FechaEntregaSolicitada DATE NULL;

        IF COL_LENGTH(N'dbo.Pedido', 'FechaEntregaSolicitada') IS NULL
            ALTER TABLE dbo.Pedido ADD FechaEntregaSolicitada DATE NULL;

        IF OBJECT_ID(N'dbo.DetalleOrdenCompra', N'U') IS NULL
            EXEC(N'
                CREATE TABLE dbo.DetalleOrdenCompra (
                    DetalleOrdenCompraID INT IDENTITY(1,1) PRIMARY KEY,
                    OrdenCompraID INT NOT NULL REFERENCES dbo.OrdenCompra(OrdenCompraID),
                    ProductoID INT NOT NULL REFERENCES dbo.Producto(ProductoID),
                    Cantidad DECIMAL(12,3) NOT NULL CHECK (Cantidad > 0),
                    PrecioUnitario DECIMAL(12,2) NOT NULL CHECK (PrecioUnitario >= 0)
                );');

        DECLARE @PedidoID INT, @NumeroOC NVARCHAR(30), @Sufijo INT;
        DECLARE pedidos_sin_orden CURSOR LOCAL FAST_FORWARD FOR
            SELECT p.PedidoID
            FROM dbo.Pedido p
            WHERE NOT EXISTS (SELECT 1 FROM dbo.OrdenCompra o WHERE o.PedidoID=p.PedidoID);

        OPEN pedidos_sin_orden;
        FETCH NEXT FROM pedidos_sin_orden INTO @PedidoID;
        WHILE @@FETCH_STATUS = 0
        BEGIN
            SET @Sufijo = 0;
            SET @NumeroOC = CONCAT(N'MIG-', @PedidoID);
            WHILE EXISTS (SELECT 1 FROM dbo.OrdenCompra WHERE NumeroOC=@NumeroOC)
            BEGIN
                SET @Sufijo += 1;
                SET @NumeroOC = CONCAT(N'MIG-', @PedidoID, N'-', @Sufijo);
            END;

            INSERT INTO dbo.OrdenCompra(PedidoID,NumeroOC,Estado)
            VALUES(@PedidoID,@NumeroOC,N'Registrada');
            FETCH NEXT FROM pedidos_sin_orden INTO @PedidoID;
        END;
        CLOSE pedidos_sin_orden;
        DEALLOCATE pedidos_sin_orden;

        EXEC sys.sp_executesql N'
            UPDATE o
            SET o.ClienteID=p.ClienteID,
                o.FechaEntregaSolicitada=p.FechaEntregaSolicitada
            FROM dbo.OrdenCompra o
            INNER JOIN dbo.Pedido p ON p.PedidoID=o.PedidoID;

            INSERT INTO dbo.DetalleOrdenCompra(OrdenCompraID,ProductoID,Cantidad,PrecioUnitario)
            SELECT o.OrdenCompraID,d.ProductoID,d.Cantidad,d.PrecioUnitario
            FROM dbo.OrdenCompra o
            INNER JOIN dbo.DetallePedido d ON d.PedidoID=o.PedidoID;';

        IF EXISTS (
            SELECT 1
            FROM dbo.OrdenCompra o
            LEFT JOIN dbo.DetalleOrdenCompra d ON d.OrdenCompraID=o.OrdenCompraID
            WHERE NOT EXISTS (SELECT 1 FROM dbo.Factura f WHERE f.OrdenCompraID=o.OrdenCompraID)
            GROUP BY o.OrdenCompraID
            HAVING ROUND(COALESCE(SUM(d.Cantidad*d.PrecioUnitario),0),2) <= 0
                OR ROUND(COALESCE(SUM(d.Cantidad*d.PrecioUnitario),0),2) > 9999999999.99
        )
            THROW 51011, 'Hay órdenes antiguas sin factura y con detalle vacío o total fuera de rango.', 1;

        DECLARE @OrdenCompraID INT, @TotalFactura DECIMAL(12,2), @NumeroFactura NVARCHAR(30), @FacturaSufijo INT;
        DECLARE ordenes_sin_factura CURSOR LOCAL FAST_FORWARD FOR
            SELECT o.OrdenCompraID, CONVERT(DECIMAL(12,2), ROUND(SUM(d.Cantidad*d.PrecioUnitario),2))
            FROM dbo.OrdenCompra o
            INNER JOIN dbo.DetalleOrdenCompra d ON d.OrdenCompraID=o.OrdenCompraID
            WHERE NOT EXISTS (SELECT 1 FROM dbo.Factura f WHERE f.OrdenCompraID=o.OrdenCompraID)
            GROUP BY o.OrdenCompraID;

        OPEN ordenes_sin_factura;
        FETCH NEXT FROM ordenes_sin_factura INTO @OrdenCompraID,@TotalFactura;
        WHILE @@FETCH_STATUS = 0
        BEGIN
            SET @FacturaSufijo = 0;
            SET @NumeroFactura = CONCAT(N'FAC-MIG-', @OrdenCompraID);
            WHILE EXISTS (SELECT 1 FROM dbo.Factura WHERE NumeroFactura=@NumeroFactura)
            BEGIN
                SET @FacturaSufijo += 1;
                SET @NumeroFactura = CONCAT(N'FAC-MIG-', @OrdenCompraID, N'-', @FacturaSufijo);
            END;

            INSERT INTO dbo.Factura(OrdenCompraID,NumeroFactura,ImporteTotal,EstadoPago)
            VALUES(@OrdenCompraID,@NumeroFactura,@TotalFactura,N'Pendiente');
            FETCH NEXT FROM ordenes_sin_factura INTO @OrdenCompraID,@TotalFactura;
        END;
        CLOSE ordenes_sin_factura;
        DEALLOCATE ordenes_sin_factura;

        DECLARE @SQL NVARCHAR(MAX) = N'';
        SELECT @SQL += N'ALTER TABLE dbo.OrdenCompra DROP CONSTRAINT ' + QUOTENAME(fk.name) + N';'
        FROM sys.foreign_keys fk
        WHERE fk.parent_object_id=OBJECT_ID(N'dbo.OrdenCompra')
          AND fk.referenced_object_id=OBJECT_ID(N'dbo.Pedido');
        IF @SQL <> N'' EXEC sys.sp_executesql @SQL;

        SET @SQL = N'';
        SELECT @SQL += N'ALTER TABLE dbo.OrdenCompra DROP CONSTRAINT ' + QUOTENAME(kc.name) + N';'
        FROM sys.key_constraints kc
        INNER JOIN sys.index_columns ic ON ic.object_id=kc.parent_object_id AND ic.index_id=kc.unique_index_id
        INNER JOIN sys.columns col ON col.object_id=ic.object_id AND col.column_id=ic.column_id
        WHERE kc.parent_object_id=OBJECT_ID(N'dbo.OrdenCompra')
          AND kc.type=N'UQ' AND col.name=N'PedidoID';
        IF @SQL <> N'' EXEC sys.sp_executesql @SQL;

        EXEC(N'ALTER TABLE dbo.OrdenCompra DROP COLUMN PedidoID;');
        IF OBJECT_ID(N'dbo.DetalleCotizacion', N'U') IS NOT NULL DROP TABLE dbo.DetalleCotizacion;
        IF OBJECT_ID(N'dbo.Cotizacion', N'U') IS NOT NULL DROP TABLE dbo.Cotizacion;
        IF OBJECT_ID(N'dbo.DetallePedido', N'U') IS NOT NULL DROP TABLE dbo.DetallePedido;
        DROP TABLE dbo.Pedido;

        EXEC(N'ALTER TABLE dbo.OrdenCompra ALTER COLUMN ClienteID INT NOT NULL;');
        IF NOT EXISTS (
            SELECT 1 FROM sys.foreign_keys
            WHERE parent_object_id=OBJECT_ID(N'dbo.OrdenCompra')
              AND referenced_object_id=OBJECT_ID(N'dbo.Cliente')
        )
            ALTER TABLE dbo.OrdenCompra WITH CHECK
            ADD CONSTRAINT FK_OrdenCompra_Cliente FOREIGN KEY (ClienteID) REFERENCES dbo.Cliente(ClienteID);
    END;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO
SELECT N'OrdenCompra directa disponible; datos anteriores migrados cuando existian.' AS Resultado;
GO
