namespace ERP.Portal.Models;

public class CadastroRequestDto
{
    public string Cnpj        { get; set; } = string.Empty;
    public string RazaoSocial { get; set; } = string.Empty;
    public string Email       { get; set; } = string.Empty;
    public string Whatsapp    { get; set; } = string.Empty;
    public string Senha       { get; set; } = string.Empty;
    public string Cidade      { get; set; } = string.Empty;
    public string Estado      { get; set; } = string.Empty;
}

public class CadastroResponseDto
{
    public string MensagemSucesso { get; set; } = string.Empty;
    public string LoginUrl        { get; set; } = string.Empty;
}
