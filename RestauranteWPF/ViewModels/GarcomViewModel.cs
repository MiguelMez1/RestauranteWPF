using System.Collections.ObjectModel;
using RestauranteWPF.Helpers;
using RestauranteWPF.Models;
using RestauranteWPF.Services;

namespace RestauranteWPF.ViewModels;

/// <summary>
/// Lógica da Tela do Garçom:
/// mesa, cards de produtos, resumo, total, envio e acompanhamento dos pedidos.
/// </summary>
public class GarcomViewModel : ObservableObject
{
    public const int MesaMinima = 1;
    public const int MesaMaxima = 100;
    private const int QuantidadeDeCards = 10;

    private readonly SupabaseService _servico;

    // Pedidos em andamento exibidos na lateral (id → card), para evitar duplicados
    private readonly Dictionary<long, PedidoCardViewModel> _acompanhamento = new();

    private string _mesaTexto = string.Empty;
    private string _mensagem = string.Empty;
    private bool _mensagemEhErro;
    private bool _enviando;

    public GarcomViewModel(SupabaseService servico)
    {
        _servico = servico;
        EnviarPedidoCommand = new AsyncRelayCommand(EnviarPedidoAsync);

        // Realtime: quando qualquer tela criar ou alterar um pedido,
        // a lista de acompanhamento do garçom é atualizada.
        // UiThread garante que a alteração ocorra na thread da interface.
        _servico.PedidoInserido += pedido => UiThread.Executar(() => AtualizarAcompanhamento(pedido));
        _servico.PedidoAtualizado += pedido => UiThread.Executar(() => AtualizarAcompanhamento(pedido));
    }

    // ------------------------------------------------------------------
    // Coleções ligadas à tela (ObservableCollection avisa o WPF sozinha)
    // ------------------------------------------------------------------

    /// <summary>Os 10 cards de produtos.</summary>
    public ObservableCollection<ProdutoCardViewModel> Produtos { get; } = new();

    /// <summary>Somente os produtos com quantidade > 0 (resumo do pedido).</summary>
    public ObservableCollection<ProdutoCardViewModel> ItensSelecionados { get; } = new();

    /// <summary>Pedidos ainda não entregues, com status em tempo real.</summary>
    public ObservableCollection<PedidoCardViewModel> PedidosEmAndamento { get; } = new();

    public AsyncRelayCommand EnviarPedidoCommand { get; }

    // ------------------------------------------------------------------
    // Propriedades exibidas na tela
    // ------------------------------------------------------------------

    /// <summary>Texto digitado no campo "Mesa".</summary>
    public string MesaTexto
    {
        get => _mesaTexto;
        set => SetProperty(ref _mesaTexto, value);
    }

    public string FaixaMesaTexto => $"(de {MesaMinima} a {MesaMaxima})";

    /// <summary>Soma dos subtotais de todos os produtos.</summary>
    public decimal Total => Produtos.Sum(p => p.Subtotal);

    public string Mensagem
    {
        get => _mensagem;
        private set => SetProperty(ref _mensagem, value);
    }

    /// <summary>true = mensagem vermelha (erro); false = verde (sucesso).</summary>
    public bool MensagemEhErro
    {
        get => _mensagemEhErro;
        private set => SetProperty(ref _mensagemEhErro, value);
    }

    public bool Enviando
    {
        get => _enviando;
        private set
        {
            if (SetProperty(ref _enviando, value))
                OnPropertyChanged(nameof(TextoBotaoEnviar));
        }
    }

    public string TextoBotaoEnviar => Enviando ? "ENVIANDO..." : "ENVIAR PEDIDO";

    // ------------------------------------------------------------------
    // Carga inicial
    // ------------------------------------------------------------------

    /// <summary>Monta os cards de produtos e carrega os pedidos em andamento.</summary>
    public async Task CarregarAsync()
    {
        try
        {
            // Cria um card para cada produto (no máximo 10)
            foreach (var produto in _servico.ProdutosAtivos.Take(QuantidadeDeCards))
            {
                var card = new ProdutoCardViewModel(produto);
                card.QuantidadeAlterada += (_, _) => AtualizarResumo();
                Produtos.Add(card);
            }

            if (Produtos.Count < QuantidadeDeCards)
                MostrarErro($"Foram encontrados {Produtos.Count} produtos ativos. Cadastre 10 na tabela produtos.");

            // Pedidos que já existiam antes de abrir o programa
            var pedidos = await _servico.BuscarPedidosEmAndamentoAsync();
            foreach (var pedido in pedidos)
            {
                // Se o Realtime já trouxe esse pedido, não sobrescreve
                if (!_acompanhamento.ContainsKey(pedido.Id))
                    AtualizarAcompanhamento(pedido);
            }
        }
        catch (Exception ex)
        {
            MostrarErro($"Erro ao carregar dados do banco: {ex.Message}");
        }
    }

