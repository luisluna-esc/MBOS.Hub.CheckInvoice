using CheckInvoice.core.Entities.Governance;
using CheckInvoice.core.Interfaces;

namespace CheckInvoice.Application.Services;

internal static class DocumentReviews
{
    public const string Receipt = "receipt";
    public const string Issue = "issue";
    public const string Transfer = "transfer";

    public const string Entered = "digitado";
    // Registrado antes de que existiera la firma digital: no entra al buzón, pero se puede
    // verificar si alguien quiere dejarlo firmado.
    public const string Previous = "anterior";
    public const string Verified = "verificado";
    public const string Approved = "aprobado";
    public const string Rejected = "rechazado";
    // Corrección aprobada: el contenido cambió y empieza una versión nueva.
    public const string Corrected = "corregido";
    public const string Reopened = "reabierto";

    public static bool IsKnownType(string type) => type is Receipt or Issue or Transfer;

    // Primera fila del historial: quién digitó el documento (se guarda con el resto del documento).
    public static Task RecordEnteredAsync(IUnitOfWork unitOfWork, string type, long documentId, long? userId) =>
        unitOfWork.Repository<DocumentReview>().AddAsync(new DocumentReview
        {
            DocumentType = type,
            DocumentId = documentId,
            Version = 1,
            Action = Entered,
            UserId = userId,
            At = DateTime.UtcNow
        });

    public static string Label(string status) => status switch
    {
        Verified => "VERIFICADO",
        Approved => "APROBADO",
        Previous => "ANTERIOR A LA FIRMA DIGITAL",
        _ => "DIGITADO"
    };
}
