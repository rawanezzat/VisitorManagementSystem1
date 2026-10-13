using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Globalization;
using VisitorManagementSystem.Data;
using VisitorManagementSystem.Resources;
using VisitorManagementSystem.ViewModels;


namespace VisitorManagementSystem.Controllers
{
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IStringLocalizer<SharedResource> _localizer;
        public DashboardController(
     ApplicationDbContext context,
     IStringLocalizer<SharedResource> localizer)
        {
            _context = context;
            _localizer = localizer;
        }



        private static string GetPeriodText(string period, bool isArabic)
        {
            if (!isArabic)
            {
                return period switch
                {
                    "All" => "All Periods",
                    "Today" => "Today",
                    "Week" => "This Week",
                    "Month" => "This Month",
                    "Custom" => "Custom Period",
                    _ => period
                };
            }

            return period switch
            {
                "All" => "كل الفترات",
                "Today" => "اليوم",
                "Week" => "هذا الأسبوع",
                "Month" => "هذا الشهر",
                "Custom" => "فترة مخصصة",
                _ => period
            };
        }


        public IActionResult Index(
            string period = "All",
            DateTime? fromDate = null,
            DateTime? toDate = null,
            string? status = null,
            int? visitTypeId = null,
            string? search = null,
            int page = 1)
        {
            int pageSize = 10;

            // =========================
            // Start Query
            // =========================

            var query = _context.Visits
                .Where(v => !v.IsDeleted)
                .Include(v => v.Visitor)
                .Include(v => v.Receptionist)
                .Include(v => v.VisitType)
                .Include(v => v.Items)
                .AsQueryable();


            // =========================
            // Date Filter
            // =========================

            var today = DateTime.Today;
            var tomorrow1 = today.AddDays(1);
            // =========================
            // Today's Statistics
            // =========================

            var todayVisits = _context.Visits
                .Where(v =>
                    !v.IsDeleted &&
                    v.CheckInTime >= today &&
                    v.CheckInTime < tomorrow1)
                .ToList();

            int totalVisitsToday = todayVisits.Count;

            int currentlyInside = todayVisits
                .Count(v => v.Status == "Inside");

            int checkedOutToday = todayVisits
                .Count(v => v.Status == "Checked Out");

            if (period == "Today")
            {
                var tomorrow = today.AddDays(1);

                query = query.Where(v =>
                    v.CheckInTime >= today &&
                    v.CheckInTime < tomorrow);
            }

            else if (period == "Week")
            {
                var startOfWeek = today.AddDays(
                    -(int)today.DayOfWeek);

                var endOfWeek = startOfWeek.AddDays(7);

                query = query.Where(v =>
                    v.CheckInTime >= startOfWeek &&
                    v.CheckInTime < endOfWeek);
            }

            else if (period == "Month")
            {
                var startOfMonth = new DateTime(
                    today.Year,
                    today.Month,
                    1);

                var startOfNextMonth = startOfMonth.AddMonths(1);

                query = query.Where(v =>
                    v.CheckInTime >= startOfMonth &&
                    v.CheckInTime < startOfNextMonth);
            }

            else if (period == "Custom" &&
                     fromDate.HasValue &&
                     toDate.HasValue)
            {
                var startDate = fromDate.Value.Date;
                var endDate = toDate.Value.Date.AddDays(1);

                query = query.Where(v =>
                    v.CheckInTime >= startDate &&
                    v.CheckInTime < endDate);
            }

            // IMPORTANT:
            // If period == "All"
            // We DON'T add any date filter.
            // So all historical visits remain available.


            // =========================
            // Status Filter
            // =========================

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(v =>
                    v.Status == status);
            }


            // =========================
            // Visit Type Filter
            // =========================

            if (visitTypeId.HasValue)
            {
                query = query.Where(v =>
                    v.VisitTypeId == visitTypeId.Value);
            }


            // =========================
            // Search
            // =========================


