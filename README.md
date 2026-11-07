# TP8 - Ventas Minoristas

Requisitos: .NET SDK 10, SQL Server LocalDB y sqlcmd.

Desde la carpeta TP8, instalar la base nueva:

```powershell
sqlcmd -S "(localdb)\MSSQLLocalDB" -E -b -i .\SQL\01_Base_SQLedi3.sql
sqlcmd -S "(localdb)\MSSQLLocalDB" -E -b -i .\SQL\02_Ampliar_Cotizaciones.sql
sqlcmd -S "(localdb)\MSSQLLocalDB" -E -b -i .\SQL\04_Categoria_y_FechaSolicitada.sql
```

Compilar:

```powershell
dotnet build .\TP8.slnx -c Release
```

API:

```powershell
dotnet run -c Release --project .\Api\EDI3Ventas.csproj --launch-profile http
```

Cliente (segunda terminal):

```powershell
dotnet run -c Release --project .\Cliente\EDI3Ventas.Cliente.csproj
```