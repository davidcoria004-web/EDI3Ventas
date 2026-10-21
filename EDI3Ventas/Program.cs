
using Microsoft.Data.SqlClient;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();

app.MapControllers();


// Cadena de conexión
string connectionString =
    builder.Configuration.GetConnectionString("VentasDB")
    ?? throw new InvalidOperationException(
        "No se encontro la conexion VentasDB en appsettings.json.");

// Endpoint principal
//app.MapGet("/", () => "API EDI3Ventas funcionando");

// Comprobar conexión a SQL Server
app.MapGet("/api/estado-db", async () =>
{
    try
    {
        await using var cn = new SqlConnection(connectionString);
        await cn.OpenAsync();

        return Results.Ok(new
        {
            mensaje = "Conexion con SQL Server correcta",
            baseDatos = cn.Database
        });
    }
    catch (SqlException ex)
    {
        return Results.Problem(
            title: "Error de conexion con SQL Server",
            detail: ex.Message,
            statusCode: 500);
    }
});



app.Run();
