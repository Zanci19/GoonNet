using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Serialization;

namespace GoonNet;

public class UserDatabase : DatabaseBase<UserAccount>
{
    protected override Guid GetId(UserAccount item) => item.Id;

    protected override XmlSerializer CreateSerializer()
        => new XmlSerializer(typeof(List<UserAccount>), new XmlRootAttribute("Users"));

    public static string HashPassword(string password)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public UserAccount? GetByUsername(string username)
        => _items.FirstOrDefault(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase));

    public UserAccount? Authenticate(string username, string password)
    {
        var hash = HashPassword(password);
        var user = _items.FirstOrDefault(u =>
            u.Username.Equals(username, StringComparison.OrdinalIgnoreCase) &&
            u.PasswordHash == hash &&
            u.IsActive);
        if (user != null)
            user.LastLogin = DateTime.Now;
        return user;
    }

    public bool ChangePassword(Guid userId, string newPassword)
    {
        var user = GetById(userId);
        if (user == null) return false;
        user.PasswordHash = HashPassword(newPassword);
        return true;
    }

    public void EnsureDefaultAdmin()
    {
        if (_items.Count == 0)
        {
            Add(new UserAccount
            {
                Username = "admin",
                PasswordHash = HashPassword("admin"),
                Role = UserRole.Admin,
                FullName = "Administrator",
                IsActive = true,
                CanEditMusic = true,
                CanEditSchedule = true,
                CanManageUsers = true,
                CanViewLogs = true
            });
        }
    }
}
