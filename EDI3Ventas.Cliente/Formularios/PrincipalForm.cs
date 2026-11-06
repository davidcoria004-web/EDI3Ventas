using System.Drawing;
using EDI3Ventas.Cliente.Infraestructura;
using EDI3Ventas.Cliente.Modelos;

namespace EDI3Ventas.Cliente.Formularios;

/// <summary>Aplicacion principal: gestiona unicamente llamadas HTTP a la API.</summary>
internal sealed class PrincipalForm : Form
{
    private readonly ApiClient api = new();
    private readonly Preferencias prefs = Preferencias.Cargar();
    private readonly TextBox url = new() { Width = 280 };
    private readonly Label estado = new() { Text = "Sin comprobar", AutoSize = true, Padding = new Padding(8, 8, 0, 0) };
    private readonly TabControl tabs = new() { Dock = DockStyle.Fill };
    private readonly DataGridView clientes = Ui.Tabla(), productos = Ui.Tabla(), pedidos = Ui.Tabla(),
        cotizaciones = Ui.Tabla(), ordenes = Ui.Tabla(), facturas = Ui.Tabla(), remitos = Ui.Tabla();

    // Crea las pestañas del cliente.
    public PrincipalForm()
    {
        Text = "EDI 3 - Gestion de Ventas Minoristas";
        Width = 1220; Height = 760; MinimumSize = new Size(950, 580);
        StartPosition = FormStartPosition.CenterScreen;
        url.Text = prefs.ApiUrl;
        api.BaseUrl = prefs.ApiUrl;

        var cabecera = Ui.Barra(new Label { Text = "URL API", AutoSize = true, Padding = new Padding(3, 11, 3, 0) },
            url, Ui.Boton("Guardar URL", (_, _) => GuardarUrl()),
            Ui.Boton("Probar API / SQL", async (_, _) => await Ui.Ejecutar(Probar)), estado);
        cabecera.Height = 52;

        tabs.TabPages.Add(CrearTabClientes());
        tabs.TabPages.Add(CrearTabProductos());
        tabs.TabPages.Add(CrearTabPedidos());
        tabs.TabPages.Add(CrearTabOrdenes());
        tabs.TabPages.Add(CrearTabFacturas());
        tabs.TabPages.Add(CrearTabRemitos());
        tabs.SelectedIndexChanged += async (_, _) => await Ui.Ejecutar(RefrescarActual);

        Controls.Add(tabs); Controls.Add(cabecera);
        Shown += async (_, _) => await Ui.Ejecutar(RefrescarActual);
    }

