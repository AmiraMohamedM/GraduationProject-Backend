namespace grad.Services
{
    using grad.Data;
    using grad.Models;
    using Microsoft.EntityFrameworkCore;

    public class ActivityLogger
    {
        private readonly AppDbContext _db;

        public ActivityLogger(AppDbContext db)
        {
            _db = db;
        }

        public async Task Log(Guid adminId, string text)
        {
            if (adminId == Guid.Empty)
            {
                var fallbackUser = await _db.Users.FirstOrDefaultAsync();
                adminId = fallbackUser?.Id ?? Guid.Empty;
            }

            var log = new ActivityLogs
            {
                UserId = adminId,
                Text = text,
                CreatedAt = DateTime.UtcNow 
            };

            _db.ActivityLogs.Add(log);
            await _db.SaveChangesAsync();
        }
    }
}
