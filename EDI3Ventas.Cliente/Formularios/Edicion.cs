using System.Drawing;
using System.Net.Mail;
using EDI3Ventas.Cliente.Modelos;

namespace EDI3Ventas.Cliente.Formularios;

/// <summary>Formulario modal base con validacion antes de aceptar.</summary>
internal abstract class EdicionForm : Form
{
    protected readonly TableLayoutPanel Campos = new()
    {
        Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 2, AutoScroll = false,
        Padding = new Padding(12), ColumnStyles = { new ColumnStyle(SizeType.Absolute, 150), new ColumnStyle(SizeType.Percent, 100) }
    };
    protected readonly Button Aceptar = Ui.Boton("Guardar");

    // Configura el formulario de edicion.
    protected EdicionForm(string titulo, int alto = 470)
    {
        Text = titulo; Width = 565; Height = alto;
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(440, 200);
        FormBorderStyle = FormBorderStyle.Sizable;
        var botones = new FlowLayoutPanel { Height = 50, Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft };
        var cancelar = Ui.Boton("Cancelar");
        cancelar.DialogResult = DialogResult.Cancel;
        Aceptar.Click += (_, _) =>
        {
            try { if (Validar()) DialogResult = DialogResult.OK; }
            catch (Exception ex) { Ui.Error(ex); }
        };
        botones.Controls.Add(cancelar);
        botones.Controls.Add(Aceptar);
        var desplazamiento = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        desplazamiento.Controls.Add(Campos);
        Controls.Add(desplazamiento); Controls.Add(botones);
        AcceptButton = Aceptar; CancelButton = cancelar;
    }

    // Autoriza la confirmacion del formulario.
    protected virtual bool Validar() => true;

    // Agrega un campo al formulario.
    protected T Agregar<T>(string etiqueta, T control) where T : Control
    {
        int fila = Campos.RowCount++;
        Campos.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var label = new Label { Text = etiqueta, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(4, 5, 4, 7) };
        control.Dock = DockStyle.Top;
        control.Margin = new Padding(4, 5, 4, 7);
        Campos.Controls.Add(label, 0, fila);
        Campos.Controls.Add(control, 1, fila);
        return control;
    }

    // Crea un campo de texto.
    protected static TextBox Entrada(string? valor = null, int max = 150) => new() { Text = valor ?? "", MaxLength = max };
    // Crea un campo numerico.
    protected static NumericUpDown Numero(decimal valor, decimal max = 100000000m, int decimales = 2) => new()
    {
        Minimum = 0, Maximum = max, DecimalPlaces = decimales,
        ThousandsSeparator = true, Value = Math.Clamp(valor, 0m, max)
    };

    // Valida campos obligatorios.
    protected static void Requerido(TextBox entrada, string campo)
    {
        if (string.IsNullOrWhiteSpace(entrada.Text))
            throw new InvalidOperationException($"{campo} es obligatorio.");
    }
}

internal sealed class ClienteForm : EdicionForm
{
    private readonly TextBox nombre, apellido, documento, email, telefono, direccion;
    public ClienteGuardar Resultado => new()
    {
        Nombre = nombre.Text.Trim(), Apellido = apellido.Text.Trim(), Documento = Blanco(documento.Text),
        Email = Blanco(email.Text), Telefono = Blanco(telefono.Text), Direccion = Blanco(direccion.Text)
    };
    // Normaliza los campos opcionales.
    private static string? Blanco(string valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    // Carga los datos del cliente.
    public ClienteForm(ClienteDto? original = null) : base(original is null ? "Nuevo cliente" : "Editar cliente", 355)
    {
        nombre = Agregar("Nombre *", Entrada(original?.Nombre, 100));
        apellido = Agregar("Apellido *", Entrada(original?.Apellido, 100));
        documento = Agregar("Documento", Entrada(original?.Documento, 30));
        email = Agregar("Email", Entrada(original?.Email, 150));
        telefono = Agregar("Telefono", Entrada(original?.Telefono, 30));
        direccion = Agregar("Direccion", Entrada(original?.Direccion, 200));
    }

    // Valida los datos del cliente.
    protected override bool Validar()
    {
        Requerido(nombre, "Nombre"); Requerido(apellido, "Apellido");
        if (!string.IsNullOrWhiteSpace(email.Text))
        {
            try { _ = new MailAddress(email.Text.Trim()); }
            catch (FormatException) { throw new InvalidOperationException("Email invalido."); }
        }
        return true;
    }
}

internal sealed class ProductoForm : EdicionForm
{
    private readonly TextBox codigo, nombre, unidad;
    private readonly NumericUpDown precio, stock;
    public ProductoGuardar Resultado => new()
    {
        Codigo = codigo.Text.Trim(), Nombre = nombre.Text.Trim(), UnidadMedida = unidad.Text.Trim(),
        PrecioUnitario = precio.Value, StockActual = stock.Value
    };

