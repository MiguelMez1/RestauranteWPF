using System.Diagnostics;
using RestauranteWPF.Models;
using static Supabase.Postgrest.Constants;
using static Supabase.Realtime.PostgresChanges.PostgresChangesOptions;

namespace RestauranteWPF.Services;

/// <summary>
/// Camada ÚNICA de comunicação com o Supabase.
/// Nenhuma tela acessa o banco diretamente: tudo passa por aqui.
///
/// Responsabilidades:
///  - Conectar ao Supabase;
///  - Ler produtos, pedidos e itens;
///  - Criar pedidos e atualizar status;
///  - Escutar o Realtime e avisar as telas por meio de eventos C#.
/// </summary>
public class SupabaseService
{
    private readonly Supabase.Client _cliente;

    // Canal do Realtime que escuta a tabela "pedidos".
    private Supabase.Realtime.RealtimeChannel? _canalPedidos;

    // Cache de produtos (id → produto). Usado pela cozinha para mostrar
    // o nome do produto sem precisar consultar o banco a cada item.
    private Dictionary<long, Produto> _produtosPorId = new();

    /// <summary>Produtos ativos, na ordem do ID (usados nos cards do garçom).</summary>
    public IReadOnlyList<Produto> ProdutosAtivos { get; private set; } = Array.Empty<Produto>();

    /// <summary>
    /// Disparado quando o Realtime avisa que um pedido foi INSERIDO.
    /// Atenção: roda em uma thread de fundo, não na thread da interface.
    /// </summary>
    public event Action<Pedido>? PedidoInserido;

    /// <summary>
    /// Disparado quando o Realtime avisa que um pedido foi ATUALIZADO (status).
    /// Também roda em thread de fundo.
    /// </summary>
    public event Action<Pedido>? PedidoAtualizado;

    public SupabaseService(string url, string chave)
    {
        var opcoes = new Supabase.SupabaseOptions
        {
            // Conecta o WebSocket do Realtime automaticamente ao inicializar.
            AutoConnectRealtime = true
        };

        _cliente = new Supabase.Client(url, chave, opcoes);
    }

    // =================================================================
    // INICIALIZAÇÃO
    // =================================================================

    /// <summary>
    /// Conecta ao Supabase, carrega os produtos e começa a escutar o Realtime.
    /// Deve ser chamado uma única vez, ao abrir o programa.
    /// </summary>
    public async Task InicializarAsync()
    {
        await _cliente.InitializeAsync();
        await CarregarProdutosAsync();

        // O Realtime é ligado ANTES das telas carregarem os pedidos,
        // assim nenhum pedido criado nesse meio tempo é perdido.
        await IniciarRealtimeAsync();
    }

    /// <summary>Encerra o Realtime ao fechar o programa.</summary>
    public void Encerrar()
    {
        try
        {
            _canalPedidos?.Unsubscribe();
            _cliente.Realtime.Disconnect();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Erro ao encerrar Realtime: {ex.Message}");
        }
    }

    // =================================================================
    // PRODUTOS
    // =================================================================

    private async Task CarregarProdutosAsync()
    {
        // SELECT * FROM produtos ORDER BY id
        var resposta = await _cliente
            .From<Produto>()
            .Order("id", Ordering.Ascending)
            .Get();

        // Guarda TODOS no cache (a cozinha precisa do nome mesmo de produtos inativos)
        _produtosPorId = resposta.Models.ToDictionary(p => p.Id);

        // Para o garçom, só os ativos
        ProdutosAtivos = resposta.Models.Where(p => p.Ativo).ToList();
    }

    /// <summary>Retorna o nome do produto pelo ID usando o cache.</summary>
    public string NomeDoProduto(long produtoId) =>
        _produtosPorId.TryGetValue(produtoId, out var produto)
            ? produto.Nome
            : $"Produto #{produtoId}";

    // =================================================================
    // PEDIDOS - CRIAÇÃO
    // =================================================================

