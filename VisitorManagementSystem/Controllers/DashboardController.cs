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
                search = search.Trim();

                query = query.Where(v =>
                    v.Visitor.FullName.Contains(search) ||
                    v.Visitor.Phone.Contains(search) ||
                    v.Visitor.Email.Contains(search) ||
                    v.CompanyName.Contains(search) ||
                    v.EmployeeToMeet.Contains(search) ||
                    v.Department.Contains(search)
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
            var query = _context.Visits
                .Include(v => v.Visitor)
                .Include(v => v.Receptionist)
                .Include(v => v.VisitType)
                .Where(v => !v.IsDeleted)
                .AsQueryable();

            // =========================
            // Date Filter
            // =========================

            var today = DateTime.Today;

            if (period == "Today")
            {
                var tomorrow = today.AddDays(1);

                query = query.Where(v =>
                    v.CheckInTime >= today &&
                    v.CheckInTime < tomorrow);
            }
            else if (period == "Week")
            {
                var startOfWeek =
                    today.AddDays(-(int)today.DayOfWeek);

                var endOfWeek =
                    startOfWeek.AddDays(7);

                query = query.Where(v =>
                    v.CheckInTime >= startOfWeek &&
                    v.CheckInTime < endOfWeek);
            }
            else if (period == "Month")
            {
                var startOfMonth =
                    new DateTime(today.Year, today.Month, 1);

                var startOfNextMonth =
                    startOfMonth.AddMonths(1);

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

            // =========================
            // Status
            // =========================

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(v =>
                    v.Status == status);
            }

            // =========================
            // Visit Type
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
                search = search.Trim();

                query = query.Where(v =>
                    v.Visitor.FullName.Contains(search) ||
                    v.Visitor.Phone.Contains(search) ||
                    v.Visitor.Email.Contains(search) ||
                    v.CompanyName.Contains(search) ||
                    v.EmployeeToMeet.Contains(search) ||
                    v.Department.Contains(search));
            }

            var visits = query
                .OrderByDescending(v => v.CheckInTime)
                .ToList();


            // =========================
            // Create Excel
            // =========================

            using var workbook = new XLWorkbook();

            var worksheet = workbook.Worksheets.Add("Visits");


            // =========================
            // Colors
            // =========================

            var navy = XLColor.FromHtml("#12256B");
            var blue = XLColor.FromHtml("#1E3A8F");
            var lightBlue = XLColor.FromHtml("#EEF1FB");
            var darkText = XLColor.FromHtml("#1F2937");
            var grayText = XLColor.FromHtml("#6B7280");
            var green = XLColor.FromHtml("#1F9D55");
            var gray = XLColor.FromHtml("#8892B0");
            var white = XLColor.White;
            var borderColor = XLColor.FromHtml("#D9DEEA");


            // =========================
            // Title
            // =========================

            worksheet.Range("A1:L1").Merge();

            worksheet.Cell("A1").Value =
                "VISITOR MANAGEMENT SYSTEM";

            worksheet.Cell("A1").Style.Font.Bold = true;
            worksheet.Cell("A1").Style.Font.FontSize = 18;
            worksheet.Cell("A1").Style.Font.FontColor = white;
            worksheet.Cell("A1").Style.Fill.BackgroundColor = navy;
            worksheet.Cell("A1").Style.Alignment.Horizontal =
                XLAlignmentHorizontalValues.Center;
            worksheet.Cell("A1").Style.Alignment.Vertical =
                XLAlignmentVerticalValues.Center;

            worksheet.Row(1).Height = 30;


            // =========================
            // Report Title
            // =========================

            worksheet.Range("A2:L2").Merge();

            worksheet.Cell("A2").Value =
                $"Visits Report - {period}";

            worksheet.Cell("A2").Style.Font.Bold = true;
            worksheet.Cell("A2").Style.Font.FontSize = 14;
            worksheet.Cell("A2").Style.Font.FontColor = blue;
            worksheet.Cell("A2").Style.Alignment.Horizontal =
                XLAlignmentHorizontalValues.Center;

            worksheet.Row(2).Height = 24;


            // =========================
            // Generated Date
            // =========================

            worksheet.Range("A3:L3").Merge();

            worksheet.Cell("A3").Value =
                $"Generated: {DateTime.Now:yyyy-MM-dd hh:mm tt}";

            worksheet.Cell("A3").Style.Font.FontColor = grayText;
            worksheet.Cell("A3").Style.Font.FontSize = 10;
            worksheet.Cell("A3").Style.Alignment.Horizontal =
                XLAlignmentHorizontalValues.Center;


            // =========================
            // Summary
            // =========================

            int totalVisits = visits.Count;

            int currentlyInside = visits.Count(v =>
                v.Status == "Inside");

            int checkedOut = visits.Count(v =>
                v.Status == "Checked Out");


            worksheet.Range("A5:C5").Merge();
            worksheet.Range("E5:G5").Merge();
            worksheet.Range("I5:K5").Merge();

            worksheet.Cell("A5").Value =
                $"Total Visits: {totalVisits}";

            worksheet.Cell("E5").Value =
                $"Currently Inside: {currentlyInside}";

            worksheet.Cell("I5").Value =
                $"Checked Out: {checkedOut}";


            foreach (var cellAddress in new[] { "A5", "E5", "I5" })
            {
                var cell = worksheet.Cell(cellAddress);

                cell.Style.Font.Bold = true;
                cell.Style.Font.FontSize = 12;
                cell.Style.Alignment.Horizontal =
                    XLAlignmentHorizontalValues.Center;
                cell.Style.Alignment.Vertical =
                    XLAlignmentVerticalValues.Center;
            }

            worksheet.Cell("A5").Style.Font.FontColor = navy;
            worksheet.Cell("E5").Style.Font.FontColor = green;
            worksheet.Cell("I5").Style.Font.FontColor = gray;


            // =========================
            // Headers
            // =========================

            int headerRow = 7;

            string[] headers =
            {
        "Visitor Name",
        "Phone",
        "Email",
        "Company",
        "Employee To Meet",
        "Department",
        "Visit Type",
        "Receptionist",
        "Status",
        "Check In",
        "Check Out",
        "Duration"
    };

            for (int i = 0; i < headers.Length; i++)
            {
                var cell = worksheet.Cell(headerRow, i + 1);

                cell.Value = headers[i];

                cell.Style.Font.Bold = true;
                cell.Style.Font.FontColor = white;
                cell.Style.Fill.BackgroundColor = navy;

                cell.Style.Alignment.Horizontal =
                    XLAlignmentHorizontalValues.Center;

                cell.Style.Alignment.Vertical =
                    XLAlignmentVerticalValues.Center;

                cell.Style.Border.BottomBorder =
                    XLBorderStyleValues.Thin;

                cell.Style.Border.BottomBorderColor =
                    borderColor;
            }

            worksheet.Row(headerRow).Height = 25;


            // =========================
            // Data
            // =========================

            int row = 8;

            foreach (var visit in visits)
            {
                worksheet.Cell(row, 1).Value =
                    visit.Visitor?.FullName ?? "";

                worksheet.Cell(row, 2).Value =
                    visit.Visitor?.Phone ?? "";

                worksheet.Cell(row, 3).Value =
                    visit.Visitor?.Email ?? "";

                worksheet.Cell(row, 4).Value =
                    visit.CompanyName ?? "";

                worksheet.Cell(row, 5).Value =
                    visit.EmployeeToMeet ?? "";

                worksheet.Cell(row, 6).Value =
                    visit.Department ?? "";

                worksheet.Cell(row, 7).Value =
                    visit.VisitType?.NameEn == "Other"
                        ? visit.OtherVisitType ?? "Other"
                        : visit.VisitType?.NameEn ?? "";

                worksheet.Cell(row, 8).Value =
                    visit.Receptionist?.Name ?? "";

                worksheet.Cell(row, 9).Value =
                    visit.Status ?? "";

                worksheet.Cell(row, 10).Value =
                    visit.CheckInTime;

                worksheet.Cell(row, 10)
                    .Style.DateFormat.Format =
                    "yyyy-MM-dd hh:mm AM/PM";


                if (visit.CheckOutTime.HasValue)
                {
                    worksheet.Cell(row, 11).Value =
                        visit.CheckOutTime.Value;

                    worksheet.Cell(row, 11)
                        .Style.DateFormat.Format =
                        "yyyy-MM-dd hh:mm AM/PM";

                    var duration =
                        visit.CheckOutTime.Value -
                        visit.CheckInTime;

                    worksheet.Cell(row, 12).Value =
                        $"{duration.Hours}h {duration.Minutes}m";
                }
                else
                {
                    worksheet.Cell(row, 11).Value = "-";
                    worksheet.Cell(row, 12).Value = "Still Inside";
                }


                // =========================
                // Font color
                // =========================

                worksheet.Range(row, 1, row, 12)
                    .Style.Font.FontColor = darkText;


                // =========================
                // Alternate row color
                // =========================

                if (row % 2 == 0)
                {
                    worksheet.Range(row, 1, row, 12)
                        .Style.Fill.BackgroundColor = lightBlue;
                }


                // =========================
                // Status colors
                // =========================

                if (visit.Status == "Inside")
                {
                    worksheet.Cell(row, 9)
                        .Style.Font.FontColor = green;

                    worksheet.Cell(row, 9)
                        .Style.Font.Bold = true;
                }
                else
                {
                    worksheet.Cell(row, 9)
                        .Style.Font.FontColor = gray;

                    worksheet.Cell(row, 9)
                        .Style.Font.Bold = true;
                }


                // Borders

                worksheet.Range(row, 1, row, 12)
                    .Style.Border.BottomBorder =
                    XLBorderStyleValues.Thin;

                worksheet.Range(row, 1, row, 12)
                    .Style.Border.BottomBorderColor =
                    borderColor;


                row++;
            }


            // =========================
            // Alignment
            // =========================

            worksheet.RangeUsed()
                .Style.Alignment.Vertical =
                XLAlignmentVerticalValues.Center;


            worksheet.Range(7, 1, row - 1, 12)
                .Style.Alignment.WrapText = true;


            // =========================
            // Column Widths
            // =========================

            worksheet.Column(1).Width = 24;
            worksheet.Column(2).Width = 16;
            worksheet.Column(3).Width = 28;
            worksheet.Column(4).Width = 22;
            worksheet.Column(5).Width = 22;
            worksheet.Column(6).Width = 18;
            worksheet.Column(7).Width = 18;
            worksheet.Column(8).Width = 20;
            worksheet.Column(9).Width = 16;
            worksheet.Column(10).Width = 22;
            worksheet.Column(11).Width = 22;
            worksheet.Column(12).Width = 18;


            // =========================
            // Freeze Header
            // =========================

            worksheet.SheetView.FreezeRows(7);


            // =========================
            // Print Settings
            // =========================

            worksheet.PageSetup.PageOrientation =
                XLPageOrientation.Landscape;

            worksheet.PageSetup.PaperSize =
                XLPaperSize.A4Paper;

            worksheet.PageSetup.FitToPages(1, 0);


            // =========================
            // Save
            // =========================

            using var stream = new MemoryStream();

            workbook.SaveAs(stream);

            return File(
                stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"Visits_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx"
            );
        }

        public IActionResult ExportPdf(
            string period = "All",
            DateTime? fromDate = null,
            DateTime? toDate = null,
            string? status = null,
            int? visitTypeId = null,
            string? search = null)
        {
            var query = _context.Visits
                .Include(v => v.Visitor)
                .Include(v => v.Receptionist)
                .Include(v => v.VisitType)
                .Where(v => !v.IsDeleted)
                .AsQueryable();


            // =========================
            // Date Filter
            // =========================

            var today = DateTime.Today;

            if (period == "Today")
            {
                var tomorrow = today.AddDays(1);

                query = query.Where(v =>
                    v.CheckInTime >= today &&
                    v.CheckInTime < tomorrow);
            }
            else if (period == "Week")
            {
                var startOfWeek =
                    today.AddDays(-(int)today.DayOfWeek);

                var endOfWeek =
                    startOfWeek.AddDays(7);

                query = query.Where(v =>
                    v.CheckInTime >= startOfWeek &&
                    v.CheckInTime < endOfWeek);
            }
            else if (period == "Month")
            {
                var startOfMonth =
                    new DateTime(today.Year, today.Month, 1);

                var startOfNextMonth =
                    startOfMonth.AddMonths(1);

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


            // =========================
            // Status
            // =========================

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(v =>
                    v.Status == status);
            }


            // =========================
            // Visit Type
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
                search = search.Trim();

                query = query.Where(v =>
                    v.Visitor.FullName.Contains(search) ||
                    v.Visitor.Phone.Contains(search) ||
                    v.Visitor.Email.Contains(search) ||
                    v.CompanyName.Contains(search) ||
                    v.EmployeeToMeet.Contains(search) ||
                    v.Department.Contains(search));
            }


            var visits = query
                .OrderByDescending(v => v.CheckInTime)
                .ToList();


            // =========================
            // Colors
            // =========================

            string navy = "#12256B";
            string blue = "#1E3A8F";
            string lightBlue = "#EEF1FB";
            string darkText = "#1F2937";
            string grayText = "#6B7280";
            string green = "#1F9D55";
            string gray = "#8892B0";
            string white = "#FFFFFF";
            string border = "#D9DEEA";


            // =========================
            // Statistics
            // =========================

            int totalVisits = visits.Count;

            int currentlyInside = visits.Count(v =>
                v.Status == "Inside");

            int checkedOut = visits.Count(v =>
                v.Status == "Checked Out");


            // =========================
            // PDF
            // =========================

            QuestPDF.Settings.License =
                LicenseType.Community;


            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());

                    page.Margin(20);


                    // =========================
                    // Header
                    // =========================

                    page.Header()
                        .Background(navy)
                        .Padding(12)
                        .Column(column =>
                        {
                            column.Item()
                                .AlignCenter()
                                .Text("VISITOR MANAGEMENT SYSTEM")
                                .FontSize(20)
                                .Bold()
                                .FontColor(white);

                            column.Item()
                                .AlignCenter()
                                .Text("Visits Report")
                                .FontSize(13)
                                .FontColor(white);

                            column.Item()
                                .AlignCenter()
                                .Text(
                                    $"Period: {period}   |   Generated: {DateTime.Now:yyyy-MM-dd hh:mm tt}"
                                )
                                .FontSize(9)
                                .FontColor(white);
                        });


                    // =========================
                    // Content
                    // =========================

                    page.Content()
                        .PaddingTop(15)
                        .Column(column =>
                        {

                            // =========================
                            // Summary
                            // =========================

                            column.Item()
                                .Row(row =>
                                {

                                    // Total
                                    row.RelativeItem()
                                        .Background(lightBlue)
                                        .Padding(10)
                                        .Column(c =>
                                        {
                                            c.Item()
                                                .AlignCenter()
                                                .Text("TOTAL VISITS")
                                                .FontSize(10)
                                                .Bold()
                                                .FontColor(navy);

                                            c.Item()
                                                .AlignCenter()
                                                .Text(totalVisits.ToString())
                                                .FontSize(18)
                                                .Bold()
                                                .FontColor(navy);
                                        });


                                    row.ConstantItem(10);


                                    // Inside
                                    row.RelativeItem()
                                        .Background("#EAF7EF")
                                        .Padding(10)
                                        .Column(c =>
                                        {
                                            c.Item()
                                                .AlignCenter()
                                                .Text("CURRENTLY INSIDE")
                                                .FontSize(10)
                                                .Bold()
                                                .FontColor(green);

                                            c.Item()
                                                .AlignCenter()
                                                .Text(currentlyInside.ToString())
                                                .FontSize(18)
                                                .Bold()
                                                .FontColor(green);
                                        });


                                    row.ConstantItem(10);


                                    // Checked Out
                                    row.RelativeItem()
                                        .Background("#F0F1F5")
                                        .Padding(10)
                                        .Column(c =>
                                        {
                                            c.Item()
                                                .AlignCenter()
                                                .Text("CHECKED OUT")
                                                .FontSize(10)
                                                .Bold()
                                                .FontColor(gray);

                                            c.Item()
                                                .AlignCenter()
                                                .Text(checkedOut.ToString())
                                                .FontSize(18)
                                                .Bold()
                                                .FontColor(gray);
                                        });

                                });


                            column.Item()
                                .PaddingTop(15);


                            // =========================
                            // Table
                            // =========================

                            column.Item()
                                .Table(table =>
                                {

                                    table.ColumnsDefinition(columns =>
                                    {
                                        columns.RelativeColumn(2.2f);
                                        columns.RelativeColumn(1.5f);
                                        columns.RelativeColumn(2.3f);
                                        columns.RelativeColumn(1.7f);
                                        columns.RelativeColumn(1.6f);
                                        columns.RelativeColumn(1.6f);
                                        columns.RelativeColumn(1.5f);
                                        columns.RelativeColumn(1.7f);
                                    });


                                    // =========================
                                    // Table Header
                                    // =========================

                                    table.Header(header =>
                                    {

                                        header.Cell()
                                            .Background(navy)
                                            .Padding(6)
                                            .Text("Visitor")
                                            .Bold()
                                            .FontColor(white)
                                            .FontSize(9);

                                        header.Cell()
                                            .Background(navy)
                                            .Padding(6)
                                            .Text("Phone")
                                            .Bold()
                                            .FontColor(white)
                                            .FontSize(9);

                                        header.Cell()
                                            .Background(navy)
                                            .Padding(6)
                                            .Text("Email")
                                            .Bold()
                                            .FontColor(white)
                                            .FontSize(9);

                                        header.Cell()
                                            .Background(navy)
                                            .Padding(6)
                                            .Text("Company")
                                            .Bold()
                                            .FontColor(white)
                                            .FontSize(9);

                                        header.Cell()
                                            .Background(navy)
                                            .Padding(6)
                                            .Text("Visit Type")
                                            .Bold()
                                            .FontColor(white)
                                            .FontSize(9);

                                        header.Cell()
                                            .Background(navy)
                                            .Padding(6)
                                            .Text("Receptionist")
                                            .Bold()
                                            .FontColor(white)
                                            .FontSize(9);

                                        header.Cell()
                                            .Background(navy)
                                            .Padding(6)
                                            .Text("Status")
                                            .Bold()
                                            .FontColor(white)
                                            .FontSize(9);

                                        header.Cell()
                                            .Background(navy)
                                            .Padding(6)
                                            .Text("Check In")
                                            .Bold()
                                            .FontColor(white)
                                            .FontSize(9);

                                    });


                                    // =========================
                                    // Rows
                                    // =========================

                                    int index = 0;

                                    foreach (var visit in visits)
                                    {
                                        bool alternate =
                                            index % 2 == 1;

                                        string rowBackground =
                                            alternate
                                                ? lightBlue
                                                : white;


                                        // Visitor
                                        table.Cell()
                                            .Background(rowBackground)
                                            .BorderBottom(1)
                                            .BorderColor(border)
                                            .Padding(6)
                                            .Text(
                                                visit.Visitor?.FullName ?? "-"
                                            )
                                            .FontColor(darkText)
                                            .FontSize(8);


                                        // Phone
                                        table.Cell()
                                            .Background(rowBackground)
                                            .BorderBottom(1)
                                            .BorderColor(border)
                                            .Padding(6)
                                            .Text(
                                                visit.Visitor?.Phone ?? "-"
                                            )
                                            .FontColor(darkText)
                                            .FontSize(8);


                                        // Email
                                        table.Cell()
                                            .Background(rowBackground)
                                            .BorderBottom(1)
                                            .BorderColor(border)
                                            .Padding(6)
                                            .Text(
                                                visit.Visitor?.Email ?? "-"
                                            )
                                            .FontColor(darkText)
                                            .FontSize(8);


                                        // Company
                                        table.Cell()
                                            .Background(rowBackground)
                                            .BorderBottom(1)
                                            .BorderColor(border)
                                            .Padding(6)
                                            .Text(
                                                visit.CompanyName ?? "-"
                                            )
                                            .FontColor(darkText)
                                            .FontSize(8);


                                        // Visit Type
                                        string visitType =
                                            visit.VisitType?.NameEn == "Other"
                                                ? visit.OtherVisitType ?? "Other"
                                                : visit.VisitType?.NameEn ?? "-";

                                        table.Cell()
                                            .Background(rowBackground)
                                            .BorderBottom(1)
                                            .BorderColor(border)
                                            .Padding(6)
                                            .Text(visitType)
                                            .FontColor(darkText)
                                            .FontSize(8);


                                        // Receptionist
                                        table.Cell()
                                            .Background(rowBackground)
                                            .BorderBottom(1)
                                            .BorderColor(border)
                                            .Padding(6)
                                            .Text(
                                                visit.Receptionist?.Name ?? "-"
                                            )
                                            .FontColor(darkText)
                                            .FontSize(8);


                                        // Status
                                        var statusCell =
                                            table.Cell()
                                                .Background(rowBackground)
                                                .BorderBottom(1)
                                                .BorderColor(border)
                                                .Padding(6);

                                        statusCell.Text(text =>
                                        {
                                            text.Span(
                                                visit.Status ?? "-"
                                            )
                                            .Bold()
                                            .FontSize(8)
                                            .FontColor(
                                                visit.Status == "Inside"
                                                    ? green
                                                    : gray
                                            );
                                        });


                                        // Check In
                                        table.Cell()
                                            .Background(rowBackground)
                                            .BorderBottom(1)
                                            .BorderColor(border)
                                            .Padding(6)
                                            .Text(
                                                visit.CheckInTime
                                                    .ToString(
                                                        "yyyy-MM-dd hh:mm tt"
                                                    )
                                            )
                                            .FontColor(darkText)
                                            .FontSize(8);


                                        index++;
                                    }

                                });


                            // =========================
                            // Footer
                            // =========================

                            column.Item()
                                .PaddingTop(12)
                                .AlignCenter()
                                .Text(
                                    "Visitor Management System"
                                )
                                .FontSize(8)
                                .FontColor(grayText);

                        });


                    // =========================
                    // Page Footer
                    // =========================

                    page.Footer()
                        .AlignCenter()
                        .Text(text =>
                        {
                            text.Span("Page ")
                                .FontSize(8)
                                .FontColor(grayText);

                            text.CurrentPageNumber()
                                .FontSize(8)
                                .FontColor(grayText);

                            text.Span(" of ")
                                .FontSize(8)
                                .FontColor(grayText);

                            text.TotalPages()
                                .FontSize(8)
                                .FontColor(grayText);
                        });

                });
            });


            var pdfBytes = document.GeneratePdf();


            return File(
                pdfBytes,
                "application/pdf",
                $"Visits_{DateTime.Now:yyyyMMdd_HHmmss}.pdf"
            );
        }

        public IActionResult ExportVisitorHistoryPdf(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
                return NotFound();

            var visits = _context.Visits
                .Include(v => v.Visitor)
                .Include(v => v.Receptionist)
                .Include(v => v.VisitType)
                .Where(v =>
                    !v.IsDeleted &&
                    v.Visitor.Phone == phone)
                .OrderByDescending(v => v.CheckInTime)
                .ToList();

            if (!visits.Any())
                return NotFound();

            QuestPDF.Settings.License =
                LicenseType.Community;

            var visitor = visits.First().Visitor;

            int totalVisits = visits.Count;


            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());

                    page.Margin(25);


                    // =====================================================
                    // HEADER
                    // =====================================================

                    page.Header()
                        .Background("#12256b")
                        .Padding(15)
                        .Row(row =>
                        {
                            row.RelativeItem()
                                .Text("VISITOR HISTORY")
                                .FontSize(20)
                                .Bold()
                                .FontColor("#FFFFFF");

                            row.ConstantItem(120)
                                .AlignRight()
                                .Text(DateTime.Now.ToString("yyyy-MM-dd"))
                                .FontSize(9)
                                .FontColor("#FFFFFF");
                        });


                    // =====================================================
                    // CONTENT
                    // =====================================================

                    page.Content()
                        .PaddingTop(18)
                        .Column(column =>
                        {
                            // =================================================
                            // VISITOR INFORMATION TITLE
                            // =================================================

                            column.Item()
                                .Text("Visitor Information")
                                .FontSize(12)
                                .Bold()
                                .FontColor("#12256b");


                            column.Item()
                                .PaddingTop(8)
                                .Table(table =>
                                {
                                    table.ColumnsDefinition(columns =>
                                    {
                                        columns.RelativeColumn(1);
                                        columns.RelativeColumn(2);

                                        columns.RelativeColumn(1);
                                        columns.RelativeColumn(2);

                                    });


                                    // Name
                                    table.Cell()
                                        .Background("#eef2ff")
                                        .Padding(8)
                                        .Text("Visitor Name")
                                        .Bold()
                                        .FontColor("#12256b");

                                    table.Cell()
                                        .Padding(8)
                                        .Text(visitor?.FullName ?? "-");


                                    // Phone
                                    table.Cell()
                                        .Background("#eef2ff")
                                        .Padding(8)
                                        .Text("Phone")
                                        .Bold()
                                        .FontColor("#12256b");

                                    table.Cell()
                                        .Padding(8)
                                        .Text(visitor?.Phone ?? "-");


                                    // Email
                                    table.Cell()
                                        .Background("#eef2ff")
                                        .Padding(8)
                                        .Text("Email")
                                        .Bold()
                                        .FontColor("#12256b");

                                    table.Cell()
                                        .Padding(8)
                                        .Text(visitor?.Email ?? "-");


                                    // Total Visits
                                    table.Cell()
                                        .Background("#eef2ff")
                                        .Padding(8)
                                        .Text("Total Visits")
                                        .Bold()
                                        .FontColor("#12256b");

                                    table.Cell()
                                        .Padding(8)
                                        .Text(totalVisits.ToString())
                                        .Bold();
                                });


                            // =================================================
                            // VISIT HISTORY TITLE
                            // =================================================

                            column.Item()
                                .PaddingTop(18)
                                .Text("Visit History")
                                .FontSize(12)
                                .Bold()
                                .FontColor("#12256b");


                            // =================================================
                            // VISITS TABLE
                            // =================================================

                            column.Item()
                                .PaddingTop(8)
                                .Table(table =>
                                {
                                    table.ColumnsDefinition(columns =>
                                    {
                                        columns.RelativeColumn(1.3f); // Date
                                        columns.RelativeColumn(1.5f); // Company
                                        columns.RelativeColumn(1.5f); // Employee
                                        columns.RelativeColumn(1.3f); // Department
                                        columns.RelativeColumn(1.4f); // Visit Type
                                        columns.RelativeColumn(1.3f); // Receptionist
                                        columns.RelativeColumn(1.1f); // Status
                                        columns.RelativeColumn(1.3f); // Check In
                                        columns.RelativeColumn(1.3f); // Check Out
                                    });


                                    // =================================================
                                    // TABLE HEADER
                                    // =================================================

                                    table.Header(header =>
                                    {
                                        header.Cell()
                                            .Background("#2b4fb3")
                                            .Padding(6)
                                            .Text("Date")
                                            .Bold()
                                            .FontSize(8)
                                            .FontColor("#FFFFFF");

                                        header.Cell()
                                            .Background("#2b4fb3")
                                            .Padding(6)
                                            .Text("Company")
                                            .Bold()
                                            .FontSize(8)
                                            .FontColor("#FFFFFF");

                                        header.Cell()
                                            .Background("#2b4fb3")
                                            .Padding(6)
                                            .Text("Employee")
                                            .Bold()
                                            .FontSize(8)
                                            .FontColor("#FFFFFF");

                                        header.Cell()
                                            .Background("#2b4fb3")
                                            .Padding(6)
                                            .Text("Department")
                                            .Bold()
                                            .FontSize(8)
                                            .FontColor("#FFFFFF");

                                        header.Cell()
                                            .Background("#2b4fb3")
                                            .Padding(6)
                                            .Text("Visit Type")
                                            .Bold()
                                            .FontSize(8)
                                            .FontColor("#FFFFFF");

                                        header.Cell()
                                            .Background("#2b4fb3")
                                            .Padding(6)
                                            .Text("Receptionist")
                                            .Bold()
                                            .FontSize(8)
                                            .FontColor("#FFFFFF");

                                        header.Cell()
                                            .Background("#2b4fb3")
                                            .Padding(6)
                                            .Text("Status")
                                            .Bold()
                                            .FontSize(8)
                                            .FontColor("#FFFFFF");

                                        header.Cell()
                                            .Background("#2b4fb3")
                                            .Padding(6)
                                            .Text("Check In")
                                            .Bold()
                                            .FontSize(8)
                                            .FontColor("#FFFFFF");

                                        header.Cell()
                                            .Background("#2b4fb3")
                                            .Padding(6)
                                            .Text("Check Out")
                                            .Bold()
                                            .FontSize(8)
                                            .FontColor("#FFFFFF");
                                    });


                                    // =================================================
                                    // TABLE DATA
                                    // =================================================

                                    foreach (var visit in visits)
                                    {
                                        table.Cell()
                                            .BorderBottom(0.5f)
                                            .BorderColor("#e7eaf0")
                                            .Padding(6)
                                            .Text(
                                                visit.CheckInTime
                                                    .ToString("yyyy-MM-dd"))
                                            .FontSize(7.5f);


                                        table.Cell()
                                            .BorderBottom(0.5f)
                                            .BorderColor("#e7eaf0")
                                            .Padding(6)
                                            .Text(
                                                visit.CompanyName ?? "-")
                                            .FontSize(7.5f);


                                        table.Cell()
                                            .BorderBottom(0.5f)
                                            .BorderColor("#e7eaf0")
                                            .Padding(6)
                                            .Text(
                                                visit.EmployeeToMeet ?? "-")
                                            .FontSize(7.5f);


                                        table.Cell()
                                            .BorderBottom(0.5f)
                                            .BorderColor("#e7eaf0")
                                            .Padding(6)
                                            .Text(
                                                visit.Department ?? "-")
                                            .FontSize(7.5f);


                                        var visitType =
                                            visit.VisitType?.NameEn == "Other"
                                                ? visit.OtherVisitType ?? "Other"
                                                : visit.VisitType?.NameEn ?? "-";

                                        table.Cell()
                                            .BorderBottom(0.5f)
                                            .BorderColor("#e7eaf0")
                                            .Padding(6)
                                            .Text(visitType)
                                            .FontSize(7.5f);


                                        table.Cell()
                                            .BorderBottom(0.5f)
                                            .BorderColor("#e7eaf0")
                                            .Padding(6)
                                            .Text(
                                                visit.Receptionist?.Name ?? "-")
                                            .FontSize(7.5f);


                                        table.Cell()
                                            .BorderBottom(0.5f)
                                            .BorderColor("#e7eaf0")
                                            .Padding(6)
                                            .Text(
                                                visit.Status ?? "-")
                                            .FontSize(7.5f);


                                        table.Cell()
                                            .BorderBottom(0.5f)
                                            .BorderColor("#e7eaf0")
                                            .Padding(6)
                                            .Text(
                                                visit.CheckInTime
                                                    .ToString("hh:mm tt"))
                                            .FontSize(7.5f);


                                        table.Cell()
                                            .BorderBottom(0.5f)
                                            .BorderColor("#e7eaf0")
                                            .Padding(6)
                                            .Text(
                                                visit.CheckOutTime.HasValue
                                                    ? visit.CheckOutTime.Value
                                                        .ToString("hh:mm tt")
                                                    : "-")
                                            .FontSize(7.5f);
                                    }
                                });
                        });


                    // =====================================================
                    // FOOTER
                    // =====================================================

                    page.Footer()
                        .AlignCenter()
                        .Text(text =>
                        {
                            text.Span("Visitor Management System")
                                .FontSize(8)
                                .FontColor("#718096");

                            text.Span("  •  ")
                                .FontSize(8)
                                .FontColor("#718096");

                            text.Span($"Total Visits: {totalVisits}")
                                .FontSize(8)
                                .Bold()
                                .FontColor("#12256b");
                        });
                });
            });


            var pdfBytes = document.GeneratePdf();

            return File(
                pdfBytes,
                "application/pdf",
                $"VisitorHistory_{visitor?.FullName}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf"
            );
        }
        public IActionResult ExportVisitorHistoryExcel(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
                return NotFound();

            var visits = _context.Visits
                .Include(v => v.Visitor)
                .Include(v => v.Receptionist)
                .Include(v => v.VisitType)
                .Where(v =>
                    !v.IsDeleted &&
                    v.Visitor.Phone == phone)
                .OrderByDescending(v => v.CheckInTime)
                .ToList();

            if (!visits.Any())
                return NotFound();

            var visitor = visits.First().Visitor;
            int totalVisits = visits.Count;

            using var workbook = new XLWorkbook();

            var worksheet = workbook.Worksheets.Add("Visitor History");

            // =========================================================
            // TITLE
            // =========================================================

            worksheet.Range("A1:J1").Merge();

            worksheet.Cell("A1").Value = "VISITOR HISTORY";

            worksheet.Cell("A1").Style.Font.Bold = true;
            worksheet.Cell("A1").Style.Font.FontSize = 18;
            worksheet.Cell("A1").Style.Font.FontColor =
                XLColor.White;

            worksheet.Cell("A1").Style.Fill.BackgroundColor =
                XLColor.FromHtml("#12256b");

            worksheet.Cell("A1").Style.Alignment.Horizontal =
                XLAlignmentHorizontalValues.Center;

            worksheet.Cell("A1").Style.Alignment.Vertical =
                XLAlignmentVerticalValues.Center;

            worksheet.Row(1).Height = 30;


            // =========================================================
            // VISITOR INFORMATION
            // =========================================================

            worksheet.Range("A3:B3").Merge();
            worksheet.Cell("A3").Value = "Visitor Information";

            worksheet.Cell("A3").Style.Font.Bold = true;
            worksheet.Cell("A3").Style.Font.FontSize = 12;
            worksheet.Cell("A3").Style.Font.FontColor =
                XLColor.White;

            worksheet.Cell("A3").Style.Fill.BackgroundColor =
                XLColor.FromHtml("#1e3a8f");


            // Name
            worksheet.Cell("A4").Value = "Visitor Name";
            worksheet.Cell("B4").Value =
                visitor?.FullName ?? "-";


            // Phone
            worksheet.Cell("A5").Value = "Phone";
            worksheet.Cell("B5").Value =
                visitor?.Phone ?? "-";


            // Email
            worksheet.Cell("A6").Value = "Email";
            worksheet.Cell("B6").Value =
                visitor?.Email ?? "-";


            // Total Visits
            worksheet.Cell("A7").Value = "Total Visits";
            worksheet.Cell("B7").Value = totalVisits;


            // Style visitor information
            var infoLabels = worksheet.Range("A4:A7");

            infoLabels.Style.Font.Bold = true;

            infoLabels.Style.Fill.BackgroundColor =
                XLColor.FromHtml("#eef2ff");

            infoLabels.Style.Font.FontColor =
                XLColor.FromHtml("#12256b");


            var infoValues = worksheet.Range("B4:B7");

            infoValues.Style.Font.Bold = true;

            infoValues.Style.Font.FontColor =
                XLColor.FromHtml("#172033");


            // =========================================================
            // VISIT HISTORY
            // =========================================================

            worksheet.Range("A9:J9").Merge();

            worksheet.Cell("A9").Value = "Visit History";

            worksheet.Cell("A9").Style.Font.Bold = true;
            worksheet.Cell("A9").Style.Font.FontSize = 12;
            worksheet.Cell("A9").Style.Font.FontColor =
                XLColor.White;

            worksheet.Cell("A9").Style.Fill.BackgroundColor =
                XLColor.FromHtml("#1e3a8f");


            // =========================================================
            // TABLE HEADERS
            // =========================================================

            worksheet.Cell("A10").Value = "Date";
            worksheet.Cell("B10").Value = "Company";
            worksheet.Cell("C10").Value = "Employee To Meet";
            worksheet.Cell("D10").Value = "Department";
            worksheet.Cell("E10").Value = "Visit Type";
            worksheet.Cell("F10").Value = "Receptionist";
            worksheet.Cell("G10").Value = "Status";
            worksheet.Cell("H10").Value = "Check In";
            worksheet.Cell("I10").Value = "Check Out";
            worksheet.Cell("J10").Value = "Duration";


            var tableHeader = worksheet.Range("A10:J10");

            tableHeader.Style.Font.Bold = true;

            tableHeader.Style.Font.FontColor =
                XLColor.White;

            tableHeader.Style.Fill.BackgroundColor =
                XLColor.FromHtml("#2b4fb3");

            tableHeader.Style.Alignment.Horizontal =
                XLAlignmentHorizontalValues.Center;

            tableHeader.Style.Alignment.Vertical =
                XLAlignmentVerticalValues.Center;

            tableHeader.Style.Alignment.WrapText = true;


            // =========================================================
            // VISITS
            // =========================================================

            int row = 11;

            foreach (var visit in visits)
            {
                // Date
                worksheet.Cell(row, 1).Value =
                    visit.CheckInTime;

                worksheet.Cell(row, 1)
                    .Style.DateFormat.Format = "yyyy-MM-dd";


                // Company
                worksheet.Cell(row, 2).Value =
                    visit.CompanyName ?? "-";


                // Employee
                worksheet.Cell(row, 3).Value =
                    visit.EmployeeToMeet ?? "-";


                // Department
                worksheet.Cell(row, 4).Value =
                    visit.Department ?? "-";


                // Visit Type
                worksheet.Cell(row, 5).Value =
                    visit.VisitType?.NameEn == "Other"
                        ? visit.OtherVisitType ?? "Other"
                        : visit.VisitType?.NameEn ?? "-";


                // Receptionist
                worksheet.Cell(row, 6).Value =
                    visit.Receptionist?.Name ?? "-";


                // Status
                worksheet.Cell(row, 7).Value =
                    visit.Status ?? "-";


                // Check In
                worksheet.Cell(row, 8).Value =
                    visit.CheckInTime;

                worksheet.Cell(row, 8)
                    .Style.DateFormat.Format = "hh:mm AM/PM";


                // Check Out
                if (visit.CheckOutTime.HasValue)
                {
                    worksheet.Cell(row, 9).Value =
                        visit.CheckOutTime.Value;

                    worksheet.Cell(row, 9)
                        .Style.DateFormat.Format = "hh:mm AM/PM";


                    var duration =
                        visit.CheckOutTime.Value -
                        visit.CheckInTime;

                    worksheet.Cell(row, 10).Value =
                        $"{duration.Hours}h {duration.Minutes}m";
                }
                else
                {
                    worksheet.Cell(row, 9).Value = "-";
                    worksheet.Cell(row, 10).Value =
                        "Still Inside";
                }

                row++;
            }


            // =========================================================
            // TABLE BORDER
            // =========================================================

            var tableRange =
                worksheet.Range(10, 1, row - 1, 10);

            tableRange.Style.Border.OutsideBorder =
                XLBorderStyleValues.Thin;

            tableRange.Style.Border.InsideBorder =
                XLBorderStyleValues.Thin;

            tableRange.Style.Border.OutsideBorderColor =
                XLColor.FromHtml("#d9deea");

            tableRange.Style.Border.InsideBorderColor =
                XLColor.FromHtml("#e7eaf0");


            // =========================================================
            // GENERAL ALIGNMENT
            // =========================================================

            worksheet.Range(10, 1, row - 1, 10)
                .Style.Alignment.Vertical =
                XLAlignmentVerticalValues.Center;


            // =========================================================
            // COLUMN WIDTHS
            // =========================================================

            worksheet.Column(1).Width = 14;
            worksheet.Column(2).Width = 20;
            worksheet.Column(3).Width = 22;
            worksheet.Column(4).Width = 18;
            worksheet.Column(5).Width = 18;
            worksheet.Column(6).Width = 18;
            worksheet.Column(7).Width = 15;
            worksheet.Column(8).Width = 16;
            worksheet.Column(9).Width = 16;
            worksheet.Column(10).Width = 16;


            // Visitor information columns
            worksheet.Column(1).Width = 18;
            worksheet.Column(2).Width = 25;


            // =========================================================
            // FREEZE HEADER
            // =========================================================

            worksheet.SheetView.FreezeRows(10);


            // =========================================================
            // PRINT SETTINGS
            // =========================================================

            worksheet.PageSetup.PageOrientation =
                XLPageOrientation.Landscape;

            worksheet.PageSetup.PaperSize =
                XLPaperSize.A4Paper;

            worksheet.PageSetup.FitToPages(1, 0);


            // =========================================================
            // EXPORT
            // =========================================================

            using var stream = new MemoryStream();

            workbook.SaveAs(stream);

            return File(
                stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"VisitorHistory_{visitor?.FullName}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx"
            );
        }




    }







}