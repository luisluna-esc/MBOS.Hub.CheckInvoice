namespace CheckInvoice.core.Entities.Security;

public class Menu
{
    public long MenuId { get; set; }
    public string Name { get; set; } = string.Empty;
    /// <summary>Clave de traducción opcional (ej. "nav.home") que el frontend resuelve en sus diccionarios i18n; si es null, se usa <see cref="Name"/> literal.</summary>
    public string? TranslationKey { get; set; }
    public string? Route { get; set; }
    public string? Icon { get; set; }
    public long? ParentMenuId { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public long? PermissionId { get; set; }
}