    // Carga los datos del producto.
    public ProductoForm(ProductoDto? original = null) : base(original is null ? "Nuevo producto" : "Editar producto", 325)
    {
        codigo = Agregar("Codigo *", Entrada(original?.Codigo, 30));
        nombre = Agregar("Nombre *", Entrada(original?.Nombre, 150));
        unidad = Agregar("Unidad de medida *", Entrada(original?.UnidadMedida, 20));
        precio = Agregar("Precio unitario", Numero(original?.PrecioUnitario ?? 0, 9999999999m, 2));
        stock = Agregar("Stock actual", Numero(original?.StockActual ?? 0, 999999999m, 3));
    }

    // Valida los datos del producto.
    protected override bool Validar()
    {
        Requerido(codigo, "Codigo"); Requerido(nombre, "Nombre"); Requerido(unidad, "Unidad de medida");
        return true;
    }
}

internal sealed class PedidoForm : EdicionForm
{
    private readonly ComboBox cliente = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox producto = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly NumericUpDown cantidad = Numero(1, 1000000, 3);
    private readonly DataGridView detalles = Ui.Tabla();
    private readonly List<LineaPedido> lineas = [];
    public PedidoNuevo Resultado => new()
    {
        ClienteID = (cliente.SelectedItem as Opcion)?.Id ?? 0,
        Detalles = lineas.Select(x => new LineaPedidoNueva { ProductoID = x.ProductoID, Cantidad = x.Cantidad }).ToList()
    };

    // Carga clientes y productos activos.
    public PedidoForm(IEnumerable<ClienteDto> clientes, IEnumerable<ProductoDto> productos) : base("Nuevo pedido minorista", 590)
    {
        Width = 780;
        cliente.Items.AddRange(clientes.Where(x => x.Activo).Select(x => new Opcion(x.ClienteID, x.Presentacion)).ToArray());
        producto.Items.AddRange(productos.Where(x => x.Activo).Select(x => new Opcion(x.ProductoID, x.Presentacion)).ToArray());
        if (cliente.Items.Count > 0) cliente.SelectedIndex = 0;
        if (producto.Items.Count > 0) producto.SelectedIndex = 0;
        Agregar("Cliente *", cliente);
        Agregar("Producto *", producto);
        cantidad.Minimum = 0.001m;
        Agregar("Cantidad *", cantidad);
        var botones = new FlowLayoutPanel { AutoSize = true };
        botones.Controls.Add(Ui.Boton("Agregar producto", (_, _) => AgregarLinea(productos)));
        botones.Controls.Add(Ui.Boton("Quitar linea", (_, _) =>
        {
            if (Ui.Seleccion<LineaPedido>(detalles) is { } actual)
            { lineas.Remove(actual); Refrescar(); }
        }));
        Agregar("Detalle", botones);
        detalles.Height = 210;
        detalles.Dock = DockStyle.Top;
        Campos.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Campos.RowCount++;
        Campos.Controls.Add(detalles, 0, Campos.RowCount - 1);
        Campos.SetColumnSpan(detalles, 2);
        Refrescar();
    }

