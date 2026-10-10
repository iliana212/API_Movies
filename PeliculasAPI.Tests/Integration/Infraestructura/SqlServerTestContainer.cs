using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;

namespace PeliculasAPI.Tests.Integration.Infraestructura;

/// <summary>
/// Un único SQL Server en contenedor para toda la ejecución de pruebas.
/// Se usa SQL Server real (y no la base en memoria de EF Core) porque el modelo
/// tiene columnas "geography" de NetTopologySuite. Requiere Docker en ejecución.
/// El contenedor lo elimina automáticamente el proceso "Ryuk" de Testcontainers al terminar.
/// </summary>
public static class SqlServerTestContainer
{
    private static readonly MsSqlContainer Contenedor =
        new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    private static readonly SemaphoreSlim Candado = new(1, 1);
    private static string? _cadenaConexion;

    public static async Task<string> ObtenerCadenaConexionAsync()
    {
        await Candado.WaitAsync();
        try
        {
            if (_cadenaConexion is null)
            {
                await Contenedor.StartAsync();
                _cadenaConexion = new SqlConnectionStringBuilder(Contenedor.GetConnectionString())
                {
                    InitialCatalog = "PeliculasTests"
                }.ConnectionString;
            }

            return _cadenaConexion;
        }
        finally
        {
            Candado.Release();
        }
    }
}
