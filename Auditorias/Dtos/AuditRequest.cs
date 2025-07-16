namespace Auditorias.Dtos
{
    public class AuditRequest
    {
        public string Responsible { get; set; }

        public int? SelectedAreaId { get; set; }

        public string Description { get; set; }

        public int IdForm {  get; set; }

        public int? IdProductionLines { get; set; }

        public int? IdPeripheralArea { get; set; }

        public int? IdOffices { get; set; }

        public List<IFormFile>? Photos { get; set; }

        public string Answers { get; set; }
    }
}