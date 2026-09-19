using Microsoft.EntityFrameworkCore;

namespace PeliculasAPI.Utilidades
{
	public static class HttpContextExtensions
	{
		public async static Task InsertarParametrosPaginacionEnCabecera<T>(this HttpContext httpContext, IQueryable<T> queryable)
		{
			ArgumentNullException.ThrowIfNull(httpContext);

			double cantidad = await queryable.CountAsync();
			httpContext.Response.Headers.Append("cantidad-total-registros", cantidad.ToString());
		}
	}
}
