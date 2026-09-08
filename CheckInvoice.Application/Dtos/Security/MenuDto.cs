namespace CheckInvoice.Application.Dtos.Security;

public class MenuDto
{
    public long MenuId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? TranslationKey { get; set; }
    public string? Route { get; set; }
    public string? Icon { get; set; }
    public long? ParentMenuId { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; }
    public long? PermissionId { get; set; }
}