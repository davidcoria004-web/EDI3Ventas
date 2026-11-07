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

    [HttpGet("pedidos")]
    public async Task<IActionResult> ListarPedidos()
    {
        var lista = new List<PedidoVista>();
        await using var cn = Conexion(); await cn.OpenAsync();
        const string sql = """
            SELECT p.PedidoID,p.ClienteID, c.Apellido + N', ' + c.Nombre AS Cliente,
                   p.FechaPedido,p.FechaEntregaSolicitada,p.Estado,
                   COALESCE((SELECT SUM(d.Cantidad * d.PrecioUnitario)
                     FROM dbo.DetallePedido d WHERE d.PedidoID=p.PedidoID),0) ImporteTotal
            FROM dbo.Pedido p INNER JOIN dbo.Cliente c ON c.ClienteID=p.ClienteID
            ORDER BY p.PedidoID DESC;
            """;
        await using (var cmd = Cmd(cn, null, sql))
        await using (var rd = await cmd.ExecuteReaderAsync())
        {
            while (await rd.ReadAsync()) lista.Add(new PedidoVista
            {
                PedidoID = rd.GetInt32(0), ClienteID = rd.GetInt32(1), Cliente = rd.GetString(2),
                FechaPedido = rd.GetDateTime(3), FechaEntregaSolicitada = rd.IsDBNull(4) ? null : rd.GetDateTime(4), Estado = rd.GetString(5), ImporteTotal = rd.GetDecimal(6)
            });
        }
        const string detalleSql = """
            SELECT d.PedidoID,d.ProductoID,p.Nombre,d.Cantidad,d.PrecioUnitario
            FROM dbo.DetallePedido d INNER JOIN dbo.Producto p ON p.ProductoID=d.ProductoID;
            """;
        var porId = lista.ToDictionary(x => x.PedidoID);
        await using (var cmd = Cmd(cn, null, detalleSql))
        await using (var rd = await cmd.ExecuteReaderAsync())
        {
            while (await rd.ReadAsync() && porId.Count > 0)
            {
                if (porId.TryGetValue(rd.GetInt32(0), out var pedido)) pedido.Detalles.Add(new DetalleVista
                { ProductoID = rd.GetInt32(1), NombreProducto = rd.GetString(2), Cantidad = rd.GetDecimal(3), PrecioUnitario = rd.GetDecimal(4) });
            }
        }
        return Ok(lista);
    }

    [HttpGet("pedidos/{id:int}")]
    public async Task<IActionResult> ObtenerPedido(int id)
    {
        var datos = (OkObjectResult)await ListarPedidos();
        var item = ((List<PedidoVista>)datos.Value!).FirstOrDefault(x => x.PedidoID == id);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost("pedidos")]
    public async Task<IActionResult> CrearPedido([FromBody] PedidoNuevoDto request)
    {
        if (request.ClienteID <= 0 || request.FechaEntregaSolicitada is null || (request.Detalles is null || request.Detalles.Count == 0) ||
            request.Detalles.Any(x => x.ProductoID <= 0 || x.Cantidad <= 0 || x.Cantidad > 999999999m || x.Cantidad != decimal.Round(x.Cantidad, 3)) ||
            request.Detalles.Select(x => x.ProductoID).Distinct().Count() != request.Detalles.Count)
            return BadRequest(new { error = "Cliente minorista, fecha solicitada, productos y cantidades validas son obligatorios." });
        await using var cn = Conexion(); await cn.OpenAsync();
        await using var tx = (SqlTransaction)await cn.BeginTransactionAsync(IsolationLevel.Serializable);
        var existe = Int(await Escalar(cn, tx, "SELECT COUNT(*) FROM dbo.Cliente WHERE ClienteID=@id AND Activo=1 AND Categoria=N'Minorista'", ("@id", request.ClienteID)));
        if (existe == 0) return BadRequest(new { error = "Cliente no existe, esta inactivo o no es minorista." });
        int pedidoId = Int(await Escalar(cn, tx,
            "INSERT INTO dbo.Pedido(ClienteID,FechaEntregaSolicitada,Estado) OUTPUT INSERTED.PedidoID VALUES(@cliente,@fecha,N'Pendiente')", ("@cliente", request.ClienteID), ("@fecha", request.FechaEntregaSolicitada.Value.Date)));
        foreach (var item in request.Detalles)
        {
            decimal precio;
            await using (var cmd = Cmd(cn, tx,
                "SELECT PrecioUnitario FROM dbo.Producto WHERE ProductoID=@id AND Activo=1", ("@id", item.ProductoID)))
            {
                var valor = await cmd.ExecuteScalarAsync();
                if (valor is null or DBNull) return BadRequest(new { error = $"Producto {item.ProductoID} no existe o esta inactivo." });
                precio = Convert.ToDecimal(valor);
            }
            await Ejecutar(cn, tx, """
                INSERT INTO dbo.DetallePedido(PedidoID,ProductoID,Cantidad,PrecioUnitario)
                VALUES(@pedido,@producto,@cantidad,@precio)
                """, ("@pedido", pedidoId), ("@producto", item.ProductoID), ("@cantidad", item.Cantidad), ("@precio", precio));
        }
        await tx.CommitAsync();
        return Created($"/api/minoristas/pedidos/{pedidoId}", new { pedidoID = pedidoId, mensaje = "Pedido registrado." });
    }

    [HttpGet("cotizaciones")]
    public async Task<IActionResult> ListarCotizaciones()
    {
        var lista = new List<CotizacionVista>();
        await using var cn = Conexion(); await cn.OpenAsync();
        const string sql = """
            SELECT c.CotizacionID,c.PedidoID,c.NumeroCotizacion,c.FechaCotizacion,c.Estado,
                   COALESCE((SELECT SUM(d.Cantidad*d.PrecioUnitario)
                   FROM dbo.DetalleCotizacion d WHERE d.CotizacionID=c.CotizacionID),0) AS Total
            FROM dbo.Cotizacion c ORDER BY c.CotizacionID DESC;
            """;
        await using var cmd = Cmd(cn, null, sql); await using var rd = await cmd.ExecuteReaderAsync();
        while (await rd.ReadAsync()) lista.Add(new CotizacionVista
        {
            CotizacionID = rd.GetInt32(0), PedidoID = rd.GetInt32(1), NumeroCotizacion = rd.GetString(2),
            FechaCotizacion = rd.GetDateTime(3), Estado = rd.GetString(4), ImporteTotal = rd.GetDecimal(5)
        });
        return Ok(lista);
    }

    [HttpGet("cotizaciones/{id:int}")]
    public async Task<IActionResult> ObtenerCotizacion(int id)
    {
        var datos = (OkObjectResult)await ListarCotizaciones();
        var item = ((List<CotizacionVista>)datos.Value!).FirstOrDefault(x => x.CotizacionID == id);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost("pedidos/{id:int}/cotizar")]
    public async Task<IActionResult> CotizarPedido(int id)
    {
        await using var cn = Conexion(); await cn.OpenAsync();
        await using var tx = (SqlTransaction)await cn.BeginTransactionAsync(IsolationLevel.Serializable);
        var estado = Convert.ToString(await Escalar(cn, tx,
            "SELECT Estado FROM dbo.Pedido WITH (UPDLOCK,HOLDLOCK) WHERE PedidoID=@id", ("@id", id)));
        if (estado is null) return NotFound(new { error = "Pedido inexistente." });
        if (estado != "Pendiente") return Conflict(new { error = "Solo se cotizan pedidos pendientes." });
        if (Int(await Escalar(cn, tx, "SELECT COUNT(*) FROM dbo.DetallePedido WHERE PedidoID=@id", ("@id", id))) == 0)
            return BadRequest(new { error = "Pedido sin productos." });
        string numero = $"COT-{id:D6}";
        int cotizacionId = Int(await Escalar(cn, tx,
            "INSERT INTO dbo.Cotizacion(PedidoID,NumeroCotizacion,Estado) OUTPUT INSERTED.CotizacionID VALUES(@id,@num,N'Emitida')",
            ("@id", id), ("@num", numero)));
        await Ejecutar(cn, tx, """
            INSERT INTO dbo.DetalleCotizacion(CotizacionID,ProductoID,Cantidad,PrecioUnitario)
            SELECT @cot,ProductoID,Cantidad,PrecioUnitario FROM dbo.DetallePedido WHERE PedidoID=@pedido
            """, ("@cot", cotizacionId), ("@pedido", id));
        await Ejecutar(cn, tx, "UPDATE dbo.Pedido SET Estado=N'Cotizado' WHERE PedidoID=@id", ("@id", id));
        await tx.CommitAsync();
        return Created($"/api/minoristas/cotizaciones/{cotizacionId}", new { cotizacionID = cotizacionId, numeroCotizacion = numero });
    }

    [HttpGet("ordenes")]
    public async Task<IActionResult> ListarOrdenes()
    {
        var lista = new List<OrdenVista>();
        await using var cn = Conexion(); await cn.OpenAsync();
        const string sql = """
            SELECT o.OrdenCompraID,o.PedidoID,o.NumeroOC,o.FechaOC,o.Estado,
                   c.Apellido+N', '+c.Nombre AS Cliente,
                   COALESCE((SELECT SUM(d.Cantidad*d.PrecioUnitario)
                   FROM dbo.DetallePedido d WHERE d.PedidoID=o.PedidoID),0),
                   f.FacturaID,f.EstadoPago
            FROM dbo.OrdenCompra o JOIN dbo.Pedido p ON o.PedidoID=p.PedidoID
            JOIN dbo.Cliente c ON p.ClienteID=c.ClienteID
            OUTER APPLY(SELECT TOP 1 FacturaID,EstadoPago FROM dbo.Factura
                        WHERE OrdenCompraID=o.OrdenCompraID ORDER BY FacturaID DESC) f
            ORDER BY o.OrdenCompraID DESC;
            """;
        await using var cmd = Cmd(cn, null, sql); await using var rd = await cmd.ExecuteReaderAsync();
        while (await rd.ReadAsync()) lista.Add(new OrdenVista
        {
            OrdenCompraID = rd.GetInt32(0), PedidoID = rd.GetInt32(1), NumeroOC = rd.GetString(2),
            FechaOC = rd.GetDateTime(3), Estado = rd.GetString(4), Cliente = rd.GetString(5),
            ImporteTotal = rd.GetDecimal(6), FacturaID = rd.IsDBNull(7) ? null : rd.GetInt32(7),
            EstadoPago = rd.IsDBNull(8) ? null : rd.GetString(8)
        });
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
    public async Task<IActionResult> CrearOrden([FromBody] OrdenNuevaDto request)
    {
        if (request.CotizacionID <= 0 || string.IsNullOrWhiteSpace(request.NumeroOC) || request.NumeroOC.Length > 30)
            return BadRequest(new { error = "Cotizacion y numero de OC (hasta 30 caracteres) obligatorios." });
        await using var cn = Conexion(); await cn.OpenAsync();
        await using var tx = (SqlTransaction)await cn.BeginTransactionAsync(IsolationLevel.Serializable);
        int pedidoId;
        string? estado;
        await using (var cmd = Cmd(cn, tx,
            "SELECT PedidoID,Estado FROM dbo.Cotizacion WITH(UPDLOCK,HOLDLOCK) WHERE CotizacionID=@id", ("@id", request.CotizacionID)))
        await using (var rd = await cmd.ExecuteReaderAsync())
        {
            if (!await rd.ReadAsync()) return NotFound(new { error = "Cotizacion inexistente." });
            pedidoId = rd.GetInt32(0); estado = rd.GetString(1);
        }
        if (estado != "Emitida") return Conflict(new { error = "La cotizacion ya fue aceptada." });
        int detalles = Int(await Escalar(cn, tx,
            "SELECT COUNT(*) FROM dbo.DetalleCotizacion WHERE CotizacionID=@id", ("@id", request.CotizacionID)));
        if (detalles == 0) return BadRequest(new { error = "Cotizacion sin detalle." });
        decimal total = Math.Round(Decimal(await Escalar(cn, tx,
            "SELECT SUM(Cantidad*PrecioUnitario) FROM dbo.DetalleCotizacion WHERE CotizacionID=@id",
            ("@id", request.CotizacionID))), 2);
        if (total <= 0 || total > 9999999999.99m) return BadRequest(new { error = "El total debe ser positivo y estar dentro del rango permitido." });
        try
        {
            int ordenId = Int(await Escalar(cn, tx, """
                INSERT INTO dbo.OrdenCompra(PedidoID,NumeroOC,Estado)
                OUTPUT INSERTED.OrdenCompraID VALUES(@pedido,@numero,N'Registrada')
                """, ("@pedido", pedidoId), ("@numero", request.NumeroOC.Trim())));
            string facturaNumero = $"FAC-{ordenId:D6}";
            int facturaId = Int(await Escalar(cn, tx, """
                INSERT INTO dbo.Factura(OrdenCompraID,NumeroFactura,ImporteTotal,EstadoPago)
                OUTPUT INSERTED.FacturaID VALUES(@orden,@numero,@total,N'Pendiente')
                """, ("@orden", ordenId), ("@numero", facturaNumero), ("@total", total)));
            await Ejecutar(cn, tx, "UPDATE dbo.Cotizacion SET Estado=N'Aceptada' WHERE CotizacionID=@id", ("@id", request.CotizacionID));
            await Ejecutar(cn, tx, "UPDATE dbo.Pedido SET Estado=N'Confirmado' WHERE PedidoID=@id", ("@id", pedidoId));
            await tx.CommitAsync();
            return Created($"/api/minoristas/ordenes/{ordenId}", new { ordenCompraID = ordenId, facturaID = facturaId, numeroFactura = facturaNumero });
        }
        catch (SqlException ex) when (EsDuplicado(ex))
        {
            await tx.RollbackAsync();
            return Conflict(new { error = "Ya existe una OC para ese pedido o con ese numero." });
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
            JOIN dbo.Pedido p ON p.PedidoID=o.PedidoID JOIN dbo.Cliente c ON c.ClienteID=p.ClienteID
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
            JOIN dbo.Pedido p ON p.PedidoID=o.PedidoID JOIN dbo.Cliente c ON c.ClienteID=p.ClienteID
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
        int pedidoId;
        await using (var cmd = Cmd(cn, tx,
            "SELECT PedidoID FROM dbo.OrdenCompra WITH(UPDLOCK,HOLDLOCK) WHERE OrdenCompraID=@id", ("@id", request.OrdenCompraID)))
        {
            object? valor = await cmd.ExecuteScalarAsync();
            if (valor is null or DBNull) return NotFound(new { error = "Orden inexistente." });
            pedidoId = Convert.ToInt32(valor);
        }
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
            "SELECT ProductoID,Cantidad FROM dbo.DetallePedido WHERE PedidoID=@id ORDER BY ProductoID", ("@id", pedidoId)))
        await using (var rd = await cmd.ExecuteReaderAsync())
        { while (await rd.ReadAsync()) lineas.Add((rd.GetInt32(0), rd.GetDecimal(1))); }
        if (lineas.Count == 0) return Conflict(new { error = "El pedido no tiene productos." });
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

public sealed class PedidoNuevoDto
{
    public int ClienteID { get; set; }
    public DateTime? FechaEntregaSolicitada { get; set; }
    public List<LineaPedidoNuevaDto> Detalles { get; set; } = [];
}
public sealed class LineaPedidoNuevaDto { public int ProductoID { get; set; } public decimal Cantidad { get; set; } }
public sealed class OrdenNuevaDto { public int CotizacionID { get; set; } public string NumeroOC { get; set; } = ""; }
public sealed class PagoNuevoDto { public decimal Importe { get; set; } public string MedioPago { get; set; } = ""; public string? Referencia { get; set; } }
public sealed class RemitoNuevoDto { public int OrdenCompraID { get; set; } }
public sealed class DetalleVista
{
    public int ProductoID { get; set; }
    public string NombreProducto { get; set; } = "";
    public decimal Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }
}
public sealed class PedidoVista
{
    public int PedidoID { get; set; }
    public int ClienteID { get; set; }
    public string Cliente { get; set; } = "";
    public DateTime FechaPedido { get; set; }
    public DateTime? FechaEntregaSolicitada { get; set; }
    public string Estado { get; set; } = "";
    public decimal ImporteTotal { get; set; }
    public List<DetalleVista> Detalles { get; set; } = [];
}
public sealed class CotizacionVista
{
    public int CotizacionID { get; set; }
    public int PedidoID { get; set; }
    public string NumeroCotizacion { get; set; } = "";
    public DateTime FechaCotizacion { get; set; }
    public string Estado { get; set; } = "";
    public decimal ImporteTotal { get; set; }
}
public sealed class OrdenVista
{
    public int OrdenCompraID { get; set; }
    public int PedidoID { get; set; }
    public string NumeroOC { get; set; } = "";
    public DateTime FechaOC { get; set; }
    public string Estado { get; set; } = "";
    public string Cliente { get; set; } = "";
    public decimal ImporteTotal { get; set; }
    public int? FacturaID { get; set; }
    public string? EstadoPago { get; set; }
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
