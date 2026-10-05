using Microsoft.AspNetCore.Mvc;
using WorldCupTerminal.Services;

namespace WorldCupTerminal.Controllers;

public class HomeController : Controller
{
    private readonly TournamentRepository _repo;

    public HomeController(TournamentRepository repo) => _repo = repo;

    public IActionResult Index()
    {
        ViewData["Section"] = "dashboard";
        return View(_repo);
    }

    public IActionResult Fixtures()
    {
        ViewData["Section"] = "fixtures";
        return View(_repo);
    }

    public IActionResult Bracket()
    {
        ViewData["Section"] = "bracket";
        return View(_repo);
    }

    public IActionResult Teams()
    {
        ViewData["Section"] = "teams";
        var ordered = _repo.Teams.OrderBy(t => t.Group).ThenBy(t => t.FifaRank).ToList();
        return View(ordered);
    }

    public IActionResult Team(string id)
    {
        var team = _repo.GetTeam(id ?? "");
        if (team is null) return NotFound();
        ViewData["Section"] = "teams";
        return View(team);
    }

    public IActionResult Match(string id)
    {
        var match = _repo.Matches.FirstOrDefault(m => m.Id == id);
        if (match is null) return NotFound();
        ViewData["Section"] = "fixtures";
        return View(match);
    }
}
