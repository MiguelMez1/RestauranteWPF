using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace RestauranteWPF.Models;

/// <summary>
/// Representa uma linha da tabela "produtos".
/// Herdar de BaseModel permite que a biblioteca do Supabase
/// converta automaticamente o JSON do banco para este objeto.
/// </summary>
[Table("produtos")]
public class Produto : BaseModel
{
    // [PrimaryKey("id", false)]: "false" = não enviar o ID ao inserir,
    // pois quem gera o ID é o banco.
    [PrimaryKey("id", false)]
    public long Id { get; set; }

    // [Column] liga a propriedade C# à coluna do banco.
    [Column("nome")]
    public string Nome { get; set; } = string.Empty;

    [Column("preco")]
    public decimal Preco { get; set; }

    [Column("ativo")]
    public bool Ativo { get; set; }
}
