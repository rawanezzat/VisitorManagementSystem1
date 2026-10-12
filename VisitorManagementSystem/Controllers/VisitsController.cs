using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VisitorManagementSystem.Data;
using VisitorManagementSystem.Models;
using VisitorManagementSystem.ViewModels;

namespace VisitorManagementSystem.Controllers
{
    public class VisitsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public VisitsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Visits/Create
        [HttpGet]
        public IActionResult Create(int? visitorId)
        {



            ViewBag.Receptionists = _context.Receptionists.ToList();
            ViewBag.VisitTypes = _context.VisitTypes.ToList();

            var model = new AddVisitViewModel();

            if (visitorId.HasValue)
            {
                var visitor = _context.Visitors
                    .FirstOrDefault(v => v.Id == visitorId.Value);

                if (visitor != null)
                {
                    model.VisitorId = visitor.Id;
                    model.FullName = visitor.FullName;
                    model.Phone = visitor.Phone;
                    model.Email = visitor.Email;
                }
            }

            return View(model);
        }

        [HttpGet]
        public IActionResult CheckPhone(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
            {
                return Json(new
                {
                    exists = false,
                    inside = false
                });
            }

            var visitor = _context.Visitors
                .FirstOrDefault(v => v.Phone == phone);

            if (visitor == null)
            {
                return Json(new
                {
                    exists = false,
                    inside = false
                });
            }

            bool alreadyInside = _context.Visits
                .Any(v =>
                    v.VisitorId == visitor.Id &&
                    !v.IsDeleted &&
                    v.Status == "Inside"
                );

            return Json(new
            {
                exists = true,
                inside = alreadyInside
            });
        }

        [HttpPost]
        public IActionResult Create(AddVisitViewModel model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Receptionists = _context.Receptionists.ToList();
                ViewBag.VisitTypes = _context.VisitTypes.ToList();

                return View(model);
            }

            if (!string.IsNullOrWhiteSpace(model.Phone))
            {
                bool alreadyInside = _context.Visits
                    .Include(v => v.Visitor)
                    .Any(v =>
                        !v.IsDeleted &&
                        v.Visitor.Phone == model.Phone &&
                        v.Status == "Inside"
                    );

                if (alreadyInside)
                {
                    ModelState.AddModelError(
                        "Phone",
                        "This visitor is still inside. Please check out first."
                    );

                    ViewBag.Receptionists = _context.Receptionists.ToList();
                    ViewBag.VisitTypes = _context.VisitTypes.ToList();

                    return View(model);
                }
            }

            Visitor visitor;

            // Existing visitor
            if (model.VisitorId.HasValue)
            {
                visitor = _context.Visitors
                    .FirstOrDefault(v => v.Id == model.VisitorId.Value);

                if (visitor == null)
                {
                    return NotFound();
                }

                // Update visitor information if needed
                visitor.FullName = model.FullName;
                visitor.Phone = model.Phone;
                visitor.Email = model.Email;
            }
            else
            {
                // New visitor
                visitor = new Visitor
                {
                    FullName = model.FullName,
                    Phone = model.Phone,
                    Email = model.Email
                };

                _context.Visitors.Add(visitor);
            }

            // Create new visit
            var visit = new Visit
            {
                Visitor = visitor,
                CompanyName = model.CompanyName,
                VisitTypeId = model.VisitTypeId,
                OtherVisitType = model.OtherVisitType,
                ReceptionistId = model.ReceptionistId,
                EmployeeToMeet = model.EmployeeToMeet,
                Department = model.Department,
                HasId = model.HasId,
                //NationalId = model.HasId
                  //  ? model.NationalId
                    //: null,

                CheckInTime = DateTime.Now,
                Status = "Inside",
                IsDeleted = false
            };

            _context.Visits.Add(visit);

            // Create Items
            foreach (var item in model.Items)
            {
                if (!string.IsNullOrWhiteSpace(item.ItemName))
                {
                    var visitorItem = new VisitorItem
                    {
                        Visit = visit,
                        ItemName = item.ItemName,
                        Quantity = item.Quantity
                    };

                    _context.VisitorItems.Add(visitorItem);
                }
            }

            _context.SaveChanges();

            return RedirectToAction("Index", "Dashboard");
        }




        [HttpGet("search-by-national-id/{nationalId}")]
        public async Task<IActionResult> SearchByNationalId(string nationalId)
        {
            var visitor = await _context.Visitors
                .FirstOrDefaultAsync(v => v.NationalId == nationalId);

            if (visitor == null)
            {
                return NotFound();
            }

            return Ok(visitor);
        }
    }
}