    // Valida y guarda la URL de la API.
    private bool GuardarUrl()
    {
        if (!Uri.TryCreate(url.Text.Trim(), UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
        { Ui.Aviso("Ingrese una URL HTTP o HTTPS valida."); return false; }
        prefs.ApiUrl = url.Text.Trim().TrimEnd('/') + "/";
        prefs.Guardar(); api.BaseUrl = prefs.ApiUrl;
        estado.Text = "URL guardada";
        return true;
    }

    // Comprueba la conexion con la API.
    private async Task Probar()
    {
        if (!GuardarUrl()) return;
        var resultado = await api.ObtenerAsync<System.Text.Json.JsonElement>("api/estado-db");
        estado.Text = resultado.TryGetProperty("baseDatos", out var bd) ? "SQL conectado: " + bd.GetString() : "API conectada";
    }

    // Crea una pestaña con barra y tabla.
    private TabPage ConTab(string titulo, DataGridView grilla, params Control[] barra)
    {
        var tab = new TabPage(titulo) { Padding = new Padding(6) };
        tab.Controls.Add(grilla); tab.Controls.Add(Ui.Barra(barra));
        return tab;
    }

    // Crea las acciones de clientes.
    private TabPage CrearTabClientes()
    {
        return ConTab("Clientes", clientes,
            Ui.Boton("Actualizar", async (_, _) => await Ui.Ejecutar(RefrescarClientes)),
            Ui.Boton("Nuevo", async (_, _) => await Ui.Ejecutar(NuevoCliente)),
            Ui.Boton("Editar", async (_, _) => await Ui.Ejecutar(EditarCliente)),
            Ui.Boton("Baja logica", async (_, _) => await Ui.Ejecutar(BajaCliente)));
    }

    // Crea las acciones de productos.
    private TabPage CrearTabProductos()
    {
        return ConTab("Productos", productos,
            Ui.Boton("Actualizar", async (_, _) => await Ui.Ejecutar(RefrescarProductos)),
            Ui.Boton("Nuevo", async (_, _) => await Ui.Ejecutar(NuevoProducto)),
            Ui.Boton("Editar", async (_, _) => await Ui.Ejecutar(EditarProducto)),
            Ui.Boton("Baja logica", async (_, _) => await Ui.Ejecutar(BajaProducto)));
    }

    // Crea las vistas de pedidos y cotizaciones.
    private TabPage CrearTabPedidos()
    {
        var tab = new TabPage("Pedidos / Cotizaciones") { Padding = new Padding(6) };
        var contenedor = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal };
        var superiores = new Panel { Dock = DockStyle.Fill };
        var barra = Ui.Barra(new Label { Text = "PEDIDOS", AutoSize = true, Padding = new Padding(4, 10, 8, 0) },
            Ui.Boton("Actualizar", async (_, _) => await Ui.Ejecutar(RefrescarPedidosCotizaciones)),
            Ui.Boton("Nuevo pedido", async (_, _) => await Ui.Ejecutar(NuevoPedido)),
            Ui.Boton("Cotizar pedido", async (_, _) => await Ui.Ejecutar(CotizarPedido)),
            Ui.Boton("Ver detalle", (_, _) => VerDetallePedido()));
        superiores.Controls.Add(pedidos); superiores.Controls.Add(barra);
        contenedor.Panel1.Controls.Add(superiores);
        var inferiores = new Panel { Dock = DockStyle.Fill };
        inferiores.Controls.Add(cotizaciones);
        inferiores.Controls.Add(Ui.Barra(new Label { Text = "COTIZACIONES EMITIDAS", AutoSize = true, Padding = new Padding(4, 10, 8, 0) }));
        contenedor.Panel2.Controls.Add(inferiores);
        tab.Controls.Add(contenedor);
        tab.Enter += (_, _) =>
        {
            if (contenedor.Height > 380 && contenedor.SplitterDistance < 160)
                contenedor.SplitterDistance = contenedor.Height / 2;
        };
        return tab;
    }

    // Crea las acciones de ordenes.
    private TabPage CrearTabOrdenes()
    {
        return ConTab("Ordenes de compra", ordenes,
            Ui.Boton("Actualizar", async (_, _) => await Ui.Ejecutar(RefrescarOrdenes)),
            Ui.Boton("Registrar OC", async (_, _) => await Ui.Ejecutar(NuevaOrden)),
            Ui.Boton("Ver factura", async (_, _) => await Ui.Ejecutar(VerFactura)));
    }

    // Crea las acciones de facturas y pagos.
    private TabPage CrearTabFacturas()
    {
        return ConTab("Facturas y pagos", facturas,
            Ui.Boton("Actualizar", async (_, _) => await Ui.Ejecutar(RefrescarFacturas)),
            Ui.Boton("Registrar pago", async (_, _) => await Ui.Ejecutar(NuevoPago)),
            Ui.Boton("Ver / imprimir", async (_, _) => await Ui.Ejecutar(VerImprimirFactura)));
    }

    // Crea las acciones de remitos.
    private TabPage CrearTabRemitos()
    {
        return ConTab("Remitos / Entregas", remitos,
            Ui.Boton("Actualizar", async (_, _) => await Ui.Ejecutar(RefrescarRemitos)),
            Ui.Boton("Preparar remito", async (_, _) => await Ui.Ejecutar(NuevoRemito)),
            Ui.Boton("Confirmar entrega", async (_, _) => await Ui.Ejecutar(EntregarRemito)),
            Ui.Boton("Anular y reponer", async (_, _) => await Ui.Ejecutar(AnularRemito)),
            Ui.Boton("Ver / imprimir", async (_, _) => await Ui.Ejecutar(VerImprimirRemito)));
    }

    // Actualiza la pestaña activa.
    private Task RefrescarActual() => tabs.SelectedIndex switch
    {
        0 => RefrescarClientes(), 1 => RefrescarProductos(), 2 => RefrescarPedidosCotizaciones(),
        3 => RefrescarOrdenes(), 4 => RefrescarFacturas(), 5 => RefrescarRemitos(), _ => Task.CompletedTask
    };

    // Consulta los clientes.
    private async Task RefrescarClientes() => Ui.Datos(clientes, await api.ObtenerAsync<List<ClienteDto>>("api/clientes"));
    // Consulta los productos.
    private async Task RefrescarProductos() => Ui.Datos(productos, await api.ObtenerAsync<List<ProductoDto>>("api/productos"));
    // Consulta pedidos y cotizaciones.
    private async Task RefrescarPedidosCotizaciones()
    {
        Ui.Datos(pedidos, await api.ObtenerAsync<List<PedidoDto>>("api/minoristas/pedidos"));
        Ui.Datos(cotizaciones, await api.ObtenerAsync<List<CotizacionDto>>("api/minoristas/cotizaciones"));
    }
    // Consulta las ordenes.
    private async Task RefrescarOrdenes() => Ui.Datos(ordenes, await api.ObtenerAsync<List<OrdenDto>>("api/minoristas/ordenes"));
    // Consulta las facturas.
    private async Task RefrescarFacturas() => Ui.Datos(facturas, await api.ObtenerAsync<List<FacturaDto>>("api/minoristas/facturas"));
    // Consulta los remitos.
    private async Task RefrescarRemitos() => Ui.Datos(remitos, await api.ObtenerAsync<List<RemitoDto>>("api/minoristas/remitos"));

    // Registra un cliente.
    private async Task NuevoCliente()
    {
        using var form = new ClienteForm();
        if (form.ShowDialog(this) != DialogResult.OK) return;
        await api.CrearAsync("api/clientes", form.Resultado);
        await RefrescarClientes();
    }

    // Modifica el cliente seleccionado.
    private async Task EditarCliente()
    {
        if (Ui.Seleccion<ClienteDto>(clientes) is not { } item) { Ui.Aviso("Seleccione un cliente para editar."); return; }
        if (!item.Activo) { Ui.Aviso("No se edita un cliente inactivo."); return; }
        using var form = new ClienteForm(item);
        if (form.ShowDialog(this) != DialogResult.OK) return;
        await api.ActualizarAsync($"api/clientes/{item.ClienteID}", form.Resultado);
        await RefrescarClientes();
    }

    // Da de baja un cliente.
    private async Task BajaCliente()
    {
        if (Ui.Seleccion<ClienteDto>(clientes) is not { Activo: true } item)
        { Ui.Aviso("Seleccione un cliente activo para dar de baja."); return; }
        if (!Ui.Confirmar($"Dar de baja al cliente {item.Presentacion}?")) return;
        await api.EliminarAsync($"api/clientes/{item.ClienteID}");
        await RefrescarClientes();
    }

    // Registra un producto.
    private async Task NuevoProducto()
    {
        using var form = new ProductoForm();
        if (form.ShowDialog(this) != DialogResult.OK) return;
        await api.CrearAsync("api/productos", form.Resultado);
        await RefrescarProductos();
    }

    // Modifica el producto seleccionado.
    private async Task EditarProducto()
    {
        if (Ui.Seleccion<ProductoDto>(productos) is not { } item) { Ui.Aviso("Seleccione un producto para editar."); return; }
        if (!item.Activo) { Ui.Aviso("No se edita un producto inactivo."); return; }
        using var form = new ProductoForm(item);
        if (form.ShowDialog(this) != DialogResult.OK) return;
        await api.ActualizarAsync($"api/productos/{item.ProductoID}", form.Resultado);
        await RefrescarProductos();
    }

    // Da de baja un producto.
    private async Task BajaProducto()
    {
        if (Ui.Seleccion<ProductoDto>(productos) is not { Activo: true } item)
        { Ui.Aviso("Seleccione un producto activo para dar de baja."); return; }
        if (!Ui.Confirmar($"Dar de baja al producto {item.Presentacion}?")) return;
        await api.EliminarAsync($"api/productos/{item.ProductoID}");
        await RefrescarProductos();
    }

    // Registra un pedido con productos.
    private async Task NuevoPedido()
    {
        var listaClientes = await api.ObtenerAsync<List<ClienteDto>>("api/clientes");
        var listaProductos = await api.ObtenerAsync<List<ProductoDto>>("api/productos");
        if (!listaClientes.Any(c => c.Activo))
        { Ui.Aviso("No hay clientes activos para registrar un pedido."); return; }
        if (!listaProductos.Any(p => p.Activo))
        { Ui.Aviso("No hay productos activos para registrar un pedido."); return; }
        using var form = new PedidoForm(listaClientes, listaProductos);
        if (form.ShowDialog(this) != DialogResult.OK) return;
        await api.CrearAsync("api/minoristas/pedidos", form.Resultado);
        await RefrescarPedidosCotizaciones();
    }

    // Emite una cotizacion para el pedido.
    private async Task CotizarPedido()
    {
        if (Ui.Seleccion<PedidoDto>(pedidos) is not { } item) { Ui.Aviso("Seleccione un pedido para cotizar."); return; }
        if (item.Estado != "Pendiente") { Ui.Aviso("Solo se puede cotizar un pedido pendiente."); return; }
        if (!Ui.Confirmar($"Cotizar el pedido {item.PedidoID} con los precios registrados?")) return;
        await api.CrearAsync($"api/minoristas/pedidos/{item.PedidoID}/cotizar", new { });
        await RefrescarPedidosCotizaciones();
    }

    // Registra una orden de compra.
    private async Task NuevaOrden()
    {
        var cotizacionesDisponibles = (await api.ObtenerAsync<List<CotizacionDto>>("api/minoristas/cotizaciones"))
            .Where(c => c.Estado == "Emitida").ToList();
        if (cotizacionesDisponibles.Count == 0)
        { Ui.Aviso("No hay cotizaciones emitidas para registrar una orden de compra."); return; }
        using var form = new OrdenForm(cotizacionesDisponibles);
        if (form.ShowDialog(this) != DialogResult.OK) return;
        await api.CrearAsync("api/minoristas/ordenes", new { cotizacionID = form.CotizacionID, numeroOC = form.NumeroOC });
        Ui.Aviso($"Orden {form.NumeroOC} registrada. Factura emitida.");
        await RefrescarOrdenes();
    }

    // Muestra la factura asociada a la orden.
    private async Task VerFactura()
    {
        if (Ui.Seleccion<OrdenDto>(ordenes) is not { } orden) { Ui.Aviso("Seleccione una orden de compra."); return; }
        var facturasActuales = await api.ObtenerAsync<List<FacturaDto>>("api/minoristas/facturas");
        var factura = facturasActuales.FirstOrDefault(f => f.OrdenCompraID == orden.OrdenCompraID);
        if (factura is null) { Ui.Aviso("No hay una factura asociada."); return; }
        Ui.Aviso($"Factura: {factura.NumeroFactura}\nTotal: {factura.ImporteTotal:N2}\nPagado: {factura.TotalPagado:N2}\nSaldo: {factura.Saldo:N2}\nEstado: {factura.EstadoPago}");
    }

    // Registra un pago de factura.
    private async Task NuevoPago()
    {
        if (Ui.Seleccion<FacturaDto>(facturas) is not { } factura) { Ui.Aviso("Seleccione una factura."); return; }
        if (factura.Saldo <= 0) { Ui.Aviso("La factura ya esta cancelada."); return; }
        using var form = new PagoForm(factura);
        if (form.ShowDialog(this) != DialogResult.OK) return;
        await api.CrearAsync($"api/minoristas/facturas/{factura.FacturaID}/pagos", form.Resultado);
        await RefrescarFacturas();
    }

    // Prepara un remito de entrega.
    private async Task NuevoRemito()
    {
        var ordenesActuales = await api.ObtenerAsync<List<OrdenDto>>("api/minoristas/ordenes");
        var remitosActuales = await api.ObtenerAsync<List<RemitoDto>>("api/minoristas/remitos");
        var ordenesAptas = ordenesActuales.Where(o => o.EstadoPago == "Cancelada" &&
            !remitosActuales.Any(r => r.OrdenCompraID == o.OrdenCompraID && r.Estado != "Anulado")).ToList();
        if (ordenesAptas.Count == 0)
        { Ui.Aviso("No hay ordenes con factura pagada pendientes de remito."); return; }
        using var form = new RemitoForm(ordenesAptas);
        if (form.ShowDialog(this) != DialogResult.OK) return;
        await api.CrearAsync("api/minoristas/remitos", new { ordenCompraID = form.OrdenCompraID });
        await RefrescarRemitos();
    }

    // Confirma la entrega del remito.
    private async Task EntregarRemito()
    {
        if (Ui.Seleccion<RemitoDto>(remitos) is not { Estado: "Preparado" } remito) { Ui.Aviso("Solo puede confirmar remitos preparados."); return; }
        if (!Ui.Confirmar($"Confirmar entrega del remito {remito.NumeroRemito}?")) return;
        await api.CrearAsync($"api/minoristas/remitos/{remito.RemitoID}/entregar", new { });
        await RefrescarRemitos();
    }

    // Anula el remito y repone stock.
    private async Task AnularRemito()
    {
        if (Ui.Seleccion<RemitoDto>(remitos) is not { Estado: "Preparado" } remito) { Ui.Aviso("Solo puede anular remitos preparados."); return; }
        if (!Ui.Confirmar($"ANULAR el remito {remito.NumeroRemito} y devolver el stock al inventario?")) return;
        await api.CrearAsync($"api/minoristas/remitos/{remito.RemitoID}/anular", new { });
        await RefrescarRemitos();
    }

    // Muestra el detalle del pedido.
    private void VerDetallePedido()
    {
        if (Ui.Seleccion<PedidoDto>(pedidos) is not { } pedido) { Ui.Aviso("Seleccione un pedido para ver el detalle."); return; }
        string lineas = string.Join(Environment.NewLine,
            pedido.Detalles.Select(d => $"{d.ProductoID,4}  {d.NombreProducto,-28} {d.Cantidad,9:N3}  $ {d.PrecioUnitario,10:N2}  $ {d.Subtotal,12:N2}"));
        using var documento = new DocumentoForm("Pedido minorista", $"PEDIDO NRO {pedido.PedidoID}\r\n" +
            $"Cliente: {pedido.Cliente}\r\nFecha: {pedido.FechaPedido:g}\r\nEstado: {pedido.Estado}\r\n\r\n" +
            "Producto  Nombre                       Cantidad    Precio    Subtotal\r\n" + lineas +
            $"\r\n\r\nTOTAL: $ {pedido.ImporteTotal:N2}");
        documento.ShowDialog(this);
    }

    // Abre la factura para imprimir.
    private async Task VerImprimirFactura()
    {
        if (Ui.Seleccion<FacturaDto>(facturas) is not { } factura) { Ui.Aviso("Seleccione una factura."); return; }
        var todasOrdenes = await api.ObtenerAsync<List<OrdenDto>>("api/minoristas/ordenes");
        var orden = todasOrdenes.FirstOrDefault(o => o.OrdenCompraID == factura.OrdenCompraID);
        var todosPedidos = await api.ObtenerAsync<List<PedidoDto>>("api/minoristas/pedidos");
        var pedido = todosPedidos.FirstOrDefault(p => p.PedidoID == orden?.PedidoID);
        string lineas = pedido == null ? "" : string.Join("\r\n", pedido.Detalles.Select(d =>
            $"{d.NombreProducto,-30} {d.Cantidad,8:N3} x $ {d.PrecioUnitario,10:N2} = $ {d.Subtotal,12:N2}"));
        string contenido = $"DOCUMENTO INTERNO - NO FISCAL\r\nFACTURA: {factura.NumeroFactura}\r\n" +
            $"Fecha: {factura.FechaEmision:g}\r\nCliente: {factura.Cliente}\r\n" +
            $"Orden de compra: {orden?.NumeroOC ?? "-"}\r\n\r\n{lineas}\r\n\r\n" +
            $"TOTAL: $ {factura.ImporteTotal:N2}\r\nPAGADO: $ {factura.TotalPagado:N2}\r\n" +
            $"SALDO: $ {factura.Saldo:N2}\r\nESTADO: {factura.EstadoPago}";
        using var documento = new DocumentoForm("Comprobante interno de factura", contenido);
        documento.ShowDialog(this);
    }

    // Abre el remito para imprimir.
    private async Task VerImprimirRemito()
    {
        if (Ui.Seleccion<RemitoDto>(remitos) is not { } remito) { Ui.Aviso("Seleccione un remito para imprimir."); return; }
        var todasOrdenes = await api.ObtenerAsync<List<OrdenDto>>("api/minoristas/ordenes");
        var orden = todasOrdenes.FirstOrDefault(o => o.OrdenCompraID == remito.OrdenCompraID);
        var todosPedidos = await api.ObtenerAsync<List<PedidoDto>>("api/minoristas/pedidos");
        var pedido = todosPedidos.FirstOrDefault(p => p.PedidoID == orden?.PedidoID);
        string lineas = pedido == null ? "" : string.Join("\r\n", pedido.Detalles.Select(d =>
            $"{d.NombreProducto,-32} {d.Cantidad,9:N3}"));
        string contenido = $"REMITO DE SALIDA: {remito.NumeroRemito}\r\n" +
            $"Emision: {remito.FechaEmision:g}\r\nCliente: {remito.Cliente}\r\n" +
            $"Orden: {orden?.NumeroOC ?? "-"}\r\nEstado: {remito.Estado}\r\n\r\n" +
            "Producto                              Cantidad\r\n" + lineas + "\r\n\r\n" +
            "Conformidad de retiro\r\nFirma: ___________________    Aclaracion: __________________";
        using var documento = new DocumentoForm("Remito", contenido);
        documento.ShowDialog(this);
    }

}
