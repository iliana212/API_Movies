using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using PeliculasAPI.Tests.Integration.Infraestructura;

namespace PeliculasAPI.Tests.Integration;

public class HealthChecksTests(ApiFactory fabrica) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Live_SinToken_Responde200()
    {
        var respuesta = await fabrica.CrearCliente().GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal("Healthy", await respuesta.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Ready_ConBaseDeDatosDisponible_Responde200ConJson()
    {
        var respuesta = await fabrica.CrearCliente().GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var cuerpo = await respuesta.LeerJsonAsync();
        Assert.Equal("Healthy", cuerpo.GetProperty("estado").GetString());
        var check = Assert.Single(cuerpo.GetProperty("checks").EnumerateArray());
        Assert.Equal("PeliculasAPI", check.GetProperty("nombre").GetString());
        Assert.Equal("Healthy", check.GetProperty("estado").GetString());
    }

    [Fact]
    public async Task Ready_ConCheckFallido_Responde503SinExponerDetalles()
    {
        // Escenario de falla: se agrega un check "ready" que siempre falla con un mensaje sensible.
        using var fabricaConFalla = fabrica.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(servicios =>
                servicios.AddHealthChecks().AddCheck(
                    "servicio-caido",
                    () => HealthCheckResult.Unhealthy("detalle-secreto-interno"),
                    tags: new[] { "ready" })));

        var respuesta = await fabricaConFalla.CreateClient().GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, respuesta.StatusCode);
        var texto = await respuesta.Content.ReadAsStringAsync();
        Assert.Contains("Unhealthy", texto);
        Assert.DoesNotContain("detalle-secreto-interno", texto);
    }
}
