namespace CoberPush.Api.Services;

/// <summary>
/// No se pudo inicializar Firebase (p. ej. falta <c>GOOGLE_APPLICATION_CREDENTIALS</c> o el archivo es inválido).
/// Se informa como 503 con un mensaje accionable; el detalle queda solo en el log.
/// </summary>
public sealed class FirebaseUnavailableException(string message, Exception inner) : Exception(message, inner);
