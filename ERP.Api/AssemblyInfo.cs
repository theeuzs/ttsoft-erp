using System.Runtime.CompilerServices;

// S25 (18/08) — expõe os métodos internal de NfeContingencyHostedService
// (e qualquer outro que precise) especificamente pro isolamento
// multi-tenant poder ser testado sem virar API pública do assembly.
[assembly: InternalsVisibleTo("ERP.Tests")]
