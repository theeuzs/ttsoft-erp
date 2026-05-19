// ============================================================
//  Script para calcular o TenantId de qualquer CNPJ.
//  Execute com: dotnet script CalcularTenantId.csx
//  Ou cole diretamente no LinqPad / console C# de sua preferência.
// ============================================================

using System.Security.Cryptography;
using System.Text;

var cnpjs = new[]
{
    ("Vila Verde",  "12820608000141"),
    // Adicione outros clientes aqui:
    // ("Loja do João", "00000000000000"),
};

foreach (var (nome, cnpj) in cnpjs)
{
    var limpo = new string(cnpj.Where(char.IsDigit).ToArray());
    var hash = SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(limpo));
    var guidBytes = new byte[16];
    Array.Copy(hash, guidBytes, 16);
    var tenantId = new Guid(guidBytes);
    Console.WriteLine($"{nome,-20} CNPJ: {limpo}  =>  TenantId: {tenantId}");
}
