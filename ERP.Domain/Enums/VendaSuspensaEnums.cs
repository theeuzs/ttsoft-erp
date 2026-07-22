// ── ERP.Domain/Enums/VendaSuspensaEnums.cs ────────────────────────────────────
namespace ERP.Domain.Enums;

/// <summary>
/// Suspensa é o único estado "pendente" — Finalizada/Descartada são terminais.
/// Não existe "Retomada" como status: retomar é uma ação temporária (trava de
/// edição), não um estado permanente da entidade. Se o operador abrir pra
/// retomar e desistir sem finalizar nem descartar, a trava é liberada e o
/// registro volta a ficar disponível como Suspensa pra qualquer operador.
/// </summary>
public enum StatusVendaSuspensa
{
    Suspensa   = 1,
    Finalizada = 2,
    Descartada = 3
}
