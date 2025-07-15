using System.ComponentModel.DataAnnotations;

namespace Auditorias.Dtos
{
    public class AnswerRequest
    {
        [Range(1, 22)]
        public int IdQuestion { get; set; }

        [Range(1, 5)]
        public int score { get; set; }
    }
}