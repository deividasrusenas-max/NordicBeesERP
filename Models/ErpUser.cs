using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NordicBeesERP.Models;

[Table("erp_users")]
public class ErpUser
{
    [Key]
    [Column("id")]
    public int Id { get; set; }
    [Column("email")]
    public string Email { get; set; } = "";
    [Column("password_hash")]
    public string PasswordHash { get; set; } = "";
    [Column("full_name")]
    public string FullName { get; set; } = "";
    [Column("role")]
    [MaxLength(20)]
    public string Role { get; set; } = "Admin";
    [Column("is_active")]
    public bool IsActive { get; set; } = true;
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}