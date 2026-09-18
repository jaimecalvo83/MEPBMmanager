namespace MEPBMmanager.Api.Services;

public static class EmailTemplates
{
    public static (string Subject, string Html) GameInviteRegistered(string gameName, string module, int maxTurns, string creatorName, string appUrl, bool isAdmin, string lang)
    {
        var isEs = lang == "es";
        var adminTag = isAdmin ? (isEs ? " como Admin" : " as Admin") : "";
        var inviteVerb = isEs ? "te ha invitado a una nueva partida:" : "has invited you to a new game:";
        var moduleWord = isEs ? "M\u00f3dulo" : "Module";
        var turnsWord = isEs ? "turnos" : "turns";
        var btnText = isEs ? "Entrar" : "Join Game";
        var adminNotice = isAdmin ? GetAdminNoticeHtml(isEs) : "";

        var subject = isEs
            ? $"\U0001f3ae MEPBM: Has sido invitado{adminTag} a \"{gameName}\""
            : $"\U0001f3ae MEPBM: You've been invited{adminTag} to \"{gameName}\"";

        var html = $"""
            <html><body style="font-family:system-ui,sans-serif;max-width:480px;margin:auto;padding:2rem;background:#f5f5f5;">
            <div style="background:#fff;padding:2rem;border-radius:8px;box-shadow:0 2px 8px rgba(0,0,0,0.1);">
            <h2 style="color:#c9a227;margin-top:0;">MEPBM Manager</h2>
            <p><strong>{HtmlEsc(creatorName)}</strong> {inviteVerb}</p>
            <div style="background:#1a1a2e;color:#e0e0e0;padding:1rem;border-radius:8px;border-left:4px solid #c9a227;">
                <p style="margin:0;font-size:1.2rem;font-weight:bold;">{HtmlEsc(gameName)}</p>
                <p style="margin:0.5rem 0 0;color:#999;">{moduleWord} {HtmlEsc(module)} &middot; {maxTurns} {turnsWord}</p>
            </div>
            {adminNotice}
            <p style="margin-top:1.5rem;">
                <a href="{HtmlEsc(appUrl)}/login" style="display:inline-block;background:#c9a227;color:#1a1a2e;padding:0.75rem 1.5rem;text-decoration:none;border-radius:4px;font-weight:bold;">{btnText}</a>
            </p>
            </div></body></html>
            """;
        return (subject, html);
    }

    public static (string Subject, string Html) GameInviteUnregistered(string gameName, string module, int maxTurns, string creatorName, string appUrl, bool isAdmin, string lang)
    {
        var isEs = lang == "es";
        var adminTag = isAdmin ? (isEs ? "Admin " : "Admin ") : "";
        var inviteVerb = isEs ? "te ha invitado a una nueva partida:" : "has invited you to a new game:";
        var moduleWord = isEs ? "M\u00f3dulo" : "Module";
        var turnsWord = isEs ? "turnos" : "turns";
        var needText = isEs ? "Necesitas" : "You need to";
        var createText = isEs ? "crear una cuenta" : "create an account";
        var acceptText = isEs ? "para aceptar esta invitaci\u00f3n." : "to accept this invitation.";
        var adminNotice = isAdmin ? GetAdminNoticeHtml(isEs) : "";
        var btnText = isEs ? "Registrarse" : "Register Now";

        var subject = isEs
            ? $"\U0001f3ae MEPBM: Invitaci\u00f3n de admin - Reg\u00edstrate para unirte a \"{gameName}\""
            : $"\U0001f3ae MEPBM: {adminTag}Invitation \u2013 Register to join \"{gameName}\"";

        var html = $"""
            <html><body style="font-family:system-ui,sans-serif;max-width:480px;margin:auto;padding:2rem;background:#f5f5f5;">
            <div style="background:#fff;padding:2rem;border-radius:8px;box-shadow:0 2px 8px rgba(0,0,0,0.1);">
            <h2 style="color:#c9a227;margin-top:0;">MEPBM Manager</h2>
            <p><strong>{HtmlEsc(creatorName)}</strong> {inviteVerb}</p>
            <div style="background:#1a1a2e;color:#e0e0e0;padding:1rem;border-radius:8px;border-left:4px solid #c9a227;">
                <p style="margin:0;font-size:1.2rem;font-weight:bold;">{HtmlEsc(gameName)}</p>
                <p style="margin:0.5rem 0 0;color:#999;">{moduleWord} {HtmlEsc(module)} &middot; {maxTurns} {turnsWord}</p>
            </div>
            <p>{needText} <a href="{HtmlEsc(appUrl)}/register" style="color:#c9a227;">{createText}</a> {acceptText}</p>
            {adminNotice}
            <p style="margin-top:1.5rem;">
                <a href="{HtmlEsc(appUrl)}/register" style="display:inline-block;background:#c9a227;color:#1a1a2e;padding:0.75rem 1.5rem;text-decoration:none;border-radius:4px;font-weight:bold;">{btnText}</a>
            </p>
            </div></body></html>
            """;
        return (subject, html);
    }

    private static string GetAdminNoticeHtml(bool isEs) => isEs
        ? """<p style="background:#2d1f3d;color:#e0b0ff;padding:0.75rem;border-radius:4px;margin-top:1rem;">&#128081; <strong>Rol de Admin:</strong> Gestionar\u00e1s el procesamiento de turnos, resolver\u00e1s disputas y supervisar\u00e1s la configuraci\u00f3n de la partida. Acepta tu rol de admin en el lobby.</p>"""
        : """<p style="background:#2d1f3d;color:#e0b0ff;padding:0.75rem;border-radius:4px;margin-top:1rem;">&#128081; <strong>Admin role:</strong> You will manage turn processing, resolve disputes, and oversee game setup. Please accept your admin role in the game lobby.</p>""";

    private static string HtmlEsc(string s) => System.Net.WebUtility.HtmlEncode(s);
}
