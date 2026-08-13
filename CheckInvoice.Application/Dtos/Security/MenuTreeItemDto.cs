namespace CheckInvoice.Application.Dtos.Security;

public class MenuTreeItemDto
{
    public long MenuId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Route { get; set; }
    public string? Icon { get; set; }
    public int DisplayOrder { get; set; }
    public List<MenuTreeItemDto> Children { get; set; } = [];
}