using System.ComponentModel;

namespace EDI3Ventas.Cliente.Modelos;

internal sealed class ClienteDto
{
    public int ClienteID { get; set; }
    public string Nombre { get; set; } = "";
    public string Apellido { get; set; } = "";
    public string? Documento { get; set; }
    public string? Email { get; set; }
    public string? Telefono { get; set; }
    public string? Direccion { get; set; }
    public DateTime? FechaAlta { get; set; }
    public bool Activo { get; set; }
    public string Categoria { get; set; } = "Minorista";
    [Browsable(false)] public string Presentacion => $"{Apellido}, {Nombre} (ID {ClienteID})";
}

internal sealed class ClienteGuardar
{
    public string Nombre { get; set; } = "";
    public string Apellido { get; set; } = "";
    public string? Documento { get; set; }
    public string? Email { get; set; }
    public string? Telefono { get; set; }
    public string? Direccion { get; set; }
}

internal sealed class ProductoDto
{
    public int ProductoID { get; set; }
    public string Codigo { get; set; } = "";
    public string Nombre { get; set; } = "";
    public string UnidadMedida { get; set; } = "";
    public decimal PrecioUnitario { get; set; }
    public decimal StockActual { get; set; }
    public bool Activo { get; set; }
    [Browsable(false)] public string Presentacion => $"{Codigo} - {Nombre} (${PrecioUnitario:N2})";
}

internal sealed class ProductoGuardar
{
    public string Codigo { get; set; } = "";
    public string Nombre { get; set; } = "";
    public string UnidadMedida { get; set; } = "";
    public decimal PrecioUnitario { get; set; }
    public decimal StockActual { get; set; }
}

internal sealed class LineaOrdenCompra
{
    public int ProductoID { get; set; }
    public string NombreProducto { get; set; } = "";
    public decimal Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }
    public decimal Subtotal => Math.Round(Cantidad * PrecioUnitario, 2);
}

internal sealed class LineaOrdenCompraNueva
{
    public int ProductoID { get; set; }
    public decimal Cantidad { get; set; }
}

internal sealed class OrdenCompraNueva
{
    public int ClienteID { get; set; }
    public DateTime FechaEntregaSolicitada { get; set; }
    public List<LineaOrdenCompraNueva> Detalles { get; set; } = [];
}

internal sealed class OrdenDto
{
    public int OrdenCompraID { get; set; }
    public int ClienteID { get; set; }
    public string NumeroOC { get; set; } = "";
    public DateTime FechaOC { get; set; }
    public DateTime? FechaEntregaSolicitada { get; set; }
    public string Estado { get; set; } = "";
    public string Cliente { get; set; } = "";
    public decimal ImporteTotal { get; set; }
    public int? FacturaID { get; set; }
    public string? EstadoPago { get; set; }
    [Browsable(false)] public List<LineaOrdenCompra> Detalles { get; set; } = [];
}

internal sealed class FacturaDto
{
    public int FacturaID { get; set; }
    public int OrdenCompraID { get; set; }
    public string NumeroFactura { get; set; } = "";
    public string Cliente { get; set; } = "";
    public DateTime FechaEmision { get; set; }
    public decimal ImporteTotal { get; set; }
    public decimal TotalPagado { get; set; }
    public decimal Saldo { get; set; }
    public string EstadoPago { get; set; } = "";
}

internal sealed class RemitoDto
{
    public int RemitoID { get; set; }
    public int OrdenCompraID { get; set; }
    public string NumeroRemito { get; set; } = "";
    public string Cliente { get; set; } = "";
    public DateTime FechaEmision { get; set; }
    public DateTime? FechaEntrega { get; set; }
    public string Estado { get; set; } = "";
}
