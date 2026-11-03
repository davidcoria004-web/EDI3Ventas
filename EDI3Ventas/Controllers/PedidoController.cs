
using EDI3Ventas.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;

namespace EDI3Ventas.Controllers;

[ApiController]
[Route("api/pedidos")]
public class PedidosController : ControllerBase
{
    private readonly string _connectionString;

    public PedidosController(IConfiguration configuration)
    {
        _connectionString =
            configuration.GetConnectionString("VentasDB")
            ?? throw new InvalidOperationException(
                "No se encontró la conexión VentasDB.");
    }

    // GET: api/pedidos
    [HttpGet]
    public async Task<IActionResult> ObtenerTodos()
    {
        var pedidos = new List<Pedido>();

        await using var cn = new SqlConnection(_connectionString);
        await cn.OpenAsync();

        const string sql = """
            SELECT PedidoID, ClienteID, FechaPedido, Estado
            FROM dbo.Pedido
            ORDER BY FechaPedido DESC;
            """;

        await using var cmd = new SqlCommand(sql, cn);
        await using var rd = await cmd.ExecuteReaderAsync();

        while (await rd.ReadAsync())
        {
            pedidos.Add(new Pedido
            {
                PedidoID = Convert.ToInt32(rd["PedidoID"]),
                ClienteID = Convert.ToInt32(rd["ClienteID"]),
                FechaPedido = Convert.ToDateTime(rd["FechaPedido"]),
                Estado = Convert.ToString(rd["Estado"])!
            });
        }

        return Ok(pedidos);
    }

    // GET: api/pedidos/1
    [HttpGet("{id:int}")]
    public async Task<IActionResult> ObtenerPorId(int id)
    {
        await using var cn = new SqlConnection(_connectionString);
        await cn.OpenAsync();

        const string sql = """
            SELECT PedidoID, ClienteID, FechaPedido, Estado
            FROM dbo.Pedido
            WHERE PedidoID = @Id;
            """;

        await using var cmd = new SqlCommand(sql, cn);
        cmd.Parameters.Add("@Id", SqlDbType.Int).Value = id;

        await using var rd = await cmd.ExecuteReaderAsync();

        if (!await rd.ReadAsync())
        {
            return NotFound(new
            {
                mensaje = "Pedido no encontrado."
            });
        }

        var pedido = new Pedido
        {
            PedidoID = Convert.ToInt32(rd["PedidoID"]),
            ClienteID = Convert.ToInt32(rd["ClienteID"]),
            FechaPedido = Convert.ToDateTime(rd["FechaPedido"]),
            Estado = Convert.ToString(rd["Estado"])!
        };

        return Ok(pedido);
    }

