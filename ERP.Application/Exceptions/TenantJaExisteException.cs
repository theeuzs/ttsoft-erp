namespace ERP.Application.Exceptions;

/// <summary>
/// S11 FIX: exceção dedicada para "CNPJ já cadastrado" — separada de
/// InvalidOperationException genérica usada para erros reais de validação
/// (CNPJ inválido, e-mail vazio, etc).
///
/// Antes (S10): CadastroController fazia catch (InvalidOperationException) e
/// retornava BadRequest(400) tanto para "já existe" quanto para "CNPJ inválido".
/// Como "já existe" tinha mensagem genérica mas AINDA retornava 400, e "CNPJ
/// novo criado com sucesso" retornava 200, um atacante conseguia diferenciar
/// os dois casos só pelo status code HTTP — oráculo de enumeração.
///
/// Com este tipo dedicado, o Controller pode capturar especificamente esse
/// caso e devolver 200 com a MESMA mensagem genérica de sucesso, fechando
/// o oráculo. Erros de validação real (CNPJ malformado) continuam 400.
/// </summary>
public class TenantJaExisteException : Exception
{
    public TenantJaExisteException()
        : base("Tenant já cadastrado.") { }
}