    /// <summary>
    /// Cria um pedido completo:
    ///  1. INSERT em "pedidos" (o banco gera ID, data/hora e status NOVO);
    ///  2. Lê o ID gerado;
    ///  3. INSERT de todos os itens em "pedido_itens".
    /// Se os itens falharem, o pedido é apagado para não existir pedido sem itens.
    /// </summary>
    /// <returns>ID gerado pelo banco para o pedido.</returns>
    public async Task<long> CriarPedidoAsync(
        int mesa,
        IReadOnlyCollection<(long ProdutoId, int Quantidade)> itens)
    {
        // Validações de segurança (a tela também valida, mas o serviço não confia só nela)
        if (mesa <= 0)
            throw new ArgumentException("Número de mesa inválido.", nameof(mesa));
        if (itens.Count == 0)
            throw new ArgumentException("O pedido precisa ter pelo menos um item.", nameof(itens));
        if (itens.Any(i => i.Quantidade <= 0))
            throw new ArgumentException("Quantidade inválida em um dos itens.", nameof(itens));

        // --- Passo 1: cria o pedido. O ID NÃO é informado aqui. ---
        var novoPedido = new Pedido { Mesa = mesa, Status = StatusPedido.Novo };
        var respostaPedido = await _cliente.From<Pedido>().Insert(novoPedido);

        // --- Passo 2: o Supabase devolve a linha criada, já com o ID gerado ---
        var pedidoCriado = respostaPedido.Models.FirstOrDefault()
            ?? throw new InvalidOperationException("O banco não retornou o pedido criado.");
        long pedidoId = pedidoCriado.Id;

        // --- Passo 3: grava os itens ligados a esse ID ---
        try
        {
            var registros = itens
                .Select(i => new PedidoItem
                {
                    PedidoId = pedidoId,
                    ProdutoId = i.ProdutoId,
                    Quantidade = i.Quantidade
                })
                .ToList();

            // Um único INSERT com todos os itens
            await _cliente.From<PedidoItem>().Insert(registros);
        }
        catch
        {
            // Desfaz o pedido "órfão" (sem itens). Se até isso falhar, só registra.
            try
            {
                await _cliente.From<Pedido>().Where(p => p.Id == pedidoId).Delete();
            }
            catch (Exception exDelete)
            {
                Debug.WriteLine($"Não foi possível remover o pedido #{pedidoId}: {exDelete.Message}");
            }
            throw; // repassa o erro original para a tela mostrar ao garçom
        }

        return pedidoId;
    }

    // =================================================================
    // PEDIDOS - CONSULTAS
    // =================================================================

    /// <summary>Busca um pedido pelo ID (ou null se não existir).</summary>
    public async Task<Pedido?> BuscarPedidoAsync(long pedidoId)
    {
        var resposta = await _cliente
            .From<Pedido>()
            .Where(p => p.Id == pedidoId)
            .Get();

        return resposta.Models.FirstOrDefault();
    }

    /// <summary>Busca todos os pedidos que ainda não foram entregues.</summary>
    public async Task<List<Pedido>> BuscarPedidosEmAndamentoAsync()
    {
        var resposta = await _cliente
            .From<Pedido>()
            .Where(p => p.Status != StatusPedido.Entregue)
            .Order("id", Ordering.Ascending)
            .Get();

        return resposta.Models;
    }

    /// <summary>Busca os itens de UM pedido.</summary>
    public async Task<List<PedidoItem>> BuscarItensAsync(long pedidoId)
    {
        var resposta = await _cliente
            .From<PedidoItem>()
            .Where(i => i.PedidoId == pedidoId)
            .Order("id", Ordering.Ascending)
            .Get();

        return resposta.Models;
    }

    /// <summary>
    /// Busca os itens de VÁRIOS pedidos em uma única consulta
    /// (WHERE pedido_id IN (...)). Usado na carga inicial da cozinha.
    /// </summary>
    public async Task<List<PedidoItem>> BuscarItensDeVariosPedidosAsync(IEnumerable<long> pedidoIds)
    {
        var ids = pedidoIds.Cast<object>().ToList();
        if (ids.Count == 0)
            return new List<PedidoItem>();

        var resposta = await _cliente
            .From<PedidoItem>()
            .Filter("pedido_id", Operator.In, ids)
            .Order("id", Ordering.Ascending)
            .Get();

        return resposta.Models;
    }

    // =================================================================
    // PEDIDOS - ATUALIZAÇÃO DE STATUS
    // =================================================================

    /// <summary>
    /// UPDATE pedidos SET status = novoStatus WHERE id = pedidoId.
    /// O Realtime avisará todas as telas conectadas sobre essa mudança.
    /// </summary>
    public async Task AtualizarStatusAsync(long pedidoId, string novoStatus)
    {
        await _cliente
            .From<Pedido>()
            .Where(p => p.Id == pedidoId)
            .Set(p => p.Status, novoStatus)
            .Update();
    }

    // =================================================================
    // SUPABASE REALTIME
    // =================================================================

    /// <summary>
    /// Assina as mudanças da tabela "pedidos".
    /// Fluxo: INSERT/UPDATE no banco → Supabase Realtime → este método → evento C# → telas.
    /// </summary>
    private async Task IniciarRealtimeAsync()
    {
        if (_canalPedidos != null)
            return; // já está escutando

        // Canal ligado ao schema "public", tabela "pedidos"
        _canalPedidos = _cliente.Realtime.Channel("realtime", "public", "pedidos");

        // Novo pedido criado (INSERT)
        _canalPedidos.AddPostgresChangeHandler(ListenType.Inserts, (_, mudanca) =>
        {
            try
            {
                // Converte o registro recebido para o modelo Pedido
                var pedido = mudanca.Model<Pedido>();
                if (pedido != null)
                    PedidoInserido?.Invoke(pedido);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Erro ao ler INSERT do Realtime: {ex.Message}");
            }
        });

        // Pedido alterado (UPDATE de status)
        _canalPedidos.AddPostgresChangeHandler(ListenType.Updates, (_, mudanca) =>
        {
            try
            {
                var pedido = mudanca.Model<Pedido>();
                if (pedido != null)
                    PedidoAtualizado?.Invoke(pedido);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Erro ao ler UPDATE do Realtime: {ex.Message}");
            }
        });

        // Efetiva a assinatura no servidor
        await _canalPedidos.Subscribe();
    }
}
