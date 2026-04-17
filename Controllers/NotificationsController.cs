using grad.Data;
using grad.DTOs;
using grad.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace grad.Controllers
{
    
    [ApiController]
    [Route("api/student/notifications")]
    [Authorize(Roles = "Student")]
    public class NotificationsController : ControllerBase
    {
        private readonly AppDbContext _db;

        public NotificationsController(AppDbContext db)
        {
            _db = db;
        }

      
        [HttpGet]
        public async Task<IActionResult> GetMyNotifications([FromQuery] bool unreadOnly = false)
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var query = _db.Notifications
                .Where(n => n.UserId == userId)
                .AsQueryable();

            if (unreadOnly)
                query = query.Where(n => !n.IsRead);

            var notifications = await query
                .OrderByDescending(n => n.CreatedAt)
                .Select(n => new NotificationResponseDto
                {
                    Id         = n.Id,
                    Title      = n.Title,
                    Body       = n.Body,
                    Type       = n.Type,
                    IsRead     = n.IsRead,
                    CreatedAt  = n.CreatedAt,
                    TimeAgo    = BuildTimeAgo(n.CreatedAt)
                })
                .ToListAsync();

            return Ok(notifications);
        }

      
        [HttpGet("unread-count")]
        public async Task<IActionResult> GetUnreadCount()
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var count = await _db.Notifications
                .CountAsync(n => n.UserId == userId && !n.IsRead);

            return Ok(new { UnreadCount = count });
        }

       
        [HttpPatch("{id:int}/read")]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var notification = await _db.Notifications
                .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);

            if (notification is null)
                return NotFound(new { message = "Notification not found." });

            notification.IsRead = true;
            await _db.SaveChangesAsync();

            var unreadCount = await _db.Notifications
                .CountAsync(n => n.UserId == userId && !n.IsRead);

            return Ok(new MarkReadResponseDto { Success = true, UnreadCount = unreadCount });
        }


        [HttpPatch("read-all")]
        public async Task<IActionResult> MarkAllAsRead()
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            await _db.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .ExecuteUpdateAsync(setters =>
                    setters.SetProperty(n => n.IsRead, true));

            return Ok(new MarkReadResponseDto { Success = true, UnreadCount = 0 });
        }

       
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteNotification(int id)
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var notification = await _db.Notifications
                .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);

            if (notification is null)
                return NotFound(new { message = "Notification not found." });

            _db.Notifications.Remove(notification);
            await _db.SaveChangesAsync();

            return Ok(new { message = "Notification deleted." });
        }

        private static string BuildTimeAgo(DateTime utcTime)
        {
            var span = DateTime.UtcNow - utcTime;
            if (span.TotalMinutes < 1)   return "Just now";
            if (span.TotalMinutes < 60)  return $"{(int)span.TotalMinutes}m ago";
            if (span.TotalHours < 24)    return $"{(int)span.TotalHours}h ago";
            if (span.TotalDays < 7)      return $"{(int)span.TotalDays}d ago";
            return utcTime.ToString("MMM dd, yyyy");
        }
    }
}
