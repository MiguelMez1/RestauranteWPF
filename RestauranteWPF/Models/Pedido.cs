using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace RestauranteWPF.Models;

/// <summary>
/// Representa uma linha da tabela "pedidos".
/// </summary>
[Table("pedidos")]
public class Pedido : BaseModel
{
    // O ID é gerado pelo PostgreSQL (identity), por isso não é enviado no INSERT.
    [PrimaryKey("id", false)]
    public long Id { get; set; }

    [Column("mesa")]
    public int Mesa { get; set; }

    // Todo pedido nasce com o status NOVO.
    [Column("status")]
    public string Status { get; set; } = StatusPedido.Novo;

    // A data/hora é preenchida pelo banco (default now()).
    // ignoreOnInsert/ignoreOnUpdate: o C# nunca envia esse campo, só lê.
    [Column("criado_em", ignoreOnInsert: true, ignoreOnUpdate: true)]
    public DateTimeOffset CriadoEm { get; set; }
}
