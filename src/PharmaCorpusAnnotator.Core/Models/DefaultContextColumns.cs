namespace PharmaCorpusAnnotator.Core.Models;

public static class DefaultContextColumns
{
    public static readonly IReadOnlyList<string> All =
    [
        "Código Nacional",
        "Tipo de fármaco",
        "Nombre genérico efecto y accesorio",
        "Nombre del laboratorio ofertante",
        "Estado",
        "Aportación del beneficiario",
        "Principio activo o asociación de principios activos",
        "Precio de venta al público con IVA",
        "Precio de referencia",
        "Diagnóstico hospitalario",
        "Tratamiento de larga duración",
        "Especial control médico",
        "Medicamento huérfano",
    ];
}
