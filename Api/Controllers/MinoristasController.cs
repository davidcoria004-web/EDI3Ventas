using System.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace EDI3Ventas.Controllers;

[ApiController]
[Route("api/minoristas")]
public sealed class MinoristasController(IConfiguration configuracion) : ControllerBase
{
    private SqlConnection Conexion() => new(configuracion.GetConnectionString("VentasDB")
        ?? throw new InvalidOperationException("Falta ConnectionStrings:VentasDB."));

    private static SqlCommand Cmd(SqlConnection cn, SqlTransaction? tx, string sql, params (string, object?)[] args)
    {
        var cmd = new SqlCommand(sql, cn, tx);
        foreach (var (clave, valor) in args) cmd.Parameters.AddWithValue(clave, valor ?? DBNull.Value);
        return cmd;
    }

    private static async Task<int> Ejecutar(SqlConnection cn, SqlTransaction? tx, string sql, params (string, object?)[] args)
    {
        await using var cmd = Cmd(cn, tx, sql, args);
        return await cmd.ExecuteNonQueryAsync();
    }

    private static async Task<object?> Escalar(SqlConnection cn, SqlTransaction? tx, string sql, params (string, object?)[] args)
    {
        await using var cmd = Cmd(cn, tx, sql, args);
        return await cmd.ExecuteScalarAsync();
    }

    private static int Int(object? valor) => valor is null or DBNull ? 0 : Convert.ToInt32(valor);
    private static decimal Decimal(object? valor) => valor is null or DBNull ? 0m : Convert.ToDecimal(valor);
    private static bool EsDuplicado(SqlException ex) => ex.Number is 2601 or 2627;

    [HttpGet("ordenes")]
    public async Task<IActionResult> ListarOrdenes()
    {
        var lista = new List<OrdenVista>();
        await using var cn = Conexion(); await cn.OpenAsync();
        const string sql = """
            SELECT o.OrdenCompraID,o.ClienteID,o.NumeroOC,o.FechaOC,o.FechaEntregaSolicitada,o.Estado,
                   c.Apellido+N', '+c.Nombre AS Cliente,
                   COALESCE((SELECT SUM(d.Cantidad*d.PrecioUnitario)
                   FROM dbo.DetalleOrdenCompra d WHERE d.OrdenCompraID=o.OrdenCompraID),0),
                   f.FacturaID,f.EstadoPago
            FROM dbo.OrdenCompra o JOIN dbo.Cliente c ON o.ClienteID=c.ClienteID
            OUTER APPLY(SELECT TOP 1 FacturaID,EstadoPago FROM dbo.Factura
                        WHERE OrdenCompraID=o.OrdenCompraID ORDER BY FacturaID DESC) f
            ORDER BY o.OrdenCompraID DESC;
            """;
        await using (var cmd = Cmd(cn, null, sql))
        await using (var rd = await cmd.ExecuteReaderAsync())
        {
            while (await rd.ReadAsync()) lista.Add(new OrdenVista
            {
                OrdenCompraID = rd.GetInt32(0), ClienteID = rd.GetInt32(1), NumeroOC = rd.GetString(2),
                FechaOC = rd.GetDateTime(3), FechaEntregaSolicitada = rd.IsDBNull(4) ? null : rd.GetDateTime(4),
                Estado = rd.GetString(5), Cliente = rd.GetString(6), ImporteTotal = rd.GetDecimal(7),
                FacturaID = rd.IsDBNull(8) ? null : rd.GetInt32(8), EstadoPago = rd.IsDBNull(9) ? null : rd.GetString(9)
            });
        }
        const string detalleSql = """
            SELECT d.OrdenCompraID,d.ProductoID,p.Nombre,d.Cantidad,d.PrecioUnitario
            FROM dbo.DetalleOrdenCompra d INNER JOIN dbo.Producto p ON p.ProductoID=d.ProductoID;
            """;
        var porId = lista.ToDictionary(x => x.OrdenCompraID);
        await using (var cmd = Cmd(cn, null, detalleSql))
        await using (var rd = await cmd.ExecuteReaderAsync())
        {
            while (await rd.ReadAsync() && porId.Count > 0)
            {
                if (porId.TryGetValue(rd.GetInt32(0), out var orden)) orden.Detalles.Add(new DetalleVista
                { ProductoID = rd.GetInt32(1), NombreProducto = rd.GetString(2), Cantidad = rd.GetDecimal(3), PrecioUnitario = rd.GetDecimal(4) });
            }
        }
        return Ok(lista);
    }

