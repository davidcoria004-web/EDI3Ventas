using System.Text.Json;

namespace EDI3Ventas.Cliente.Infraestructura;

internal sealed class Preferencias
{
    public string ApiUrl { get; set; } = "http://localhost:5232/";
    private static string Ruta => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "EDI3Ventas.Cliente", "preferencias.json");

    // Lee la URL guardada.
    public static Preferencias Cargar()
    {
        try { return JsonSerializer.Deserialize<Preferencias>(File.ReadAllText(Ruta)) ?? new(); }
        catch (IOException) { return new(); }
        catch (JsonException) { return new(); }
        catch (UnauthorizedAccessException) { return new(); }
    }

    // Guarda la URL localmente.
    public void Guardar()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Ruta)!);
        File.WriteAllText(Ruta, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }
}
