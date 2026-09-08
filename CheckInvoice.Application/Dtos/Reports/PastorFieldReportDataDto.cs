namespace CheckInvoice.Application.Dtos.Reports;

/// <summary>
/// Una fila de la sección "Cuentas por Cobrar" del reporte "Campo Pastor"
/// (sp_get_pastor_field_receivables).
/// </summary>
public class PastorFieldReceivableRowDto
{
    public long AccountReceivableId { get; set; }
    public long? IssueId { get; set; }
    public DateTime? IssueDate { get; set; }
    public DateOnly? DueDate { get; set; }
    public decimal OutstandingBalance { get; set; }
    public string Status { get; set; } = string.Empty;
}

/// <summary>
/// Una fila de la sección "Entregas" del reporte "Campo Pastor" (sp_get_pastor_field_deliveries):
/// una Salida del cliente en el período filtrado, con su total (mismo cálculo que
/// PortalService.GetMyIssues — suma de issue_detail.total_cost).
/// </summary>
public class PastorFieldDeliveryRowDto
{
    public long IssueId { get; set; }
    public DateTime IssueDate { get; set; }
    public decimal Total { get; set; }
}

/// <summary>
/// Una fila de la sección "Depósitos" del reporte "Campo Pastor" (sp_get_pastor_field_deposits):
/// un Pago contra alguna Cuenta por Cobrar del cliente en el período filtrado. "Depósito" aquí
/// es Pago — no existe una tabla "deposito" separada ligada a producto en este sistema.
/// </summary>
public class PastorFieldDepositRowDto
{
    public long PaymentId { get; set; }
    public DateTime PaymentDate { get; set; }
    public long? IssueId { get; set; }
    public decimal Amount { get; set; }
    public string? PaymentMethod { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// Resultado de sp_get_pastor_field_last_delivery: fecha de la última Salida del cliente,
/// sin restringir por el rango de fechas filtrado (siempre la verdadera última entrega).
/// </summary>
public class PastorFieldLastDeliveryDto
{
    public DateTime? LastDeliveryDate { get; set; }
}

/// <summary>
/// Datos completos del reporte "Campo Pastor" para un cliente: Cuentas por Cobrar (+ resumen),
/// última entrega, y entregas/depósitos del período filtrado.
/// </summary>
public class PastorFieldReportDataDto
{
    public List<PastorFieldReceivableRowDto> Receivables { get; set; } = new();
    public decimal TotalOutstanding { get; set; }

    /// <summary>
    /// Cantidad de cuentas por cobrar pendientes, en vez de "próxima fecha límite": cada cuenta
    /// tiene su propio plazo independiente (el pastor puede sacar más productos después con
    /// otro plazo), así que una sola fecha suelta sugiere erróneamente que todo vence junto.
    /// La fecha de cada cuenta ya se ve en su propia fila de la tabla. Mismo criterio que ya
    /// usa la pantalla de autoservicio "Mi Cuenta" del Pastor.
    /// </summary>
    public int PendingCount { get; set; }

    public DateTime? LastDeliveryDate { get; set; }
    public List<PastorFieldDeliveryRowDto> Deliveries { get; set; } = new();
    public List<PastorFieldDepositRowDto> Deposits { get; set; } = new();
}
