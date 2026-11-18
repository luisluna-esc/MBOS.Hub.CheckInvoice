using System.Net;
using System.Security.Claims;
using CheckInvoice.core.Entities.ResponseApi.Details;
using CheckInvoice.core.Entities.ResponseApi.DisplayFormat;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CheckInvoice.Web.Filters;

/// <summary>
/// Pastor y Caja tienen el permiso "trabajo" para lo suyo, pero ese permiso abre casi todo el
/// servidor. Si un usuario solo tiene alguno de estos roles, aquí se le deja usar únicamente lo
/// que piden sus pantallas: el Pastor, su portal ("Mi Cuenta"); la Caja, Cuentas por Cobrar. Un
/// usuario que además tenga otro rol (Contador, M-BOS, etc.) no se limita.
/// </summary>
public class RoleScopeFilter : IAsyncAuthorizationFilter
{
    // Lo que puede usar cualquier usuario con sesión: login, su menú, el buzón y las fotos.
    private static readonly (string Controller, string? Action, string? Method)[] Common =
    [
        ("Auth", null, null),
        ("Menus", "GetMyMenu", "GET"),
        ("Inbox", null, null),
        ("Products", "GetImage", "GET"),
    ];

    private static readonly Dictionary<string, (string Controller, string? Action, string? Method)[]> Scopes = new()
    {
        ["Pastor"] =
        [
            ("Portal", null, null),
        ],
        ["Caja"] =
        [
            ("AccountReceivables", null, "GET"),
            ("Payments", null, null),
            ("Clients", null, "GET"),
            ("Reports", "GetAccountReceivableVoucherPdf", "GET"),
        ],
    };

    public Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;
        if (user.Identity?.IsAuthenticated != true ||
            context.ActionDescriptor is not ControllerActionDescriptor action)
        {
            return Task.CompletedTask;
        }

        var roles = user.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
        // Sin roles, o con algún rol que no está limitado: no se aplica nada aquí.
        if (roles.Count == 0 || roles.Any(role => !Scopes.ContainsKey(role)))
        {
            return Task.CompletedTask;
        }

        var method = context.HttpContext.Request.Method;
        var allowed = Common.Concat(roles.SelectMany(role => Scopes[role]))
            .Any(rule =>
                string.Equals(rule.Controller, action.ControllerName, StringComparison.OrdinalIgnoreCase)
                && (rule.Action is null || string.Equals(rule.Action, action.ActionName, StringComparison.OrdinalIgnoreCase))
                && (rule.Method is null || string.Equals(rule.Method, method, StringComparison.OrdinalIgnoreCase)));

        if (!allowed)
        {
            context.Result = new ObjectResult(new ResponseGetObject
            {
                Messages = [new Message { Type = MessageType.Error, Description = "No tienes acceso a esta opción." }],
                StatusCode = HttpStatusCode.Forbidden
            })
            { StatusCode = (int)HttpStatusCode.Forbidden };
        }

        return Task.CompletedTask;
    }
}
