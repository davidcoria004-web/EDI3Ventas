
using EDI3Ventas.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace EDI3Ventas.Controllers;

[ApiController]
[Route("api/clientes")]
public class ClientesController : ControllerBase
{
    private readonly string _connectionString;

    public ClientesController(IConfiguration configuration)
    {
        _connectionString =
            configuration.GetConnectionString("VentasDB")
            ?? throw new InvalidOperationException(
                "No se encontro la conexion VentasDB.");
    }

    [HttpGet]
    public async Task<IActionResult> ObtenerTodos()
    {
        var clientes = new List<Cliente>();

        await using var cn = new SqlConnection(_connectionString);
        await cn.OpenAsync();

        const string sql = """
            SELECT ClienteID, Nombre, Apellido, Documento,
                   Email, Telefono, Direccion, FechaAlta, Activo, Categoria
            FROM dbo.Cliente
            ORDER BY Apellido, Nombre;
            """;

        await using var cmd = new SqlCommand(sql, cn);
        await using var rd = await cmd.ExecuteReaderAsync();

        while (await rd.ReadAsync())
        {
            clientes.Add(LeerCliente(rd));
        }

        return Ok(clientes);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> ObtenerPorId(int id)
    {
        await using var cn = new SqlConnection(_connectionString);
        await cn.OpenAsync();

        const string sql = """
            SELECT ClienteID, Nombre, Apellido, Documento,
                   Email, Telefono, Direccion, FechaAlta, Activo, Categoria
            FROM dbo.Cliente
            WHERE ClienteID = @Id;
            """;

        await using var cmd = new SqlCommand(sql, cn);
        cmd.Parameters.AddWithValue("@Id", id);

        await using var rd = await cmd.ExecuteReaderAsync();

        if (!await rd.ReadAsync())
            return NotFound(new { mensaje = "Cliente no encontrado." });

        return Ok(LeerCliente(rd));
    }

    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] ClienteRequest cliente)
    {
        if (string.IsNullOrWhiteSpace(cliente.Nombre) ||
            string.IsNullOrWhiteSpace(cliente.Apellido))
        {
            return BadRequest(new
            {
                error = "Nombre y apellido son obligatorios."
            });
        }

        await using var cn = new SqlConnection(_connectionString);
        await cn.OpenAsync();

        const string sql = """
            INSERT INTO dbo.Cliente
                (Nombre, Apellido, Documento, Email, Telefono,
                 Direccion, FechaAlta, Activo)
            OUTPUT INSERTED.ClienteID
            VALUES
                (@Nombre, @Apellido, @Documento, @Email, @Telefono,
                 @Direccion, @FechaAlta, 1);
            """;

        await using var cmd = new SqlCommand(sql, cn);
        AgregarParametrosCliente(cmd, cliente);
        cmd.Parameters.AddWithValue("@FechaAlta", DateTime.Now);

        var valor = await cmd.ExecuteScalarAsync();
        if (valor is not int id)
            return StatusCode(500, new { error = "No se obtuvo el ID del cliente creado." });

        return CreatedAtAction(
            nameof(ObtenerPorId),
            new { id },
            new { clienteID = id, mensaje = "Cliente creado correctamente." });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Actualizar(
        int id, [FromBody] ClienteRequest cliente)
    {
        if (string.IsNullOrWhiteSpace(cliente.Nombre) ||
            string.IsNullOrWhiteSpace(cliente.Apellido))
        {
            return BadRequest(new
            {
                error = "Nombre y apellido son obligatorios."
            });
        }

        await using var cn = new SqlConnection(_connectionString);
        await cn.OpenAsync();

        const string sql = """
            UPDATE dbo.Cliente
            SET Nombre = @Nombre,
                Apellido = @Apellido,
                Documento = @Documento,
                Email = @Email,
                Telefono = @Telefono,
                Direccion = @Direccion
            WHERE ClienteID = @Id AND Activo = 1;
            """;

        await using var cmd = new SqlCommand(sql, cn);
        cmd.Parameters.AddWithValue("@Id", id);
        AgregarParametrosCliente(cmd, cliente);

        int filas = await cmd.ExecuteNonQueryAsync();

        if (filas == 0)
            return NotFound(new { mensaje = "Cliente inexistente o inactivo." });

        return Ok(new { mensaje = "Cliente actualizado correctamente." });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Eliminar(int id)
    {
        await using var cn = new SqlConnection(_connectionString);
        await cn.OpenAsync();

        const string sql = """
            UPDATE dbo.Cliente
            SET Activo = 0
            WHERE ClienteID = @Id AND Activo = 1;
            """;

        await using var cmd = new SqlCommand(sql, cn);
        cmd.Parameters.AddWithValue("@Id", id);

        int filas = await cmd.ExecuteNonQueryAsync();

        if (filas == 0)
            return NotFound(new { mensaje = "Cliente inexistente o ya inactivo." });

        return Ok(new { mensaje = "Cliente dado de baja correctamente." });
    }

    private static void AgregarParametrosCliente(
        SqlCommand cmd, ClienteRequest cliente)
    {
        cmd.Parameters.AddWithValue("@Nombre", cliente.Nombre.Trim());
        cmd.Parameters.AddWithValue("@Apellido", cliente.Apellido.Trim());
        cmd.Parameters.AddWithValue("@Documento",
            (object?)cliente.Documento ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Email",
            (object?)cliente.Email ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Telefono",
            (object?)cliente.Telefono ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Direccion",
            (object?)cliente.Direccion ?? DBNull.Value);
    }

    private static Cliente LeerCliente(SqlDataReader rd)
    {
        return new Cliente
        {
            ClienteID = rd.GetInt32(rd.GetOrdinal("ClienteID")),
            Nombre = rd.GetString(rd.GetOrdinal("Nombre")),
            Apellido = rd.GetString(rd.GetOrdinal("Apellido")),
            Documento = rd["Documento"] is DBNull
                ? null : rd["Documento"].ToString(),
            Email = rd["Email"] is DBNull
                ? null : rd["Email"].ToString(),
            Telefono = rd["Telefono"] is DBNull
                ? null : rd["Telefono"].ToString(),
            Direccion = rd["Direccion"] is DBNull
                ? null : rd["Direccion"].ToString(),
            FechaAlta = rd["FechaAlta"] is DBNull
                ? null : Convert.ToDateTime(rd["FechaAlta"]),
            Activo = Convert.ToBoolean(rd["Activo"]),
            Categoria = rd.GetString(rd.GetOrdinal("Categoria"))
        };
    }
}
