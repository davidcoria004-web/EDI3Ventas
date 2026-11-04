namespace EDI3Ventas.Models;

public class OrdenCompra
{
    public int OrdenCompraID { get; set; }

    public string NroOrdenCompra { get; set; } = string.Empty;

    public DateTime FechaOC { get; set; }

    public int ClienteID { get; set; }

    public string Estado { get; set; } = "Pendiente";

    public string? Observaciones { get; set; }

    public List<DetalleOrdenCompra> Detalles { get; set; } = new();
}