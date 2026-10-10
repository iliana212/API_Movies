using PeliculasAPI.Tests.Integration.Infraestructura;

namespace PeliculasAPI.Tests.Integration;

public class VersionadoTests(ApiFactory fabrica) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task RutaConVersion1_Responde200()
    {
        var respuesta = await fabrica.CrearCliente().GetAsync("/api/v1/generos/todos");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    [Fact]
    public async Task Respuesta_IncluyeEncabezadoApiSupportedVersions()
    {
		var respuesta = await fabrica.CrearCliente().GetAsync("/api/v1/peliculas/filtrar");

		Assert.True(respuesta.Headers.TryGetValues("api-supported-versions", out var versiones));
        Assert.Contains("1.0", versiones!);
    }

    [Fact]
    public async Task RutaSinVersion_Responde404()
    {
        var respuesta = await fabrica.CrearCliente().GetAsync("/api/generos/todos");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    [Fact]
    public async Task VersionNoSoportada_NoResponde200()
    {
        var respuesta = await fabrica.CrearCliente().GetAsync("/api/v2/generos/todos");

        // Asp.Versioning responde 400 (versión no soportada); aceptamos 404 por si cambia la política.
        Assert.True(respuesta.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound,
            $"Se esperaba 400 o 404 y llegó {(int)respuesta.StatusCode}");
    }
}
