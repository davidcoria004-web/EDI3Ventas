
using EDI3Ventas.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace EDI3Ventas.Controllers;

[ApiController]
[Route("api/productos")]
public class ProductosController : ControllerBase
{
    private readonly string _connectionString;

    public ProductosController(IConfiguration configuration)
    {
        _connectionString =
            configuration.GetConnectionString("VentasDB")
            ?? throw new InvalidOperationException(
                "No se encontro la conexion VentasDB.");
    }

    // GET: api/productos
    [HttpGet]
    public async Task<IActionResult> ObtenerTodos()
    {
        var productos = new List<Producto>();

        await using var cn = new SqlConnection(_connectionString);
        await cn.OpenAsync();

        const string sql = """
            SELECT ProductoID, Codigo, Nombre, UnidadMedida,
                   PrecioUnitario, StockActual, Activo
            FROM dbo.Producto
            ORDER BY Nombre;
            """;

        await using var cmd = new SqlCommand(sql, cn);
        await using var rd = await cmd.ExecuteReaderAsync();

        while (await rd.ReadAsync())
        {
            productos.Add(LeerProducto(rd));
        }

        return Ok(productos);
    }

    // GET: api/productos/1
    [HttpGet("{id:int}")]
    public async Task<IActionResult> ObtenerPorId(int id)
    {
        await using var cn = new SqlConnection(_connectionString);
        await cn.OpenAsync();

        const string sql = """
            SELECT ProductoID, Codigo, Nombre, UnidadMedida,
                   PrecioUnitario, StockActual, Activo
            FROM dbo.Producto
            WHERE ProductoID = @Id;
            """;

        await using var cmd = new SqlCommand(sql, cn);
        cmd.Parameters.AddWithValue("@Id", id);

        await using var rd = await cmd.ExecuteReaderAsync();

        if (!await rd.ReadAsync())
            return NotFound(new { mensaje = "Producto no encontrado." });

        return Ok(LeerProducto(rd));
    }

    // POST: api/productos
    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] ProductoRequest producto)
    {
        if (string.IsNullOrWhiteSpace(producto.Codigo) ||
            string.IsNullOrWhiteSpace(producto.Nombre) ||
            string.IsNullOrWhiteSpace(producto.UnidadMedida))
        {
            return BadRequest(new
            {
                error = "Código, nombre y unidad de medida son obligatorios."
            });
        }

        if (producto.PrecioUnitario < 0 || producto.StockActual < 0)
        {
            return BadRequest(new
            {
                error = "El precio y el stock no pueden ser negativos."
            });
        }

        await using var cn = new SqlConnection(_connectionString);
        await cn.OpenAsync();

        const string sql = """
            INSERT INTO dbo.Producto
                (Codigo, Nombre, UnidadMedida, PrecioUnitario, StockActual, Activo)
            OUTPUT INSERTED.ProductoID
            VALUES
                (@Codigo, @Nombre, @UnidadMedida, @PrecioUnitario, @StockActual, 1);
            """;

        await using var cmd = new SqlCommand(sql, cn);
        AgregarParametros(cmd, producto);

        try
        {
            var id = (int)await cmd.ExecuteScalarAsync()!;

            return CreatedAtAction(
                nameof(ObtenerPorId),
                new { id },
                new
                {
                    productoID = id,
                    mensaje = "Producto creado correctamente."
                });
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627)
        {
            return Conflict(new
            {
                error = "Ya existe un producto con ese código."
            });
        }
    }

    // PUT: api/productos/1
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Actualizar(
        int id, [FromBody] ProductoRequest producto)
    {
        if (string.IsNullOrWhiteSpace(producto.Codigo) ||
            string.IsNullOrWhiteSpace(producto.Nombre) ||
            string.IsNullOrWhiteSpace(producto.UnidadMedida))
        {
            return BadRequest(new
            {
                error = "Código, nombre y unidad de medida son obligatorios."
            });
        }

        if (producto.PrecioUnitario < 0 || producto.StockActual < 0)
        {
            return BadRequest(new
            {
                error = "El precio y el stock no pueden ser negativos."
            });
        }

        await using var cn = new SqlConnection(_connectionString);
        await cn.OpenAsync();

        const string sql = """
            UPDATE dbo.Producto
            SET Codigo = @Codigo,
                Nombre = @Nombre,
                UnidadMedida = @UnidadMedida,
                PrecioUnitario = @PrecioUnitario,
                StockActual = @StockActual
            WHERE ProductoID = @Id AND Activo = 1;
            """;

        await using var cmd = new SqlCommand(sql, cn);
        cmd.Parameters.AddWithValue("@Id", id);
        AgregarParametros(cmd, producto);

        try
        {
            int filas = await cmd.ExecuteNonQueryAsync();

            if (filas == 0)
                return NotFound(new
                {
                    mensaje = "Producto inexistente o inactivo."
                });

            return Ok(new { mensaje = "Producto actualizado correctamente." });
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627)
        {
            return Conflict(new
            {
                error = "Ya existe otro producto con ese código."
            });
        }
    }

    // DELETE: api/productos/1
    // Baja lógica: conserva el registro.
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Eliminar(int id)
    {
        await using var cn = new SqlConnection(_connectionString);
        await cn.OpenAsync();

        const string sql = """
            UPDATE dbo.Producto
            SET Activo = 0
            WHERE ProductoID = @Id AND Activo = 1;
            """;

        await using var cmd = new SqlCommand(sql, cn);
        cmd.Parameters.AddWithValue("@Id", id);

        int filas = await cmd.ExecuteNonQueryAsync();

        if (filas == 0)
            return NotFound(new
            {
                mensaje = "Producto inexistente o ya inactivo."
            });

        return Ok(new { mensaje = "Producto dado de baja correctamente." });
    }

    private static void AgregarParametros(
        SqlCommand cmd, ProductoRequest producto)
    {
        cmd.Parameters.AddWithValue("@Codigo", producto.Codigo.Trim());
        cmd.Parameters.AddWithValue("@Nombre", producto.Nombre.Trim());
        cmd.Parameters.AddWithValue("@UnidadMedida", producto.UnidadMedida.Trim());
        cmd.Parameters.AddWithValue("@PrecioUnitario", producto.PrecioUnitario);
        cmd.Parameters.AddWithValue("@StockActual", producto.StockActual);
    }

    private static Producto LeerProducto(SqlDataReader rd)
    {
        return new Producto
        {
            ProductoID = Convert.ToInt32(rd["ProductoID"]),
            Codigo = Convert.ToString(rd["Codigo"])!,
            Nombre = Convert.ToString(rd["Nombre"])!,
            UnidadMedida = Convert.ToString(rd["UnidadMedida"])!,
            PrecioUnitario = Convert.ToDecimal(rd["PrecioUnitario"]),
            StockActual = Convert.ToInt32(rd["StockActual"]),
            Activo = Convert.ToBoolean(rd["Activo"])
        };
    }
}