    // POST: api/pedidos
    [HttpPost]
    public async Task<IActionResult> Crear(
        [FromBody] PedidoRequest request)
    {
        if (request.ClienteID <= 0)
        {
            return BadRequest(new
            {
                error = "El ClienteID debe ser mayor que cero."
            });
        }

        if (request.Detalles is null || request.Detalles.Count == 0)
        {
            return BadRequest(new
            {
                error = "El pedido debe contener al menos un producto."
            });
        }

        if (request.Detalles.Any(d =>
            d.ProductoID <= 0 || d.Cantidad <= 0))
        {
            return BadRequest(new
            {
                error = "Cada producto debe tener un ID válido y una cantidad mayor que cero."
            });
        }

        if (request.Detalles
            .GroupBy(d => d.ProductoID)
            .Any(g => g.Count() > 1))
        {
            return BadRequest(new
            {
                error = "No repitas productos en el detalle del pedido."
            });
        }

        await using var cn = new SqlConnection(_connectionString);
        await cn.OpenAsync();

        await using var tx = (SqlTransaction)await cn.BeginTransactionAsync();

        try
        {
            // Verificar que el cliente exista y esté activo.
            const string sqlCliente = """
                SELECT COUNT(1)
                FROM dbo.Cliente
                WHERE ClienteID = @ClienteID
                  AND Activo = 1;
                """;

            await using (var cmdCliente =
                new SqlCommand(sqlCliente, cn, tx))
            {
                cmdCliente.Parameters.Add("@ClienteID", SqlDbType.Int)
                    .Value = request.ClienteID;

                int existe = Convert.ToInt32(
                    await cmdCliente.ExecuteScalarAsync());

                if (existe == 0)
                {
                    await tx.RollbackAsync();

                    return BadRequest(new
                    {
                        error = "El cliente no existe o está inactivo."
                    });
                }
            }

            // Crear el encabezado del pedido.
            const string sqlPedido = """
                INSERT INTO dbo.Pedido
                    (ClienteID, FechaPedido, Estado)
                OUTPUT INSERTED.PedidoID
                VALUES
                    (@ClienteID, @FechaPedido, @Estado);
                """;

            int pedidoId;

            await using (var cmdPedido =
                new SqlCommand(sqlPedido, cn, tx))
            {
                cmdPedido.Parameters.Add("@ClienteID", SqlDbType.Int)
                    .Value = request.ClienteID;

                cmdPedido.Parameters.Add("@FechaPedido", SqlDbType.DateTime)
                    .Value = DateTime.Now;

                cmdPedido.Parameters.Add("@Estado", SqlDbType.VarChar, 30)
                    .Value = "Pendiente";

                pedidoId = Convert.ToInt32(
                    await cmdPedido.ExecuteScalarAsync());
            }

            decimal importeTotal = 0;

            // Insertar cada producto del pedido.
            foreach (var detalle in request.Detalles)
            {
                const string sqlProducto = """
                    SELECT PrecioUnitario
                    FROM dbo.Producto
                    WHERE ProductoID = @ProductoID
                      AND Activo = 1;
                    """;

                decimal precio;

                await using (var cmdProducto =
                    new SqlCommand(sqlProducto, cn, tx))
                {
                    cmdProducto.Parameters.Add("@ProductoID", SqlDbType.Int)
                        .Value = detalle.ProductoID;

                    var resultado = await cmdProducto.ExecuteScalarAsync();

                    if (resultado is null || resultado == DBNull.Value)
                    {
                        await tx.RollbackAsync();

                        return BadRequest(new
                        {
                            error = $"El producto {detalle.ProductoID} no existe o está inactivo."
                        });
                    }

                    precio = Convert.ToDecimal(resultado);
                }

                const string sqlDetalle = """
                    INSERT INTO dbo.DetallePedido
                        (PedidoID, ProductoID, Cantidad, PrecioUnitario)
                    VALUES
                        (@PedidoID, @ProductoID, @Cantidad, @PrecioUnitario);
                    """;

                await using (var cmdDetalle =
                    new SqlCommand(sqlDetalle, cn, tx))
                {
                    cmdDetalle.Parameters.Add("@PedidoID", SqlDbType.Int)
                        .Value = pedidoId;

                    cmdDetalle.Parameters.Add("@ProductoID", SqlDbType.Int)
                        .Value = detalle.ProductoID;

                    cmdDetalle.Parameters.Add("@Cantidad", SqlDbType.Int)
                        .Value = detalle.Cantidad;

                    var precioParam = cmdDetalle.Parameters.Add(
                        "@PrecioUnitario", SqlDbType.Decimal);

                    precioParam.Precision = 18;
                    precioParam.Scale = 2;
                    precioParam.Value = precio;

                    await cmdDetalle.ExecuteNonQueryAsync();
                }

                importeTotal += precio * detalle.Cantidad;
            }

            await tx.CommitAsync();

            return CreatedAtAction(
                nameof(ObtenerPorId),
                new { id = pedidoId },
                new
                {
                    pedidoID = pedidoId,
                    clienteID = request.ClienteID,
                    estado = "Pendiente",
                    importeTotal,
                    mensaje = "Pedido creado correctamente."
                });
        }
        catch (SqlException)
        {
            await tx.RollbackAsync();
            throw;
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }
}
