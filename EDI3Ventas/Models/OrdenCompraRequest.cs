namespace EDI3Ventas.Models;

public class OrdenCompraRequest
{
    public int PedidoID { get; set; }
    public string NumeroOC { get; set; } = string.Empty;
}