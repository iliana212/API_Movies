using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using PeliculasAPI.DTOs;
using PeliculasAPI.Entidades;
using PeliculasAPI.Utilidades;
using System.Text;

namespace PeliculasAPI.Controllers
{
	[Route("api/generos")]
	[ApiController]
	[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "esadmin")]
	public class GenerosController : CustomBaseController
	{
		private readonly IOutputCacheStore _outputCacheStore;
		private readonly ApplicationDBContext _dbContext;
		private readonly IMapper _mapper;
		private const string cacheTag = "generos";

		public GenerosController(ApplicationDBContext dBContext,  IMapper mapper, IOutputCacheStore outputCacheStore): base(dBContext, mapper, outputCacheStore, cacheTag)
		{
			_outputCacheStore = outputCacheStore;
			_dbContext = dBContext;
			_mapper = mapper;
		}


		[HttpGet]
		[OutputCache(Tags = ["generos"]/*, PolicyName = nameof(PoliticaCache)*/)]
		public async Task<ActionResult<List<GeneroDTO>>> Get([FromQuery] PaginacionDTO paginacion)
		{
			return await Get<Genero, GeneroDTO>(paginacion, ordenarPor: g => g.Id);
		}

		[HttpGet("todos")]
		[OutputCache(Tags = ["generos"])]
		[AllowAnonymous]
		public async Task<ActionResult<List<GeneroDTO>>> Get()
		{
			return await Get<Genero, GeneroDTO>(ordenarPor: g => g.Id);
		}

		[HttpGet("{id}", Name = "ObtenerGeneroPorId")]
		[OutputCache(Tags = ["generos"])]
		public async Task<ActionResult<GeneroDTO>> Get(int id)
		{
			return await Get<Genero, GeneroDTO>(id);
		}

		[HttpPost]
		public async Task<IActionResult> Post([FromBody] GeneroCreacionDTO generoDTO)
		{
			return await Post<GeneroCreacionDTO, Genero, GeneroDTO>(generoDTO, "ObtenerGeneroPorId");
		}

		[HttpPut("{id}")]
		public async Task<IActionResult> Put(int id, [FromBody] GeneroCreacionDTO generoDTO) 
		{
			return await Put<GeneroCreacionDTO, Genero>(id, generoDTO);
		}


		[HttpDelete("{id:int}")]
		public async Task<IActionResult> Delete(int id) 
		{ 
			return await Delete<Genero>(id);
		}

	}
}
