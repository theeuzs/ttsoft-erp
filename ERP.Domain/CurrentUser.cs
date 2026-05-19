using System;

namespace ERP.Domain;

// Classe estática no centro do sistema para o WPF e o Banco conversarem!
public static class CurrentUser
{
    public static Guid? Id { get; set; }
    public static string Name { get; set; } = string.Empty;
}