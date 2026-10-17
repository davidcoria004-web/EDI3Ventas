
namespace EDI3Ventas.Models;

public class Producto
{
    public int ProductoID { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string UnidadMedida { get; set; } = string.Empty;
    public decimal PrecioUnitario { get; set; }
    public int StockActual { get; set; }
    public bool Activo { get; set; } = true;
}