            if (!string.IsNullOrWhiteSpace(search))
            {
                string term = search.Trim();

                query = query.Where(v =>
                    (v.Visitor != null &&
                     ((v.Visitor.FullName != null &&
                       v.Visitor.FullName.Contains(term)) ||
                      (v.Visitor.Phone != null &&
                       v.Visitor.Phone.Contains(term))))
                    ||
                    (v.CompanyName != null &&
                     v.CompanyName.Contains(term))
                    ||
                    (v.EmployeeToMeet != null &&
                     v.EmployeeToMeet.Contains(term))
                );
            }


            // =========================
            // Get Data
            // =========================

            var filteredVisits = query
                .OrderByDescending(v => v.CheckInTime)
                .ToList();


            // =========================
            // Remove Duplicate Visitors
            // =========================

            var uniqueVisits = filteredVisits
                .Where(v =>
                    !string.IsNullOrWhiteSpace(v.Visitor?.Phone))
                .GroupBy(v => v.Visitor.Phone)
                .Select(g => g
                    .OrderByDescending(v => v.CheckInTime)
                    .First())
                .Concat(
                    filteredVisits
                        .Where(v =>
                            string.IsNullOrWhiteSpace(v.Visitor?.Phone))
                )
                .OrderByDescending(v => v.CheckInTime)
                .ToList();


            // =========================
            // Pagination
            // =========================

            int totalVisits = uniqueVisits.Count;

            int totalPages = (int)Math.Ceiling(
                totalVisits / (double)pageSize);

            if (totalPages == 0)
                totalPages = 1;

            if (page < 1)
                page = 1;

            if (page > totalPages)
                page = totalPages;

            var visits = uniqueVisits
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();


            // =========================
            // Phones With History
            // =========================

