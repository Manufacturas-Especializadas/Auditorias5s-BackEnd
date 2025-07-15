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
using Newtonsoft.Json;
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
                var auditorName = filter.AuditorName?.Trim().ToLower();

                var auditData = await (
                        from a in _context.Audits
                        where string.IsNullOrEmpty(filter.AuditorName) ||
                              a.Responsible.ToLower().Contains(filter.AuditorName.Trim().ToLower())
                        join f in _context.Forms on a.IdForm equals f.Id
                        select new
                        {
                            Audit = a,
                            FormName = f.Name,
                            Answers = (
                                from ans in _context.Answers
                                join q in _context.Questions on ans.IdQuestion equals q.Id
                                join s in _context.Sections on q.IdSection equals s.Id
                                where ans.IdAudit == a.Id
                                select new
                                {
                                    ans.Score,
                                    QuestionText = q.Text,
                                    SectionId = s.Id,
                                    SectionName = s.Name
                                }
                            ).ToList()
                        }
                    ).ToListAsync();

                Debug.WriteLine($"Se encontraron {auditData.Count} auditorías.");

                foreach (var d in auditData)
                {
                    Debug.WriteLine($"AuditId: {d.Audit.Id}, Answers: {d.Answers.Count}");
                }

                if (!auditData.Any())
                {
                    return NotFound("No se encontraron auditorías con los criterios especificados.");
                }

                using var workbook = new XLWorkbook();
                var worksheet = workbook.Worksheets.Add("Auditorias Detalladas");
                var summarySheet = workbook.Worksheets.Add("Resumen");

                string[] headers = { "Auditor", "Fecha", "Área", "Formulario", "Sección", "Pregunta", "Puntuación", "Evidencia", "Descripción" };
                for (int i = 0; i < headers.Length; i++)
                {
                    worksheet.Cell(1, i + 1).Value = headers[i];
                    worksheet.Cell(1, i + 1).Style.Font.Bold = true;
                }

                int row = 2;
                var sectionScores = new Dictionary<int, List<decimal>>();
                var httpClient = new HttpClient();

                foreach (var data in auditData)
                {
                    var audit = data.Audit;
                    var answersBySection = data.Answers
                        .GroupBy(a => new { a.SectionId, a.SectionName })
                        .OrderBy(g => g.Key.SectionName);

                    foreach (var sectionGroup in answersBySection)
                    {
                        var sectionAnswers = sectionGroup.ToList();
                        decimal sectionScore = sectionAnswers.Sum(a => a.Score) / sectionAnswers.Count;
                        decimal weightedScore = sectionScore * 0.2m;

                        if (!sectionScores.ContainsKey(audit.Id))
                            sectionScores[audit.Id] = new List<decimal>();

                        sectionScores[audit.Id].Add(weightedScore);

                        foreach (var ans in sectionAnswers)
                        {
                            worksheet.Cell(row, 1).Value = audit.Responsible;
                            worksheet.Cell(row, 2).Value = audit.Date!.Value.ToString("dd/MM/yyyy");
                            worksheet.Cell(row, 3).Value = audit.Area;
                            worksheet.Cell(row, 4).Value = data.FormName;
                            worksheet.Cell(row, 5).Value = ans.SectionName;
                            worksheet.Cell(row, 6).Value = ans.QuestionText;
                            worksheet.Cell(row, 7).Value = ans.Score;
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

                string[] summaryHeaders = { "Auditor", "Fecha", "Área", "Formulario", "1S", "2S", "3S", "4S", "5S", "Total Secciones", "Puntaje Final", "Evidencia" };
                for (int i = 0; i < summaryHeaders.Length; i++)
                {
                    var cell = summarySheet.Cell(1, i + 1);
                    cell.Value = summaryHeaders[i];
                    cell.Style.Font.Bold = true;
                    cell.Style.Fill.BackgroundColor = XLColor.LightGray;
                }

                int summaryRow = 2;
                foreach (var data in auditData)
                {
                    var audit = data.Audit;
                    var scores = sectionScores.ContainsKey(audit.Id) ? sectionScores[audit.Id] : new List<decimal>();

                    summarySheet.Cell(summaryRow, 1).Value = audit.Responsible;
                    summarySheet.Cell(summaryRow, 2).Value = audit.Date!.Value.ToString("dd/MM/yyyy");
                    summarySheet.Cell(summaryRow, 3).Value = audit.Area;
                    summarySheet.Cell(summaryRow, 4).Value = data.FormName;

                    for (int i = 0; i < 5; i++)
                    {
                        var cell = summarySheet.Cell(summaryRow, 5 + i);
                        cell.Value = i < scores.Count ? scores[i] : 0;
                        cell.Style.NumberFormat.Format = "0.00";
                    }

                    decimal totalScore = scores.Sum();
                    decimal finalScore = totalScore * 0.2m;

                    summarySheet.Cell(summaryRow, 10).Value = scores.Count;

                    var finalCell = summarySheet.Cell(summaryRow, 11);
                    finalCell.Value = finalScore;
                    finalCell.Style.Font.Bold = true;
                    finalCell.Style.NumberFormat.Format = "0.00";

                    if (!string.IsNullOrEmpty(audit.PhotoUrl))
                    {
                        try
                        {
                            var imageBytes = await httpClient.GetByteArrayAsync(audit.PhotoUrl);
                            using var imageStream = new MemoryStream(imageBytes);
                            var image = summarySheet.AddPicture(imageStream)
                                .MoveTo(summarySheet.Cell(summaryRow, 12));
                            image.Height = 40;
                            image.Width = 40;
                            summarySheet.Row(summaryRow).Height = 45;
                        }
                        catch
                        {
                            summarySheet.Cell(summaryRow, 12).Value = "Ver evidencia";
                        }
                    }
                    else
                    {
                        summarySheet.Cell(summaryRow, 12).Value = "Sin evidencia";
                    }

                    summaryRow++;
                }

                worksheet.Columns().AdjustToContents();
                summarySheet.Columns().AdjustToContents();
                worksheet.SheetView.Freeze(1, 0);
                summarySheet.SheetView.Freeze(1, 0);

                var stream = new MemoryStream();
                workbook.SaveAs(stream);
                stream.Position = 0;

                var fileName = $"Auditorias_{filter.AuditorName ?? "Todos"}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
                return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error al generar el reporte: {ex.Message}");
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

            if (request.Photo != null && request.Photo.Length > 0)
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

            var answers = JsonConvert.DeserializeObject<List<AnswerDTO>>(request.Answers);

            foreach (var answer in answers)
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