
using EDI3Ventas.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;

namespace EDI3Ventas.Controllers;

[ApiController]
[Route("api/ordenes-compra")]
public class OrdenesCompraController : ControllerBase
{
    private readonly string _connectionString;

    public OrdenesCompraController(IConfiguration configuration)
    {
        _connectionString =
            configuration.GetConnectionString("VentasDB")
            ?? throw new InvalidOperationException(
                "No se encontró la conexión VentasDB.");
    }

    [HttpGet]
    public async Task<IActionResult> ObtenerTodas()
    {
        var ordenes = new List<OrdenCompra>();

        await using var cn = new SqlConnection(_connectionString);
        await cn.OpenAsync();

        const string sql = """
            SELECT OrdenCompraID, PedidoID, NumeroOC, FechaOC, Estado
            FROM dbo.OrdenCompra
            ORDER BY FechaOC DESC;
            """;

        await using var cmd = new SqlCommand(sql, cn);
        await using var rd = await cmd.ExecuteReaderAsync();

        while (await rd.ReadAsync())
            ordenes.Add(LeerOrden(rd));

        return Ok(ordenes);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> ObtenerPorId(int id)
    {
        await using var cn = new SqlConnection(_connectionString);
        await cn.OpenAsync();

        const string sql = """
            SELECT OrdenCompraID, PedidoID, NumeroOC, FechaOC, Estado
            FROM dbo.OrdenCompra
            WHERE OrdenCompraID = @Id;
            """;

        await using var cmd = new SqlCommand(sql, cn);
        cmd.Parameters.Add("@Id", SqlDbType.Int).Value = id;

        await using var rd = await cmd.ExecuteReaderAsync();

        if (!await rd.ReadAsync())
            return NotFound(new { mensaje = "Orden de compra no encontrada." });

        return Ok(LeerOrden(rd));
    }

    [HttpPost]
    public async Task<IActionResult> Crear(
        [FromBody] OrdenCompraRequest request)
    {
        if (request.PedidoID <= 0 ||
            string.IsNullOrWhiteSpace(request.NumeroOC))
        {
            return BadRequest(new
            {
                error = "El pedido y el número de orden son obligatorios."
            });
        }

        await using var cn = new SqlConnection(_connectionString);
        await cn.OpenAsync();

        // Verificamos que el pedido exista.
        const string verificarPedido = """
            SELECT COUNT(1)
            FROM dbo.Pedido
            WHERE PedidoID = @PedidoID;
            """;

        await using (var verificar = new SqlCommand(verificarPedido, cn))
        {
            verificar.Parameters.Add("@PedidoID", SqlDbType.Int)
                .Value = request.PedidoID;

            int existe = Convert.ToInt32(
                await verificar.ExecuteScalarAsync());

            if (existe == 0)
                return BadRequest(new
                {
                    error = "El pedido indicado no existe."
                });
        }

        const string sql = """
            INSERT INTO dbo.OrdenCompra
                (PedidoID, NumeroOC, FechaOC, Estado)
            OUTPUT INSERTED.OrdenCompraID
            VALUES
                (@PedidoID, @NumeroOC, @FechaOC, @Estado);
            """;

        await using var cmd = new SqlCommand(sql, cn);

        cmd.Parameters.Add("@PedidoID", SqlDbType.Int)
            .Value = request.PedidoID;

        cmd.Parameters.Add("@NumeroOC", SqlDbType.VarChar, 50)
            .Value = request.NumeroOC.Trim();

        cmd.Parameters.Add("@FechaOC", SqlDbType.DateTime)
            .Value = DateTime.Now;

        cmd.Parameters.Add("@Estado", SqlDbType.VarChar, 30)
            .Value = "Pendiente";

        try
        {
            int id = Convert.ToInt32(await cmd.ExecuteScalarAsync());

            return CreatedAtAction(
                nameof(ObtenerPorId),
                new { id },
                new
                {
                    ordenCompraID = id,
                    mensaje = "Orden de compra creada correctamente."
                });
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627)
        {
            return Conflict(new
            {
                error = "El número de orden de compra ya existe."
            });
        }
    }

    [HttpPut("{id:int}/estado")]
    public async Task<IActionResult> CambiarEstado(
        int id, [FromBody] string estado)
    {
        string[] estadosPermitidos =
        {
            "Pendiente", "EnPreparacion", "Completada", "Cancelada"
        };

        if (!estadosPermitidos.Contains(estado))
            return BadRequest(new
            {
                error = "Estado inválido.",
                estadosPermitidos
            });

        await using var cn = new SqlConnection(_connectionString);
        await cn.OpenAsync();

        const string sql = """
            UPDATE dbo.OrdenCompra
            SET Estado = @Estado
            WHERE OrdenCompraID = @Id;
            """;

        await using var cmd = new SqlCommand(sql, cn);

        cmd.Parameters.Add("@Id", SqlDbType.Int).Value = id;
        cmd.Parameters.Add("@Estado", SqlDbType.VarChar, 30)
            .Value = estado;

        int filas = await cmd.ExecuteNonQueryAsync();

        if (filas == 0)
            return NotFound(new
            {
                mensaje = "Orden de compra no encontrada."
            });

        return Ok(new { mensaje = "Estado actualizado correctamente." });
    }

    private static OrdenCompra LeerOrden(SqlDataReader rd) => new()
    {
        OrdenCompraID = Convert.ToInt32(rd["OrdenCompraID"]),
        PedidoID = Convert.ToInt32(rd["PedidoID"]),
        NumeroOC = Convert.ToString(rd["NumeroOC"])!,
        FechaOC = Convert.ToDateTime(rd["FechaOC"]),
        Estado = Convert.ToString(rd["Estado"])!
    };
}
