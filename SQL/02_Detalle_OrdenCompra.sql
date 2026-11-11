USE EDI3_TP8;
GO
IF OBJECT_ID(N'dbo.DetalleOrdenCompra', N'U') IS NULL
CREATE TABLE dbo.DetalleOrdenCompra (
    DetalleOrdenCompraID INT IDENTITY(1,1) PRIMARY KEY,
    OrdenCompraID INT NOT NULL REFERENCES dbo.OrdenCompra(OrdenCompraID),
    ProductoID INT NOT NULL REFERENCES dbo.Producto(ProductoID),
    Cantidad DECIMAL(12,3) NOT NULL CHECK (Cantidad > 0),
    PrecioUnitario DECIMAL(12,2) NOT NULL CHECK (PrecioUnitario >= 0)
);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Factura_Orden' AND object_id = OBJECT_ID(N'dbo.Factura'))
    CREATE UNIQUE INDEX UX_Factura_Orden ON dbo.Factura (OrdenCompraID);
GO
