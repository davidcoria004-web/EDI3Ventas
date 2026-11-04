
using EDI3Ventas.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;

namespace EDI3Ventas.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdenesCompraController : ControllerBase
{
    private readonly IConfiguration _configuration;

    public OrdenesCompraController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    private string ObtenerConexion()
    {
        return _configuration.GetConnectionString("VentasDB")
            ?? throw new InvalidOperationException(
                "No se encontró la cadena de conexión VentasDB.");
    }

    // GET: api/OrdenesCompra
    [HttpGet]
    public async Task<IActionResult> ObtenerTodas()
    {
        var ordenes = new Dictionary<int, OrdenCompra>();

        const string sql = @"
            SELECT
                oc.OrdenCompraID,
                oc.NroOrdenCompra,
                oc.FechaOC,
                oc.ClienteID,
                oc.Estado,
                oc.Observaciones,
                d.DetalleOCID,
                d.ProductoID,
                p.Nombre AS NombreProducto,
                d.Cantidad,
                d.PrecioUnitario
            FROM dbo.OrdenCompra oc
            LEFT JOIN dbo.DetalleOrdenCompra d
                ON oc.OrdenCompraID = d.OrdenCompraID
            LEFT JOIN dbo.Producto p
                ON d.ProductoID = p.ProductoID
            ORDER BY oc.OrdenCompraID DESC;";

        await using var conexion =
            new SqlConnection(ObtenerConexion());

        await using var comando =
            new SqlCommand(sql, conexion);

        await conexion.OpenAsync();

        await using var reader =
            await comando.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            int id = reader.GetInt32(
                reader.GetOrdinal("OrdenCompraID"));

            if (!ordenes.TryGetValue(id, out var orden))
            {
                orden = new OrdenCompra
                {
                    OrdenCompraID = id,

                    NroOrdenCompra = reader.GetString(
                        reader.GetOrdinal("NroOrdenCompra")),

                    FechaOC = reader.GetDateTime(
                        reader.GetOrdinal("FechaOC")),

                    ClienteID = reader.GetInt32(
                        reader.GetOrdinal("ClienteID")),

                    Estado = reader.GetString(
                        reader.GetOrdinal("Estado")),

                    Observaciones = reader.IsDBNull(
                        reader.GetOrdinal("Observaciones"))
                        ? null
                        : reader.GetString(
                            reader.GetOrdinal("Observaciones"))
                };

                ordenes.Add(id, orden);
            }

            if (!reader.IsDBNull(
                reader.GetOrdinal("DetalleOCID")))
            {
                orden.Detalles.Add(new DetalleOrdenCompra
                {
                    DetalleOCID = reader.GetInt32(
                        reader.GetOrdinal("DetalleOCID")),

                    OrdenCompraID = id,

                    ProductoID = reader.GetInt32(
                        reader.GetOrdinal("ProductoID")),

                    NombreProducto = reader.IsDBNull(
                        reader.GetOrdinal("NombreProducto"))
                        ? null
                        : reader.GetString(
                            reader.GetOrdinal("NombreProducto")),

                    Cantidad = reader.GetDecimal(
                        reader.GetOrdinal("Cantidad")),

                    PrecioUnitario = reader.GetDecimal(
                        reader.GetOrdinal("PrecioUnitario"))
                });
            }
        }

