# Sistema de Restaurante — WPF + C# + Supabase

Duas telas (Garçom e Cozinha) conectadas pelo **Supabase Realtime**: o pedido enviado pelo garçom aparece na cozinha sozinho, sem botão de atualizar.

## Como rodar

1. **Crie um projeto** em https://supabase.com.
2. **Crie o banco:** abra *SQL Editor → New query*, cole o conteúdo de `database/supabase_setup.sql` e clique em *Run*. Isso cria as 3 tabelas, os 10 produtos, as permissões e liga o Realtime na tabela `pedidos`.
3. **Configure a conexão:** em *Project Settings → API*, copie a *Project URL* e a chave **anon (public)** para `RestauranteWPF/SupabaseConfig.cs`.
4. **Abra** `RestauranteWPF.sln` no Visual Studio 2022 (com o workload *Desenvolvimento para desktop com .NET* e o .NET 8).
5. Aperte **F5**. O NuGet baixa o pacote `Supabase` e as duas telas abrem.

## Como testar o tempo real

- Informe a mesa, escolha produtos e clique em **ENVIAR PEDIDO** → o card aparece na coluna *Novos* da cozinha.
- Clique em **COMEÇAR PREPARO / FINALIZAR / MARCAR COMO ENTREGUE** → o card muda de coluna e o status também muda na lista "Pedidos em andamento" do garçom.
- Para provar que funciona entre computadores: rode o `.exe` duas vezes (ou em duas máquinas). A alteração feita em uma aparece na outra.

## Estrutura

```
RestauranteWPF/
├── Models/        Produto, Pedido, PedidoItem (tabelas) + StatusPedido
├── Services/      SupabaseService (único ponto de acesso ao banco e ao Realtime)
├── ViewModels/    GarcomViewModel, CozinhaViewModel e os ViewModels dos cards
├── Views/         GarcomWindow e CozinhaWindow (XAML)
├── Helpers/       ObservableObject, RelayCommand, UiThread (Dispatcher)
├── Imagens/       1.png … 10.png (imagem de cada produto pelo ID)
└── App.xaml       estilos e inicialização
```

## Decisões importantes

- **ID sequencial:** gerado pelo PostgreSQL (`generated always as identity`). O C# nunca envia o ID; ele lê o valor retornado pelo INSERT. Se um envio falhar no meio, a sequência pode "pular" um número — isso é comportamento normal do PostgreSQL.
- **Pedido sem itens:** se a gravação dos itens falhar, o pedido recém-criado é apagado.
- **Ordem dos eventos:** o evento INSERT do pedido chega antes dos itens estarem gravados. Por isso a cozinha tenta buscar os itens algumas vezes (500 ms entre tentativas) antes de montar o card.
- **Duplicação:** a cozinha guarda os IDs já exibidos/em processamento e ignora eventos repetidos.
- **Thread da interface:** os eventos do Realtime chegam em outra thread; `UiThread` usa o `Dispatcher` para atualizar as `ObservableCollection`.
- **Segurança:** as políticas RLS liberam a chave anon, o que é adequado para um projeto acadêmico sem login. Nunca use a chave `service_role` no aplicativo.

## Problemas comuns

| Sintoma | Verifique |
|---|---|
| "Não foi possível conectar" | URL e chave em `SupabaseConfig.cs` |
| Pedido é salvo, mas não aparece na cozinha | Se o passo 7 do SQL rodou (*Database → Publications → supabase_realtime* deve incluir `pedidos`) |
| "permission denied" / "row-level security" | Se as seções 5 e 6 do SQL rodaram |
| Card com "Sem imagem" | Se os arquivos estão em `Imagens/` com o ID do produto |
| Quero zerar os pedidos e voltar ao #1 | Última linha do SQL (comentada) |
