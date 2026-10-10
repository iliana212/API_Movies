using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using PeliculasAPI.DTOs;
using PeliculasAPI.Entidades;
using PeliculasAPI.Utilidades;

namespace PeliculasAPI.Tests.Unit;

public class AutoMapperProfilesTests
{
    private readonly GeometryFactory _geometryFactory = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
    private readonly IMapper _mapper;

    public AutoMapperProfilesTests()
    {
        // Misma configuración que Program.cs.
        var configuracion = new MapperConfiguration(
            cfg => cfg.AddProfile(new AutoMapperProfiles(_geometryFactory)),
            NullLoggerFactory.Instance);
        _mapper = configuracion.CreateMapper();
    }

    private static FormFile ArchivoFalso(string nombre) =>
        new(new MemoryStream(new byte[] { 1, 2, 3 }), 0, 3, "archivo", nombre);

    [Fact]
    public void CineCreacionDTO_ACine_ColocaLongitudEnXYLatitudEnY()
    {
        var dto = new CineCreacionDTO { Nombre = "Cine Centro", Latitud = 21.1236, Longitud = -101.6869 };

        var cine = _mapper.Map<Cine>(dto);

        // En NetTopologySuite X = longitud y Y = latitud; invertirlos es un error clásico.
        Assert.Equal(-101.6869, cine.Ubicacion.X);
        Assert.Equal(21.1236, cine.Ubicacion.Y);
        Assert.Equal(4326, cine.Ubicacion.SRID);
    }

    [Fact]
    public void Cine_ACineDTO_DevuelveLatitudDesdeYLongitudDesdeX()
    {
        var cine = new Cine
        {
            Id = 1,
            Nombre = "Cine Norte",
            Ubicacion = _geometryFactory.CreatePoint(new Coordinate(-99.1332, 19.4326))
        };

        var dto = _mapper.Map<CineDTO>(cine);

        Assert.Equal(19.4326, dto.Latitud);
        Assert.Equal(-99.1332, dto.Longitud);
    }

    [Theory]
    [InlineData(90.0, 180.0)]
    [InlineData(-90.0, -180.0)]
    [InlineData(0.0, 0.0)]
    public void Cine_IdaYVuelta_ConservaCoordenadasEnLosLimites(double latitud, double longitud)
    {
        var entrada = new CineCreacionDTO { Nombre = "Cine Limite", Latitud = latitud, Longitud = longitud };

        var salida = _mapper.Map<CineDTO>(_mapper.Map<Cine>(entrada));

        Assert.Equal(latitud, salida.Latitud);
        Assert.Equal(longitud, salida.Longitud);
    }

    [Fact]
    public void ActorCreacionDTO_AActor_IgnoraLaFoto()
    {
        var dto = new ActorCreacionDTO
        {
            Nombre = "Keanu Reeves",
            FechaNacimiento = new DateTime(1964, 9, 2),
            Foto = ArchivoFalso("foto.jpg")
        };

        var actor = _mapper.Map<Actor>(dto);

        // La URL de la foto la asigna el controlador tras guardar el archivo, no el mapeo.
        Assert.Null(actor.Foto);
        Assert.Equal("Keanu Reeves", actor.Nombre);
        Assert.Equal(new DateTime(1964, 9, 2), actor.FechaNacimiento);
    }

    [Fact]
    public void PeliculaCreacionDTO_APelicula_ConvierteIdsEnRelacionesEIgnoraElPoster()
    {
        var dto = new PeliculaCreacionDTO
        {
            Titulo = "Matrix",
            FechaLanzamiento = new DateTime(1999, 3, 31),
            Poster = ArchivoFalso("poster.jpg"),
            GenerosIds = new List<int> { 1, 2 },
            CinesIds = new List<int> { 3 },
            Actores = new List<ActorPeliculaCreacionDTO> { new() { Id = 7, Personaje = "Neo" } }
        };

        var pelicula = _mapper.Map<Pelicula>(dto);

        Assert.Equal("Matrix", pelicula.Titulo);
        Assert.Null(pelicula.Poster);
        Assert.Equal(new[] { 1, 2 }, pelicula.PeliculasGeneros.Select(g => g.GeneroId));
        Assert.Equal(new[] { 3 }, pelicula.PeliculasCines.Select(c => c.CineId));
        var actor = Assert.Single(pelicula.PeliculasActores);
        Assert.Equal(7, actor.ActorId);
        Assert.Equal("Neo", actor.Personaje);
    }

    [Fact]
    public void PeliculaCreacionDTO_ConListasVacias_GeneraRelacionesVacias()
    {
        var dto = new PeliculaCreacionDTO
        {
            Titulo = "Sin relaciones",
            GenerosIds = new List<int>(),
            CinesIds = new List<int>(),
            Actores = new List<ActorPeliculaCreacionDTO>()
        };

        var pelicula = _mapper.Map<Pelicula>(dto);

        Assert.Empty(pelicula.PeliculasGeneros);
        Assert.Empty(pelicula.PeliculasCines);
        Assert.Empty(pelicula.PeliculasActores);
    }

    [Fact]
    public void Pelicula_APeliculaDetallesDTO_ProyectaGenerosCinesYActores()
    {
        var pelicula = new Pelicula { Id = 10, Titulo = "Matrix", FechaLanzamiento = new DateTime(1999, 3, 31) };
        pelicula.PeliculasGeneros.Add(new PeliculaGenero
        {
            GeneroId = 1,
            Genero = new Genero { Id = 1, Nombre = "Acción" }
        });
        pelicula.PeliculasCines.Add(new PeliculaCine
        {
            CineId = 3,
            Cine = new Cine
            {
                Id = 3,
                Nombre = "Cine Sur",
                Ubicacion = _geometryFactory.CreatePoint(new Coordinate(-101.68, 21.12))
            }
        });
        pelicula.PeliculasActores.Add(new PeliculaActor
        {
            ActorId = 7,
            Personaje = "Neo",
            Actor = new Actor { Id = 7, Nombre = "Keanu Reeves", Foto = "https://almacen.test/keanu.jpg" }
        });

        var dto = _mapper.Map<PeliculaDetallesDTO>(pelicula);

        Assert.Equal(10, dto.Id);
        var genero = Assert.Single(dto.Generos);
        Assert.Equal(1, genero.Id);
        Assert.Equal("Acción", genero.Nombre);

        var cine = Assert.Single(dto.Cines);
        Assert.Equal(3, cine.Id);
        Assert.Equal("Cine Sur", cine.Nombre);
        Assert.Equal(21.12, cine.Latitud);
        Assert.Equal(-101.68, cine.Longitud);

        var actor = Assert.Single(dto.Actores);
        Assert.Equal(7, actor.Id);
        Assert.Equal("Keanu Reeves", actor.Nombre);
        Assert.Equal("Neo", actor.Personaje);
        Assert.Equal("https://almacen.test/keanu.jpg", actor.Foto);
    }

    [Fact]
    public void IdentityUser_AUsuarioDTO_CopiaElEmail()
    {
        var usuario = new IdentityUser { Email = "ana@pruebas.test", UserName = "ana@pruebas.test" };

        var dto = _mapper.Map<UsuarioDTO>(usuario);

        Assert.Equal("ana@pruebas.test", dto.Email);
    }
}
