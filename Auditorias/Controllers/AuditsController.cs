using Auditorias.Dtos;
using Auditorias.Models;
using Auditorias.Services;
using ClosedXML.Excel;
using DocumentFormat.OpenXml.Office2010.Excel;
using DocumentFormat.OpenXml.Office2010.Word.DrawingShape;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

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

        [HttpGet]
        [Route("GetListAudits")]
        public async Task<IActionResult> GetListAuditsAsync()
        {
            var list = await _context.Audits.AsNoTracking().ToListAsync();

            if(list == null || list.Count == 0) 
            { 
                return NotFound(); 
            }

            return Ok(list);
        }

        [HttpPost]
        [Route("DownloadExcel")]
        public async Task<IActionResult> ExportToExcel([FromBody] AuditFilterDto filter)
        {
            try
            {
                var query = _context.Audits
                    .Where(a => string.IsNullOrEmpty(filter.AuditorName) ||
                               a.Responsible.ToLower().Contains(filter.AuditorName.ToLower()))
                    .Select(a => new
                    {
                        Audit = a,
                        Answers = _context.Answers
                            .Where(ans => ans.IdAudit == a.Id)
                            .Select(ans => new
                            {
                                ans.Score,
                                Question = _context.Questions
                                    .Where(q => q.Id == ans.IdQuestion)
                                    .Select(q => new
                                    {
                                        q.Text,
                                        Section = _context.Sections
                                            .Where(s => s.Id == q.IdSection)
                                            .Select(s => new { s.Id, s.Name })
                                            .FirstOrDefault()
                                    })
                                    .FirstOrDefault()
                            })
                            .ToList(),
                        Form = _context.Forms
                            .Where(f => f.Id == a.IdForm)
                            .Select(f => new { f.Name })
                            .FirstOrDefault()
                    });

                var auditData = await query.ToListAsync();

                if (!auditData.Any())
                {
                    return NotFound("No se encontraron auditorías con los criterios especificados");
                }

                using (var workbook = new XLWorkbook())
                {
                    var worksheet = workbook.Worksheets.Add("Auditorias Detalladas");
                    var summaryWorksheet = workbook.Worksheets.Add("Resumen");

                    var detailHeaders = new string[] {
                        "Auditor", "Fecha", "Área", "Formulario", "Sección",
                        "Pregunta", "Puntuación", "Evidencia",
                        "Descripción"
                    };

                    for (int i = 0; i < detailHeaders.Length; i++)
                    {
                        worksheet.Cell(1, i + 1).Value = detailHeaders[i];
                        worksheet.Cell(1, i + 1).Style.Font.Bold = true;
                    }

                    int row = 2;
                    var sectionScores = new Dictionary<int, List<decimal>>();
                    var httpClient = new HttpClient();

                    foreach (var data in auditData)
                    {
                        var audit = data.Audit;
                        var answersBySection = data.Answers
                            .Where(a => a.Question?.Section != null)
                            .GroupBy(a => a.Question.Section.Id)
                            .OrderBy(g => g.First().Question.Section.Name);

                        foreach (var sectionGroup in answersBySection)
                        {
                            var section = sectionGroup.First().Question.Section;
                            var sectionAnswers = sectionGroup.ToList();

                            decimal sectionScore = sectionAnswers.Sum(a => a.Score) / sectionAnswers.Count;
                            decimal weightedScore = sectionScore * 0.2m;

                            if (!sectionScores.ContainsKey(audit.Id))
                            {
                                sectionScores[audit.Id] = new List<decimal>();
                            }
                            sectionScores[audit.Id].Add(weightedScore);

                            foreach (var answer in sectionAnswers)
                            {
                                worksheet.Cell(row, 1).Value = audit.Responsible;
                                worksheet.Cell(row, 2).Value = audit.Date!.Value.ToString("dd/MM/yyyy");
                                worksheet.Cell(row, 3).Value = audit.Area;
                                worksheet.Cell(row, 4).Value = data.Form?.Name;
                                worksheet.Cell(row, 5).Value = section.Name;
                                worksheet.Cell(row, 6).Value = answer.Question?.Text;                               
                                worksheet.Cell(row, 8).Value = weightedScore;

                                if (!string.IsNullOrEmpty(audit.PhotoUrl))
                                {
                                    try
                                    {
                                        var imageBytes = await httpClient.GetByteArrayAsync(audit.PhotoUrl);
                                        using var imageStream = new MemoryStream(imageBytes);

                                        var image = worksheet.AddPicture(imageStream)
                                            .MoveTo(worksheet.Cell(row, 9));

                                        image.Height = 60;
                                        image.Width = 60;
                                        worksheet.Row(row).Height = 65;
                                    }
                                    catch
                                    {
                                        worksheet.Cell(row, 9).Value = "Error al cargar";
                                    }
                                }
                                else
                                {
                                    worksheet.Cell(row, 9).Value = "Sin evidencia";
                                }

                                worksheet.Cell(row, 10).Value = audit.Description;
                                row++;
                            }
                        }
                    }

                    var summaryHeaders = new string[] {
                        "Auditor", "Fecha", "Área", "Formulario",
                        "1S", "2S", "3S", "4S", "5S",
                        "Total Secciones", "Puntuación Total", "Evidencia"
                    };

                    for (int i = 0; i < summaryHeaders.Length; i++)
                    {
                        var cell = summaryWorksheet.Cell(1, i + 1);
                        cell.Value = summaryHeaders[i];
                        cell.Style.Font.Bold = true;
                        cell.Style.Fill.BackgroundColor = XLColor.LightGray;
                    }

                    int summaryRow = 2;
                    foreach (var data in auditData)
                    {
                        var audit = data.Audit;
                        var sectionResults = sectionScores.ContainsKey(audit.Id) ?
                            sectionScores[audit.Id] : new List<decimal>();

                        summaryWorksheet.Cell(summaryRow, 1).Value = audit.Responsible;
                        summaryWorksheet.Cell(summaryRow, 2).Value = audit.Date!.Value.ToString("dd/MM/yyyy");
                        summaryWorksheet.Cell(summaryRow, 3).Value = audit.Area;
                        summaryWorksheet.Cell(summaryRow, 4).Value = data.Form?.Name;

                        for (int i = 0; i < 5; i++)
                        {
                            var cell = summaryWorksheet.Cell(summaryRow, 5 + i);
                            cell.Value = i < sectionResults.Count ? sectionResults[i] : 0;
                            cell.Style.NumberFormat.Format = "0.00";
                        }

                        decimal totalScore = sectionResults.Sum();
                        decimal finalScore = totalScore * 0.2m;

                        summaryWorksheet.Cell(summaryRow, 10).Value = sectionResults.Count;

                        var finalScoreCell = summaryWorksheet.Cell(summaryRow, 11);
                        finalScoreCell.Value = finalScore;
                        finalScoreCell.Style.NumberFormat.Format = "0.00";
                        finalScoreCell.Style.Font.Bold = true;

                        if (!string.IsNullOrEmpty(audit.PhotoUrl))
                        {
                            try
                            {
                                var imageBytes = await httpClient.GetByteArrayAsync(audit.PhotoUrl);
                                using var imageStream = new MemoryStream(imageBytes);

                                var image = summaryWorksheet.AddPicture(imageStream)
                                    .MoveTo(summaryWorksheet.Cell(summaryRow, 12));

                                image.Height = 40;
                                image.Width = 40;
                                summaryWorksheet.Row(summaryRow).Height = 45;
                            }
                            catch
                            {
                                summaryWorksheet.Cell(summaryRow, 12).Value = "Ver evidencia";
                            }
                        }
                        else
                        {
                            summaryWorksheet.Cell(summaryRow, 12).Value = "Sin evidencia";
                        }

                        summaryRow++;
                    }

                    worksheet.Columns().AdjustToContents();
                    summaryWorksheet.Columns().AdjustToContents();

                    worksheet.Column(9).Width = 20;
                    summaryWorksheet.Column(12).Width = 15;

                    worksheet.SheetView.Freeze(1, 0);
                    summaryWorksheet.SheetView.Freeze(1, 0);

                    var stream = new MemoryStream();
                    workbook.SaveAs(stream);
                    stream.Position = 0;

                    var fileName = $"Auditorias_{filter.AuditorName ?? "Todos"}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
                    return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error al generar reporte: {ex.Message}");
            }
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