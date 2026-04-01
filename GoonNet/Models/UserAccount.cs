using System;
using System.Xml.Serialization;

namespace GoonNet;

[XmlRoot("UserAccount")]
public class UserAccount
{
    [XmlAttribute]
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.ReadOnly;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedDate { get; set; } = DateTime.Now;
    public DateTime? LastLogin { get; set; }
    public bool CanEditMusic { get; set; }
    public bool CanEditSchedule { get; set; }
    public bool CanManageUsers { get; set; }
    public bool CanViewLogs { get; set; } = true;
}
