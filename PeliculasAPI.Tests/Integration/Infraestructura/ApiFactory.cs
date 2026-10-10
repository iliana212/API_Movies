using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using PeliculasAPI.Servicios;

namespace PeliculasAPI.Tests.Integration.Infraestructura;

/// <summary>
/// Levanta la API real contra SQL Server en contenedor. Se usa como class fixture:
/// cada clase de pruebas obtiene su propia instancia (y por tanto sus propios limitadores de peticiones),
/// pero todas comparten la misma base de datos, por eso los datos de cada prueba llevan nombres únicos.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string JwtKey = "llave-secreta-solo-para-pruebas-0123456789";
    public const string Issuer = "https://pruebas.local";
    public const string Audience = "https://pruebas.local";
    public const string PasswordPruebas = "Prueba#12345";

    private static int _contadorIp;

    public async Task InitializeAsync()
    {
        var cadenaConexion = await SqlServerTestContainer.ObtenerCadenaConexionAsync();

        // Program.cs lee parte de la configuración (p. ej. origenesPermitidos) mientras construye la app,
        // antes de que la fábrica pueda inyectar valores. Las variables de entorno sí llegan a tiempo.
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", cadenaConexion);
        Environment.SetEnvironmentVariable("Jwt__Issuer", Issuer);
        Environment.SetEnvironmentVariable("Jwt__Audience", Audience);
        Environment.SetEnvironmentVariable("Jwt__llavejwt", JwtKey);
        Environment.SetEnvironmentVariable("origenesPermitidos", "http://localhost:5173");

        // Aplica las migraciones reales: así también se comprueba que el esquema se crea correctamente.
        using var alcance = Services.CreateScope();
        var contexto = alcance.ServiceProvider.GetRequiredService<ApplicationDBContext>();
        await contexto.Database.MigrateAsync();
    }

    // WebApplicationFactory ya define DisposeAsync() -> ValueTask; la de xUnit va explícita.
    Task IAsyncLifetime.DisposeAsync() => Task.CompletedTask;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(servicios =>
        {
            // Permite que cada prueba simule una IP de cliente distinta (el rate limiting particiona por IP).
            servicios.AddTransient<IStartupFilter, IpSimuladaStartupFilter>();

            // No escribir archivos reales en wwwroot ni depender de Azure.
            servicios.RemoveAll<IAlmacenadorArchivos>();
            servicios.AddSingleton<IAlmacenadorArchivos, AlmacenadorEnMemoria>();
        });
    }

    public static string CrearToken(string email, bool esAdmin = false)
    {
        var claims = new List<Claim> { new("email", email) };
        if (esAdmin)
            claims.Add(new Claim("esadmin", "true"));

        var llave = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtKey));
        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: new SigningCredentials(llave, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>Cliente HTTP con una IP simulada única (o la indicada) y, opcionalmente, un token.</summary>
    public HttpClient CrearCliente(string? token = null, string? ip = null)
    {
        var cliente = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        cliente.DefaultRequestHeaders.Add(IpSimuladaStartupFilter.Encabezado, ip ?? SiguienteIp());

        if (token is not null)
            cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return cliente;
    }

    /// <summary>Cliente con un token de administrador (claim esadmin). No requiere usuario en la base de datos.</summary>
    public HttpClient CrearClienteAdmin() =>
        CrearCliente(CrearToken($"admin-{Guid.NewGuid():N}@pruebas.test", esAdmin: true));

    /// <summary>Crea un usuario real en la base de datos, sin pasar por el endpoint de registro.</summary>
    public async Task<IdentityUser> CrearUsuarioAsync(string email, bool esAdmin = false)
    {
        using var alcance = Services.CreateScope();
        var userManager = alcance.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

        var usuario = new IdentityUser { UserName = email, Email = email };
        var resultado = await userManager.CreateAsync(usuario, PasswordPruebas);
        Assert.True(resultado.Succeeded, string.Join(", ", resultado.Errors.Select(e => e.Description)));

        if (esAdmin)
            await userManager.AddClaimAsync(usuario, new Claim("esadmin", "true"));

        return usuario;
    }

    private static string SiguienteIp()
    {
        var n = Interlocked.Increment(ref _contadorIp);
        return $"10.{(n >> 16) & 255}.{(n >> 8) & 255}.{n & 255}";
    }
}

/// <summary>Toma la IP del encabezado X-Test-Ip y la asigna a la conexión, antes del resto del pipeline.</summary>
internal sealed class IpSimuladaStartupFilter : IStartupFilter
{
    public const string Encabezado = "X-Test-Ip";

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> siguiente) => app =>
    {
        app.Use(async (contexto, continuar) =>
        {
            if (contexto.Request.Headers.TryGetValue(Encabezado, out var valor)
                && System.Net.IPAddress.TryParse(valor.ToString(), out var direccion))
            {
                contexto.Connection.RemoteIpAddress = direccion;
            }

            await continuar();
        });

        siguiente(app);
    };
}

/// <summary>Almacenamiento de archivos falso: devuelve una URL determinista y no escribe nada.</summary>
internal sealed class AlmacenadorEnMemoria : IAlmacenadorArchivos
{
    public Task<string> Almacenar(string contenedor, IFormFile archivo) =>
        Task.FromResult($"https://almacen.pruebas/{contenedor}/{Guid.NewGuid():N}{Path.GetExtension(archivo.FileName)}");

    public Task Borrar(string? ruta, string contenedor) => Task.CompletedTask;
}
