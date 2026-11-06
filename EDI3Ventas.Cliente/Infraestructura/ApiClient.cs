using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EDI3Ventas.Cliente.Infraestructura;

/// <summary>Cliente HTTP comun; la UI nunca accede directamente a SQL Server.</summary>
internal sealed class ApiClient
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    public string BaseUrl { get; set; } = "http://localhost:5232/";

    // Forma la URL del endpoint.
    private Uri Url(string ruta)
    {
        if (!Uri.TryCreate(BaseUrl.Trim().TrimEnd('/') + "/", UriKind.Absolute, out var raiz) ||
            raiz.Scheme is not ("http" or "https"))
            throw new InvalidOperationException("La URL de la API debe comenzar con http:// o https://.");
        return new Uri(raiz, ruta.TrimStart('/'));
    }

    // Consulta la API mediante GET.
    public async Task<T> ObtenerAsync<T>(string ruta)
    {
        using var res = await Http.GetAsync(Url(ruta));
        await Validar(res);
        return (await res.Content.ReadFromJsonAsync<T>(Json))
               ?? throw new InvalidOperationException("La API devolvio una respuesta vacia.");
    }

    // Envia solicitudes HTTP con JSON.
    public async Task<JsonElement> EnviarAsync(HttpMethod verbo, string ruta, object? cuerpo = null)
    {
        using var msg = new HttpRequestMessage(verbo, Url(ruta));
        if (cuerpo != null) msg.Content = JsonContent.Create(cuerpo, options: Json);
        using var res = await Http.SendAsync(msg);
        await Validar(res);
        if (res.Content.Headers.ContentLength == 0) return default;
        var texto = await res.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(texto)) return default;
        try { return JsonSerializer.Deserialize<JsonElement>(texto, Json); }
        catch (JsonException) { return default; }
    }

    // Envia una solicitud POST.
    public Task<JsonElement> CrearAsync(string ruta, object body) => EnviarAsync(HttpMethod.Post, ruta, body);
    // Envia una solicitud PUT.
    public Task<JsonElement> ActualizarAsync(string ruta, object body) => EnviarAsync(HttpMethod.Put, ruta, body);
    // Envia una solicitud DELETE.
    public Task<JsonElement> EliminarAsync(string ruta) => EnviarAsync(HttpMethod.Delete, ruta);

    // Comprueba la respuesta HTTP.
    private static async Task Validar(HttpResponseMessage respuesta)
    {
        if (respuesta.IsSuccessStatusCode) return;
        string mensaje = $"HTTP {(int)respuesta.StatusCode} {respuesta.ReasonPhrase}";
        try
        {
            var texto = await respuesta.Content.ReadAsStringAsync();
            if (!string.IsNullOrWhiteSpace(texto))
            {
                try
                {
                    using var doc = JsonDocument.Parse(texto);
                    var r = doc.RootElement;
                    foreach (var nombre in new[] { "error", "mensaje", "detail", "title" })
                    {
                        if (r.ValueKind == JsonValueKind.Object && r.TryGetProperty(nombre, out var val) && val.ValueKind == JsonValueKind.String)
                        { mensaje += ": " + val.GetString(); break; }
                    }
                    if (r.ValueKind == JsonValueKind.String) mensaje += ": " + r.GetString();
                }
                catch (JsonException) { mensaje += ": " + texto[..Math.Min(250, texto.Length)]; }
            }
        }
        catch (HttpRequestException) { }
        throw new ApiException(mensaje, respuesta.StatusCode);
    }
}

internal sealed class ApiException(string message, HttpStatusCode status) : Exception(message)
{
    public HttpStatusCode Status { get; } = status;
}
