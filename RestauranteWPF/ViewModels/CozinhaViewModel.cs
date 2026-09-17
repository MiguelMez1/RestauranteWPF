using System.Collections.ObjectModel;
using RestauranteWPF.Helpers;
using RestauranteWPF.Models;
using RestauranteWPF.Services;

namespace RestauranteWPF.ViewModels;

/// <summary>
/// Lógica da Tela da Cozinha:
/// recebe pedidos pelo Realtime, organiza por status e altera o status.
/// </summary>
public class CozinhaViewModel : ObservableObject
{
    // O garçom grava primeiro o pedido e DEPOIS os itens. Por isso o evento
    // INSERT do pedido pode chegar antes dos itens estarem no banco.
    // Nesse caso, tentamos buscar os itens mais algumas vezes.
    private const int TentativasBuscarItens = 6;
    private const int EsperaEntreTentativasMs = 500;

    private readonly SupabaseService _servico;

    // Todos os pedidos exibidos (id → card). Evita cards duplicados.
    private readonly Dictionary<long, PedidoCardViewModel> _pedidos = new();

    // Pedidos sendo montados agora (evento repetido do Realtime é ignorado)
    private readonly HashSet<long> _emProcessamento = new();

    // Status que chegaram enquanto o pedido ainda estava sendo montado
    private readonly Dictionary<long, string> _statusPendentes = new();

    private string _mensagem = string.Empty;

    public CozinhaViewModel(SupabaseService servico)
    {
        _servico = servico;

        // Realtime → thread da interface → métodos abaixo.
        // Todo acesso aos dicionários e coleções acontece na thread da UI,
        // então não há risco de duas threads mexerem neles ao mesmo tempo.
        _servico.PedidoInserido += pedido => UiThread.Executar(() => AoInserirPedidoAsync(pedido.Id));
        _servico.PedidoAtualizado += pedido => UiThread.Executar(() => AoAtualizarPedidoAsync(pedido));
    }

    // Uma coleção por coluna (pedidos separados por status)
    public ObservableCollection<PedidoCardViewModel> PedidosNovos { get; } = new();
    public ObservableCollection<PedidoCardViewModel> PedidosPreparando { get; } = new();
    public ObservableCollection<PedidoCardViewModel> PedidosProntos { get; } = new();

    /// <summary>Mensagem de erro exibida no topo da tela (vazia = sem erro).</summary>
    public string Mensagem
    {
        get => _mensagem;
        private set => SetProperty(ref _mensagem, value);
    }

    // ------------------------------------------------------------------
    // Carga inicial
    // ------------------------------------------------------------------

