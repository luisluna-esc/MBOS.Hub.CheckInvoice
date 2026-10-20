namespace CheckInvoice.core.Entities.Products;

public class Product
{
    public long ProductId { get; set; }
    public string? Code { get; set; }
    public string Name { get; set; } = string.Empty;
    public long? DepartmentId { get; set; }
    public long? SubDepartmentId { get; set; }
    public long? MediaTypeId { get; set; }
    public bool IsActive { get; set; } = true;

    // Códigos contables del informe para contabilidad. Siempre los mismos: se guardan al crear
    // el producto y no se muestran en pantalla.
    public string FundCode { get; set; } = AccountingCodes.Fund;
    public string FunctionCode { get; set; } = AccountingCodes.Function;
    public string RestrictionCode { get; set; } = AccountingCodes.Restriction;
}

/// <summary>Valores fijos del formato de importación contable ("Transaction_Entry").</summary>
public static class AccountingCodes
{
    public const string Account = "4121565";
    public const string Fund = "10";
    public const string Function = "0000";
    public const string Restriction = "0A";
    public const string SendMemo = "False";
}
