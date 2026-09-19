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
	[Route("api/actores")]
	[ApiController]
	[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "esadmin")]
	public class ActoresController : CustomBaseController
	{
		private readonly ApplicationDBContext _dbContext;
		private readonly IMapper _mapper;
		private readonly IOutputCacheStore _outputCacheStore;
		private readonly IAlmacenadorArchivos _almacenador;		
		private const string cacheTag = "actores";
		private readonly string contenedor = "actores";

		public ActoresController(ApplicationDBContext dbContext, IMapper mapper, 
			IOutputCacheStore outputCacheStore, IAlmacenadorArchivos almacenador) :base(dbContext,mapper,outputCacheStore,cacheTag)
		{
			_dbContext = dbContext;
			_mapper = mapper;
			_outputCacheStore = outputCacheStore;
			_almacenador = almacenador;
		}

		[HttpGet]
		[OutputCache(Tags = [cacheTag])]
		public async Task<List<ActorDTO>> Get([FromQuery] PaginacionDTO paginacion) {
			return await Get<Actor, ActorDTO>(paginacion, ordenarPor: g => g.Nombre);
		}

		[HttpGet("{id:int}", Name = "ObtenerActorPorId")]
		[OutputCache(Tags = [cacheTag])]
		public async Task<ActionResult<ActorDTO>> Get(int id) {
			return await Get<Actor, ActorDTO>(id);
		}

		[HttpGet("{nombre}")]
		public async Task<ActionResult<List<PeliculaActorDTO>>> Get (string nombre)
		{
			return await _dbContext.Actores.Where(a => a.Nombre.Contains(nombre))
				.ProjectTo<PeliculaActorDTO>(_mapper.ConfigurationProvider).ToListAsync();
		}

		[HttpPost]
		public async Task<IActionResult> Post([FromForm] ActorCreacionDTO actorDTO)
		{
			var actor = _mapper.Map<Actor>(actorDTO);

			if (actorDTO.Foto is not null)
			{
				var url = await _almacenador.Almacenar(contenedor, actorDTO.Foto);
				actor.Foto = url;
			}

			_dbContext.Add(actor);
			await _dbContext.SaveChangesAsync();
			await _outputCacheStore.EvictByTagAsync(cacheTag, default);

			return CreatedAtRoute("ObtenerActorPorId", new { id = actor.Id }, actor);
		}

		[HttpPut("{id:int}")]
		public async Task<IActionResult> Put(int id, [FromForm] ActorCreacionDTO actorDTO)
		{
			var actor = await _dbContext.Actores.FirstOrDefaultAsync(a => a.Id == id);
			
			if (actor is null)
			{
				return NotFound();
			}

			actor = _mapper.Map(actorDTO, actor);

			if(actorDTO.Foto is not null)
			{
				actor.Foto = await _almacenador.Editar(actor.Foto, contenedor, actorDTO.Foto);
			}

			await _dbContext.SaveChangesAsync();
			await _outputCacheStore.EvictByTagAsync(cacheTag, default);

			return NoContent();
		}

		[HttpDelete("{id:int}")]
		public async Task<IActionResult> Delete(int id) {
			return await Delete<Actor>(id);
		}
	}
}
