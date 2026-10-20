using System.Net;
using CheckInvoice.Application.Dtos.Governance;
using CheckInvoice.Application.Interfaces.Governance;
using CheckInvoice.Application.Interfaces.Security;
using CheckInvoice.core.Entities.Governance;
using CheckInvoice.core.Entities.Movements;
using CheckInvoice.core.Entities.ResponseApi.Details;
using CheckInvoice.core.Entities.ResponseApi.DisplayFormat;
using CheckInvoice.core.Entities.Security;
using CheckInvoice.core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CheckInvoice.Application.Services;

// Revisión y firma digital de Entradas, Salidas y Transferencias. Sigue el mismo esquema que las
// anulaciones: el Contador o el Tesorero verifican y firman; el Contador aprueba o rechaza (con
// motivo). Un documento aprobado solo se reabre con motivo y
// eso empieza una versión nueva. Cada firma pide la contraseña de quien firma (reemplaza la
// firma en papel) y guarda la huella del contenido firmado. El stock se mueve al digitar.
public class DocumentReviewService : IDocumentReviewService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public DocumentReviewService(IUnitOfWork unitOfWork, ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    private sealed class DocumentState
    {
        public required Func<string> GetStatus { get; init; }
        public required Action<string> SetStatus { get; init; }
        public required Func<int> GetVersion { get; init; }
        public required Action<int> SetVersion { get; init; }
        public required bool IsVoided { get; init; }
        public required Action Update { get; init; }
    }

    private async Task<DocumentState?> LoadAsync(string documentType, long documentId)
    {
        switch (documentType)
        {
            case DocumentReviews.Receipt:
            {
                var receipt = await _unitOfWork.Repository<Receipt>().GetByIdAsync(documentId);
                return receipt is null ? null : new DocumentState
                {
                    GetStatus = () => receipt.ReviewStatus,
                    SetStatus = value => receipt.ReviewStatus = value,
                    GetVersion = () => receipt.ReviewVersion,
                    SetVersion = value => receipt.ReviewVersion = value,
                    IsVoided = receipt.IsVoided,
                    Update = () => _unitOfWork.Repository<Receipt>().Update(receipt)
                };
            }
            case DocumentReviews.Issue:
            {
                var issue = await _unitOfWork.Repository<Issue>().GetByIdAsync(documentId);
                return issue is null ? null : new DocumentState
                {
                    GetStatus = () => issue.ReviewStatus,
                    SetStatus = value => issue.ReviewStatus = value,
                    GetVersion = () => issue.ReviewVersion,
                    SetVersion = value => issue.ReviewVersion = value,
                    IsVoided = issue.IsVoided,
                    Update = () => _unitOfWork.Repository<Issue>().Update(issue)
                };
            }
            case DocumentReviews.Transfer:
            {
                var transfer = await _unitOfWork.Repository<Transfer>().GetByIdAsync(documentId);
                return transfer is null ? null : new DocumentState
                {
                    GetStatus = () => transfer.ReviewStatus,
                    SetStatus = value => transfer.ReviewStatus = value,
                    GetVersion = () => transfer.ReviewVersion,
                    SetVersion = value => transfer.ReviewVersion = value,
                    IsVoided = transfer.IsVoided,
                    Update = () => _unitOfWork.Repository<Transfer>().Update(transfer)
                };
            }
            default:
                return null;
        }
    }

    public async Task<ResponseGetObject> GetReview(string documentType, long documentId)
    {
        var document = await LoadAsync(documentType, documentId);
        if (document is null)
        {
            return new ResponseGetObject
            {
                Data = new(),
                Messages = [new Message { Type = MessageType.Error, Description = "No se encontró el documento." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        var rows = await (
            from r in _unitOfWork.Repository<DocumentReview>().Query()
            join u in _unitOfWork.Repository<AppUser>().Query() on r.UserId equals u.AppUserId into users
            from u in users.DefaultIfEmpty()
            where r.DocumentType == documentType && r.DocumentId == documentId
            orderby r.DocumentReviewId
            select new { Review = r, UserName = u != null ? u.FirstName + " " + u.LastName : null, UserEmail = u != null ? u.Email : null })
            .ToListAsync();

        var currentHash = await DocumentFingerprint.ComputeAsync(_unitOfWork, documentType, documentId);

        return new ResponseGetObject
        {
            Data = new DocumentReviewDto
            {
                Status = document.GetStatus(),
                Version = document.GetVersion(),
                IsVoided = document.IsVoided,
                Entries = rows.Select(row => new DocumentReviewEntryDto
                {
                    Version = row.Review.Version,
                    Action = row.Review.Action,
                    UserName = row.UserName,
                    UserEmail = row.UserEmail,
                    At = row.Review.At,
                    Reason = row.Review.Reason,
                    SignatureCode = row.Review.ContentHash is null ? null : DocumentFingerprint.SignatureCode(row.Review),
                    // Solo las firmas de la versión vigente se comparan con el contenido actual.
                    SignatureValid = row.Review.ContentHash is null || row.Review.Version != document.GetVersion()
                        ? null
                        : row.Review.ContentHash == currentHash
                }).ToList()
            },
            Messages = [],
            StatusCode = HttpStatusCode.OK
        };
    }

    public Task<ResponsePost> Verify(string documentType, long documentId, string password) =>
        SignAsync(documentType, documentId, password, DocumentReviews.Entered, DocumentReviews.Verified, null,
            "Solo se verifica un documento digitado.", "Documento verificado y firmado.");

    public Task<ResponsePost> Approve(string documentType, long documentId, string password, string? notes) =>
        SignAsync(documentType, documentId, password, DocumentReviews.Verified, DocumentReviews.Approved, notes,
            "Primero hay que verificar el documento.", "Documento aprobado y firmado.");

    public Task<ResponsePost> Reject(string documentType, long documentId, string password, string reason) =>
        SignAsync(documentType, documentId, password, DocumentReviews.Verified, DocumentReviews.Rejected, reason,
            "Solo se rechaza un documento verificado.", "Documento rechazado. Vuelve a quien lo digitó para corregirlo.");

    public Task<ResponsePost> Reopen(string documentType, long documentId, string password, string reason) =>
        SignAsync(documentType, documentId, password, DocumentReviews.Approved, DocumentReviews.Reopened, reason,
            "Solo se reabre un documento aprobado.", "Documento reabierto. Queda como una versión nueva.");

    private async Task<ResponsePost> SignAsync(
        string documentType, long documentId, string password, string requiredStatus, string action, string? reason,
        string wrongStatusMessage, string successMessage)
    {
        var document = await LoadAsync(documentType, documentId);
        if (document is null)
        {
            return Error(documentId, "No se encontró el documento.", HttpStatusCode.NotFound);
        }

        if (document.IsVoided)
        {
            return Error(documentId, "El documento está anulado: ya no se revisa.", HttpStatusCode.BadRequest);
        }

        var currentStatus = document.GetStatus();
        var verifyingPrevious = action == DocumentReviews.Verified && currentStatus == DocumentReviews.Previous;
        if (currentStatus != requiredStatus && !verifyingPrevious)
        {
            return Error(documentId, wrongStatusMessage, HttpStatusCode.BadRequest);
        }

        var needsReason = action is DocumentReviews.Rejected or DocumentReviews.Reopened;
        if (needsReason && string.IsNullOrWhiteSpace(reason))
        {
            return Error(documentId, "Escribe el motivo.", HttpStatusCode.BadRequest);
        }

        // La contraseña es la firma: confirma que quien firma es el dueño de la sesión.
        var user = _currentUserService.AppUserId.HasValue
            ? await _unitOfWork.Repository<AppUser>().GetByIdAsync(_currentUserService.AppUserId.Value)
            : null;
        if (user is null || string.IsNullOrEmpty(password) || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            return Error(documentId, "La contraseña no es correcta. La firma no se registró.", HttpStatusCode.BadRequest);
        }

        var now = DateTime.UtcNow;
        var version = document.GetVersion();
        await _unitOfWork.Repository<DocumentReview>().AddAsync(new DocumentReview
        {
            DocumentType = documentType,
            DocumentId = documentId,
            Version = version,
            Action = action,
            UserId = user.AppUserId,
            // Al segundo: la base guarda microsegundos y el código de firma se recalcula después.
            At = new DateTime(now.Ticks - now.Ticks % TimeSpan.TicksPerSecond, DateTimeKind.Utc),
            Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
            ContentHash = await DocumentFingerprint.ComputeAsync(_unitOfWork, documentType, documentId)
        });

        if (needsReason)
        {
            // Rechazar o reabrir: vuelve a digitado para corregirlo, como una versión nueva.
            document.SetStatus(DocumentReviews.Entered);
            document.SetVersion(version + 1);
        }
        else
        {
            document.SetStatus(action);
        }

        document.Update();
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = documentId,
            Messages = [new Message { Type = MessageType.Success, Description = successMessage }],
            StatusCode = HttpStatusCode.OK
        };
    }

    private static ResponsePost Error(long id, string message, HttpStatusCode statusCode) => new()
    {
        Id = id,
        Messages = [new Message { Type = MessageType.Error, Description = message }],
        StatusCode = statusCode
    };
}
