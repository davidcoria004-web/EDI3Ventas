namespace EDI3Ventas.Models;

public class PedidoRequest
{
    public int ClienteID { get; set; }

    public List<DetallePedidoRequest> Detalles { get; set; } = new();
}

public class DetallePedidoRequest
{
    public int ProductoID { get; set; }
    public int Cantidad { get; set; }
}