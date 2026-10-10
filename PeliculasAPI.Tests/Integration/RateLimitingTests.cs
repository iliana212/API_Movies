using PeliculasAPI.Tests.Integration.Infraestructura;

namespace PeliculasAPI.Tests.Integration;

/// <summary>
/// Límites vigentes en Program.cs: login 5 por minuto y registro 3 cada 10 minutos, ambos por IP.
/// Cada prueba usa su propia IP simulada (encabezado X-Test-Ip) para no compartir cupo con las demás.
/// </summary>
public class RateLimitingTests(ApiFactory fabrica) : IClassFixture<ApiFactory>
{
    private const string RutaLogin = "/api/v1/usuarios/login";
    private const string RutaRegistrar = "/api/v1/usuarios/registrar";

    private static object LoginInvalido() => new { email = "nadie@pruebas.test", password = "Incorrecta#999" };

    private static object RegistroInvalido() => new { email = $"{Guid.NewGuid():N}@pruebas.test", password = "123" };

    [Fact]
    public async Task Login_AlSuperarCincoIntentosPorMinuto_Responde429ConRetryAfter()
    {
        var cliente = fabrica.CrearCliente(ip: "203.0.113.10");

        for (var intento = 1; intento <= 5; intento++)
        {
            var permitido = await cliente.PostAsJsonAsync(RutaLogin, LoginInvalido());
            Assert.Equal(HttpStatusCode.BadRequest, permitido.StatusCode);   // credenciales inválidas, pero dentro del cupo
        }

        var bloqueado = await cliente.PostAsJsonAsync(RutaLogin, LoginInvalido());

        Assert.Equal(HttpStatusCode.TooManyRequests, bloqueado.StatusCode);
        Assert.True(bloqueado.Headers.Contains("Retry-After"), "Falta el encabezado Retry-After");
		var cuerpo = await bloqueado.Content.ReadAsStringAsync();
		Assert.Contains("Demasiados intentos", cuerpo);
	}

    [Fact]
    public async Task Login_IpDistintaNoComparteElCupo()
    {
        var agotada = fabrica.CrearCliente(ip: "203.0.113.20");
        for (var intento = 1; intento <= 6; intento++)
            await agotada.PostAsJsonAsync(RutaLogin, LoginInvalido());

        var otroCliente = fabrica.CrearCliente(ip: "203.0.113.21");
        var respuesta = await otroCliente.PostAsJsonAsync(RutaLogin, LoginInvalido());

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);   // no 429
    }

    [Fact]
    public async Task Registrar_AlCuartoIntentoEnDiezMinutos_Responde429()
    {
        var cliente = fabrica.CrearCliente(ip: "203.0.113.30");

        for (var intento = 1; intento <= 3; intento++)
        {
            var permitido = await cliente.PostAsJsonAsync(RutaRegistrar, RegistroInvalido());
            Assert.Equal(HttpStatusCode.BadRequest, permitido.StatusCode);   // contraseña débil, pero dentro del cupo
        }

        var bloqueado = await cliente.PostAsJsonAsync(RutaRegistrar, RegistroInvalido());

        Assert.Equal(HttpStatusCode.TooManyRequests, bloqueado.StatusCode);
    }

    [Fact]
    public async Task Login_YRegistro_TienenCuposIndependientes()
    {
        var cliente = fabrica.CrearCliente(ip: "203.0.113.40");
        for (var intento = 1; intento <= 6; intento++)
            await cliente.PostAsJsonAsync(RutaLogin, LoginInvalido());   // agota el cupo de login

        var registro = await cliente.PostAsJsonAsync(RutaRegistrar, RegistroInvalido());

        Assert.Equal(HttpStatusCode.BadRequest, registro.StatusCode);    // el registro sigue disponible
    }

    [Fact]
    public async Task EndpointsSinLimite_NoSeBloqueanConMuchasPeticiones()
    {
        var cliente = fabrica.CrearCliente(ip: "203.0.113.50");

        for (var intento = 1; intento <= 20; intento++)
        {
            var respuesta = await cliente.GetAsync("/api/v1/generos/todos");
            Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        }
    }
}