    [HttpGet("ordenes/{id:int}")]
    public async Task<IActionResult> ObtenerOrden(int id)
    {
        var datos = (OkObjectResult)await ListarOrdenes();
        var item = ((List<OrdenVista>)datos.Value!).FirstOrDefault(x => x.OrdenCompraID == id);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost("ordenes")]
    public async Task<IActionResult> CrearOrden([FromBody] OrdenCompraNuevaDto request)
    {
        if (request.ClienteID <= 0 || request.FechaEntregaSolicitada is null || request.Detalles is null || request.Detalles.Count == 0 ||
            request.Detalles.Any(x => x.ProductoID <= 0 || x.Cantidad <= 0 || x.Cantidad > 999999999m || x.Cantidad != decimal.Round(x.Cantidad, 3)) ||
            request.Detalles.Select(x => x.ProductoID).Distinct().Count() != request.Detalles.Count)
            return BadRequest(new { error = "Cliente, fecha solicitada, productos y cantidades validas son obligatorios." });
        await using var cn = Conexion(); await cn.OpenAsync();
        await using var tx = (SqlTransaction)await cn.BeginTransactionAsync(IsolationLevel.Serializable);
        var existe = Int(await Escalar(cn, tx, "SELECT COUNT(*) FROM dbo.Cliente WHERE ClienteID=@id AND Activo=1 AND Categoria=N'Minorista'", ("@id", request.ClienteID)));
        if (existe == 0) return BadRequest(new { error = "Cliente no existe, esta inactivo o no es minorista." });
        try
        {
            int ordenId = Int(await Escalar(cn, tx, """
                INSERT INTO dbo.OrdenCompra(ClienteID,FechaEntregaSolicitada,NumeroOC,Estado)
                OUTPUT INSERTED.OrdenCompraID VALUES(@cliente,@fecha,@temporal,N'Registrada')
                """, ("@cliente", request.ClienteID), ("@fecha", request.FechaEntregaSolicitada.Value.Date),
                ("@temporal", "TMP-" + Guid.NewGuid().ToString("N")[..26])));
            string numeroOC = $"OC-{ordenId:D6}";
            await Ejecutar(cn, tx, "UPDATE dbo.OrdenCompra SET NumeroOC=@numero WHERE OrdenCompraID=@id",
                ("@numero", numeroOC), ("@id", ordenId));
            decimal total = 0m;
            foreach (var item in request.Detalles)
            {
                object? valor = await Escalar(cn, tx,
                    "SELECT PrecioUnitario FROM dbo.Producto WHERE ProductoID=@id AND Activo=1", ("@id", item.ProductoID));
                if (valor is null or DBNull) return BadRequest(new { error = $"Producto {item.ProductoID} no existe o esta inactivo." });
                decimal precio = Convert.ToDecimal(valor);
                total += item.Cantidad * precio;
                await Ejecutar(cn, tx, """
                    INSERT INTO dbo.DetalleOrdenCompra(OrdenCompraID,ProductoID,Cantidad,PrecioUnitario)
                    VALUES(@orden,@producto,@cantidad,@precio)
                    """, ("@orden", ordenId), ("@producto", item.ProductoID), ("@cantidad", item.Cantidad), ("@precio", precio));
            }
            total = Math.Round(total, 2);
            if (total <= 0 || total > 9999999999.99m)
                return BadRequest(new { error = "El total debe ser positivo y estar dentro del rango permitido." });
            string facturaNumero = $"FAC-{ordenId:D6}";
            int facturaId = Int(await Escalar(cn, tx, """
                INSERT INTO dbo.Factura(OrdenCompraID,NumeroFactura,ImporteTotal,EstadoPago)
                OUTPUT INSERTED.FacturaID VALUES(@orden,@numero,@total,N'Pendiente')
                """, ("@orden", ordenId), ("@numero", facturaNumero), ("@total", total)));
            await tx.CommitAsync();
            return Created($"/api/minoristas/ordenes/{ordenId}",
                new { ordenCompraID = ordenId, numeroOC, facturaID = facturaId, numeroFactura = facturaNumero });
        }
        catch (SqlException ex) when (EsDuplicado(ex))
        {
            await tx.RollbackAsync();
            return Conflict(new { error = "Ya existe una orden de compra con ese numero." });
        }
    }

