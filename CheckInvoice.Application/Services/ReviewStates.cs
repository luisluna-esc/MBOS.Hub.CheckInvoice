namespace CheckInvoice.Application.Services;

// Filtro "Actualizado / Desactualizado" de los documentos con firma digital: actualizado es el
// que ya tiene firma (verificado o aprobado) o es anterior a la firma digital; desactualizado, el
// digitado o reabierto. En los reportes es el select Estado: "Actualizado" manda "updated" (solo lo
// actualizado); "Desactualizado" no manda nada (todo). Quién puede firmar se controla al
// firmar, así que el filtro no consulta roles. En SQL es fn_review_matches.
internal static class ReviewStates
{
    public const string Updated = "updated";
    public const string Outdated = "outdated";

    /// <summary>Estados que cuentan como actualizados.</summary>
    public static readonly string[] UpdatedStatuses = [DocumentReviews.Verified, DocumentReviews.Approved, DocumentReviews.Previous];

    /// <summary>Lo que se pasa a los procedimientos: solo los dos valores conocidos.</summary>
    public static object ForSql(string? reviewState) =>
        reviewState is Updated or Outdated ? reviewState : DBNull.Value;

    /// <summary>Línea del encabezado de los reportes.</summary>
    public static string Label(string? reviewState) => reviewState switch
    {
        Updated => "Actualizado",
        // Desactualizado en los reportes cuenta todo, también lo que todavía no tiene firma.
        _ => "Desactualizado"
    };
}
