using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using MoodPlaylistGenerator.Models;
using MoodPlaylistGenerator.ViewModels;
using Microsoft.Extensions.Configuration;
using MoodPlaylistGenerator.Services;
using System.IO;
using MoodPlaylist.SQLite.Services;
using ServiceReferences = MoodPlaylist.SQLite.Services;

namespace MoodPlaylistGenerator.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IConfiguration _configuration;
        private readonly ILocalMediaService _localMediaService;
        private readonly ServiceReferences.SongService? _songService;
        private readonly ServiceReferences.PlaylistService? _playlistService;

        public HomeController(
            ILogger<HomeController> logger,
            IConfiguration configuration,
            ILocalMediaService localMediaService,
            ServiceReferences.SongService? songService = null,
            ServiceReferences.PlaylistService? playlistService = null)
        {
            _logger = logger;
            _configuration = configuration;
            _localMediaService = localMediaService;
            _songService = songService;
            _playlistService = playlistService;
        }

        public async Task<IActionResult> Index()
        {
            if (User.Identity?.IsAuthenticated == true && _songService != null && _playlistService != null)
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
                var allSongs = await _songService.GetUserSongsAsync(userId);
                var allPlaylists = await _playlistService.GetUserPlaylistsAsync(userId);
                var moods = await _songService.GetAllMoodsAsync();
                var songCounts = await _playlistService.GetMoodSongCountsAsync(userId);

                var dashboardModel = new DashboardViewModel
                {
                    RecentSongs = allSongs.Take(5).ToList(),
                    RecentPlaylists = allPlaylists.Take(5).ToList(),
                    Moods = moods,
                    MoodSongCounts = songCounts,
                    TotalSongs = allSongs.Count,
                    TotalPlaylists = allPlaylists.Count
                };

                return View("Dashboard", dashboardModel);
            }

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> UploadMedia(IFormFile mediaFile)
        {
            if (mediaFile == null || mediaFile.Length == 0)
            {
                return RedirectToAction("Index");
            }

            string filePath = await _localMediaService.SaveFileAsync(mediaFile);

            return RedirectToAction("PlayLocalMedia", new { filePath = filePath });
        }

        public IActionResult PlayLocalMedia(string filePath)
        {
            var fullPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", filePath.TrimStart('/'));

            if (!System.IO.File.Exists(fullPath))
            {
                ViewBag.MediaUrl = _configuration["FallbackVideo:RickRollUrl"];
                ViewBag.IsLocal = false;
            }
            else
            {
                ViewBag.MediaUrl = filePath;
                ViewBag.IsLocal = true;
            }

            return View("Index");
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
