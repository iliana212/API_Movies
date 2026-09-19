using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PeliculasAPI.DTOs;
using PeliculasAPI.Utilidades;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace PeliculasAPI.Controllers
{
	[Route("api/usuarios")]
	[ApiController]
	[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "esadmin")]
	public class UsuariosController: ControllerBase
	{
		private readonly UserManager<IdentityUser> _userManager;
		private readonly SignInManager<IdentityUser> _signInManager;
		private readonly IConfiguration _configuration;
		private readonly ApplicationDBContext _context;
		private readonly IMapper _mapper;

		public UsuariosController(UserManager<IdentityUser> userManager, SignInManager<IdentityUser> signInManager, 
			IConfiguration configuration, ApplicationDBContext context, IMapper mapper)
		{
			_userManager = userManager;
			_signInManager = signInManager;
			_configuration = configuration;
			_context = context;
			_mapper = mapper;
		}

		[HttpGet("ListadoUsuarios")]
		public async Task<ActionResult<List<UsuarioDTO>>> ListadoUsurios([FromQuery] PaginacionDTO paginacion)
		{
			var queryable = _context.Users.AsQueryable();
			await HttpContext.InsertarParametrosPaginacionEnCabecera(queryable);
			var usuarios = await queryable.ProjectTo<UsuarioDTO>(_mapper.ConfigurationProvider)
				.OrderBy(x => x.Email).Paginar(paginacion).ToListAsync();

			return usuarios;
		}

		[HttpPost("registrar")]
		[AllowAnonymous]
		public async Task<ActionResult<RespuestaAutenticacionDTO>> Registrar(CredencialesUsuarioDTO credencialesDTO)
		{
			var usuario = new IdentityUser
			{
				Email = credencialesDTO.Email,
				UserName = credencialesDTO.Email
			};

			var resultado = await _userManager.CreateAsync(usuario, credencialesDTO.Password);

			if (resultado.Succeeded)
			{
				return await ConstruirToken(usuario);
			}
			else
			{
				return BadRequest(resultado.Errors);
			}
		}

		[HttpPost("login")]
		[AllowAnonymous]
		public async Task<ActionResult<RespuestaAutenticacionDTO>> Login(CredencialesUsuarioDTO credencialesDTO)
		{
			var usuario = await _userManager.FindByEmailAsync(credencialesDTO.Email);
			if (usuario is null)
			{
				var errores = LoginIncorrecto();
				return BadRequest(errores);
			}

			var resultado = await _signInManager.CheckPasswordSignInAsync(usuario, credencialesDTO.Password, lockoutOnFailure: false);

			if (resultado.Succeeded)
			{
				return await ConstruirToken(usuario);
			}
			else
			{
				var errores = LoginIncorrecto();
				return BadRequest(errores);
			}
		}

		[HttpPost("HacerAdmin")]
		public async Task<IActionResult> HacerAdmin(EditarClaimDTO claimDTO)
		{
			var usuario = await _userManager.FindByEmailAsync(claimDTO.Email);
			if (usuario is null)
			{
				return NotFound();
			}

			await _userManager.AddClaimAsync(usuario, new Claim("esadmin", "true"));
			return NoContent();
		}

		[HttpPost("RemoverAdmin")]
		public async Task<IActionResult> RemoverAdmin(EditarClaimDTO claimDTO)
		{
			var usuario = await _userManager.FindByEmailAsync(claimDTO.Email);
			if (usuario is null)
			{
				return NotFound();
			}

			await _userManager.RemoveClaimAsync(usuario, new Claim("esadmin", "true"));
			return NoContent();
		}

		private IEnumerable<IdentityError> LoginIncorrecto()
		{
			var identityError = new IdentityError() { Description = "Login incorrecto" };
			var errores = new List<IdentityError> { identityError };
			return errores;
		}

		private async Task<RespuestaAutenticacionDTO> ConstruirToken(IdentityUser identityUser)
		{
			var claims = new List<Claim> {
				new Claim("email", identityUser.Email!),
				new Claim("variable", "valor")
			};

			var claimDB = await _userManager.GetClaimsAsync(identityUser);
			claims.AddRange(claimDB);

			var claves = _configuration.GetSection("Jwt");
			var llave = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(claves["llavejwt"]!));
			var creds = new SigningCredentials(llave, SecurityAlgorithms.HmacSha256);
			var minutos = int.Parse(claves["ExpiraMinutos"] ?? "60");
			var expiration = DateTime.UtcNow.AddMinutes(minutos);

			var tokenSeguridad = new JwtSecurityToken(
				issuer: claves["Issuer"], 
				audience: claves["Audience"], 
				claims: claims, 
				expires: expiration, 
				signingCredentials: creds
			);
			var token = new JwtSecurityTokenHandler().WriteToken(tokenSeguridad);

			return new RespuestaAutenticacionDTO { Token = token, Expiracion = expiration };

		}
	}
}
