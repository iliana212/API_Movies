using PeliculasAPI.Tests.Integration.Infraestructura;

namespace PeliculasAPI.Tests.Integration;

public class AdministracionUsuariosTests(ApiFactory fabrica) : IClassFixture<ApiFactory>
{
    private const string Ruta = "/api/v1/usuarios";

    private static string EmailUnico() => $"{Guid.NewGuid():N}@pruebas.test";

    // Inicia sesión por la API y devuelve un cliente con el token recién emitido
    // (el claim esadmin viaja dentro del token, por eso hay que volver a iniciar sesión tras un cambio).
    private async Task<HttpClient> IniciarSesionAsync(string email)
    {
        var login = await fabrica.CrearCliente()
            .PostAsJsonAsync($"{Ruta}/login", new { email, password = ApiFactory.PasswordPruebas });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var token = (await login.LeerJsonAsync()).GetProperty("token").GetString()!;
        return fabrica.CrearCliente(token);
    }

    [Fact]
    public async Task HacerAdmin_ComoAdmin_OtorgaAccesoAEndpointsDeAdmin()
    {
        var email = EmailUnico();
        await fabrica.CrearUsuarioAsync(email);
        var antes = await IniciarSesionAsync(email);
        Assert.Equal(HttpStatusCode.Forbidden, (await antes.GetAsync("/api/v1/generos")).StatusCode);

        var promocion = await fabrica.CrearClienteAdmin().PostAsJsonAsync($"{Ruta}/HacerAdmin", new { email });

        Assert.Equal(HttpStatusCode.NoContent, promocion.StatusCode);
        var despues = await IniciarSesionAsync(email);
        Assert.Equal(HttpStatusCode.OK, (await despues.GetAsync("/api/v1/generos")).StatusCode);
    }

    [Fact]
    public async Task RemoverAdmin_ComoAdmin_QuitaElAcceso()
    {
        var email = EmailUnico();
        await fabrica.CrearUsuarioAsync(email, esAdmin: true);
        var comoAdmin = await IniciarSesionAsync(email);
        Assert.Equal(HttpStatusCode.OK, (await comoAdmin.GetAsync("/api/v1/generos")).StatusCode);

        var retiro = await fabrica.CrearClienteAdmin().PostAsJsonAsync($"{Ruta}/RemoverAdmin", new { email });

        Assert.Equal(HttpStatusCode.NoContent, retiro.StatusCode);
        var sinPermiso = await IniciarSesionAsync(email);
        Assert.Equal(HttpStatusCode.Forbidden, (await sinPermiso.GetAsync("/api/v1/generos")).StatusCode);
    }

    [Fact]
    public async Task HacerAdmin_ConUsuarioInexistente_Responde404()
    {
        var respuesta = await fabrica.CrearClienteAdmin()
            .PostAsJsonAsync($"{Ruta}/HacerAdmin", new { email = EmailUnico() });

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    [Fact]
    public async Task HacerAdmin_SinSerAdmin_Responde403()
    {
        var noAdmin = fabrica.CrearCliente(ApiFactory.CrearToken("usuario@pruebas.test"));

        var respuesta = await noAdmin.PostAsJsonAsync($"{Ruta}/HacerAdmin", new { email = EmailUnico() });

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Fact]
    public async Task ListadoUsuarios_ComoAdmin_Responde200ConTotal()
    {
        var email = EmailUnico();
        await fabrica.CrearUsuarioAsync(email);

        var respuesta = await fabrica.CrearClienteAdmin().GetAsync($"{Ruta}/ListadoUsuarios?recordsPorPagina=50");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var total = int.Parse(respuesta.Headers.GetValues("cantidad-total-registros").Single());
        Assert.True(total >= 1);
    }
}
