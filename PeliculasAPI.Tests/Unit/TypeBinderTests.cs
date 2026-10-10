using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Primitives;
using PeliculasAPI.DTOs;
using PeliculasAPI.Utilidades;

namespace PeliculasAPI.Tests.Unit;

public class TypeBinderTests
{
    private const string NombreCampo = "campo";

    // Fixture: contexto de enlace con un valor (o ninguno) para el campo "campo".
    private static DefaultModelBindingContext CrearContexto(Type tipoModelo, string? valor)
    {
        var datos = new Dictionary<string, StringValues>();
        if (valor is not null)
            datos[NombreCampo] = valor;

        return new DefaultModelBindingContext
        {
            ModelName = NombreCampo,
            ModelMetadata = new EmptyModelMetadataProvider().GetMetadataForType(tipoModelo),
            ValueProvider = new QueryStringValueProvider(BindingSource.Query, new QueryCollection(datos), CultureInfo.InvariantCulture),
            ModelState = new ModelStateDictionary()
        };
    }

    private static async Task<DefaultModelBindingContext> EnlazarAsync(Type tipoModelo, string? valor)
    {
        var contexto = CrearContexto(tipoModelo, valor);
        await new TypeBinder().BindModelAsync(contexto);
        return contexto;
    }

    [Fact]
    public async Task ListaDeEnteros_ConJsonValido_Enlaza()
    {
        var contexto = await EnlazarAsync(typeof(List<int>), "[1,2,3]");

        Assert.True(contexto.Result.IsModelSet);
        var lista = Assert.IsType<List<int>>(contexto.Result.Model);
        Assert.Equal(new[] { 1, 2, 3 }, lista);
    }

    [Fact]
    public async Task ListaVacia_Enlaza()
    {
        var contexto = await EnlazarAsync(typeof(List<int>), "[]");

        Assert.True(contexto.Result.IsModelSet);
        Assert.Empty(Assert.IsType<List<int>>(contexto.Result.Model));
    }

    [Fact]
    public async Task ListaDeActores_ConClavesEnMinuscula_Enlaza()
    {
        // El cliente (React/Angular) envía las claves en camelCase.
        var json = """[{"id":5,"personaje":"Neo"},{"id":6,"personaje":"Trinity"}]""";

        var contexto = await EnlazarAsync(typeof(List<ActorPeliculaCreacionDTO>), json);

        Assert.True(contexto.Result.IsModelSet);
        var actores = Assert.IsType<List<ActorPeliculaCreacionDTO>>(contexto.Result.Model);
        Assert.Equal(2, actores.Count);
        Assert.Equal(5, actores[0].Id);
        Assert.Equal("Neo", actores[0].Personaje);
        Assert.Equal("Trinity", actores[1].Personaje);
    }

    [Fact]
    public async Task SinValor_NoEnlazaNiAgregaErrores()
    {
        var contexto = await EnlazarAsync(typeof(List<int>), null);

        Assert.False(contexto.Result.IsModelSet);
        Assert.True(contexto.ModelState.IsValid);
    }

    [Fact]
    public async Task JsonMalFormado_AgregaErrorAlModelState()
    {
        var contexto = await EnlazarAsync(typeof(List<int>), "esto-no-es-json");

        Assert.False(contexto.Result.IsModelSet);
        Assert.False(contexto.ModelState.IsValid);
        Assert.True(contexto.ModelState.ContainsKey(NombreCampo));
    }

    [Fact]
    public async Task TipoIncorrecto_AgregaErrorAlModelState()
    {
        // Se esperaba List<int> pero llegan cadenas.
        var contexto = await EnlazarAsync(typeof(List<int>), """["a","b"]""");

        Assert.False(contexto.Result.IsModelSet);
        Assert.False(contexto.ModelState.IsValid);
    }

    [Fact]
    public async Task ActorSinPersonaje_AgregaErrorAlModelState()
    {
        // "Personaje" es un miembro required: si falta, el JSON no es válido para el DTO.
        var contexto = await EnlazarAsync(typeof(List<ActorPeliculaCreacionDTO>), """[{"id":1}]""");

        Assert.False(contexto.Result.IsModelSet);
        Assert.False(contexto.ModelState.IsValid);
    }
}
