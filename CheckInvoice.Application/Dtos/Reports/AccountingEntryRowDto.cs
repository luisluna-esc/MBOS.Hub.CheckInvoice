namespace CheckInvoice.Application.Dtos.Reports;

/// <summary>
/// Una fila del formato de importación contable ("Transaction_Entry"): un producto de una
/// salida. Las primeras columnas son las del archivo; el resto es para revisar en pantalla.
/// </summary>
public class AccountingEntryRowDto
{
    public string AccountCode { get; set; } = string.Empty;
    /// <summary>Código contable del departamento del producto; vacío si aún no se cargó.</summary>
    public string SubAccountCode { get; set; } = string.Empty;
    public string FundCode { get; set; } = string.Empty;
    public string FunctionCode { get; set; } = string.Empty;
    public string RestrictionCode { get; set; } = string.Empty;
    /// <summary>Total de la línea: negativo en salidas, positivo en entradas.</summary>
    public decimal EntityValue { get; set; }
    public string SendMemo { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public long DocumentId { get; set; }
    public DateTime DocumentDate { get; set; }
    public string? WarehouseName { get; set; }
    public string? DepartmentName { get; set; }
    public string? ProductCode { get; set; }
    /// <summary>Solo en entradas: N° de nota ("MB 3-10-26") y tipo de entrada.</summary>
    public string? DocumentNumber { get; set; }
    public string? DocumentType { get; set; }
}
