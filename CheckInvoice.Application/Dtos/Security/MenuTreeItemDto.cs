namespace CheckInvoice.Application.Dtos.Security;

public class MenuTreeItemDto
{
    public long MenuId { get; set; }
    public string Name { get; set; } = string.Empty;
    /// <summary>Clave de traducción opcional (ej. "nav.home"); si es null, el frontend usa <see cref="Name"/> literal.</summary>
    public string? TranslationKey { get; set; }
    public string? Route { get; set; }
    public string? Icon { get; set; }
    public int DisplayOrder { get; set; }
    /// <summary>Código del permiso requerido para ver este item (null = visible para cualquier usuario autenticado). Permite al frontend re-filtrar por "rol activo".</summary>
    public string? PermissionCode { get; set; }
    public List<MenuTreeItemDto> Children { get; set; } = [];
}