    [HttpGet("facturas")]
    public async Task<IActionResult> ListarFacturas()
    {
        var lista = new List<FacturaVista>();
        await using var cn = Conexion(); await cn.OpenAsync();
        const string sql = """
            SELECT f.FacturaID,f.OrdenCompraID,f.NumeroFactura,
                   c.Apellido+N', '+c.Nombre AS Cliente,f.FechaEmision,f.ImporteTotal,
                   COALESCE((SELECT SUM(pg.Importe) FROM dbo.Pago pg WHERE pg.FacturaID=f.FacturaID),0) Pagado,
                   f.EstadoPago
            FROM dbo.Factura f JOIN dbo.OrdenCompra o ON o.OrdenCompraID=f.OrdenCompraID
            JOIN dbo.Cliente c ON c.ClienteID=o.ClienteID
            ORDER BY f.FacturaID DESC;
            """;
        await using var cmd = Cmd(cn, null, sql); await using var rd = await cmd.ExecuteReaderAsync();
        while (await rd.ReadAsync())
        {
            decimal total = rd.GetDecimal(5), pagado = rd.GetDecimal(6);
            lista.Add(new FacturaVista
            {
                FacturaID = rd.GetInt32(0), OrdenCompraID = rd.GetInt32(1), NumeroFactura = rd.GetString(2),
                Cliente = rd.GetString(3), FechaEmision = rd.GetDateTime(4), ImporteTotal = total,
                TotalPagado = pagado, Saldo = Math.Max(0m, total - pagado), EstadoPago = rd.GetString(7)
            });
        }
        return Ok(lista);
    }

