using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;

namespace KSol.NextCloudMailToFolder.Controllers;

public class IdentityController : Controller
{
    private readonly ILogger<IdentityController> _logger;

    public IdentityController(ILogger<IdentityController> logger)
    {
        _logger = logger;
    }

    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync("Cookies");
        return RedirectToAction("Index", "Home");
    }
}
