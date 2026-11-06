using System.ComponentModel;
using System.Drawing;

namespace EDI3Ventas.Cliente.Formularios;

/// <summary>Controles estandar WinForms y mensajes de error centralizados.</summary>
internal static class Ui
{
    // Crea una tabla de solo lectura.
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

    // Crea un boton WinForms.
    public static Button Boton(string titulo, EventHandler? evento = null)
    {
        var boton = new Button { Text = titulo, AutoSize = true, MinimumSize = new Size(100, 30), Margin = new Padding(4) };
        if (evento != null) boton.Click += evento;
        return boton;
    }

    // Alinea controles horizontalmente.
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

    // Carga los registros en la tabla.
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

    // Obtiene el registro seleccionado.
    public static T? Seleccion<T>(DataGridView tabla) where T : class =>
        tabla.CurrentRow?.DataBoundItem as T;

    // Solicita confirmacion al usuario.
    public static bool Confirmar(string pregunta) =>
        MessageBox.Show(pregunta, "Confirmar", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;

    // Muestra un aviso.
    public static void Aviso(string texto) => MessageBox.Show(texto, "EDI3 Ventas", MessageBoxButtons.OK, MessageBoxIcon.Information);

    // Muestra el error recibido.
    public static void Error(Exception error) => MessageBox.Show(
        error is HttpRequestException ? "No se pudo conectar a la API. Verifique su URL y que el servidor este ejecutandose.\n" + error.Message :
        error is TaskCanceledException ? "La API no respondio dentro del tiempo establecido." : error.Message,
        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);

    // Ejecuta la operacion y controla errores.
    public static async Task Ejecutar(Func<Task> operacion)
    {
        try { await operacion(); }
        catch (Exception ex) { Error(ex); }
    }
}
