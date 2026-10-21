namespace EDI3Ventas.Models;

public class Pedido
{
    public int PedidoID { get; set; }
    public int ClienteID { get; set; }
    public DateTime FechaPedido { get; set; }
    public string Estado { get; set; } = "Pendiente";
    public decimal ImporteTotal { get; set; }

    public List<DetallePedido> Detalles { get; set; } = new();
}