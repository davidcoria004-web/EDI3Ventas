namespace EDI3Ventas.Models;

public class OrdenCompra
{
    public int OrdenCompraID { get; set; }
    public int PedidoID { get; set; }
    public string NumeroOC { get; set; } = string.Empty;
    public DateTime FechaOC { get; set; }
    public string Estado { get; set; } = "Pendiente";
}