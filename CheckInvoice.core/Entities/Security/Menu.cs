namespace CheckInvoice.core.Entities.Security;

public class Menu
{
    public long MenuId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Route { get; set; }
    public string? Icon { get; set; }
    public long? ParentMenuId { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public long? PermissionId { get; set; }
}