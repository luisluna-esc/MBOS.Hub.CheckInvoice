namespace CheckInvoice.Application.Dtos.Governance;

public class DocumentReviewDto
{
    public string Status { get; set; } = string.Empty;
    public int Version { get; set; }
    public bool IsVoided { get; set; }
    public List<DocumentReviewEntryDto> Entries { get; set; } = [];
}

public class DocumentReviewEntryDto
{
    public int Version { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? UserName { get; set; }
    public string? UserEmail { get; set; }
    public DateTime At { get; set; }
    public string? Reason { get; set; }
    // Código de la firma digital (ej. 3F9A-07C2-B1D4). Null en "digitado".
    public string? SignatureCode { get; set; }
    // Firmas de la versión vigente: true si el documento no cambió desde que se firmó.
    public bool? SignatureValid { get; set; }
}

// Firmar: la contraseña de quien firma; motivo obligatorio al rechazar o reabrir.
public class DocumentSignDto
{
    public string Password { get; set; } = string.Empty;
    public string? Reason { get; set; }
}
