using System.IdentityModel.Tokens.Jwt;
using PeliculasAPI.Tests.Integration.Infraestructura;

namespace PeliculasAPI.Tests.Integration;

public class AutenticacionEndpointsTests(ApiFactory fabrica) : IClassFixture<ApiFactory>
{
    private const string RutaRegistrar = "/api/v1/usuarios/registrar";
    private const string RutaLogin = "/api/v1/usuarios/login";

    private static string EmailUnico() => $"{Guid.NewGuid():N}@pruebas.test";

    // Cada cliente usa una IP simulada distinta, así que el rate limiting no interfiere entre pruebas.

    // ---------- Registro ----------

    [Fact]
    public async Task Registrar_ConDatosValidos_Responde200ConToken()
    {
        var respuesta = await fabrica.CrearCliente()
            .PostAsJsonAsync(RutaRegistrar, new { email = EmailUnico(), password = ApiFactory.PasswordPruebas });

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var cuerpo = await respuesta.LeerJsonAsync();
        var token = cuerpo.GetProperty("token").GetString()!;
        Assert.Equal(3, token.Split('.').Length);   // un JWT tiene encabezado.carga.firma
        Assert.True(cuerpo.GetProperty("expiracion").GetDateTime() > DateTime.UtcNow);
    }

    [Fact]
    public async Task Registrar_ConEmailDuplicado_Responde400()
    {
        var email = EmailUnico();
        await fabrica.CrearUsuarioAsync(email);

        var respuesta = await fabrica.CrearCliente()
            .PostAsJsonAsync(RutaRegistrar, new { email, password = ApiFactory.PasswordPruebas });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task Registrar_ConPasswordDebil_Responde400()
    {
        var respuesta = await fabrica.CrearCliente()
            .PostAsJsonAsync(RutaRegistrar, new { email = EmailUnico(), password = "123" });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task Registrar_ConEmailInvalido_Responde400()
    {
        var respuesta = await fabrica.CrearCliente()
            .PostAsJsonAsync(RutaRegistrar, new { email = "no-es-un-correo", password = ApiFactory.PasswordPruebas });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task Registrar_SinPassword_Responde400()
    {
        var respuesta = await fabrica.CrearCliente()
            .PostAsJsonAsync(RutaRegistrar, new { email = EmailUnico() });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    // ---------- Login ----------

    [Fact]
    public async Task Login_ConCredencialesValidas_Responde200ConClaimEmail()
    {
        var email = EmailUnico();
        await fabrica.CrearUsuarioAsync(email);

        var respuesta = await fabrica.CrearCliente()
            .PostAsJsonAsync(RutaLogin, new { email, password = ApiFactory.PasswordPruebas });

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var token = (await respuesta.LeerJsonAsync()).GetProperty("token").GetString()!;
        var claims = new JwtSecurityTokenHandler().ReadJwtToken(token).Claims;
        Assert.Equal(email, claims.Single(c => c.Type == "email").Value);
    }

    [Fact]
    public async Task Login_ConPasswordIncorrecta_Responde400()
    {
        var email = EmailUnico();
        await fabrica.CrearUsuarioAsync(email);

        var respuesta = await fabrica.CrearCliente()
            .PostAsJsonAsync(RutaLogin, new { email, password = "Incorrecta#999" });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Contains("Login incorrecto", await respuesta.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Login_ConEmailInexistente_DevuelveElMismoErrorQueConPasswordIncorrecta()
    {
        var email = EmailUnico();
        await fabrica.CrearUsuarioAsync(email);
        var cliente = fabrica.CrearCliente();

        var passwordMala = await cliente.PostAsJsonAsync(RutaLogin, new { email, password = "Incorrecta#999" });
        var usuarioInexistente = await cliente.PostAsJsonAsync(RutaLogin,
            new { email = EmailUnico(), password = "Incorrecta#999" });

        // Mismo estado y mismo cuerpo: la respuesta no revela si el correo está registrado.
        Assert.Equal(passwordMala.StatusCode, usuarioInexistente.StatusCode);
        Assert.Equal(
            await passwordMala.Content.ReadAsStringAsync(),
            await usuarioInexistente.Content.ReadAsStringAsync());
    }
}
