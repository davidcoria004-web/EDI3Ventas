namespace EDI3Ventas.Models;

public class DetalleOrdenCompra
{
    public int DetalleOCID { get; set; }

    public int OrdenCompraID { get; set; }

    public int ProductoID { get; set; }

    public string? NombreProducto { get; set; }

    public decimal Cantidad { get; set; }

    public decimal PrecioUnitario { get; set; }

    public decimal Subtotal => Cantidad * PrecioUnitario;
}