using PeliculasAPI.Entidades;
using System.ComponentModel.DataAnnotations;

namespace PeliculasAPI.DTOs
{
	public class ActorDTO:IId
	{
		public int Id { get; set; }
		public required string Nombre { get; set; }
		public DateTime FechaNacimiento { get; set; }
		public string? Foto { get; set; }
	}
}
