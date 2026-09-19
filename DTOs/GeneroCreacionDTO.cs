using PeliculasAPI.Validaciones;
using System.ComponentModel.DataAnnotations;

namespace PeliculasAPI.DTOs
{
	public class GeneroCreacionDTO
	{
		[Required(ErrorMessage = "El campo {0} es obligatorio")]
		[StringLength(50)]
		[PrimeraLetraMayuscula]
		public required string Nombre { get; set; }
	}
}
