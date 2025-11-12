using EduOnline.Core.Communication.Mediator;
using EduOnline.Core.ControleDeAcesso;
using EduOnline.Core.Mensagens;
using EduOnline.Core.Mensagens.Notifications;
using FluentValidation.Results;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace EduOnline.WebApps.ApiRest.Controllers;

[ApiController]
public abstract class MainController : ControllerBase
{
    private readonly DomainNotificationHandler _domainNotifications;
    private readonly IMediatorHandler _mediatorHandler;
    private readonly INotificador _notificador;
    public readonly IUser AppUser;

    protected Guid UsuarioId { get; set; }
    protected bool UsuarioAutenticado { get; set; }

    protected MainController(INotificador notificador,
                             IUser appUser)
    {
        _notificador = notificador;
        AppUser = appUser;

        if (appUser.IsAuthenticated())
        {
            UsuarioId = appUser.GetUserId();
            UsuarioAutenticado = true;
        }
    }

    protected MainController(INotificationHandler<DomainNotification> notifications,
                                 IMediatorHandler mediatorHandler,
                                 IUser appUser)
    {
        _domainNotifications = (DomainNotificationHandler)notifications;
        _mediatorHandler = mediatorHandler;
        AppUser = appUser;

        if (appUser.IsAuthenticated())
        {
            UsuarioId = appUser.GetUserId();
            UsuarioAutenticado = true;
        }
    }

    protected bool OperacaoValida()
    {
        return !_notificador?.TemNotificacao() ?? false || !_domainNotifications.TemNotificacao();
    }

    protected ActionResult CustomResponse(object result = null)
    {
        if (OperacaoValida())
        {
            return Ok(new
            {
                success = true,
                data = result
            });
        }

        return BadRequest(new
        {
            success = false,
            errors = _notificador?.ObterNotificacoes().Select(n => n.Mensagem) ?? _domainNotifications.ObterNotificacoes().Select(d => d.Value)
        });
    }

    protected ActionResult NotificarValidationResult(ValidationResult validationResult)
    {
        if (!validationResult.IsValid)
        {
            foreach (var item in validationResult.Errors)
            {
                NotificarErro(item.ErrorMessage);
            }
        }

        return CustomResponse();
    }
    protected ActionResult CustomResponse(ModelStateDictionary modelState)
    {
        if (!modelState.IsValid) NotificarErroModelInvalida(modelState);
        return CustomResponse();
    }

    protected void NotificarErroModelInvalida(ModelStateDictionary modelState)
    {
        var erros = modelState.Values.SelectMany(e => e.Errors);
        foreach (var erro in erros)
        {
            var errorMsg = erro.Exception == null ? erro.ErrorMessage : erro.Exception.Message;
            NotificarErro(errorMsg);
        }
    }

    protected void NotificarErro(string mensagem)
    {
        _notificador?.Handle(new Notificacao(mensagem));

        _domainNotifications?.Handle(new DomainNotification(Guid.NewGuid().ToString(), mensagem), CancellationToken.None).Wait();
    }
}
