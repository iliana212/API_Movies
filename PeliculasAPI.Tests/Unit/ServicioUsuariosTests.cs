using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using PeliculasAPI.Servicios;

namespace PeliculasAPI.Tests.Unit;

public class ServicioUsuariosTests
{
    // Fixture: UserManager simulado que devuelve (o no) un usuario al buscar por email.
    private static UserManager<IdentityUser> CrearUserManager(IdentityUser? usuarioEncontrado)
    {
        var almacen = Substitute.For<IUserStore<IdentityUser>>();
        var userManager = Substitute.For<UserManager<IdentityUser>>(
            almacen, null, null, null, null, null, null, null, null);
        userManager.FindByEmailAsync(Arg.Any<string>())
            .Returns(Task.FromResult<IdentityUser?>(usuarioEncontrado));
        return userManager;
    }

    // Fixture: HttpContext con un usuario autenticado que tiene los claims indicados.
    private static IHttpContextAccessor CrearAccessor(params Claim[] claims)
    {
        var contexto = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "pruebas"))
        };
        return new HttpContextAccessor { HttpContext = contexto };
    }

    [Fact]
    public async Task ObtenerUsuarioId_ConClaimEmailYUsuarioExistente_DevuelveElId()
    {
        var usuario = new IdentityUser { Id = "id-123", Email = "ana@pruebas.test" };
        var servicio = new ServicioUsuarios(
            CrearAccessor(new Claim("email", "ana@pruebas.test")),
            CrearUserManager(usuario));

        var id = await servicio.ObtenerUsuarioId();

        Assert.Equal("id-123", id);
    }

    [Fact]
    public async Task ObtenerUsuarioId_BuscaAlUsuarioPorElEmailDelClaim()
    {
        var userManager = CrearUserManager(new IdentityUser { Id = "id-1" });
        var servicio = new ServicioUsuarios(
            CrearAccessor(new Claim("email", "ana@pruebas.test")),
            userManager);

        await servicio.ObtenerUsuarioId();

        await userManager.Received(1).FindByEmailAsync("ana@pruebas.test");
    }

    [Fact]
    public async Task ObtenerUsuarioId_SinClaimEmail_Lanza()
    {
        var servicio = new ServicioUsuarios(
            CrearAccessor(new Claim("otro-claim", "valor")),
            CrearUserManager(new IdentityUser { Id = "id-1" }));

        await Assert.ThrowsAnyAsync<Exception>(() => servicio.ObtenerUsuarioId());
    }

    [Fact]
    public async Task ObtenerUsuarioId_ConUsuarioInexistente_Lanza()
    {
        var servicio = new ServicioUsuarios(
            CrearAccessor(new Claim("email", "fantasma@pruebas.test")),
            CrearUserManager(null));

        await Assert.ThrowsAnyAsync<Exception>(() => servicio.ObtenerUsuarioId());
    }
}