    /// <summary>Carrega os pedidos que já estavam em andamento ao abrir a tela.</summary>
    public async Task CarregarAsync()
    {
        try
        {
            var pedidos = await _servico.BuscarPedidosEmAndamentoAsync();

            // Busca todos os itens de uma vez e agrupa por pedido
            var itens = await _servico.BuscarItensDeVariosPedidosAsync(pedidos.Select(p => p.Id));
            var itensPorPedido = itens
                .GroupBy(i => i.PedidoId)
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var pedido in pedidos)
            {
                // Já exibido ou sendo montado pelo Realtime → ignora
                if (_pedidos.ContainsKey(pedido.Id) || _emProcessamento.Contains(pedido.Id))
                    continue;

                // Pedido sem itens não é exibido
                if (!itensPorPedido.TryGetValue(pedido.Id, out var itensDoPedido))
                    continue;

                AdicionarCard(pedido, itensDoPedido);
            }
        }
        catch (Exception ex)
        {
            Mensagem = $"Erro ao carregar pedidos: {ex.Message}";
        }
    }

    // ------------------------------------------------------------------
    // Eventos do Realtime
    // ------------------------------------------------------------------

    /// <summary>
    /// Novo pedido recebido pelo Realtime:
    /// identifica → busca dados → busca itens → monta o card → exibe.
    /// </summary>
    private async Task AoInserirPedidoAsync(long pedidoId)
    {
        // Proteção contra duplicação: já exibido OU já em processamento
        if (_pedidos.ContainsKey(pedidoId) || !_emProcessamento.Add(pedidoId))
            return;

        try
        {
            // Busca os dados atualizados do pedido
            var pedido = await _servico.BuscarPedidoAsync(pedidoId);
            if (pedido == null || pedido.Status == StatusPedido.Entregue)
                return;

            // Busca os itens (com novas tentativas, se ainda não existirem)
            var itens = await BuscarItensComEsperaAsync(pedidoId);
            if (itens.Count == 0)
                return; // pedido sem itens (ex.: envio que falhou e foi desfeito)

            AdicionarCard(pedido, itens);
            Mensagem = string.Empty;
        }
        catch (Exception ex)
        {
            Mensagem = $"Erro ao receber o pedido #{pedidoId}: {ex.Message}";
        }
        finally
        {
            _emProcessamento.Remove(pedidoId);
        }
    }

    /// <summary>Status alterado (por esta ou por outra tela conectada).</summary>
    private async Task AoAtualizarPedidoAsync(Pedido pedido)
    {
        // Pedido já exibido → atualiza status e muda de coluna
        if (_pedidos.TryGetValue(pedido.Id, out var card))
        {
            if (card.Status != pedido.Status)
            {
                card.Status = pedido.Status;
                PosicionarCard(card);
            }
            return;
        }

        // Pedido ainda sendo montado → guarda o status para aplicar depois
        if (_emProcessamento.Contains(pedido.Id))
        {
            _statusPendentes[pedido.Id] = pedido.Status;
            return;
        }

        // Pedido que esta tela ainda não conhecia → trata como novo
        if (pedido.Status != StatusPedido.Entregue)
            await AoInserirPedidoAsync(pedido.Id);
    }

    private async Task<List<PedidoItem>> BuscarItensComEsperaAsync(long pedidoId)
    {
        for (int tentativa = 1; tentativa <= TentativasBuscarItens; tentativa++)
        {
            var itens = await _servico.BuscarItensAsync(pedidoId);
            if (itens.Count > 0)
                return itens;

            await Task.Delay(EsperaEntreTentativasMs);
        }

        return new List<PedidoItem>();
    }

    // ------------------------------------------------------------------
    // Alteração de status (botão do card)
    // ------------------------------------------------------------------

    /// <summary>
    /// NOVO → PREPARANDO → PRONTO → ENTREGUE.
    /// Grava no Supabase; o Realtime avisa as outras telas.
    /// </summary>
    private async Task AvancarStatusAsync(PedidoCardViewModel card)
    {
        var proximo = card.ProximoStatus;
        if (proximo == null)
            return;

        try
        {
            await _servico.AtualizarStatusAsync(card.Id, proximo);

            // Atualiza já nesta tela, sem esperar o Realtime.
            // Se o evento do Realtime chegar antes, o status já estará igual e nada muda.
            if (card.Status != proximo)
            {
                card.Status = proximo;
                PosicionarCard(card);
            }

            Mensagem = string.Empty;
        }
        catch (Exception ex)
        {
            Mensagem = $"Não foi possível atualizar o pedido #{card.Id}: {ex.Message}";
        }
    }

    // ------------------------------------------------------------------
    // Organização dos cards nas colunas
    // ------------------------------------------------------------------

    private void AdicionarCard(Pedido pedido, IEnumerable<PedidoItem> itens)
    {
        // Converte os itens em textos: "2x X-Burger"
        var textos = itens.Select(i => $"{i.Quantidade}x {_servico.NomeDoProduto(i.ProdutoId)}");

        var card = new PedidoCardViewModel(pedido, textos, AvancarStatusAsync);
        _pedidos[pedido.Id] = card;

        // Aplica um status que chegou enquanto o pedido era montado
        if (_statusPendentes.Remove(pedido.Id, out var statusPendente))
            card.Status = statusPendente;

        PosicionarCard(card);
    }

    /// <summary>
    /// Coloca o card na coluna do seu status (em ordem de ID).
    /// Pedidos ENTREGUES saem da tela da cozinha.
    /// </summary>
    private void PosicionarCard(PedidoCardViewModel card)
    {
        PedidosNovos.Remove(card);
        PedidosPreparando.Remove(card);
        PedidosProntos.Remove(card);

        ObservableCollection<PedidoCardViewModel>? destino = card.Status switch
        {
            StatusPedido.Novo => PedidosNovos,
            StatusPedido.Preparando => PedidosPreparando,
            StatusPedido.Pronto => PedidosProntos,
            _ => null
        };

        if (destino == null)
        {
            _pedidos.Remove(card.Id); // entregue: some da cozinha
            return;
        }

        // Mais antigos primeiro (ordem de chegada)
        int posicao = 0;
        while (posicao < destino.Count && destino[posicao].Id < card.Id)
            posicao++;
        destino.Insert(posicao, card);
    }
}
