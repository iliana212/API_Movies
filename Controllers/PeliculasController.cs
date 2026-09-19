using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using PeliculasAPI.DTOs;
using PeliculasAPI.Entidades;
using PeliculasAPI.Servicios;
using PeliculasAPI.Utilidades;

namespace PeliculasAPI.Controllers
{
	[Route("api/peliculas")]
	[ApiController]
	[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "esadmin")]
	public class PeliculasController : CustomBaseController
	{
		private readonly ApplicationDBContext _context;
		private readonly IMapper _mapper;
		private readonly IOutputCacheStore _cacheStore;
		private readonly IAlmacenadorArchivos _almacenador;
		private const string cacheTag = "peliculas";
		private readonly string contenedor = "peliculas";
		private readonly IServicioUsuarios _servicioUsuarios;

		public PeliculasController(ApplicationDBContext context, IMapper mapper, IOutputCacheStore cacheStore, IAlmacenadorArchivos almacenador, IServicioUsuarios servicioUsuarios) : base(context, mapper, cacheStore, cacheTag)
		{
			_context = context;
			_mapper = mapper;
			_cacheStore = cacheStore;
			_almacenador = almacenador;
			_servicioUsuarios = servicioUsuarios;
		}

		[HttpGet("landing")]
		[OutputCache(Tags = [cacheTag])]
		[AllowAnonymous]
		public async Task<ActionResult<LandingPageDTO>> Get()
		{
			var top = 6;
			var hoy = DateTime.Today;

			var proximosEstrenos = await _context.Peliculas.Where(x => x.FechaLanzamiento > hoy)
				.OrderBy(o => o.FechaLanzamiento).Take(top).ProjectTo<PeliculaDTO>(_mapper.ConfigurationProvider).ToListAsync();

			var enCines = await _context.Peliculas.Where(x => x.PeliculasCines.Select(c => c.PeliculaId).Contains(x.Id))
				.OrderBy(o => o.FechaLanzamiento).Take(top).ProjectTo<PeliculaDTO>(_mapper.ConfigurationProvider).ToListAsync();

			var resultado = new LandingPageDTO
			{
				EnCines = enCines,
				ProximosEstrenos = proximosEstrenos
			};

			return resultado;
		}

		[HttpGet("{id:int}", Name = "ObtenerPeliculaPorId")]
		[AllowAnonymous]
		public async Task<ActionResult<PeliculaDetallesDTO>> Get(int id)
		{
			var pelicula = await _context.Peliculas.ProjectTo<PeliculaDetallesDTO>(_mapper.ConfigurationProvider)
				.FirstOrDefaultAsync(p => p.Id == id);

			if (pelicula is null)
				return NotFound();

			var promedioVoto = 0.0;
			var usuarioVoto = 0;

			if (await _context.RatingsPeliculas.AnyAsync(r => r.PeliculaId == id))
			{
				promedioVoto = await _context.RatingsPeliculas.Where(x => x.PeliculaId == id).AverageAsync(r => r.Puntuacion);

				if (HttpContext.User.Identity!.IsAuthenticated)
				{
					var usuarioId = await _servicioUsuarios.ObtenerUsuarioId();
					var ratingDB = await _context.RatingsPeliculas.FirstOrDefaultAsync(x => x.UsuarioId == usuarioId && x.PeliculaId == id);

					if(ratingDB is not null)
					{
						usuarioVoto = ratingDB.Puntuacion;
					}
				}			
			}

			pelicula.PromedioVoto = promedioVoto;
			pelicula.VotoUsuario = usuarioVoto;
			
			return pelicula;
		}

		[HttpGet("filtrar")]
		[AllowAnonymous]
		public async Task<ActionResult<List<PeliculaDTO>>> Filtrar([FromQuery] PeliculasFiltrarDTO filtroDTO)
		{
			var peliculasQueryable = _context.Peliculas.AsQueryable();

			if (!string.IsNullOrWhiteSpace(filtroDTO.Titulo))
			{
				peliculasQueryable = peliculasQueryable.Where(x => x.Titulo.Contains(filtroDTO.Titulo));
			}

			if (filtroDTO.EnCines)
			{
				peliculasQueryable = peliculasQueryable.Where(x => x.PeliculasCines.Select(p => p.PeliculaId).Contains(x.Id));
			}

			if (filtroDTO.ProximosEstrenos)
			{
				peliculasQueryable = peliculasQueryable.Where(x => x.FechaLanzamiento > DateTime.Today);
			}

			if (filtroDTO.GeneroId != 0)
			{
				peliculasQueryable = peliculasQueryable.Where(x => x.PeliculasGeneros.Select(p => p.GeneroId).Contains(filtroDTO.GeneroId));
			}

			await HttpContext.InsertarParametrosPaginacionEnCabecera(peliculasQueryable);

			var peliculas = await peliculasQueryable
				.Paginar(filtroDTO.Paginacion)
				.ProjectTo<PeliculaDTO>(_mapper.ConfigurationProvider)
				.ToListAsync();

			return peliculas;
		}

		[HttpGet("PostGet")]
		public async Task<ActionResult<PeliculasPostGetDTO>> PostGet()
		{
			var cines = await _context.Cines.ProjectTo<CineDTO>(_mapper.ConfigurationProvider).ToListAsync();
			var generos = await _context.Generos.ProjectTo<GeneroDTO>(_mapper.ConfigurationProvider).ToListAsync();

			return new PeliculasPostGetDTO
			{
				Cines = cines,
				Generos = generos
			};
		}

		[HttpPost]
		public async Task<IActionResult> Post([FromForm] PeliculaCreacionDTO peliculaDTO)
		{
			var pelicula = _mapper.Map<Pelicula>(peliculaDTO);

			if (peliculaDTO.Poster is not null)
			{
				var url = await _almacenador.Almacenar(contenedor, peliculaDTO.Poster);
				pelicula.Poster = url;
			}

			AsignarOrdenActores(pelicula);
			_context.Add(pelicula);
			await _context.SaveChangesAsync();
			await _cacheStore.EvictByTagAsync(cacheTag, default);
			var peliDTO = _mapper.Map<PeliculaDTO>(pelicula);
			return CreatedAtRoute("ObtenerPeliculaPorId", new { id = pelicula.Id }, peliDTO);
		}

		[HttpGet("PutGet/{id:int}")]
		public async Task<ActionResult<PeliculasPutGetDTO>> PutGet(int id)
		{
			var pelicula = await _context.Peliculas.ProjectTo<PeliculaDetallesDTO>(_mapper.ConfigurationProvider)
				.FirstOrDefaultAsync(x => x.Id == id);

			if (pelicula is null)
				return NotFound();

			var generosSeleccionados = pelicula.Generos.Select(x => x.Id).ToList();
			var generosNoSeleccionados = await _context.Generos.Where(x => !generosSeleccionados.Contains(x.Id))
				.ProjectTo<GeneroDTO>(_mapper.ConfigurationProvider).ToListAsync();

			var cinesSeleccionados = pelicula.Cines.Select(x => x.Id).ToList();
			var cinesNoSeleccinados = await _context.Cines.Where(x => !cinesSeleccionados.Contains(x.Id))
				.ProjectTo<CineDTO>(_mapper.ConfigurationProvider).ToListAsync();

			return new PeliculasPutGetDTO
			{
				Pelicula = pelicula,
				GenerosSeleccionados = pelicula.Generos,
				GenerosNoSeleccionados = generosNoSeleccionados,
				CinesSeleccionados = pelicula.Cines,
				CinesNoSeleccionados = cinesNoSeleccinados,
				Actores = pelicula.Actores
			};
		}

		[HttpPut("{id:int}")]
		public async Task<IActionResult> Put(int id, [FromForm] PeliculaCreacionDTO peliculaDTO)
		{
			var pelicula = await _context.Peliculas.Include(p => p.PeliculasGeneros).Include(p => p.PeliculasCines).Include(p => p.PeliculasActores)
				.FirstOrDefaultAsync(x => x.Id == id);

			if (pelicula is null)
				return NotFound();

			pelicula = _mapper.Map(peliculaDTO, pelicula);

			if (peliculaDTO.Poster is not null)
			{
				pelicula.Poster = await _almacenador.Editar(pelicula.Poster, contenedor, peliculaDTO.Poster);
			}

			AsignarOrdenActores(pelicula);

			await _context.SaveChangesAsync();
			await _cacheStore.EvictByTagAsync(cacheTag, default);
			return NoContent();
		}

		[HttpDelete("{id:int}")]
		public async Task<IActionResult> Delete(int id)
		{
			return await Delete<Pelicula>(id);
		}

		private void AsignarOrdenActores(Pelicula pelicula)
		{
			if(pelicula.PeliculasActores is not null)
			{
				for(int i =0;i < pelicula.PeliculasActores.Count; i++)
				{
					pelicula.PeliculasActores[i].Orden = i;
				}
			}
		}
	}
}
