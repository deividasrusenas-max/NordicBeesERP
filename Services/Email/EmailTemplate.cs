namespace NordicBeesERP.Services.Email;

/// <summary>A row of <c>email_templates</c> (or a built-in default with the same shape).</summary>
public sealed record EmailTemplate(string Code, string Language, string Subject, string BodyHtml);
