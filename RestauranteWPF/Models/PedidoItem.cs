using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace RestauranteWPF.Models;

/// <summary>
/// Representa uma linha da tabela "pedido_itens":
/// qual produto, em qual quantidade, pertence a qual pedido.
/// </summary>
[Table("pedido_itens")]
public class PedidoItem : BaseModel
{
    [PrimaryKey("id", false)]
    public long Id { get; set; }

    // Chave estrangeira para pedidos.id
    [Column("pedido_id")]
    public long PedidoId { get; set; }

    // Chave estrangeira para produtos.id
    [Column("produto_id")]
    public long ProdutoId { get; set; }

    [Column("quantidade")]
    public int Quantidade { get; set; }
}
