using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using PeliculasAPI.DTOs;
using PeliculasAPI.Entidades;
using PeliculasAPI.Utilidades;
using System.Linq.Expressions;

namespace PeliculasAPI.Controllers
{
	public abstract class CustomBaseController: ControllerBase
	{
		private readonly ApplicationDBContext _context;
		private readonly IMapper _mapper;
		private readonly IOutputCacheStore _cacheStore;
		private readonly string _cacheTag;

		public CustomBaseController(ApplicationDBContext context, IMapper mapper, IOutputCacheStore cacheStore, string cacheTag)
		{
			_context = context;
			_mapper = mapper;
			_cacheStore = cacheStore;
			_cacheTag = cacheTag;
		}

		protected async Task<List<TDTO>> Get<TEntidad, TDTO>(
			Expression<Func<TEntidad, object>> ordenarPor) where TEntidad : class
		{			 
			return await _context.Set<TEntidad>().OrderBy(ordenarPor)
				.ProjectTo<TDTO>(_mapper.ConfigurationProvider)
				.ToListAsync();
		}

		protected async Task<List<TDTO>> Get<TEntidad, TDTO>(PaginacionDTO paginacion, 
			Expression<Func<TEntidad, object>> ordenarPor) where TEntidad : class
		{
			var queryable = _context.Set<TEntidad>().AsQueryable();
			await HttpContext.InsertarParametrosPaginacionEnCabecera(queryable);
			return await queryable.OrderBy(ordenarPor)
				.Paginar(paginacion)
				.ProjectTo<TDTO>(_mapper.ConfigurationProvider)
				.ToListAsync();
		}

		protected async Task<ActionResult<TDTO>> Get<TEntidad,TDTO>(int id) where TEntidad: class, IId where TDTO : IId
		{
			var entidad = await _context.Set<TEntidad>()
				.ProjectTo<TDTO>(_mapper.ConfigurationProvider)
				.FirstOrDefaultAsync(x => x.Id == id);

			if(entidad is null)
				return NotFound();

			return entidad;

		}

		protected async Task<IActionResult> Post<TCreacionDTO,TEntidad,TDTO>(TCreacionDTO creacionDTO, string nombreRuta) where TEntidad:class, IId
		{
			var entidad = _mapper.Map<TEntidad>(creacionDTO);
			_context.Add(entidad);
			await _context.SaveChangesAsync();
			await _cacheStore.EvictByTagAsync(_cacheTag, default);
			var entidadDTO = _mapper.Map<TDTO>(entidad);
			return CreatedAtRoute(nombreRuta, new { id = entidad.Id }, entidadDTO);
		}

		protected async Task<IActionResult> Put<TCreacionDTO, TEntidad>(int id, TCreacionDTO creacionDTO) where TEntidad:class, IId
		{
			var entidadExiste = await _context.Set<TEntidad>().AnyAsync(x => x.Id == id);

			if (!entidadExiste)
			{
				return NotFound();
			}

			var entidad = _mapper.Map<TEntidad>(creacionDTO);
			entidad.Id = id;
			_context.Update(entidad);
			await _context.SaveChangesAsync();
			await _cacheStore.EvictByTagAsync(_cacheTag, default);

			return NoContent();
		}

		protected async Task<IActionResult> Delete<TEntidad>(int id) where TEntidad:class, IId
		{
			var registrosBorrados = await _context.Set<TEntidad>().Where(x => x.Id == id).ExecuteDeleteAsync();

			if (registrosBorrados == 0)
			{
				return NotFound();
			}

			await _cacheStore.EvictByTagAsync(_cacheTag, default);
			return NoContent();
		}
	}
}
