using Auditorias.Dtos;
using Auditorias.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Auditorias.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuditsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AuditsController(AppDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        [Route("Register")]
        public async Task<IActionResult> CreateAudit([FromBody] AuditRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var audit = new Audits
            {
                Responsible = request.Responsible,
                Area = request.Area,
                Date = DateTime.Now,
                Description = request.Description,
                IdForm = request.IdForm,
            };

            _context.Audits.Add(audit);
            await _context.SaveChangesAsync();

            foreach (var answer in request.Answers)
            {
                _context.Answers.Add(new Answers
                {
                    IdAudit = audit.Id,
                    IdQuestion = answer.IdQuestion,
                    Score = answer.score,
                });
            }

            await _context.SaveChangesAsync();
            return Ok(new { idAudit = audit.Id });
        }
    }
}