    [HttpGet("facturas/{id:int}")]
    public async Task<IActionResult> ObtenerFactura(int id)
    {
        var datos = (OkObjectResult)await ListarFacturas();
        var item = ((List<FacturaVista>)datos.Value!).FirstOrDefault(x => x.FacturaID == id);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost("facturas/{id:int}/pagos")]
    public async Task<IActionResult> RegistrarPago(int id, [FromBody] PagoNuevoDto request)
    {
        if (request.Importe <= 0 || request.Importe != decimal.Round(request.Importe, 2) ||
            string.IsNullOrWhiteSpace(request.MedioPago) || request.MedioPago.Length > 30 || request.Referencia?.Length > 100)
            return BadRequest(new { error = "Importe positivo de dos decimales y medio de pago obligatorios." });
        await using var cn = Conexion(); await cn.OpenAsync();
        await using var tx = (SqlTransaction)await cn.BeginTransactionAsync(IsolationLevel.Serializable);
        object? existente = await Escalar(cn, tx,
            "SELECT ImporteTotal FROM dbo.Factura WITH(UPDLOCK,HOLDLOCK) WHERE FacturaID=@id", ("@id", id));
        if (existente is null or DBNull) return NotFound(new { error = "Factura no encontrada." });
        decimal total = Decimal(existente);
        decimal pagado = Decimal(await Escalar(cn, tx, "SELECT SUM(Importe) FROM dbo.Pago WHERE FacturaID=@id", ("@id", id)));
        decimal saldo = total - pagado;
        if (saldo <= 0 || request.Importe > saldo)
            return Conflict(new { error = $"Pago supera el saldo permitido: {saldo:N2}." });
        await Ejecutar(cn, tx, """
            INSERT INTO dbo.Pago(FacturaID,Importe,MedioPago,Referencia)
            VALUES(@id,@importe,@medio,@ref)
            """, ("@id", id), ("@importe", request.Importe), ("@medio", request.MedioPago.Trim()),
            ("@ref", string.IsNullOrWhiteSpace(request.Referencia) ? null : request.Referencia.Trim()));
        string estado = pagado + request.Importe >= total ? "Cancelada" : "Parcial";
        await Ejecutar(cn, tx, "UPDATE dbo.Factura SET EstadoPago=@estado WHERE FacturaID=@id", ("@estado", estado), ("@id", id));
        await tx.CommitAsync();
        return Ok(new { mensaje = "Pago registrado.", estadoPago = estado, saldo = saldo - request.Importe });
    }

    [HttpGet("remitos")]
    public async Task<IActionResult> ListarRemitos()
    {
        var lista = new List<RemitoVista>();
        await using var cn = Conexion(); await cn.OpenAsync();
        const string sql = """
            SELECT r.RemitoID,r.OrdenCompraID,r.NumeroRemito,
                   c.Apellido+N', '+c.Nombre,r.FechaEmision,r.FechaEntrega,r.Estado
            FROM dbo.Remito r JOIN dbo.OrdenCompra o ON o.OrdenCompraID=r.OrdenCompraID
            JOIN dbo.Cliente c ON c.ClienteID=o.ClienteID
            ORDER BY r.RemitoID DESC;
            """;
        await using var cmd = Cmd(cn, null, sql); await using var rd = await cmd.ExecuteReaderAsync();
        while (await rd.ReadAsync()) lista.Add(new RemitoVista
        {
            RemitoID = rd.GetInt32(0), OrdenCompraID = rd.GetInt32(1), NumeroRemito = rd.GetString(2),
            Cliente = rd.GetString(3), FechaEmision = rd.GetDateTime(4),
            FechaEntrega = rd.IsDBNull(5) ? null : rd.GetDateTime(5), Estado = rd.GetString(6)
        });
        return Ok(lista);
    }

