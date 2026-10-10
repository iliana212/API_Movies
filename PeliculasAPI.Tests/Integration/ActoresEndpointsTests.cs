using PeliculasAPI.Tests.Integration.Infraestructura;

namespace PeliculasAPI.Tests.Integration;

public class ActoresEndpointsTests(ApiFactory fabrica) : IClassFixture<ApiFactory>
{
    private const string Ruta = "/api/v1/actores";

    [Fact]
    public async Task Crear_ConDatosValidos_Responde201ConLocation()
    {
        var admin = fabrica.CrearClienteAdmin();
        using var formulario = DatosDePrueba.FormularioActor($"Actor {Guid.NewGuid():N}");

        var respuesta = await admin.PostAsync(Ruta, formulario);

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        var id = await respuesta.LeerIdAsync();
        Assert.EndsWith($"{Ruta}/{id}", respuesta.Headers.Location!.AbsolutePath);
    }

    [Fact]
    public async Task Crear_SinNombre_Responde400()
    {
        using var formulario = DatosDePrueba.FormularioActor(nombre: null);

        var respuesta = await fabrica.CrearClienteAdmin().PostAsync(Ruta, formulario);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task Crear_ConFoto_GuardaLaUrlDelAlmacenador()
    {
        var admin = fabrica.CrearClienteAdmin();
        var id = await admin.CrearActorAsync(conFoto: true);

        var actor = await admin.GetFromJsonAsync<JsonElement>($"{Ruta}/{id}");

        Assert.StartsWith("https://almacen.pruebas/actores/", actor.GetProperty("foto").GetString());
    }

    [Fact]
    public async Task BuscarPorNombre_DevuelveCoincidenciasParciales()
    {
        var admin = fabrica.CrearClienteAdmin();
        var marca = $"Zq{Guid.NewGuid():N}";
        await admin.CrearActorAsync(nombre: $"{marca} Primero");
        await admin.CrearActorAsync(nombre: $"Otro {marca}");
        await admin.CrearActorAsync(nombre: $"Distinto {Guid.NewGuid():N}");

        var resultado = await admin.GetFromJsonAsync<JsonElement>($"{Ruta}/{marca}");

        Assert.Equal(2, resultado.GetArrayLength());
        Assert.All(resultado.EnumerateArray(),
            actor => Assert.Contains(marca, actor.GetProperty("nombre").GetString()));
    }

    [Fact]
    public async Task BuscarPorNombre_SinCoincidencias_DevuelveListaVacia()
    {
        var resultado = await fabrica.CrearClienteAdmin()
            .GetFromJsonAsync<JsonElement>($"{Ruta}/NoExiste{Guid.NewGuid():N}");

        Assert.Equal(0, resultado.GetArrayLength());
    }

    [Fact]
    public async Task BuscarPorNombre_SinToken_Responde401()
    {
        var respuesta = await fabrica.CrearCliente().GetAsync($"{Ruta}/Cualquiera");

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }
}
