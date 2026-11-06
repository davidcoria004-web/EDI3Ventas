# EDI3Ventas - TP Final EDI 3

Sistema de ventas minoristas. Solucion .NET 10, SQL Server y cliente Windows Forms nativo.

## Estructura
- `EDI3Ventas/`: ASP.NET Core Web API; clientes, productos y circuito minorista.
- `EDI3Ventas.Cliente/`: interfaz Windows Forms y consumo HTTP.
- `SQL/`: instalacion, extension y verificacion del esquema.

## Compilacion
```powershell
dotnet restore .\EDI3Ventas.slnx
dotnet build .\EDI3Ventas.slnx -c Release --no-restore
```

## Ejecucion (dos terminales)
```powershell
dotnet run --project .\EDI3Ventas\EDI3Ventas.csproj --launch-profile http
```
```powershell
dotnet run --project .\EDI3Ventas.Cliente\EDI3Ventas.Cliente.csproj
```

La API debe usar la cadena de conexion `VentasDB` para SQL Server. El cliente utiliza por defecto `http://localhost:5232/`.

## SQL
Ejecutar `SQL/01_Base_SQLedi3.sql` y `SQL/02_Ampliar_Cotizaciones.sql` solo sobre la instancia de prueba correspondiente. Comprobar con `SQL/03_Verificar_Esquema.sql`. No publicar datos ni respaldos de la base.

## Notas
