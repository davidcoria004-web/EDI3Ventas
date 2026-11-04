namespace EDI3Ventas.Models;

public class OrdenCompraRequest
{
    public string NroOrdenCompra { get; set; } = string.Empty;

    public int ClienteID { get; set; }

    public string? Observaciones { get; set; }

    public List<DetalleOrdenCompraRequest> Detalles { get; set; } = new();
}

public class DetalleOrdenCompraRequest
{
    public int ProductoID { get; set; }

    public decimal Cantidad { get; set; }

    public decimal PrecioUnitario { get; set; }
}