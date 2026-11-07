using System.Drawing;
using System.Drawing.Printing;

namespace EDI3Ventas.Cliente.Formularios;

internal sealed class DocumentoForm : Form
{
    private readonly TextBox texto = new()
    {
        Dock = DockStyle.Fill, ReadOnly = true, Multiline = true, ScrollBars = ScrollBars.Both,
        Font = new Font("Consolas", 10F), WordWrap = false
    };

    public DocumentoForm(string titulo, string contenido)
    {
        Text = titulo; Width = 830; Height = 620; MinimumSize = new Size(600, 400);
        StartPosition = FormStartPosition.CenterParent;
        texto.Text = contenido;
        var pie = Ui.Barra(
            Ui.Boton("Copiar", (_, _) => Clipboard.SetText(texto.Text)),
            Ui.Boton("Vista de impresion", (_, _) => MostrarImpresion()),
            Ui.Boton("Cerrar", (_, _) => Close()));
        pie.Dock = DockStyle.Bottom;
        Controls.Add(texto); Controls.Add(pie);
    }

    private void MostrarImpresion()
    {
        using var documento = new PrintDocument();
        string[] lineas = texto.Lines;
        int indice = 0;
        documento.PrintPage += (_, e) =>
        {
            using var fuente = new Font("Consolas", 10F);
            float y = e.MarginBounds.Top, salto = fuente.GetHeight(e.Graphics!) * 1.35f;
            while (indice < lineas.Length && y + salto <= e.MarginBounds.Bottom)
            {
                e.Graphics!.DrawString(lineas[indice++], fuente, Brushes.Black, e.MarginBounds.Left, y);
                y += salto;
            }
            e.HasMorePages = indice < lineas.Length;
        };
        using var preview = new PrintPreviewDialog { Document = documento, Width = 900, Height = 700 };
        try { preview.ShowDialog(this); }
        catch (Exception ex) { Ui.Error(ex); }
    }
}