    [HttpGet("remitos/{id:int}")]
    public async Task<IActionResult> ObtenerRemito(int id)
    {
        var datos = (OkObjectResult)await ListarRemitos();
        var item = ((List<RemitoVista>)datos.Value!).FirstOrDefault(x => x.RemitoID == id);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost("remitos")]
    public async Task<IActionResult> PrepararRemito([FromBody] RemitoNuevoDto request)
    {
        if (request.OrdenCompraID <= 0) return BadRequest(new { error = "Orden de compra invalida." });
        await using var cn = Conexion(); await cn.OpenAsync();
        await using var tx = (SqlTransaction)await cn.BeginTransactionAsync(IsolationLevel.Serializable);
        bool existeOrden = Int(await Escalar(cn, tx, "SELECT COUNT(*) FROM dbo.OrdenCompra WITH(UPDLOCK,HOLDLOCK) WHERE OrdenCompraID=@id",
            ("@id", request.OrdenCompraID))) > 0;
        if (!existeOrden) return NotFound(new { error = "Orden inexistente." });
        if (Int(await Escalar(cn, tx,
            "SELECT COUNT(*) FROM dbo.Remito WHERE OrdenCompraID=@id AND Estado<>N'Anulado'", ("@id", request.OrdenCompraID))) != 0)
            return Conflict(new { error = "Ya existe un remito no anulado para esta OC." });
        int facturaId; decimal total; string estadoPago;
        await using (var cmd = Cmd(cn, tx, """
            SELECT TOP 1 FacturaID,ImporteTotal,EstadoPago FROM dbo.Factura WITH(UPDLOCK,HOLDLOCK)
            WHERE OrdenCompraID=@id ORDER BY FacturaID DESC
            """, ("@id", request.OrdenCompraID)))
        await using (var rd = await cmd.ExecuteReaderAsync())
        {
            if (!await rd.ReadAsync()) return Conflict(new { error = "La OC no tiene factura." });
            facturaId = rd.GetInt32(0); total = rd.GetDecimal(1); estadoPago = rd.GetString(2);
        }
        decimal pagos = Decimal(await Escalar(cn, tx, "SELECT SUM(Importe) FROM dbo.Pago WHERE FacturaID=@id", ("@id", facturaId)));
        if (estadoPago != "Cancelada" || pagos < total)
            return Conflict(new { error = "No se entrega: factura impaga o pago no registrado." });

        var lineas = new List<(int ProductoID, decimal Cantidad)>();
        await using (var cmd = Cmd(cn, tx,
            "SELECT ProductoID,Cantidad FROM dbo.DetalleOrdenCompra WHERE OrdenCompraID=@id ORDER BY ProductoID", ("@id", request.OrdenCompraID)))
        await using (var rd = await cmd.ExecuteReaderAsync())
        { while (await rd.ReadAsync()) lineas.Add((rd.GetInt32(0), rd.GetDecimal(1))); }
        if (lineas.Count == 0) return Conflict(new { error = "La orden no tiene productos." });
        foreach (var linea in lineas)
        {
            object? stock = await Escalar(cn, tx, "SELECT StockActual FROM dbo.Producto WITH(UPDLOCK,HOLDLOCK) WHERE ProductoID=@id AND Activo=1",
                ("@id", linea.ProductoID));
            if (stock is null or DBNull || Decimal(stock) < linea.Cantidad)
                return Conflict(new { error = $"Stock insuficiente o producto inactivo: {linea.ProductoID}." });
        }
        int remitoId = Int(await Escalar(cn, tx, """
            INSERT INTO dbo.Remito(OrdenCompraID,NumeroRemito,Estado)
            OUTPUT INSERTED.RemitoID VALUES(@id,@temporal,N'Preparado')
            """, ("@id", request.OrdenCompraID), ("@temporal", "TMP-" + Guid.NewGuid().ToString("N")[..26])));
        string numeroRemito = $"REM-{remitoId:D6}";
        await Ejecutar(cn, tx, "UPDATE dbo.Remito SET NumeroRemito=@numero WHERE RemitoID=@id",
            ("@numero", numeroRemito), ("@id", remitoId));
        foreach (var linea in lineas)
        {
            await Ejecutar(cn, tx,
                "INSERT INTO dbo.DetalleRemito(RemitoID,ProductoID,Cantidad) VALUES(@r,@p,@c)",
                ("@r", remitoId), ("@p", linea.ProductoID), ("@c", linea.Cantidad));
            int cambiadas = await Ejecutar(cn, tx,
                "UPDATE dbo.Producto SET StockActual=StockActual-@c WHERE ProductoID=@p AND StockActual>=@c",
                ("@p", linea.ProductoID), ("@c", linea.Cantidad));
            if (cambiadas != 1) return Conflict(new { error = "El stock cambio durante la operacion." });
        }
        await tx.CommitAsync();
        return Created($"/api/minoristas/remitos/{remitoId}", new { remitoID = remitoId, numeroRemito });
    }

