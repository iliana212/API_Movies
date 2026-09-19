using PeliculasAPI.Validaciones;
using System.ComponentModel.DataAnnotations;

namespace PeliculasAPI.Entidades
{
	public class Genero : IId
	{
		public int Id { get; set; }
		
		[Required(ErrorMessage = "El campo {0} es obligatorio")]
		[StringLength(50)]
		[PrimeraLetraMayuscula]
		public required string Nombre { get; set; }
	}
}
