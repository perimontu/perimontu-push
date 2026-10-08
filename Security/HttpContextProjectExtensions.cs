namespace CoberPush.Api.Security;

public static class HttpContextProjectExtensions
{
    private const string ITEM_KEY = "CoberPush.ProjectContext";

    /// <summary>Lo deja disponible para el handler el filtro de API key.</summary>
    public static void SetProjectContext(this HttpContext context, ProjectContext projectContext)
    {
        context.Items[ITEM_KEY] = projectContext;
    }

    /// <exception cref="InvalidOperationException">El endpoint no pasó por el filtro de API key.</exception>
    public static ProjectContext GetProjectContext(this HttpContext context)
    {
        return context.Items[ITEM_KEY] as ProjectContext
            ?? throw new InvalidOperationException("El endpoint no está protegido por el filtro de proyecto/API key.");
    }
}
