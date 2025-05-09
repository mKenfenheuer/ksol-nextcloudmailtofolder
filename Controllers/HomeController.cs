using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using KSol.NextCloudMailToFolder.Models;
using Microsoft.AspNetCore.Authorization;
using KSol.NextCloudMailToFolder.Data;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Text;
using static KSol.NextCloudMailToFolder.Mail.SmtpServerService;
using KSol.NextCloudMailToFolder.Mail;

namespace KSol.NextCloudMailToFolder.Controllers;

[Authorize]
public class HomeController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<HomeController> _logger;
    private readonly SmtpServerConfiguration _configuration = new SmtpServerConfiguration();

    public HomeController(ILogger<HomeController> logger, ApplicationDbContext context, IConfiguration configuration)
    {
        _logger = logger;
        _context = context;
        configuration.Bind("SMTP", _configuration);
    }

    public async Task<IActionResult> Index()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? throw new Exception("User ID not found in claims.");
        return View(await _context.Destinations.Where(d => d.UserId == userId).ToListAsync());
    }

    public async Task<IActionResult> Create()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? throw new Exception("User ID not found in claims.");

        NamesGenerator gen = new NamesGenerator();
        var destination = $"{gen.GetRandomName()}@{_configuration.Hostname}";
        while (await _context.Destinations.AnyAsync(d => d.Recipient == destination))
        {
            destination = $"{gen.GetRandomName()}.{userId}@{gen.GetRandomName()}.nextcloud";
        }
        return View(new Destination()
        {
            UserId = userId,
            Recipient = destination,
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Name,Recipient,Path")] Destination destination)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? throw new Exception("User ID not found in claims.");
        if (ModelState.IsValid)
        {
            destination.UserId = userId;
            _context.Add(destination);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        return View(destination);
    }

    public async Task<IActionResult> Edit(string? id)
    {
        if (id == null || _context.Destinations == null)
        {
            return NotFound();
        }

        var destination = await _context.Destinations.FindAsync(id);
        if (destination == null)
        {
            return NotFound();
        }
        return View(destination);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, [Bind("Id,Name,Recipient,Path")] Destination destination)
    {
        if (id != destination.Id)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? throw new Exception("User ID not found in claims.");
                destination.UserId = userId;
                _context.Update(destination);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!DestinationExists(destination.Id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }
            return RedirectToAction(nameof(Index));
        }
        return View(destination);
    }

    public async Task<IActionResult> Delete(string id)
    {
        if (id == null || _context.Destinations == null)
        {
            return NotFound();
        }

        var destination = await _context.Destinations
            .FirstOrDefaultAsync(m => m.Id == id);
        if (destination == null)
        {
            return NotFound();
        }

        return View(destination);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(string id)
    {
        if (_context.Destinations == null)
        {
            return Problem("Entity set 'ApplicationDbContext.Destinations'  is null.");
        }
        var destination = await _context.Destinations.FindAsync(id);
        if (destination != null)
        {
            _context.Destinations.Remove(destination);
        }

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    private bool DestinationExists(string id)
    {
        return (_context.Destinations?.Any(e => e.Id == id)).GetValueOrDefault();
    }


    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