    // ------------------------------------------------------------------
    // Resumo do pedido
    // ------------------------------------------------------------------

    /// <summary>Recalcula a lista do resumo e o total sempre que uma quantidade muda.</summary>
    private void AtualizarResumo()
    {
        ItensSelecionados.Clear();
        foreach (var produto in Produtos.Where(p => p.Quantidade > 0))
            ItensSelecionados.Add(produto);

        OnPropertyChanged(nameof(Total));

        // Ao mexer no pedido, some a mensagem de sucesso anterior
        if (!MensagemEhErro)
            Mensagem = string.Empty;
    }

    // ------------------------------------------------------------------
    // Envio do pedido
    // ------------------------------------------------------------------

    private async Task EnviarPedidoAsync()
    {
        Mensagem = string.Empty;

        // 1. Valida a mesa
        if (!TentarObterMesa(out int mesa, out string erroMesa))
        {
            MostrarErro(erroMesa);
            return;
        }

        // 2. Pega só os produtos com quantidade maior que zero
        var itens = Produtos
            .Where(p => p.Quantidade > 0)
            .Select(p => (ProdutoId: p.Produto.Id, Quantidade: p.Quantidade))
            .ToList();

        if (itens.Count == 0)
        {
            MostrarErro("Selecione pelo menos um produto antes de enviar.");
            return;
        }

        // 3. Envia ao Supabase (pedido + itens)
        Enviando = true;
        try
        {
            long pedidoId = await _servico.CriarPedidoAsync(mesa, itens);

            // 4. Limpa a tela para o próximo pedido
            LimparSelecao();
            MostrarSucesso($"Pedido #{pedidoId} da mesa {mesa} enviado para a cozinha.");
        }
        catch (Exception ex)
        {
            // Erro de conexão/banco: a seleção é mantida para o garçom tentar de novo
            MostrarErro($"Não foi possível enviar o pedido: {ex.Message}");
        }
        finally
        {
            Enviando = false;
        }
    }

    /// <summary>Valida o campo mesa: obrigatório, numérico e dentro da faixa.</summary>
    private bool TentarObterMesa(out int mesa, out string erro)
    {
        mesa = 0;
        erro = string.Empty;
        var texto = MesaTexto.Trim();

        if (string.IsNullOrEmpty(texto))
        {
            erro = "Informe o número da mesa.";
            return false;
        }

        if (!int.TryParse(texto, out mesa) || mesa < MesaMinima || mesa > MesaMaxima)
        {
            erro = $"Mesa inválida. Use um número de {MesaMinima} a {MesaMaxima}.";
            return false;
        }

        return true;
    }

    /// <summary>Zera as quantidades e o campo mesa após o envio.</summary>
    private void LimparSelecao()
    {
        foreach (var produto in Produtos)
            produto.Quantidade = 0; // cada mudança já atualiza o resumo

        MesaTexto = string.Empty;
    }

    private void MostrarErro(string texto)
    {
        MensagemEhErro = true;
        Mensagem = texto;
    }

    private void MostrarSucesso(string texto)
    {
        MensagemEhErro = false;
        Mensagem = texto;
    }

    // ------------------------------------------------------------------
    // Acompanhamento em tempo real
    // ------------------------------------------------------------------

    /// <summary>
    /// Adiciona, atualiza ou remove um pedido da lista lateral.
    /// Chamado na carga inicial e a cada evento do Realtime.
    /// </summary>
    private void AtualizarAcompanhamento(Pedido pedido)
    {
        // Pedido já está na lista → só atualiza (ou remove se foi entregue)
        if (_acompanhamento.TryGetValue(pedido.Id, out var existente))
        {
            if (pedido.Status == StatusPedido.Entregue)
            {
                PedidosEmAndamento.Remove(existente);
                _acompanhamento.Remove(pedido.Id);
            }
            else
            {
                existente.Status = pedido.Status;
            }
            return;
        }

        // Pedidos entregues não entram na lista
        if (pedido.Status == StatusPedido.Entregue)
            return;

        var card = new PedidoCardViewModel(pedido, Array.Empty<string>(), avancarStatus: null);
        _acompanhamento[pedido.Id] = card;

        // Mantém os pedidos mais recentes no topo
        int posicao = 0;
        while (posicao < PedidosEmAndamento.Count && PedidosEmAndamento[posicao].Id > pedido.Id)
            posicao++;
        PedidosEmAndamento.Insert(posicao, card);
    }
}
