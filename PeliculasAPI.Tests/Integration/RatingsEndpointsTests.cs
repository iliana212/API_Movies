using PeliculasAPI.Tests.Integration.Infraestructura;

namespace PeliculasAPI.Tests.Integration;

public class RatingsEndpointsTests(ApiFactory fabrica) : IClassFixture<ApiFactory>
{
    private const string RutaRating = "/api/v1/rating";
    private const string RutaPeliculas = "/api/v1/peliculas";

    // Fixture: un usuario real en la base de datos con su cliente autenticado.
    // ServicioUsuarios busca al usuario por el claim "email", así que debe existir.
    private async Task<HttpClient> CrearUsuarioAutenticadoAsync()
    {
        var email = $"{Guid.NewGuid():N}@pruebas.test";
        await fabrica.CrearUsuarioAsync(email);
        return fabrica.CrearCliente(ApiFactory.CrearToken(email));
    }

    private Task<int> CrearPeliculaAsync() => fabrica.CrearClienteAdmin().CrearPeliculaAsync();

    private static Task<HttpResponseMessage> Calificar(HttpClient cliente, int peliculaId, int puntuacion) =>
        cliente.PostAsJsonAsync(RutaRating, new { peliculaId, puntuacion });

    [Fact]
    public async Task Calificar_SinToken_Responde401()
    {
        var respuesta = await Calificar(fabrica.CrearCliente(), peliculaId: 1, puntuacion: 3);

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task Calificar_PrimerVoto_Responde204YActualizaElDetalle()
    {
        var usuario = await CrearUsuarioAutenticadoAsync();
        var peliculaId = await CrearPeliculaAsync();

        var respuesta = await Calificar(usuario, peliculaId, 4);

        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);
        var detalle = await usuario.GetFromJsonAsync<JsonElement>($"{RutaPeliculas}/{peliculaId}");
        Assert.Equal(4, detalle.GetProperty("promedioVoto").GetDouble());
        Assert.Equal(4, detalle.GetProperty("votoUsuario").GetInt32());
    }

    [Fact]
    public async Task Calificar_DosVecesElMismoUsuario_ActualizaSinDuplicar()
    {
        var usuario = await CrearUsuarioAutenticadoAsync();
        var peliculaId = await CrearPeliculaAsync();

        await Calificar(usuario, peliculaId, 2);
        await Calificar(usuario, peliculaId, 5);

        // Si se duplicara el voto, el promedio sería 3.5 en lugar de 5.
        var detalle = await usuario.GetFromJsonAsync<JsonElement>($"{RutaPeliculas}/{peliculaId}");
        Assert.Equal(5, detalle.GetProperty("promedioVoto").GetDouble());
        Assert.Equal(5, detalle.GetProperty("votoUsuario").GetInt32());
    }

    [Fact]
    public async Task Calificar_DosUsuarios_PromedioEsLaMedia()
    {
        var ana = await CrearUsuarioAutenticadoAsync();
        var luis = await CrearUsuarioAutenticadoAsync();
        var peliculaId = await CrearPeliculaAsync();

        await Calificar(ana, peliculaId, 2);
        await Calificar(luis, peliculaId, 5);

        var detalleDeAna = await ana.GetFromJsonAsync<JsonElement>($"{RutaPeliculas}/{peliculaId}");
        Assert.Equal(3.5, detalleDeAna.GetProperty("promedioVoto").GetDouble());
        Assert.Equal(2, detalleDeAna.GetProperty("votoUsuario").GetInt32());   // cada usuario ve su propio voto
    }

    [Fact]
    public async Task Detalle_Anonimo_MuestraPromedioYVotoUsuarioCero()
    {
        var usuario = await CrearUsuarioAutenticadoAsync();
        var peliculaId = await CrearPeliculaAsync();
        await Calificar(usuario, peliculaId, 4);

        var detalle = await fabrica.CrearCliente().GetFromJsonAsync<JsonElement>($"{RutaPeliculas}/{peliculaId}");

        Assert.Equal(4, detalle.GetProperty("promedioVoto").GetDouble());
        Assert.Equal(0, detalle.GetProperty("votoUsuario").GetInt32());
    }

    [Theory]
    [InlineData(1)]   // límite inferior válido
    [InlineData(5)]   // límite superior válido
    public async Task Calificar_ConPuntuacionEnLosLimitesValidos_Responde204(int puntuacion)
    {
        var usuario = await CrearUsuarioAutenticadoAsync();
        var peliculaId = await CrearPeliculaAsync();

        var respuesta = await Calificar(usuario, peliculaId, puntuacion);

        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);
    }

    [Theory]
    [InlineData(0)]    // justo debajo del mínimo
    [InlineData(6)]    // justo encima del máximo
    [InlineData(-1)]
    public async Task Calificar_ConPuntuacionFueraDeRango_Responde400(int puntuacion)
    {
        var usuario = await CrearUsuarioAutenticadoAsync();
        var peliculaId = await CrearPeliculaAsync();

        var respuesta = await Calificar(usuario, peliculaId, puntuacion);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task Calificar_PeliculaInexistente_Responde400ReferenciaInvalida()
    {
        var usuario = await CrearUsuarioAutenticadoAsync();

        var respuesta = await Calificar(usuario, peliculaId: 999999, puntuacion: 3);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        var problema = await respuesta.LeerJsonAsync();
        Assert.Equal("Referencia inválida", problema.GetProperty("title").GetString());
    }
}
