namespace ERP.Domain.Enums;

public enum StatusPedidoCompra
{
    Rascunho    = 0,   // Sendo montado, ainda não enviado
    Enviado     = 1,   // Pedido enviado ao fornecedor
    Recebido    = 2,   // Mercadoria chegou — estoque atualizado automaticamente
    Cancelado   = 3
}
