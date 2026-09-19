using AutoMapper;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using PeliculasAPI.DTOs;
using PeliculasAPI.Entidades;

namespace PeliculasAPI.Controllers
{
	[Route("api/cines")]
	[ApiController]
	[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "esadmin")]
	public class CinesController : CustomBaseController
	{
		private readonly ApplicationDBContext _context;
		private readonly IMapper _mapper;
		private readonly IOutputCacheStore _cacheStore;
		private const string cacheTag = "cines";

		public CinesController(ApplicationDBContext context, IMapper mapper, IOutputCacheStore cacheStore) : base(context, mapper, cacheStore, cacheTag)
		{
			{
				_context = context;
				_mapper = mapper;
				_cacheStore = cacheStore;
			}
		}

		[HttpGet]
		[OutputCache(Tags = [cacheTag])]
		public async Task<List<CineDTO>> Get([FromQuery] PaginacionDTO paginacion) {
			return await Get<Cine, CineDTO>(paginacion, ordenarPor: o => o.Nombre);
		}

		[HttpGet("{id:int}", Name = "ObtenerCinePorId")]
		[OutputCache(Tags = [cacheTag])]
		public async Task<ActionResult<CineDTO>> Get(int id)
		{
			return await Get<Cine, CineDTO>(id);
		}

		[HttpPost]
		public async Task<IActionResult> Post([FromBody] CineCreacionDTO cineDTO)
		{
			return await Post<CineCreacionDTO, Cine, CineDTO>(cineDTO, "ObtenerCinePorId");
		}

		[HttpPut("{id:int}")]
		public async Task<IActionResult> Put(int id, [FromBody] CineCreacionDTO cineDTO)
		{
			return await Put<CineCreacionDTO, Cine>(id, cineDTO);
		}

		[HttpDelete("{id:int}")]
		public async Task<IActionResult> Delete(int id)
		{
			return await Delete<Cine>(id);
		}


	}
}
