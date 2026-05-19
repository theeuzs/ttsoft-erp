namespace ERP.Domain.Enums;

public enum StatusEntrega
{
    Pendente    = 0,  // Aguardando saída
    EmRota      = 1,  // Motorista saiu com a entrega
    Entregue    = 2,  // Cliente recebeu e assinou
    Cancelada   = 3,  // Entrega cancelada (cliente não estava, endereço errado, etc.)
    Reagendada  = 4   // Cliente pediu para entregar em outro dia
}
