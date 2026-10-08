namespace CoberPush.Api.Services;

/// <summary>
/// No se pudo inicializar Firebase de un proyecto (p. ej. falta la variable con la ruta de la credencial,
/// el archivo no existe o es inválido). Se informa como 503; el detalle queda solo en el log.
/// </summary>
public sealed class FirebaseUnavailableException : Exception
{
    public FirebaseUnavailableException(string message) : base(message)
    {
    }

    public FirebaseUnavailableException(string message, Exception inner) : base(message, inner)
    {
    }
}