            var phonesWithHistory = _context.Visits
                .Where(v =>
                    !v.IsDeleted &&
                    v.Visitor.Phone != null)
                .Include(v => v.Visitor)
                .AsEnumerable()
                .GroupBy(v => v.Visitor.Phone)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key!)
                .ToList();


            // =========================
            // ViewModel
            // =========================

            var model = new DashboardViewModel
            {
                Visits = visits,

                Period = period,

                FromDate = fromDate,

                ToDate = toDate,

                Status = status,

                VisitTypeId = visitTypeId,

                Search = search,

                CurrentPage = page,

                TotalPages = totalPages,

                VisitTypes = _context.VisitTypes.ToList(),

                Receptionists = _context.Receptionists.ToList(),

                PhonesWithHistory = phonesWithHistory,

                // Dashboard Statistics
                TotalVisitsToday = totalVisitsToday,

                CurrentlyInside = currentlyInside,

                CheckedOutToday = checkedOutToday
            };

            return View(model);
        }
        public IActionResult CheckOut(int id)
        {
            var visit = _context.Visits
                .FirstOrDefault(v => v.Id == id && !v.IsDeleted);

            if (visit == null)
            {
                return NotFound();
            }

            visit.CheckOutTime = DateTime.Now;
            visit.Status = "Checked Out";

            _context.SaveChanges();

            return RedirectToAction("Index");
        }


        public IActionResult Delete(int id)
        {
            var visit = _context.Visits
                .FirstOrDefault(v => v.Id == id && !v.IsDeleted);

            if (visit == null)
            {
                return NotFound();
            }

            visit.IsDeleted = true;

            _context.SaveChanges();

            return RedirectToAction("Index");
        }

        [HttpGet]
        public IActionResult GetVisit(int id)
        {
            var visit = _context.Visits
                .Include(v => v.Visitor)
                .Include(v => v.Receptionist)
                .Include(v => v.VisitType)
                .Include(v => v.Items)
                .FirstOrDefault(v => v.Id == id);

            if (visit == null)
                return NotFound();

            // ==========================================
            // Detect Current Language
            // ==========================================

            bool isArabic =
                CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar";


            // ==========================================
            // Visit Type
            // ==========================================

            string visitType;

            if (isArabic)
            {
                visitType = visit.VisitType?.NameAr
                            ?? visit.VisitType?.NameEn
                            ?? "-";
            }
            else
            {
                visitType = visit.VisitType?.NameEn
                            ?? "-";
            }


            // ==========================================
            // Other Visit Type
            // ==========================================

            string otherVisitType;

            if (isArabic)
            {
                otherVisitType =
                    visit.OtherVisitType ?? "أخرى";
            }
            else
            {
                otherVisitType =
                    visit.OtherVisitType ?? "Other";
            }


            // ==========================================
            // Status
            // ==========================================

            string status;

            if (isArabic)
            {
                status = visit.Status switch
                {
                    "Inside" => "داخل المبنى",
                    "Checked Out" => "تم تسجيل الخروج",
                    _ => visit.Status ?? "-"
                };
            }
            else
            {
                status = visit.Status ?? "-";
            }


            // ==========================================
            // Duration
            // ==========================================

            string duration;

            if (visit.CheckOutTime.HasValue)
            {
                var timeSpan =
                    visit.CheckOutTime.Value - visit.CheckInTime;

                duration = timeSpan.ToString(@"hh\:mm");
            }
            else
            {
                duration = isArabic
                    ? "ما زال بالداخل"
                    : "Still Inside";
            }


            // ==========================================
            // Return JSON
            // ==========================================

            return Json(new
            {
                // Basic
                id = visit.Id,

                // Visitor
                fullName = visit.Visitor?.FullName,
                visitorName = visit.Visitor?.FullName,

                phone = visit.Visitor?.Phone,
                email = visit.Visitor?.Email,

                // Visit
                companyName = visit.CompanyName,
                employeeToMeet = visit.EmployeeToMeet,
                department = visit.Department,

                // IMPORTANT FOR EDIT
                visitTypeId = visit.VisitTypeId,
                visitType = visitType,

                otherVisitType = visit.OtherVisitType,

                // IMPORTANT FOR EDIT
                receptionistId = visit.ReceptionistId,
                receptionist = visit.Receptionist?.Name,

                // IMPORTANT FOR EDIT
                nationalId = visit.Visitor?.NationalId,
                hasId = visit.HasId,

                // View
                status = status,

                checkIn = visit.CheckInTime.ToString("hh:mm tt"),

                checkOut = visit.CheckOutTime.HasValue
                    ? visit.CheckOutTime.Value.ToString("hh:mm tt")
                    : "-",

                duration = duration
            });
        }
        [HttpGet]
        public IActionResult GetTodayVisits()
        {
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);

            var visits = _context.Visits
                .Include(v => v.Visitor)
                .Include(v => v.Receptionist)
                .Include(v => v.VisitType)
                .Where(v =>
                    !v.IsDeleted &&
                    v.CheckInTime >= today &&
                    v.CheckInTime < tomorrow
                )
                .OrderByDescending(v => v.CheckInTime)
                .Select(v => new
                {
                    id = v.Id,

                    visitorName = v.Visitor != null
                        ? v.Visitor.FullName
                        : "-",

                    phone = v.Visitor != null
                        ? v.Visitor.Phone
                        : "-",

                    company = v.CompanyName,

                    employeeToMeet = v.EmployeeToMeet,

                    // IMPORTANT
                    visitType = v.VisitType != null
                        ? v.VisitType.NameEn
                        : v.OtherVisitType,

                    otherVisitType = v.OtherVisitType,

                    receptionist = v.Receptionist != null
                        ? v.Receptionist.Name
                        : "-",

                    status = v.Status,

                    checkIn = v.CheckInTime.ToString("hh:mm tt"),

                    checkOut = v.CheckOutTime.HasValue
                        ? v.CheckOutTime.Value.ToString("hh:mm tt")
                        : "-"
                })
                .ToList();

            return Json(visits);
        }
        [HttpPost]
        public IActionResult Edit(int id, AddVisitViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return RedirectToAction("Index");
            }

            var visit = _context.Visits
                .Include(v => v.Visitor)
                .FirstOrDefault(v => v.Id == id && !v.IsDeleted);

            if (visit == null)
            {
                return NotFound();
            }


            // ==========================================
            // Check Receptionist
            // ==========================================

            var receptionistExists =
                _context.Receptionists
                    .Any(r => r.Id == model.ReceptionistId);

            if (!receptionistExists)
            {
                // Invalid ReceptionistId
                return RedirectToAction("Index");
            }


            // ==========================================
            // Update Visitor
            // ==========================================

            visit.Visitor.FullName =
                model.FullName;

            visit.Visitor.Phone =
                model.Phone;

            visit.Visitor.Email =
                model.Email;


            // ==========================================
            // Update Visit
            // ==========================================

            visit.CompanyName =
                model.CompanyName;

            visit.EmployeeToMeet =
                model.EmployeeToMeet;

            visit.Department =
                model.Department;

            visit.VisitTypeId =
                model.VisitTypeId;

            visit.OtherVisitType =
                model.OtherVisitType;

            visit.ReceptionistId =
                model.ReceptionistId;

            visit.HasId =
                model.HasId;


            // ==========================================
            // National ID
            // ==========================================

            visit.Visitor.NationalId =
                model.HasId
                    ? model.NationalId
                    : null;


            // ==========================================
            // Save
            // ==========================================

            _context.SaveChanges();


            return RedirectToAction("Index");
        }

        public IActionResult Visitors()
        {
            var visitors = _context.Visits
                .Include(v => v.Visitor)
                .Where(v => !v.IsDeleted)
                .AsEnumerable()
                .GroupBy(v => v.Visitor.Phone)
                .Select(g => new VisitorHistoryViewModel
                {
                    VisitorId = g.First().VisitorId,
                    FullName = g.First().Visitor.FullName,
                    Phone = g.Key,
                    CompanyName = g.OrderByDescending(v => v.CheckInTime)
                                   .First().CompanyName,
                    LastVisit = g.Max(v => v.CheckInTime),
                    VisitCount = g.Count()
                })
                .OrderByDescending(v => v.LastVisit)
                .ToList();

            return View(visitors);
        }

        public IActionResult VisitorHistory(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
                return NotFound();

            var visits = _context.Visits
                .Include(v => v.Visitor)
                .Include(v => v.Receptionist)
                .Include(v => v.VisitType)
                .Include(v => v.Items)
                .Where(v =>
                    !v.IsDeleted &&
                    v.Visitor.Phone == phone)
                .OrderByDescending(v => v.CheckInTime)
                .ToList();

            if (!visits.Any())
                return NotFound();

            ViewBag.Phone = phone;

            return View(visits);
        }


        public IActionResult ExportExcel(
            string period = "All",
            DateTime? fromDate = null,
            DateTime? toDate = null,
            string? status = null,
            int? visitTypeId = null,
            string? search = null)
        {
            bool isArabic =
                CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar";

            var query = _context.Visits
                .Include(v => v.Visitor)
                .Include(v => v.VisitType)
                .Include(v => v.Receptionist)
                .AsQueryable();

            var today = DateTime.Today;

            switch (period)
            {
                case "Today":
                    query = query.Where(v =>
                        v.CheckInTime >= today &&
                        v.CheckInTime < today.AddDays(1));
                    break;

                case "Week":
                    var startOfWeek = today.AddDays(-(int)today.DayOfWeek);
                    var endOfWeek = startOfWeek.AddDays(7);

                    query = query.Where(v =>
                        v.CheckInTime >= startOfWeek &&
                        v.CheckInTime < endOfWeek);
                    break;

                case "Month":
                    var startOfMonth = new DateTime(today.Year, today.Month, 1);

                    query = query.Where(v =>
                        v.CheckInTime >= startOfMonth &&
                        v.CheckInTime < startOfMonth.AddMonths(1));
                    break;

                case "Custom":
                    if (fromDate.HasValue)
                    {
                        var start = fromDate.Value.Date;
                        query = query.Where(v => v.CheckInTime >= start);
                    }

                    if (toDate.HasValue)
                    {
                        var end = toDate.Value.Date.AddDays(1);
                        query = query.Where(v => v.CheckInTime < end);
                    }
                    break;
            }

            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(v => v.Status == status);

            if (visitTypeId.HasValue)
                query = query.Where(v => v.VisitTypeId == visitTypeId.Value);


            if (!string.IsNullOrWhiteSpace(search))
            {
                string term = search.Trim();

                query = query.Where(v =>
                    (v.Visitor != null &&
                     ((v.Visitor.FullName != null &&
                       v.Visitor.FullName.Contains(term)) ||
                      (v.Visitor.Phone != null &&
                       v.Visitor.Phone.Contains(term))))
                    ||
                    (v.CompanyName != null &&
                     v.CompanyName.Contains(term))
                    ||
                    (v.EmployeeToMeet != null &&
                     v.EmployeeToMeet.Contains(term))
                );
            }

            var visits = query.OrderByDescending(v => v.CheckInTime).ToList();

            using var workbook = new XLWorkbook();

            var worksheet = workbook.Worksheets.Add(
                isArabic ? "تقرير الزوار" : "Visitors Report");

            string[] headers = isArabic
                ? new[]
                {
            "اسم الزائر", "الهاتف", "الشركة", "الموظف المطلوب",
            "القسم", "نوع الزيارة", "موظف الاستقبال",
            "الحالة", "وقت الدخول", "وقت الخروج"
                }
                : new[]
                {
            "Visitor", "Phone", "Company", "Employee To Meet",
            "Department", "Visit Type", "Receptionist",
            "Status", "Check In", "Check Out"
                };

            for (int i = 0; i < headers.Length; i++)
                worksheet.Cell(1, i + 1).Value = headers[i];

            var header = worksheet.Range(1, 1, 1, headers.Length);

            header.Style.Font.Bold = true;
            header.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E3A8A");
            header.Style.Font.FontColor = XLColor.White;
            header.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            header.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

            int row = 2;

            foreach (var visit in visits)
            {
                worksheet.Cell(row, 1).Value = visit.Visitor?.FullName ?? "";
                worksheet.Cell(row, 2).Value = visit.Visitor?.Phone ?? "";
                worksheet.Cell(row, 3).Value = visit.CompanyName ?? "";
                worksheet.Cell(row, 4).Value = visit.EmployeeToMeet ?? "";
                worksheet.Cell(row, 5).Value = visit.Department ?? "";
                worksheet.Cell(row, 6).Value = (
                    isArabic ? visit.VisitType?.NameAr : visit.VisitType?.NameEn) ?? "";
                worksheet.Cell(row, 7).Value = visit.Receptionist?.Name ?? "";
                worksheet.Cell(row, 8).Value = visit.Status == "Inside"
                    ? (isArabic ? "بالداخل" : "Inside")
                    : (isArabic ? "غادر" : "Checked Out");

                worksheet.Cell(row, 9).Value = visit.CheckInTime;
                worksheet.Cell(row, 9).Style.DateFormat.Format = "yyyy-MM-dd HH:mm";

                if (visit.CheckOutTime.HasValue)
                {
                    worksheet.Cell(row, 10).Value = visit.CheckOutTime.Value;
                    worksheet.Cell(row, 10).Style.DateFormat.Format = "yyyy-MM-dd HH:mm";
                }
                else
                {
                    worksheet.Cell(row, 10).Value =
                        isArabic ? "لا يزال بالداخل" : "Still Inside";
                }

                row++;
            }

            int summaryRow = row + 1;

            worksheet.Cell(summaryRow, 1).Value =
                isArabic ? "إجمالي الزيارات" : "Total Visits";
            worksheet.Cell(summaryRow, 2).Value = visits.Count;

            worksheet.Range(summaryRow, 1, summaryRow, 2).Style.Font.Bold = true;

            worksheet.Columns().AdjustToContents();

            if (isArabic)
                worksheet.RightToLeft = true;

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);

            return File(
                stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                isArabic ? "تقرير_الزوار.xlsx" : "Visitors_Report.xlsx");
        }


        public IActionResult ExportPdf(
    string period = "All",
    DateTime? fromDate = null,
    DateTime? toDate = null,
    string? status = null,
    int? visitTypeId = null,
    string? search = null)
{
    bool isArabic =
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar";

    var query = _context.Visits
        .Include(v => v.Visitor)
        .Include(v => v.VisitType)
        .Include(v => v.Receptionist)
        .AsQueryable();

    var today = DateTime.Today;

    switch (period)
    {
        case "Today":
            query = query.Where(v =>
                v.CheckInTime >= today &&
                v.CheckInTime < today.AddDays(1));
            break;

        case "Week":
            var startOfWeek = today.AddDays(
                -(int)today.DayOfWeek);

            var endOfWeek = startOfWeek.AddDays(7);

            query = query.Where(v =>
                v.CheckInTime >= startOfWeek &&
                v.CheckInTime < endOfWeek);
            break;

        case "Month":
            var startOfMonth =
                new DateTime(today.Year, today.Month, 1);

            query = query.Where(v =>
                v.CheckInTime >= startOfMonth &&
                v.CheckInTime < startOfMonth.AddMonths(1));
            break;

        case "Custom":
            if (fromDate.HasValue)
            {
                var start = fromDate.Value.Date;
                query = query.Where(v => v.CheckInTime >= start);
            }

            if (toDate.HasValue)
            {
                var end = toDate.Value.Date.AddDays(1);
                query = query.Where(v => v.CheckInTime < end);
            }
            break;
    }

    if (!string.IsNullOrWhiteSpace(status))
        query = query.Where(v => v.Status == status);

    if (visitTypeId.HasValue)
        query = query.Where(v =>
            v.VisitTypeId == visitTypeId.Value);


            if (!string.IsNullOrWhiteSpace(search))
            {
                string term = search.Trim();

                query = query.Where(v =>
                    (v.Visitor != null &&
                     ((v.Visitor.FullName != null &&
                       v.Visitor.FullName.Contains(term)) ||
                      (v.Visitor.Phone != null &&
                       v.Visitor.Phone.Contains(term))))
                    ||
                    (v.CompanyName != null &&
                     v.CompanyName.Contains(term))
                    ||
                    (v.EmployeeToMeet != null &&
                     v.EmployeeToMeet.Contains(term))
                );
            }

            var visits = query
        .OrderByDescending(v => v.CheckInTime)
        .ToList();

    string title = isArabic
        ? "تقرير الزوار"
        : "Visitors Report";

    string periodText = GetPeriodText(period, isArabic);

    var pdf = Document.Create(document =>
    {
        document.Page(page =>
        {
            page.Size(PageSizes.A4.Landscape());
            page.Margin(20);
            page.DefaultTextStyle(s => s.FontSize(8));

            page.Header().Column(column =>
            {
                column.Item().AlignCenter()
                    .Text(title).FontSize(18).Bold();

                column.Item().AlignCenter()
                    .Text($"{(isArabic ? "الفترة" : "Period")}: {periodText}")
                    .FontSize(10);

                column.Item().PaddingVertical(8)
                    .LineHorizontal(1)
                    .LineColor(Colors.Grey.Lighten2);
            });

            page.Content().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    for (int i = 0; i < 10; i++)
                        columns.RelativeColumn();
                });

                string[] headers = isArabic
                    ? new[]
                    {
                        "اسم الزائر", "الهاتف", "الشركة",
                        "الموظف المطلوب", "القسم", "نوع الزيارة",
                        "موظف الاستقبال", "الحالة",
                        "وقت الدخول", "وقت الخروج"
                    }
                    : new[]
                    {
                        "Visitor", "Phone", "Company",
                        "Employee To Meet", "Department",
                        "Visit Type", "Receptionist", "Status",
                        "Check In", "Check Out"
                    };

                table.Header(header =>
                {
                    foreach (var heading in headers)
                    {
                        header.Cell()
                            .Background(Colors.Blue.Darken2)
                            .Padding(5)
                            .Text(heading)
                            .FontColor(Colors.White)
                            .Bold();
                    }
                });

                foreach (var visit in visits)
                {
                    var visitor = visit.Visitor;
                    var visitType = visit.VisitType;
                    var receptionist = visit.Receptionist;

                    string[] values =
                    {
                        visitor?.FullName ?? "",
                        visitor?.Phone ?? "",
                        visit.CompanyName ?? "",
                        visit.EmployeeToMeet ?? "",
                        visit.Department ?? "",
                        (isArabic
                            ? visitType?.NameAr
                            : visitType?.NameEn) ?? "",
                        receptionist?.Name ?? "",
                        visit.Status == "Inside"
                            ? (isArabic ? "بالداخل" : "Inside")
                            : (isArabic ? "غادر" : "Checked Out"),
                        visit.CheckInTime.ToString("yyyy-MM-dd HH:mm"),
                        visit.CheckOutTime?.ToString("yyyy-MM-dd HH:mm") ?? "—"
                    };

                    foreach (var value in values)
                    {
                        table.Cell()
                            .BorderBottom(1)
                            .BorderColor(Colors.Grey.Lighten2)
                            .Padding(4)
                            .Text(value);
                    }
                }
            });

            page.Footer().AlignCenter().Text(text =>
            {
                text.Span(isArabic
                    ? "تاريخ التصدير: "
                    : "Exported on: ");

                text.Span(DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
            });
        });
    });

    var pdfBytes = pdf.GeneratePdf();

    return File(
        pdfBytes,
        "application/pdf",
        isArabic ? "تقرير_الزوار.pdf" : "Visitors_Report.pdf");
}

        public IActionResult ExportVisitorHistoryPdf(string phone)
        {
            bool isArabic =
                CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar";

            if (string.IsNullOrWhiteSpace(phone))
                return BadRequest(isArabic ? "رقم الهاتف مطلوب." : "Phone number is required.");

            phone = phone.Trim();

            var visits = _context.Visits
                .Include(v => v.Visitor)
                .Include(v => v.VisitType)
                .Include(v => v.Receptionist)
                .Where(v => v.Visitor != null && v.Visitor.Phone == phone)
                .OrderByDescending(v => v.CheckInTime)
                .ToList();

            var pdf = Document.Create(document =>
            {
                document.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(20);
                    page.DefaultTextStyle(s => s.FontSize(9));

                    page.Header().Column(column =>
                    {
                        column.Item().AlignCenter()
                            .Text(isArabic ? "سجل زيارات الزائر" : "Visitor History")
                            .FontSize(18).Bold();

                        column.Item().AlignCenter()
                            .Text($"{(isArabic ? "رقم الهاتف" : "Phone")}: {phone}")
                            .FontSize(11);
                    });

                    page.Content().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            for (int i = 0; i < 7; i++)
                                columns.RelativeColumn();
                        });

                        string[] headers = isArabic
                            ? new[] { "الشركة", "الموظف المطلوب", "القسم",
                        "نوع الزيارة", "الحالة", "وقت الدخول", "وقت الخروج" }
                            : new[] { "Company", "Employee To Meet", "Department",
                        "Visit Type", "Status", "Check In", "Check Out" };

                        table.Header(header =>
                        {
                            foreach (var heading in headers)
                            {
                                header.Cell().Background(Colors.Blue.Darken2)
                                    .Padding(5).Text(heading)
                                    .FontColor(Colors.White).Bold();
                            }
                        });

                        foreach (var visit in visits)
                        {
                            string[] values =
                            {
                        visit.CompanyName ?? "",
                        visit.EmployeeToMeet ?? "",
                        visit.Department ?? "",
                        (isArabic ? visit.VisitType?.NameAr : visit.VisitType?.NameEn) ?? "",
                        visit.Status == "Inside"
                            ? (isArabic ? "بالداخل" : "Inside")
                            : (isArabic ? "غادر" : "Checked Out"),
                        visit.CheckInTime.ToString("yyyy-MM-dd HH:mm"),
                        visit.CheckOutTime?.ToString("yyyy-MM-dd HH:mm") ?? "—"
                    };

                            foreach (var value in values)
                                table.Cell().BorderBottom(1)
                                    .BorderColor(Colors.Grey.Lighten2)
                                    .Padding(5).Text(value);
                        }
                    });

                    page.Footer().AlignCenter().Text(text =>
                    {
                        text.Span(isArabic ? "عدد الزيارات: " : "Total visits: ");
                        text.Span(visits.Count.ToString());
                    });
                });
            });

            return File(
                pdf.GeneratePdf(),
                "application/pdf",
                isArabic ? "سجل_زيارات_الزائر.pdf" : "Visitor_History.pdf");
        }

        public IActionResult ExportVisitorHistoryExcel(string phone)
        {
            bool isArabic =
                CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar";

            if (string.IsNullOrWhiteSpace(phone))
                return BadRequest(isArabic ? "رقم الهاتف مطلوب." : "Phone number is required.");

            phone = phone.Trim();

            var visits = _context.Visits
                .Include(v => v.Visitor)
                .Include(v => v.VisitType)
                .Include(v => v.Receptionist)
                .Where(v => v.Visitor != null && v.Visitor.Phone == phone)
                .OrderByDescending(v => v.CheckInTime)
                .ToList();

            using var workbook = new XLWorkbook();

            var worksheet = workbook.Worksheets.Add(
                isArabic ? "سجل الزيارات" : "Visitor History");

            string[] headers = isArabic
                ? new[] { "اسم الزائر", "رقم الهاتف", "الشركة", "الموظف المطلوب",
            "القسم", "نوع الزيارة", "موظف الاستقبال", "الحالة",
            "وقت الدخول", "وقت الخروج" }
                : new[] { "Visitor Name", "Phone", "Company", "Employee To Meet",
            "Department", "Visit Type", "Receptionist", "Status",
            "Check In", "Check Out" };

            for (int i = 0; i < headers.Length; i++)
                worksheet.Cell(1, i + 1).Value = headers[i];

            var header = worksheet.Range(1, 1, 1, headers.Length);
            header.Style.Font.Bold = true;
            header.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E3A8A");
            header.Style.Font.FontColor = XLColor.White;
            header.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            int row = 2;

            foreach (var visit in visits)
            {
                worksheet.Cell(row, 1).Value = visit.Visitor?.FullName ?? "";
                worksheet.Cell(row, 2).Value = visit.Visitor?.Phone ?? "";
                worksheet.Cell(row, 3).Value = visit.CompanyName ?? "";
                worksheet.Cell(row, 4).Value = visit.EmployeeToMeet ?? "";
                worksheet.Cell(row, 5).Value = visit.Department ?? "";
                worksheet.Cell(row, 6).Value =
                    (isArabic ? visit.VisitType?.NameAr : visit.VisitType?.NameEn) ?? "";
                worksheet.Cell(row, 7).Value = visit.Receptionist?.Name ?? "";
                worksheet.Cell(row, 8).Value = visit.Status == "Inside"
                    ? (isArabic ? "بالداخل" : "Inside")
                    : (isArabic ? "غادر" : "Checked Out");

                worksheet.Cell(row, 9).Value = visit.CheckInTime;
                worksheet.Cell(row, 9).Style.DateFormat.Format = "yyyy-MM-dd HH:mm";

                if (visit.CheckOutTime.HasValue)
                {
                    worksheet.Cell(row, 10).Value = visit.CheckOutTime.Value;
                    worksheet.Cell(row, 10).Style.DateFormat.Format = "yyyy-MM-dd HH:mm";
                }
                else
                {
                    worksheet.Cell(row, 10).Value =
                        isArabic ? "لا يزال بالداخل" : "Still Inside";
                }

                row++;
            }

            int summaryRow = row + 1;
            worksheet.Cell(summaryRow, 1).Value =
                isArabic ? "إجمالي الزيارات" : "Total Visits";
            worksheet.Cell(summaryRow, 2).Value = visits.Count;
            worksheet.Range(summaryRow, 1, summaryRow, 2).Style.Font.Bold = true;

            worksheet.Columns().AdjustToContents();

            if (isArabic)
                worksheet.RightToLeft = true;

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);

            return File(
                stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                isArabic ? "سجل_زيارات_الزائر.xlsx" : "Visitor_History.xlsx");
        }



    }







}