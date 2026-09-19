using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PeliculasAPI.DTOs;
using PeliculasAPI.Entidades;
using PeliculasAPI.Servicios;

namespace PeliculasAPI.Controllers
{
	[ApiController]
	[Route("api/rating")]
	public class RatingsController : ControllerBase
	{
		private readonly ApplicationDBContext _context;
		private readonly IServicioUsuarios _servicioUsuarios;
		
		public RatingsController(ApplicationDBContext context, IServicioUsuarios servicioUsuarios)
		{
			_context = context;
			_servicioUsuarios = servicioUsuarios;
		}

		[HttpPost]
		[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
		public async Task<IActionResult> Post([FromBody] RatingCreacionDTO ratingDTO)
		{
			var usuarioId = await _servicioUsuarios.ObtenerUsuarioId();
			var ratingActual = await _context.RatingsPeliculas.FirstOrDefaultAsync(x => x.PeliculaId == ratingDTO.PeliculaId && x.UsuarioId == usuarioId);
		
			if (ratingActual is null)
			{
				var rating = new Rating() { 
					UsuarioId = usuarioId, 
					PeliculaId = ratingDTO.PeliculaId, 
					Puntuacion = ratingDTO.Puntuacion 
				};
				_context.Add(rating);
			}
			else
			{
				ratingActual.Puntuacion = ratingDTO.Puntuacion;
			}

			await _context.SaveChangesAsync();
			return NoContent();	
		
		}



	}
}