    [HttpPost("remitos/{id:int}/entregar")]
    public async Task<IActionResult> EntregarRemito(int id)
    {
        await using var cn = Conexion(); await cn.OpenAsync();
        int filas = await Ejecutar(cn, null, """
            UPDATE dbo.Remito SET Estado=N'Entregado',FechaEntrega=SYSDATETIME()
            WHERE RemitoID=@id AND Estado=N'Preparado'
            """, ("@id", id));
        return filas == 1 ? Ok(new { mensaje = "Mercaderia entregada." }) : Conflict(new { error = "El remito no existe o no esta preparado." });
    }

    [HttpPost("remitos/{id:int}/anular")]
    public async Task<IActionResult> AnularRemito(int id)
    {
        await using var cn = Conexion(); await cn.OpenAsync();
        await using var tx = (SqlTransaction)await cn.BeginTransactionAsync(IsolationLevel.Serializable);
        string? estado = Convert.ToString(await Escalar(cn, tx,
            "SELECT Estado FROM dbo.Remito WITH(UPDLOCK,HOLDLOCK) WHERE RemitoID=@id", ("@id", id)));
        if (estado is null) return NotFound(new { error = "Remito inexistente." });
        if (estado != "Preparado") return Conflict(new { error = "Solo se pueden anular remitos preparados." });
        await Ejecutar(cn, tx, """
            UPDATE p SET p.StockActual=p.StockActual+dr.Cantidad
            FROM dbo.Producto p INNER JOIN dbo.DetalleRemito dr ON dr.ProductoID=p.ProductoID
            WHERE dr.RemitoID=@id
            """, ("@id", id));
        await Ejecutar(cn, tx,
            "UPDATE dbo.Remito SET Estado=N'Anulado',FechaEntrega=NULL WHERE RemitoID=@id", ("@id", id));
        await tx.CommitAsync();
        return Ok(new { mensaje = "Remito anulado y stock repuesto." });
    }
}

public sealed class OrdenCompraNuevaDto
{
    public int ClienteID { get; set; }
    public DateTime? FechaEntregaSolicitada { get; set; }
    public List<LineaOrdenCompraNuevaDto> Detalles { get; set; } = [];
}
public sealed class LineaOrdenCompraNuevaDto { public int ProductoID { get; set; } public decimal Cantidad { get; set; } }
public sealed class PagoNuevoDto { public decimal Importe { get; set; } public string MedioPago { get; set; } = ""; public string? Referencia { get; set; } }
public sealed class RemitoNuevoDto { public int OrdenCompraID { get; set; } }
public sealed class DetalleVista
{
    public int ProductoID { get; set; }
    public string NombreProducto { get; set; } = "";
    public decimal Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }
}
public sealed class OrdenVista
{
    public int OrdenCompraID { get; set; }
    public int ClienteID { get; set; }
    public string NumeroOC { get; set; } = "";
    public DateTime FechaOC { get; set; }
    public DateTime? FechaEntregaSolicitada { get; set; }
    public string Estado { get; set; } = "";
    public string Cliente { get; set; } = "";
    public decimal ImporteTotal { get; set; }
    public int? FacturaID { get; set; }
    public string? EstadoPago { get; set; }
    public List<DetalleVista> Detalles { get; set; } = [];
}
public sealed class FacturaVista
{
    public int FacturaID { get; set; }
    public int OrdenCompraID { get; set; }
    public string NumeroFactura { get; set; } = "";
    public string Cliente { get; set; } = "";
    public DateTime FechaEmision { get; set; }
    public decimal ImporteTotal { get; set; }
    public decimal TotalPagado { get; set; }
    public decimal Saldo { get; set; }
    public string EstadoPago { get; set; } = "";
}
public sealed class RemitoVista
{
    public int RemitoID { get; set; }
    public int OrdenCompraID { get; set; }
    public string NumeroRemito { get; set; } = "";
    public string Cliente { get; set; } = "";
    public DateTime FechaEmision { get; set; }
    public DateTime? FechaEntrega { get; set; }
    public string Estado { get; set; } = "";
}
