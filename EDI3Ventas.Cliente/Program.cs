using System.Globalization;
using EDI3Ventas.Cliente.Formularios;

namespace EDI3Ventas.Cliente;

internal static class Program
{
    [STAThread]
    // Inicia la aplicacion WinForms.
    private static void Main()
    {
        // Formatos argentinos sin alterar los valores numericos enviados en JSON.
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.GetCultureInfo("es-AR");
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("es-AR");
        ApplicationConfiguration.Initialize();
        Application.Run(new PrincipalForm());
    }
}
