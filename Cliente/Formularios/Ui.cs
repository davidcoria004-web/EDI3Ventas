using System.ComponentModel;
using System.Drawing;

namespace EDI3Ventas.Cliente.Formularios;

internal static class Ui
{
    public static DataGridView Tabla()
    {
        return new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
            AutoGenerateColumns = true, MultiSelect = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            RowHeadersVisible = false,
            AllowUserToResizeRows = false
        };
    }

    public static Button Boton(string titulo, EventHandler? evento = null)
    {
        var boton = new Button { Text = titulo, AutoSize = true, MinimumSize = new Size(100, 30), Margin = new Padding(4) };
        if (evento != null) boton.Click += evento;
        return boton;
    }

    public static FlowLayoutPanel Barra(params Control[] controles)
    {
        var barra = new FlowLayoutPanel
        {
            Dock = DockStyle.Top, FlowDirection = FlowDirection.LeftToRight,
            AutoScroll = true, WrapContents = false, Height = 46,
            Padding = new Padding(5, 4, 5, 4)
        };
        barra.Controls.AddRange(controles);
        return barra;
    }

    public static void Datos<T>(DataGridView tabla, IEnumerable<T> datos)
    {
        tabla.DataSource = new BindingList<T>(datos.ToList());
        foreach (DataGridViewColumn columna in tabla.Columns)
        {
            if (columna.Name is "Presentacion" or "Detalles") columna.Visible = false;
            if (columna.ValueType == typeof(decimal) || columna.ValueType == typeof(decimal?))
                columna.DefaultCellStyle.Format = columna.Name is "Cantidad" or "StockActual" ? "N3" : "N2";
            if (columna.ValueType == typeof(DateTime) || columna.ValueType == typeof(DateTime?))
                columna.DefaultCellStyle.Format = "g";
        }
    }

    public static T? Seleccion<T>(DataGridView tabla) where T : class =>
        tabla.CurrentRow?.DataBoundItem as T;

    public static bool ConfirmarCotizacion(IWin32Window propietario, int pedidoId)
    {
        using var ventana = new Form
        {
            Text = "Emitir cotizacion",
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            ClientSize = new Size(430, 130),
            MaximizeBox = false,
            MinimizeBox = false,
            ShowInTaskbar = false
        };
        var mensaje = new Label
        {
            Text = $"Emitir la cotizacion del pedido {pedidoId}?",
            Location = new Point(16, 14),
            Size = new Size(398, 54),
            TextAlign = ContentAlignment.MiddleLeft
        };
        var aceptar = new Button
        {
            Text = "Aceptar",
            Location = new Point(218, 86),
            Size = new Size(92, 30),
            DialogResult = DialogResult.OK
        };
        var rechazar = new Button
        {
            Text = "Rechazar",
            Location = new Point(320, 86),
            Size = new Size(92, 30),
            DialogResult = DialogResult.Cancel
        };
        ventana.Controls.AddRange(new Control[] { mensaje, aceptar, rechazar });
        ventana.AcceptButton = aceptar;
        ventana.CancelButton = rechazar;
        return ventana.ShowDialog(propietario) == DialogResult.OK;
    }
    public static bool Confirmar(string pregunta) =>
        MessageBox.Show(pregunta, "Confirmar", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;

    public static void Aviso(string texto) => MessageBox.Show(texto, "EDI3 Ventas", MessageBoxButtons.OK, MessageBoxIcon.Information);

    public static void Error(Exception error) => MessageBox.Show(
        error is HttpRequestException ? "No se pudo conectar a la API. Verifique su URL y que el servidor este ejecutandose.\n" + error.Message :
        error is TaskCanceledException ? "La API no respondio dentro del tiempo establecido." : error.Message,
        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);

    public static async Task Ejecutar(Func<Task> operacion)
    {
        try { await operacion(); }
        catch (Exception ex) { Error(ex); }
    }
}
