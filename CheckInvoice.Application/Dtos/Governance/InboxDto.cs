namespace CheckInvoice.Application.Dtos.Governance;

// Cuántos hay en cada pestaña del buzón. El contador del menú es Pending + Unread.
// Pestañas: Pendientes, Respuestas (Unread + Seen) y Lo que firmé (Signed).
public class InboxCountsDto
{
    public int Pending { get; set; }
    public int Unread { get; set; }
    public int Seen { get; set; }
    public int Signed { get; set; }
    // Pendientes por clase: toVerify, toApprove, voidRequest, receiptEdit.
    public Dictionary<string, int> ByKind { get; set; } = [];
    public bool CanVerify { get; set; }
    public bool CanApprove { get; set; }
}

// Filtros del buzón. Tab: pending, answers (unread + seen) o signed.
public class InboxQueryDto
{
    public string? Tab { get; set; }
    public string? Kind { get; set; }
    public string? DocumentType { get; set; }
    public string? Search { get; set; }
    public DateOnly? DateFrom { get; set; }
    public DateOnly? DateTo { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

// Fila de sp_get_inbox.
public class InboxRowDto
{
    // toVerify, toApprove, voidRequest, receiptEdit, reviewApproved, reviewRejected o reviewSigned.
    public string Kind { get; set; } = string.Empty;
    // receipt, issue o transfer.
    public string DocumentType { get; set; } = string.Empty;
    public long DocumentId { get; set; }
    public string Number { get; set; } = string.Empty;
    public string? Detail { get; set; }
    // En respuestas: approved o rejected. En "Lo que firmé": verificado, aprobado, rechazado,
    // reabierto (firmas) o approved / rejected (anulaciones y correcciones).
    public string? Status { get; set; }
    public DateTime At { get; set; }
    // Solo notificaciones: clave para marcarla como vista.
    public string? NotificationKey { get; set; }
    public bool Seen { get; set; }
    public int TotalRecords { get; set; }
}

public class InboxCountRow
{
    public string Tab { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public int Total { get; set; }
}

public class InboxMarkedRow
{
    public int Marked { get; set; }
}

public class InboxSeenDto
{
    // Claves a marcar; vacío o null marca todas las sin leer.
    public List<string>? Keys { get; set; }
}
