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
        [Route("GetListProductionLines")]
        public async Task<IActionResult> GetListProductionLines()
        {
            var list = await _context.ProductionLines
                            .AsNoTracking()
                            .ToListAsync();
            
            if(list == null)
            {
                return NotFound();
            }

            return Ok(list);                              
        }

        [HttpGet]
        [Route("GetListPeripheralArea")]
        public async Task<IActionResult> GetListPeripheralArea()
        {
            var list = await _context.PeripheralArea
                            .AsNoTracking()
                            .ToListAsync();

            if (list == null)
            {
                return NotFound();
            }

            return Ok(list);
        }

        [HttpGet]
        [Route("GetListOffices")]
        public async Task<IActionResult> GetListOffices()
        {
            var list = await _context.Offices
                                .AsNoTracking()
                                .ToListAsync();

            if(list == null)
            {
                return NotFound();
            }

            return Ok(list);
        }

        [HttpGet]
        [Route("GetListAudits")]
        public async Task<IActionResult> GetListAuditsAsync()
        {
            var audits = await _context.Audits
                    .AsNoTracking()
                    .ToListAsync();


            var grouped = audits
                .GroupBy(a => new
                {
                    a.Responsible,
                    Year = a.Date.Year,
                    Week = System.Globalization.CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(
                            a.Date,
                            System.Globalization.CalendarWeekRule.FirstFourDayWeek,
                            DayOfWeek.Monday
                        )
                })
                .Select(g => g.OrderByDescending(a => a.Date).First())
                .OrderByDescending(a => a.Date)
                .ToList();

            return Ok(grouped);
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
                    join o in _context.Offices on a.IdOffices equals o.Id into officeGroup
                    from o in officeGroup.DefaultIfEmpty()
                    join p in _context.PeripheralArea on a.IdPeripheralArea equals p.Id into peripheralGroup
                    from p in peripheralGroup.DefaultIfEmpty()
                    join l in _context.ProductionLines on a.IdProductionLines equals l.Id into prodGroup
                    from l in prodGroup.DefaultIfEmpty()
                    select new
                    {
                        Audit = a,
                        FormName = f.Name,
                        AreaName = l != null ? l.Name :
                                   p != null ? p.Name :
                                   o != null ? o.Name : "Sin área",
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
                    }).ToListAsync();

                if (!auditData.Any())
                {
                    return NotFound("No se encontraron auditorías con los criterios especificados.");
                }

                using var workbook = new XLWorkbook();
                var worksheet = workbook.Worksheets.Add("Auditorias Detalladas");
                var summarySheet = workbook.Worksheets.Add("Resumen");
                const int MaxPhotos = 10;
                string[] headers = { "Auditor", "Fecha", "Área", "Formulario", "Sección", "Pregunta", "Puntuación", "Descripción" };
                for (int i = 0; i < headers.Length; i++)
                {
                    worksheet.Cell(1, i + 1).Value = headers[i];
                    worksheet.Cell(1, i + 1).Style.Font.Bold = true;
                }
                for (int i = 0; i < MaxPhotos; i++)
                {
                    worksheet.Cell(1, headers.Length + i + 1).Value = $"Evidencia {i + 1}";
                    worksheet.Cell(1, headers.Length + i + 1).Style.Font.Bold = true;
                }

                int row = 2;
                var httpClient = new HttpClient();
                var sectionScores = new Dictionary<int, List<decimal>>();

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
                            worksheet.Cell(row, 2).Value = audit.Date.ToString("dd/MM/yyyy");
                            worksheet.Cell(row, 3).Value = data.AreaName;
                            worksheet.Cell(row, 4).Value = data.FormName;
                            worksheet.Cell(row, 5).Value = ans.SectionName;
                            worksheet.Cell(row, 6).Value = ans.QuestionText;
                            worksheet.Cell(row, 7).Value = ans.Score;
                            worksheet.Cell(row, 8).Value = audit.Description;

                            var photoUrls = string.IsNullOrEmpty(audit.PhotoUrl)
                                ? new List<string>()
                                : audit.PhotoUrl.Split(';', StringSplitOptions.RemoveEmptyEntries).ToList();

                            for (int i = 0; i < Math.Min(photoUrls.Count, MaxPhotos); i++)
                            {
                                try
                                {
                                    var bytes = await httpClient.GetByteArrayAsync(photoUrls[i]);
                                    using var stream1 = new MemoryStream(bytes);
                                    var img = worksheet.AddPicture(stream1)
                                        .MoveTo(worksheet.Cell(row, headers.Length + 1 + i));
                                    img.Height = 60;
                                    img.Width = 60;
                                    worksheet.Row(row).Height = 65;
                                }
                                catch
                                {
                                    worksheet.Cell(row, headers.Length + 1 + i).Value = "Error";
                                }
                            }

                            if (!photoUrls.Any())
                                worksheet.Cell(row, headers.Length + 1).Value = "Sin evidencia";

                            row++;
                        }
                    }
                }

                string[] summaryHeaders = { "Auditor", "Fecha", "Área", "Formulario", "1S", "2S", "3S", "4S", "5S", "Total Secciones", "Puntaje Final" };
                for (int i = 0; i < summaryHeaders.Length; i++)
                {
                    var cell = summarySheet.Cell(1, i + 1);
                    cell.Value = summaryHeaders[i];
                    cell.Style.Font.Bold = true;
                    cell.Style.Fill.BackgroundColor = XLColor.LightGray;
                }
                for (int i = 0; i < MaxPhotos; i++)
                {
                    var col = summaryHeaders.Length + i + 1;
                    var cell = summarySheet.Cell(1, col);
                    cell.Value = $"Evidencia {i + 1}";
                    cell.Style.Font.Bold = true;
                    cell.Style.Fill.BackgroundColor = XLColor.LightGray;
                }

                int summaryRow = 2;
                foreach (var data in auditData)
                {
                    var audit = data.Audit;
                    var scores = sectionScores.ContainsKey(audit.Id) ? sectionScores[audit.Id] : new List<decimal>();

                    summarySheet.Cell(summaryRow, 1).Value = audit.Responsible;
                    summarySheet.Cell(summaryRow, 2).Value = audit.Date.ToString("dd/MM/yyyy");
                    summarySheet.Cell(summaryRow, 3).Value = data.AreaName;
                    summarySheet.Cell(summaryRow, 4).Value = data.FormName;

                    for (int i = 0; i < 5; i++)
                    {
                        var cell = summarySheet.Cell(summaryRow, 5 + i);
                        cell.Value = i < scores.Count ? scores[i] : 0;
                        cell.Style.NumberFormat.Format = "0.00";
                    }

                    summarySheet.Cell(summaryRow, 10).Value = scores.Count;

                    var finalScore = scores.Sum() * 0.2m;
                    var finalCell = summarySheet.Cell(summaryRow, 11);
                    finalCell.Value = finalScore;
                    finalCell.Style.Font.Bold = true;
                    finalCell.Style.NumberFormat.Format = "0.00";

                    var resumenUrls = string.IsNullOrEmpty(audit.PhotoUrl)
                        ? new List<string>()
                        : audit.PhotoUrl.Split(';', StringSplitOptions.RemoveEmptyEntries).ToList();

                    for (int i = 0; i < Math.Min(resumenUrls.Count, MaxPhotos); i++)
                    {
                        try
                        {
                            var bytes = await httpClient.GetByteArrayAsync(resumenUrls[i]);
                            using var stream2 = new MemoryStream(bytes);
                            var img = summarySheet.AddPicture(stream2)
                                .MoveTo(summarySheet.Cell(summaryRow, summaryHeaders.Length + 1 + i));
                            img.Height = 40;
                            img.Width = 40;
                            summarySheet.Row(summaryRow).Height = 45;
                        }
                        catch
                        {
                            summarySheet.Cell(summaryRow, summaryHeaders.Length + 1 + i).Value = "Error";
                        }
                    }

                    if (!resumenUrls.Any())
                        summarySheet.Cell(summaryRow, summaryHeaders.Length + 1).Value = "Sin evidencia";

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

            string? photoUrl = null;

            if (request.Photos != null && request.Photos.Count > 10)
            {
                return BadRequest("Se permite un máximo de 10 fotos.");
            }

            var urls = new List<string>();

            if (request.Photos != null && request.Photos.Any())
            {
                foreach (var photo in request.Photos)
                {
                    if (photo.Length > 0)
                    {
                        var url = await _azureStorageServices.StoragePhotos(_container, photo);
                        urls.Add(url);
                    }
                }

                photoUrl = urls.Count > 0 ? string.Join(";", urls) : null;
            }

            var audit = new Audits
            {
                Responsible = request.Responsible,
                Date = DateTime.Now,
                Description = request.Description ?? "",
                IdForm = request.IdForm,
                IdProductionLines = request.IdProductionLines,
                IdPeripheralArea = request.IdPeripheralArea,
                IdOffices = request.IdOffices,
                PhotoUrl = photoUrl
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