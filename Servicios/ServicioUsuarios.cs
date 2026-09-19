using Microsoft.AspNetCore.Identity;

namespace PeliculasAPI.Servicios
{
	public class ServicioUsuarios : IServicioUsuarios
	{
		private readonly IHttpContextAccessor _contextAccessor;
		private readonly UserManager<IdentityUser> _userManager;

		public ServicioUsuarios(IHttpContextAccessor contextAccessor, UserManager<IdentityUser> userManager)
		{
			_contextAccessor = contextAccessor;
			_userManager = userManager;
		}

		public async Task<string> ObtenerUsuarioId()
		{
			var email = _contextAccessor.HttpContext!.User.Claims.FirstOrDefault(x => x.Type == "email")!.Value;
			var usuario = await _userManager.FindByEmailAsync(email);
			return usuario!.Id;
		}
	}
}