        return Ok(ordenes.Values);
    }

    // GET: api/OrdenesCompra/5
    [HttpGet("{id:int}")]
    public async Task<IActionResult> ObtenerPorId(int id)
    {
        var ordenes = await ObtenerOrdenes(id);

        if (ordenes.Count == 0)
        {
            return NotFound(new
            {
                mensaje = "No se encontró la Orden de Compra."
            });
        }

        return Ok(ordenes[0]);
    }

    // POST: api/OrdenesCompra
    [HttpPost]
    public async Task<IActionResult> Crear(
        [FromBody] OrdenCompraRequest request)
    {
        if (request == null)
        {
            return BadRequest("Debe enviar los datos de la Orden de Compra.");
        }

        if (string.IsNullOrWhiteSpace(request.NroOrdenCompra))
        {
            return BadRequest(
                "El número de Orden de Compra es obligatorio.");
        }

        if (request.ClienteID <= 0)
        {
            return BadRequest("Debe indicar un cliente válido.");
        }

        if (request.Detalles == null ||
            request.Detalles.Count == 0)
        {
            return BadRequest(
                "La Orden de Compra debe tener al menos un producto.");
        }

        if (request.Detalles.Any(d =>
            d.ProductoID <= 0 ||
            d.Cantidad <= 0 ||
            d.PrecioUnitario < 0))
        {
            return BadRequest(
                "Los productos deben tener identificadores válidos, cantidades positivas y precios no negativos.");
        }

        if (request.Detalles
            .GroupBy(d => d.ProductoID)
            .Any(g => g.Count() > 1))
        {
            return BadRequest(
                "No se puede repetir un producto en el detalle de la orden.");
        }

        await using var conexion =
            new SqlConnection(ObtenerConexion());

        await conexion.OpenAsync();

        await using var transaccion =
            (SqlTransaction)await conexion.BeginTransactionAsync();

        try
        {
            // Verificar que el cliente exista y esté activo.
            const string sqlCliente = @"
                SELECT COUNT(1)
                FROM dbo.Cliente
                WHERE ClienteID = @ClienteID
                  AND Activo = 1;";

            await using (var cmdCliente =
                new SqlCommand(sqlCliente, conexion, transaccion))
            {
                cmdCliente.Parameters.Add(
                    "@ClienteID", SqlDbType.Int)
                    .Value = request.ClienteID;

                int existe = Convert.ToInt32(
                    await cmdCliente.ExecuteScalarAsync());

                if (existe == 0)
                {
                    await transaccion.RollbackAsync();

                    return BadRequest(
                        "El cliente no existe o está inactivo.");
                }
            }

            // Verificar que todos los productos existan y estén activos.
            const string sqlProducto = @"
                SELECT COUNT(1)
                FROM dbo.Producto
                WHERE ProductoID = @ProductoID
                  AND Activo = 1;";

            foreach (var detalle in request.Detalles)
            {
                await using var cmdProducto =
                    new SqlCommand(sqlProducto, conexion, transaccion);

                cmdProducto.Parameters.Add(
                    "@ProductoID", SqlDbType.Int)
                    .Value = detalle.ProductoID;

                int existe = Convert.ToInt32(
                    await cmdProducto.ExecuteScalarAsync());

                if (existe == 0)
                {
                    await transaccion.RollbackAsync();

                    return BadRequest(
                        $"El producto {detalle.ProductoID} no existe o está inactivo.");
                }
            }

            // Registrar la cabecera de la Orden de Compra.
            const string sqlOrden = @"
                INSERT INTO dbo.OrdenCompra
                (
                    NroOrdenCompra,
                    FechaOC,
                    ClienteID,
                    Estado,
                    Observaciones
                )
                OUTPUT INSERTED.OrdenCompraID
                VALUES
                (
                    @NroOrdenCompra,
                    @FechaOC,
                    @ClienteID,
                    @Estado,
                    @Observaciones
                );";

            int ordenCompraID;

            await using (var cmdOrden =
                new SqlCommand(sqlOrden, conexion, transaccion))
            {
                cmdOrden.Parameters.Add(
                    "@NroOrdenCompra", SqlDbType.VarChar, 50)
                    .Value = request.NroOrdenCompra.Trim();

                cmdOrden.Parameters.Add(
                    "@FechaOC", SqlDbType.DateTime)
                    .Value = DateTime.Now;

                cmdOrden.Parameters.Add(
                    "@ClienteID", SqlDbType.Int)
                    .Value = request.ClienteID;

                cmdOrden.Parameters.Add(
                    "@Estado", SqlDbType.VarChar, 30)
                    .Value = "Pendiente";

                cmdOrden.Parameters.Add(
                    "@Observaciones", SqlDbType.VarChar, -1)
                    .Value = (object?)request.Observaciones
                        ?? DBNull.Value;

                ordenCompraID = Convert.ToInt32(
                    await cmdOrden.ExecuteScalarAsync());
            }

            // Registrar los detalles de la Orden de Compra.
            const string sqlDetalle = @"
                INSERT INTO dbo.DetalleOrdenCompra
                (
                    OrdenCompraID,
                    ProductoID,
                    Cantidad,
                    PrecioUnitario
                )
                VALUES
                (
                    @OrdenCompraID,
                    @ProductoID,
                    @Cantidad,
                    @PrecioUnitario
                );";

            foreach (var detalle in request.Detalles)
            {
                await using var cmdDetalle =
                    new SqlCommand(sqlDetalle, conexion, transaccion);

                cmdDetalle.Parameters.Add(
                    "@OrdenCompraID", SqlDbType.Int)
                    .Value = ordenCompraID;

                cmdDetalle.Parameters.Add(
                    "@ProductoID", SqlDbType.Int)
                    .Value = detalle.ProductoID;

                var cantidad = cmdDetalle.Parameters.Add(
                    "@Cantidad", SqlDbType.Decimal);

                cantidad.Precision = 12;
                cantidad.Scale = 2;
                cantidad.Value = detalle.Cantidad;

                var precio = cmdDetalle.Parameters.Add(
                    "@PrecioUnitario", SqlDbType.Decimal);

                precio.Precision = 12;
                precio.Scale = 2;
                precio.Value = detalle.PrecioUnitario;

                await cmdDetalle.ExecuteNonQueryAsync();
            }

            await transaccion.CommitAsync();

            return CreatedAtAction(
                nameof(ObtenerPorId),
                new { id = ordenCompraID },
                new
                {
                    mensaje = "Orden de Compra registrada correctamente.",
                    OrdenCompraID = ordenCompraID
                });
        }
        catch (SqlException ex)
            when (ex.Number == 2601 || ex.Number == 2627)
        {
            await transaccion.RollbackAsync();

            return Conflict(
                "Ya existe una Orden de Compra con ese número.");
        }
        catch
        {
            await transaccion.RollbackAsync();
            throw;
        }
    }

    // PUT: api/OrdenesCompra/5/estado
    // Al aprobar la OC, genera automáticamente su factura.
    [HttpPut("{id:int}/estado")]
    public async Task<IActionResult> ActualizarEstado(
        int id,
        [FromBody] string nuevoEstado)
    {
        string[] estadosPermitidos =
        {
            "Pendiente",
            "Aprobada",
            "Rechazada"
        };

        if (string.IsNullOrWhiteSpace(nuevoEstado) ||
            !estadosPermitidos.Any(e =>
                e.Equals(
                    nuevoEstado.Trim(),
                    StringComparison.OrdinalIgnoreCase)))
        {
            return BadRequest(
                "Estado inválido. Los estados permitidos son Pendiente, Aprobada y Rechazada.");
        }

        string estado = estadosPermitidos.First(e =>
            e.Equals(
                nuevoEstado.Trim(),
                StringComparison.OrdinalIgnoreCase));

        await using var conexion =
            new SqlConnection(ObtenerConexion());

        await conexion.OpenAsync();

        // La transacción Serializable y el bloqueo de la OC
        // evitan aprobaciones simultáneas de la misma orden.
        await using var transaccion =
            (SqlTransaction)await conexion.BeginTransactionAsync(
                IsolationLevel.Serializable);

        try
        {
            // 1. Buscar y bloquear la Orden de Compra.
            const string sqlBuscar = @"
                SELECT Estado
                FROM dbo.OrdenCompra WITH (UPDLOCK, HOLDLOCK)
                WHERE OrdenCompraID = @ID;";

            string? estadoActual;

            await using (var cmd = new SqlCommand(
                sqlBuscar, conexion, transaccion))
            {
                cmd.Parameters.Add(
                    "@ID", SqlDbType.Int).Value = id;

                object? resultado =
                    await cmd.ExecuteScalarAsync();

                estadoActual =
                    resultado == null || resultado == DBNull.Value
                    ? null
                    : Convert.ToString(resultado);
            }

            if (estadoActual == null)
            {
                await transaccion.RollbackAsync();

                return NotFound(
                    "No se encontró la Orden de Compra.");
            }

            // 2. Comprobar si ya existe una factura.
            const string sqlFacturaExistente = @"
                SELECT NumeroFactura
                FROM dbo.Factura
                WHERE OrdenCompraID = @ID;";

            string? numeroFactura = null;

            await using (var cmd = new SqlCommand(
                sqlFacturaExistente, conexion, transaccion))
            {
                cmd.Parameters.Add(
                    "@ID", SqlDbType.Int).Value = id;

                object? resultado =
                    await cmd.ExecuteScalarAsync();

                if (resultado != null &&
                    resultado != DBNull.Value)
                {
                    numeroFactura = Convert.ToString(resultado);
                }
            }

            bool facturaCreada = false;

            // Si ya tiene factura, no permitimos volver a
            // Pendiente o Rechazada.
            if (numeroFactura != null && estado != "Aprobada")
            {
                await transaccion.RollbackAsync();

                return Conflict(
                    "La Orden de Compra ya tiene una factura emitida y no puede cambiar a ese estado.");
            }

            // 3. Si se aprueba y no tiene factura,
            // calcular el importe total de los detalles.
            if (estado == "Aprobada" && numeroFactura == null)
            {
                const string sqlTotal = @"
                    SELECT SUM(Cantidad * PrecioUnitario)
                    FROM dbo.DetalleOrdenCompra
                    WHERE OrdenCompraID = @ID;";

                decimal importeTotal;

                await using (var cmd = new SqlCommand(
                    sqlTotal, conexion, transaccion))
                {
                    cmd.Parameters.Add(
                        "@ID", SqlDbType.Int).Value = id;

                    object? resultado =
                        await cmd.ExecuteScalarAsync();

                    if (resultado == null ||
                        resultado == DBNull.Value)
                    {
                        await transaccion.RollbackAsync();

                        return BadRequest(
                            "No se puede aprobar la OC porque no tiene detalles.");
                    }

                    importeTotal = Convert.ToDecimal(resultado);
                }

                // 4. Generar el número interno de factura.
                numeroFactura = $"FAC-{id:D6}";

                // 5. Insertar la factura.
                const string sqlInsertarFactura = @"
                    INSERT INTO dbo.Factura
                    (
                        OrdenCompraID,
                        NumeroFactura,
                        FechaEmision,
                        ImporteTotal,
                        EstadoPago
                    )
                    VALUES
                    (
                        @OrdenCompraID,
                        @NumeroFactura,
                        @FechaEmision,
                        @ImporteTotal,
                        @EstadoPago
                    );";

                await using (var cmd = new SqlCommand(
                    sqlInsertarFactura, conexion, transaccion))
                {
                    cmd.Parameters.Add(
                        "@OrdenCompraID", SqlDbType.Int)
                        .Value = id;

                    cmd.Parameters.Add(
                        "@NumeroFactura", SqlDbType.VarChar, 30)
                        .Value = numeroFactura;

                    cmd.Parameters.Add(
                        "@FechaEmision", SqlDbType.DateTime2)
                        .Value = DateTime.Now;

                    var importe = cmd.Parameters.Add(
                        "@ImporteTotal", SqlDbType.Decimal);

                    importe.Precision = 12;
                    importe.Scale = 2;
                    importe.Value = importeTotal;

                    cmd.Parameters.Add(
                        "@EstadoPago", SqlDbType.NVarChar, 20)
                        .Value = "Pendiente";

                    await cmd.ExecuteNonQueryAsync();
                }

                facturaCreada = true;
            }

            // 6. Actualizar el estado de la Orden de Compra.
            const string sqlActualizar = @"
                UPDATE dbo.OrdenCompra
                SET Estado = @Estado
                WHERE OrdenCompraID = @ID;";

            await using (var cmd = new SqlCommand(
                sqlActualizar, conexion, transaccion))
            {
                cmd.Parameters.Add(
                    "@Estado", SqlDbType.VarChar, 30)
                    .Value = estado;

                cmd.Parameters.Add(
                    "@ID", SqlDbType.Int)
                    .Value = id;

                await cmd.ExecuteNonQueryAsync();
            }

            // 7. Confirmar ambas operaciones.
            await transaccion.CommitAsync();

            return Ok(new
            {
                mensaje = estado == "Aprobada"
                    ? facturaCreada
                        ? "Orden de Compra aprobada y factura generada correctamente."
                        : "La Orden de Compra ya estaba facturada. No se generó otra factura."
                    : "Estado de la Orden de Compra actualizado correctamente.",

                OrdenCompraID = id,
                Estado = estado,
                FacturaCreada = facturaCreada,
                NumeroFactura = numeroFactura
            });
        }
        catch (SqlException ex)
            when (ex.Number == 2601 || ex.Number == 2627)
        {
            await transaccion.RollbackAsync();

            return Conflict(
                "No se pudo generar la factura porque ya existe una factura para esta Orden de Compra.");
        }
        catch
        {
            await transaccion.RollbackAsync();
            throw;
        }
    }

    // Método privado para recuperar una Orden de Compra.
    private async Task<List<OrdenCompra>> ObtenerOrdenes(
        int? id = null)
    {
        var ordenes = new Dictionary<int, OrdenCompra>();

        const string sql = @"
            SELECT
                oc.OrdenCompraID,
                oc.NroOrdenCompra,
                oc.FechaOC,
                oc.ClienteID,
                oc.Estado,
                oc.Observaciones,
                d.DetalleOCID,
                d.ProductoID,
                p.Nombre AS NombreProducto,
                d.Cantidad,
                d.PrecioUnitario
            FROM dbo.OrdenCompra oc
            LEFT JOIN dbo.DetalleOrdenCompra d
                ON oc.OrdenCompraID = d.OrdenCompraID
            LEFT JOIN dbo.Producto p
                ON d.ProductoID = p.ProductoID
            WHERE (@ID IS NULL OR oc.OrdenCompraID = @ID)
            ORDER BY oc.OrdenCompraID DESC;";

        await using var conexion =
            new SqlConnection(ObtenerConexion());

        await using var comando =
            new SqlCommand(sql, conexion);

        comando.Parameters.Add(
            "@ID", SqlDbType.Int)
            .Value = (object?)id ?? DBNull.Value;

        await conexion.OpenAsync();

        await using var reader =
            await comando.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            int ordenID = reader.GetInt32(
                reader.GetOrdinal("OrdenCompraID"));

            if (!ordenes.TryGetValue(ordenID, out var orden))
            {
                orden = new OrdenCompra
                {
                    OrdenCompraID = ordenID,

                    NroOrdenCompra = reader.GetString(
                        reader.GetOrdinal("NroOrdenCompra")),

                    FechaOC = reader.GetDateTime(
                        reader.GetOrdinal("FechaOC")),

                    ClienteID = reader.GetInt32(
                        reader.GetOrdinal("ClienteID")),

                    Estado = reader.GetString(
                        reader.GetOrdinal("Estado")),

                    Observaciones = reader.IsDBNull(
                        reader.GetOrdinal("Observaciones"))
                        ? null
                        : reader.GetString(
                            reader.GetOrdinal("Observaciones"))
                };

                ordenes.Add(ordenID, orden);
            }

            if (!reader.IsDBNull(
                reader.GetOrdinal("DetalleOCID")))
            {
                orden.Detalles.Add(new DetalleOrdenCompra
                {
                    DetalleOCID = reader.GetInt32(
                        reader.GetOrdinal("DetalleOCID")),

                    OrdenCompraID = ordenID,

                    ProductoID = reader.GetInt32(
                        reader.GetOrdinal("ProductoID")),

                    NombreProducto = reader.IsDBNull(
                        reader.GetOrdinal("NombreProducto"))
                        ? null
                        : reader.GetString(
                            reader.GetOrdinal("NombreProducto")),

                    Cantidad = reader.GetDecimal(
                        reader.GetOrdinal("Cantidad")),

                    PrecioUnitario = reader.GetDecimal(
                        reader.GetOrdinal("PrecioUnitario"))
                });
            }
        }

        return ordenes.Values.ToList();
    }
}
