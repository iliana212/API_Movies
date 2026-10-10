using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using PeliculasAPI.Utilidades;

namespace PeliculasAPI.Tests.Unit;

public class GlobalExceptionHandlerTests
{
    // Fixture: entorno de ejecución simulado (Development / Production).
    private sealed class EntornoFalso(string nombre) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = nombre;
        public string ApplicationName { get; set; } = "PeliculasAPI.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed record Resultado(bool Manejado, int StatusHttp, string? ContentType, JsonElement Cuerpo);

    private static async Task<Resultado> EjecutarAsync(Exception excepcion, string entorno = "Production")
    {
        var manejador = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance, new EntornoFalso(entorno));
        var contexto = new DefaultHttpContext();
        contexto.Request.Method = HttpMethods.Get;
        contexto.Request.Path = "/api/v1/generos";
        contexto.TraceIdentifier = "traza-de-prueba";
        contexto.Response.Body = new MemoryStream();

        var manejado = await manejador.TryHandleAsync(contexto, excepcion, CancellationToken.None);

        contexto.Response.Body.Position = 0;
        using var documento = await JsonDocument.ParseAsync(contexto.Response.Body);
        return new Resultado(manejado, contexto.Response.StatusCode, contexto.Response.ContentType, documento.RootElement.Clone());
    }

    [Fact]
    public async Task CualquierExcepcion_SeMarcaComoManejadaYRespondeJson()
    {
        var resultado = await EjecutarAsync(new InvalidOperationException("x"));

        Assert.True(resultado.Manejado);
        Assert.StartsWith("application/json", resultado.ContentType);
    }

    [Fact]
    public async Task ConflictoDeConcurrencia_Responde409()
    {
        var resultado = await EjecutarAsync(new DbUpdateConcurrencyException("conflicto"));

        Assert.Equal(StatusCodes.Status409Conflict, resultado.StatusHttp);
        Assert.Equal("Conflicto de concurrencia", resultado.Cuerpo.GetProperty("title").GetString());
    }

    [Fact]
    public async Task ErrorGenericoDeBaseDeDatos_Responde400()
    {
        var resultado = await EjecutarAsync(new DbUpdateException("fallo", new Exception("interna")));

        Assert.Equal(StatusCodes.Status400BadRequest, resultado.StatusHttp);
        Assert.Equal("Error al guardar los datos", resultado.Cuerpo.GetProperty("title").GetString());
    }

    [Fact]
    public async Task SolicitudHttpInvalida_Responde400()
    {
        var resultado = await EjecutarAsync(new BadHttpRequestException("cuerpo inválido"));

        Assert.Equal(StatusCodes.Status400BadRequest, resultado.StatusHttp);
        Assert.Equal("Solicitud inválida", resultado.Cuerpo.GetProperty("title").GetString());
    }

    [Fact]
    public async Task ErrorInesperado_EnProduccion_Responde500SinFiltrarElMensaje()
    {
        var resultado = await EjecutarAsync(new InvalidOperationException("cadena de conexion secreta"), "Production");

        Assert.Equal(StatusCodes.Status500InternalServerError, resultado.StatusHttp);
        Assert.Equal("Ocurrió un error inesperado.", resultado.Cuerpo.GetProperty("detail").GetString());
        Assert.DoesNotContain("secreta", resultado.Cuerpo.GetRawText());
    }

    [Fact]
    public async Task ErrorInesperado_EnDesarrollo_IncluyeElMensajeReal()
    {
        var resultado = await EjecutarAsync(new InvalidOperationException("detalle visible"), "Development");

        Assert.Equal(StatusCodes.Status500InternalServerError, resultado.StatusHttp);
        Assert.Equal("detalle visible", resultado.Cuerpo.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Respuesta_IncluyeTraceIdInstanceYStatusCoherente()
    {
        var resultado = await EjecutarAsync(new DbUpdateConcurrencyException("c"));

        Assert.Equal("traza-de-prueba", resultado.Cuerpo.GetProperty("traceId").GetString());
        Assert.Equal("/api/v1/generos", resultado.Cuerpo.GetProperty("instance").GetString());
        Assert.Equal(resultado.StatusHttp, resultado.Cuerpo.GetProperty("status").GetInt32());
    }
}
