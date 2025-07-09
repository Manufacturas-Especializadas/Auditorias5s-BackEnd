using Auditorias.Dtos;
using Auditorias.Models;
using Auditorias.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Auditorias.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuditsController : ControllerBase
    {
        private readonly string _container = "auditorias5s";
        private readonly AppDbContext _context;
        private readonly AzureStorageServices _azureStorageServices;

        public AuditsController(AppDbContext context, AzureStorageServices azureStorageServices)
        {
            _context = context;
            _azureStorageServices = azureStorageServices;
        }

        [HttpPost]
        [Route("Register")]
        public async Task<IActionResult> CreateAudit([FromForm] AuditRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            string photoUrl = null;

            if(request.Photo != null && request.Photo.Length > 0)
            {
                try
                {
                    photoUrl = await _azureStorageServices.StoragePhotos(_container, request.Photo);
                }
                catch (Exception ex)
                {
                    return BadRequest(ex.Message);
                }
            }

            var audit = new Audits
            {
                Responsible = request.Responsible,
                Area = request.Area,
                Date = DateTime.Now,
                Description = request.Description,
                IdForm = request.IdForm,
                PhotoUrl = photoUrl,
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