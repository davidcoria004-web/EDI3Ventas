namespace EDI3Ventas.Models;

public class DetallePedido
{
    public int DetallePedidoID { get; set; }
    public int PedidoID { get; set; }
    public int ProductoID { get; set; }
    public string? NombreProducto { get; set; }
    public int Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }

    public decimal Subtotal => Cantidad * PrecioUnitario;
}