    // Agrega un producto al pedido.
    private void AgregarLinea(IEnumerable<ProductoDto> productos)
    {
        if (producto.SelectedItem is not Opcion op) { Ui.Aviso("Seleccione un producto."); return; }
        if (lineas.Any(x => x.ProductoID == op.Id)) { Ui.Aviso("El producto ya esta agregado."); return; }
        var item = productos.First(x => x.ProductoID == op.Id);
        lineas.Add(new LineaPedido { ProductoID = op.Id, NombreProducto = item.Nombre, Cantidad = cantidad.Value, PrecioUnitario = item.PrecioUnitario });
        Refrescar();
    }

    // Actualiza el detalle del pedido.
    private void Refrescar() => Ui.Datos(detalles, lineas);

    // Valida cliente y detalle del pedido.
    protected override bool Validar()
    {
        if (cliente.SelectedItem is not Opcion) throw new InvalidOperationException("Seleccione un cliente activo.");
        if (lineas.Count == 0) throw new InvalidOperationException("Agregue al menos un producto.");
        return true;
    }
}

internal sealed record Opcion(int Id, string Etiqueta)
{
    // Muestra la etiqueta de la opcion.
    public override string ToString() => Etiqueta;
}

internal sealed class OrdenForm : EdicionForm
{
    private readonly ComboBox cotizacion = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox numero;
    public int CotizacionID => (cotizacion.SelectedItem as Opcion)?.Id ?? 0;
    public string NumeroOC => numero.Text.Trim();
    // Carga las cotizaciones disponibles.
    public OrdenForm(IEnumerable<CotizacionDto> disponibles) : base("Registrar orden de compra", 230)
    {
        cotizacion.Items.AddRange(disponibles.Select(x => new Opcion(x.CotizacionID,
            $"{x.NumeroCotizacion} - pedido {x.PedidoID} - ${x.ImporteTotal:N2}")).ToArray());
        if (cotizacion.Items.Count > 0) cotizacion.SelectedIndex = 0;
        Agregar("Cotizacion *", cotizacion);
        numero = Agregar("Numero OC *", Entrada("", 30));
    }
    // Valida los datos de la orden.
    protected override bool Validar()
    {
        if (CotizacionID <= 0) throw new InvalidOperationException("No hay cotizaciones disponibles.");
        Requerido(numero, "Numero OC"); return true;
    }
}

internal sealed class PagoForm : EdicionForm
{
    private readonly NumericUpDown importe;
    private readonly ComboBox medio = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox referencia;
    public object Resultado => new { importe = importe.Value, medioPago = medio.Text, referencia = referencia.Text.Trim() };
    // Carga el saldo y los medios de pago.
    public PagoForm(FacturaDto factura) : base("Registrar pago", 285)
    {
        Agregar("Saldo de factura", new Label { Text = $"$ {factura.Saldo:N2}", AutoSize = true });
        importe = Agregar("Importe *", Numero(factura.Saldo, factura.Saldo, 2));
        medio.Items.AddRange(["Transferencia", "Efectivo", "Tarjeta", "Otro"]);
        medio.SelectedIndex = 0;
        Agregar("Medio de pago *", medio);
        referencia = Agregar("Referencia", Entrada("", 100));
    }
    // Valida el importe del pago.
    protected override bool Validar()
    {
        if (importe.Value <= 0) throw new InvalidOperationException("El pago debe ser mayor a cero.");
        return true;
    }
}

internal sealed class RemitoForm : EdicionForm
{
    private readonly ComboBox orden = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    public int OrdenCompraID => (orden.SelectedItem as Opcion)?.Id ?? 0;
    // Carga las ordenes aptas para remito.
    public RemitoForm(IEnumerable<OrdenDto> disponibles) : base("Preparar remito", 205)
    {
        orden.Items.AddRange(disponibles.Select(x => new Opcion(x.OrdenCompraID,
            $"{x.NumeroOC} - {x.Cliente} - ${x.ImporteTotal:N2}")).ToArray());
        if (orden.Items.Count > 0) orden.SelectedIndex = 0;
        Agregar("OC con pago *", orden);
    }
    // Valida la orden seleccionada.
    protected override bool Validar()
    {
        if (OrdenCompraID <= 0) throw new InvalidOperationException("No hay ordenes con factura cancelada.");
        return true;
    }
}
