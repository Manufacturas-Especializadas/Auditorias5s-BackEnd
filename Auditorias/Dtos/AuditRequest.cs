namespace Auditorias.Dtos
{
    public class AuditRequest
    {
        public string Responsible { get; set; }

        public string Area { get; set; }

        public string Description { get; set; }

        public int IdForm {  get; set; }

        public IFormFile? Photo { get; set; }

        public string Answers { get; set; }
    }
}