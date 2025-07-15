using Auditorias.Models;

namespace Auditorias.Dtos
{
    public class AuditExportDto
    {
        public Audits Audit { get; set; }
        public List<Answers> Answers { get; set; }
    }
}