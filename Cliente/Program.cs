using System.Globalization;
using EDI3Ventas.Cliente.Formularios;

namespace EDI3Ventas.Cliente;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.GetCultureInfo("es-AR");
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("es-AR");
        ApplicationConfiguration.Initialize();
        Application.Run(new PrincipalForm());
    }
}
