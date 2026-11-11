IF DB_ID(N'EDI3_TP8') IS NULL CREATE DATABASE EDI3_TP8;
GO
USE EDI3_TP8;
GO

IF OBJECT_ID(N'dbo.Cliente', N'U') IS NULL
CREATE TABLE dbo.Cliente (
    ClienteID INT IDENTITY(1,1) PRIMARY KEY,
    Nombre NVARCHAR(100) NOT NULL,
    Apellido NVARCHAR(100) NOT NULL,
    Documento NVARCHAR(30) NULL,
    Email NVARCHAR(150) NULL,
    Telefono NVARCHAR(30) NULL,
    Direccion NVARCHAR(200) NULL,
    FechaAlta DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    Activo BIT NOT NULL DEFAULT 1
);
GO
IF OBJECT_ID(N'dbo.Producto', N'U') IS NULL
CREATE TABLE dbo.Producto (
    ProductoID INT IDENTITY(1,1) PRIMARY KEY,
    Codigo NVARCHAR(30) NOT NULL UNIQUE,
    Nombre NVARCHAR(150) NOT NULL,
    UnidadMedida NVARCHAR(20) NOT NULL,
    PrecioUnitario DECIMAL(12,2) NOT NULL CHECK (PrecioUnitario >= 0),
    StockActual DECIMAL(12,3) NOT NULL DEFAULT 0 CHECK (StockActual >= 0),
    Activo BIT NOT NULL DEFAULT 1
);
GO
IF OBJECT_ID(N'dbo.OrdenCompra', N'U') IS NULL
CREATE TABLE dbo.OrdenCompra (
    OrdenCompraID INT IDENTITY(1,1) PRIMARY KEY,
    ClienteID INT NOT NULL REFERENCES dbo.Cliente(ClienteID),
    NumeroOC NVARCHAR(30) NOT NULL UNIQUE,
    FechaOC DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    FechaEntregaSolicitada DATE NULL,
    Estado NVARCHAR(20) NOT NULL DEFAULT N'Registrada'
);
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
IF OBJECT_ID(N'dbo.Factura', N'U') IS NULL
CREATE TABLE dbo.Factura (
    FacturaID INT IDENTITY(1,1) PRIMARY KEY,
    OrdenCompraID INT NOT NULL REFERENCES dbo.OrdenCompra(OrdenCompraID),
    NumeroFactura NVARCHAR(30) NOT NULL UNIQUE,
    FechaEmision DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    ImporteTotal DECIMAL(12,2) NOT NULL CHECK (ImporteTotal >= 0),
    EstadoPago NVARCHAR(20) NOT NULL DEFAULT N'Pendiente'
        CHECK (EstadoPago IN (N'Pendiente',N'Parcial',N'Cancelada'))
);
GO
IF OBJECT_ID(N'dbo.Pago', N'U') IS NULL
CREATE TABLE dbo.Pago (
    PagoID INT IDENTITY(1,1) PRIMARY KEY,
    FacturaID INT NOT NULL REFERENCES dbo.Factura(FacturaID),
    FechaPago DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    Importe DECIMAL(12,2) NOT NULL CHECK (Importe > 0),
    MedioPago NVARCHAR(30) NOT NULL,
    Referencia NVARCHAR(100) NULL
);
GO
IF OBJECT_ID(N'dbo.Remito', N'U') IS NULL
CREATE TABLE dbo.Remito (
    RemitoID INT IDENTITY(1,1) PRIMARY KEY,
    OrdenCompraID INT NOT NULL REFERENCES dbo.OrdenCompra(OrdenCompraID),
    NumeroRemito NVARCHAR(30) NOT NULL UNIQUE,
    FechaEmision DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    FechaEntrega DATETIME2 NULL,
    Estado NVARCHAR(20) NOT NULL DEFAULT N'Pendiente'
);
GO
IF OBJECT_ID(N'dbo.DetalleRemito', N'U') IS NULL
CREATE TABLE dbo.DetalleRemito (
    DetalleRemitoID INT IDENTITY(1,1) PRIMARY KEY,
    RemitoID INT NOT NULL REFERENCES dbo.Remito(RemitoID),
    ProductoID INT NOT NULL REFERENCES dbo.Producto(ProductoID),
    Cantidad DECIMAL(12,3) NOT NULL CHECK (Cantidad > 0)
);
GO
SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE='BASE TABLE' ORDER BY TABLE_NAME;
GO
