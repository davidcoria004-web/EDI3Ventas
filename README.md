# TP8 - Ventas Minoristas

Requisitos: .NET SDK 10, SQL Server LocalDB y sqlcmd.

Desde la carpeta TP8, instalar la base nueva:

```powershell
sqlcmd -S "(localdb)\MSSQLLocalDB" -E -b -i .\SQL\01_Base_SQLedi3.sql
sqlcmd -S "(localdb)\MSSQLLocalDB" -E -b -i .\SQL\02_Detalle_OrdenCompra.sql
sqlcmd -S "(localdb)\MSSQLLocalDB" -E -b -i .\SQL\04_Categoria_y_FechaSolicitada.sql
sqlcmd -S "(localdb)\MSSQLLocalDB" -E -b -i .\SQL\05_Pedido_a_OrdenCompra.sql
```

Las órdenes de compra se registran directamente con cliente, productos y cantidades. Para una base
existente, ejecutar también la migración `05_Pedido_a_OrdenCompra.sql` para trasladar los datos
anteriores y retirar las tablas del flujo anterior.

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