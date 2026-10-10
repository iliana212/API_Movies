namespace PeliculasAPI.Tests.Integration.Infraestructura;

/// <summary>
/// Helpers para crear datos a través de la API real. Todos los nombres llevan un GUID
/// porque las pruebas comparten la base de datos y no deben depender de los datos de otras.
/// </summary>
public static class DatosDePrueba
{
    public static string NombreGenero() => "G" + Guid.NewGuid().ToString("N");   // empieza con mayúscula, 33 caracteres

    public static string Titulo(string prefijo = "Peli") => $"{prefijo} {Guid.NewGuid():N}";

    public static async Task<JsonElement> LeerJsonAsync(this HttpResponseMessage respuesta) =>
        await respuesta.Content.ReadFromJsonAsync<JsonElement>();

    public static async Task<int> LeerIdAsync(this HttpResponseMessage respuesta) =>
        (await respuesta.LeerJsonAsync()).GetProperty("id").GetInt32();

    public static async Task<int> CrearGeneroAsync(this HttpClient admin, string? nombre = null)
    {
        var respuesta = await admin.PostAsJsonAsync("/api/v1/generos", new { nombre = nombre ?? NombreGenero() });
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        return await respuesta.LeerIdAsync();
    }

    public static async Task<int> CrearCineAsync(
        this HttpClient admin, double latitud = 21.1236, double longitud = -101.6869, string? nombre = null)
    {
        var respuesta = await admin.PostAsJsonAsync("/api/v1/cines",
            new { nombre = nombre ?? $"Cine {Guid.NewGuid():N}", latitud, longitud });
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        return await respuesta.LeerIdAsync();
    }

    public static MultipartFormDataContent FormularioActor(string? nombre, bool conFoto = false)
    {
        var formulario = new MultipartFormDataContent();
        if (nombre is not null)
            formulario.Add(new StringContent(nombre), "Nombre");
        formulario.Add(new StringContent("1980-05-20"), "FechaNacimiento");
        if (conFoto)
            formulario.Add(new ByteArrayContent(new byte[] { 1, 2, 3 }), "Foto", "foto.jpg");
        return formulario;
    }

    public static async Task<int> CrearActorAsync(this HttpClient admin, string? nombre = null, bool conFoto = false)
    {
        using var formulario = FormularioActor(nombre ?? $"Actor {Guid.NewGuid():N}", conFoto);
        var respuesta = await admin.PostAsync("/api/v1/actores", formulario);
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        return await respuesta.LeerIdAsync();
    }

    /// <summary>
    /// Formulario multipart que espera PeliculasController (listas como JSON dentro de campos de texto).
    /// Si <paramref name="titulo"/> es null, el campo se omite (para probar la validación).
    /// </summary>
    public static MultipartFormDataContent FormularioPelicula(
        string? titulo,
        DateTime? fecha = null,
        IEnumerable<int>? generos = null,
        IEnumerable<int>? cines = null,
        IEnumerable<(int ActorId, string Personaje)>? actores = null,
        bool conPoster = false)
    {
        var formulario = new MultipartFormDataContent();
        if (titulo is not null)
            formulario.Add(new StringContent(titulo), "Titulo");

        formulario.Add(new StringContent((fecha ?? new DateTime(2020, 1, 15)).ToString("yyyy-MM-dd")), "FechaLanzamiento");
        formulario.Add(new StringContent(JsonSerializer.Serialize(generos ?? Array.Empty<int>())), "GenerosIds");
        formulario.Add(new StringContent(JsonSerializer.Serialize(cines ?? Array.Empty<int>())), "CinesIds");

        var actoresJson = (actores ?? Array.Empty<(int ActorId, string Personaje)>())
            .Select(a => new { id = a.ActorId, personaje = a.Personaje });
        formulario.Add(new StringContent(JsonSerializer.Serialize(actoresJson)), "Actores");

        if (conPoster)
            formulario.Add(new ByteArrayContent(new byte[] { 1, 2, 3 }), "Poster", "poster.jpg");

        return formulario;
    }

    public static async Task<int> CrearPeliculaAsync(
        this HttpClient admin,
        string? titulo = null,
        DateTime? fecha = null,
        IEnumerable<int>? generos = null,
        IEnumerable<int>? cines = null,
        IEnumerable<(int ActorId, string Personaje)>? actores = null,
        bool conPoster = false)
    {
        using var formulario = FormularioPelicula(titulo ?? Titulo(), fecha, generos, cines, actores, conPoster);
        var respuesta = await admin.PostAsync("/api/v1/peliculas", formulario);
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        return await respuesta.LeerIdAsync();
    }
}
