USE EDI3_TP8;
GO
SET XACT_ABORT ON;
BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.Cliente', N'U') IS NULL OR OBJECT_ID(N'dbo.OrdenCompra', N'U') IS NULL
        THROW 51010, 'Faltan las tablas Cliente o OrdenCompra.', 1;

    IF COL_LENGTH('dbo.Cliente', 'Categoria') IS NULL
        ALTER TABLE dbo.Cliente
        ADD Categoria NVARCHAR(20) NOT NULL
            CONSTRAINT DF_Cliente_Categoria DEFAULT N'Minorista' WITH VALUES;

    IF NOT EXISTS (
        SELECT 1 FROM sys.check_constraints
        WHERE parent_object_id = OBJECT_ID(N'dbo.Cliente')
          AND name = N'CK_Cliente_Categoria'
    )
        EXEC sys.sp_executesql N'ALTER TABLE dbo.Cliente WITH CHECK
          ADD CONSTRAINT CK_Cliente_Categoria
          CHECK (Categoria IN (N''Minorista'', N''Mayorista'', N''Distribuidor''));';

    IF COL_LENGTH('dbo.OrdenCompra', 'FechaEntregaSolicitada') IS NULL
        ALTER TABLE dbo.OrdenCompra
        ADD FechaEntregaSolicitada DATE NULL;

    IF OBJECT_ID(N'dbo.Pedido', N'U') IS NOT NULL
       AND COL_LENGTH('dbo.Pedido', 'FechaEntregaSolicitada') IS NULL
        ALTER TABLE dbo.Pedido
        ADD FechaEntregaSolicitada DATE NULL;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO
SELECT N'Columnas de cliente y orden de compra disponibles' AS Resultado;
GO
