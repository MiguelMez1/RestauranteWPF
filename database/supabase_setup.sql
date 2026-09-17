-- =====================================================================
-- SISTEMA DE RESTAURANTE - SCRIPT DO BANCO (Supabase / PostgreSQL)
-- Execute este script inteiro no Supabase: SQL Editor > New query > Run
-- O script pode ser executado mais de uma vez sem causar erros.
-- =====================================================================


-- ---------------------------------------------------------------------
-- 1. TABELA PRODUTOS
-- Guarda os produtos vendidos pelo restaurante.
-- "generated always as identity" = o próprio PostgreSQL gera o ID
-- usando uma sequência numérica (1, 2, 3...).
-- ---------------------------------------------------------------------
create table if not exists public.produtos (
    id     bigint generated always as identity primary key,
    nome   text           not null,
    preco  numeric(10, 2) not null check (preco >= 0),
    ativo  boolean        not null default true
);


-- ---------------------------------------------------------------------
-- 2. TABELA PEDIDOS
-- Cada linha é um pedido feito pelo garçom.
-- - id: gerado pelo banco (Pedido #1, #2, #3...)
-- - mesa: precisa ser maior que zero
-- - status: começa como NOVO e só aceita os 4 status do sistema
-- - criado_em: data/hora preenchida automaticamente pelo banco
-- ---------------------------------------------------------------------
create table if not exists public.pedidos (
    id         bigint generated always as identity primary key,
    mesa       integer     not null check (mesa > 0),
    status     text        not null default 'NOVO'
               check (status in ('NOVO', 'PREPARANDO', 'PRONTO', 'ENTREGUE')),
    criado_em  timestamptz not null default now()
);


-- ---------------------------------------------------------------------
-- 3. TABELA PEDIDO_ITENS
-- Liga os produtos aos pedidos (relacionamento N:N).
-- - Um pedido pode ter vários itens.
-- - Um produto pode aparecer em vários pedidos.
-- "on delete cascade": se um pedido for apagado, os itens dele também são.
-- ---------------------------------------------------------------------
create table if not exists public.pedido_itens (
    id          bigint  generated always as identity primary key,
    pedido_id   bigint  not null references public.pedidos (id) on delete cascade,
    produto_id  bigint  not null references public.produtos (id),
    quantidade  integer not null check (quantidade > 0)
);

-- Índice para deixar rápida a busca "itens do pedido X".
create index if not exists idx_pedido_itens_pedido_id
    on public.pedido_itens (pedido_id);


-- ---------------------------------------------------------------------
-- 4. PRODUTOS DE DEMONSTRAÇÃO (10 produtos)
-- Só insere se a tabela estiver vazia. A coluna "ordem" garante que os
-- IDs fiquem de 1 a 10 nessa sequência — o app usa o ID para achar a
-- imagem do produto (Imagens/1.png, Imagens/2.png...).
-- ---------------------------------------------------------------------
insert into public.produtos (nome, preco)
select v.nome, v.preco
from (values
    (1,  'X-Burger',     25.90),
    (2,  'X-Salada',     24.90),
    (3,  'X-Bacon',      28.90),
    (4,  'Hambúrguer',   19.90),
    (5,  'Batata Frita', 12.00),
    (6,  'Nuggets',      16.90),
    (7,  'Pizza',        45.00),
    (8,  'Coca-Cola',     6.00),
    (9,  'Suco',          8.50),
    (10, 'Água',          4.00)
) as v(ordem, nome, preco)
where not exists (select 1 from public.produtos)
order by v.ordem;


-- ---------------------------------------------------------------------
-- 5. PERMISSÕES
-- O app usa a chave "anon" (pública). Aqui liberamos só o necessário:
-- - produtos: leitura
-- - pedidos: leitura, criação, atualização de status e exclusão
--   (a exclusão é usada só para desfazer um pedido cujos itens falharam)
-- - pedido_itens: leitura e criação
-- ---------------------------------------------------------------------
grant usage on schema public to anon;
grant select                          on public.produtos     to anon;
grant select, insert, update, delete  on public.pedidos      to anon;
grant select, insert                  on public.pedido_itens to anon;
grant usage, select on all sequences in schema public to anon;


-- ---------------------------------------------------------------------
-- 6. RLS (Row Level Security)
-- O Supabase recomenda deixar o RLS ligado. As políticas abaixo liberam
-- o acesso para a chave anon. ATENÇÃO: isso é adequado para um projeto
-- acadêmico sem login; em produção seria preciso autenticação.
-- O Realtime também respeita essas políticas: sem a política de SELECT
-- em "pedidos", a tela da cozinha não recebe os eventos.
-- ---------------------------------------------------------------------
alter table public.produtos     enable row level security;
alter table public.pedidos      enable row level security;
alter table public.pedido_itens enable row level security;

drop policy if exists "anon_le_produtos" on public.produtos;
create policy "anon_le_produtos" on public.produtos
    for select to anon using (true);

drop policy if exists "anon_le_pedidos" on public.pedidos;
create policy "anon_le_pedidos" on public.pedidos
    for select to anon using (true);

drop policy if exists "anon_cria_pedidos" on public.pedidos;
create policy "anon_cria_pedidos" on public.pedidos
    for insert to anon with check (true);

drop policy if exists "anon_atualiza_pedidos" on public.pedidos;
create policy "anon_atualiza_pedidos" on public.pedidos
    for update to anon using (true) with check (true);

drop policy if exists "anon_remove_pedidos" on public.pedidos;
create policy "anon_remove_pedidos" on public.pedidos
    for delete to anon using (true);

drop policy if exists "anon_le_itens" on public.pedido_itens;
create policy "anon_le_itens" on public.pedido_itens
    for select to anon using (true);

drop policy if exists "anon_cria_itens" on public.pedido_itens;
create policy "anon_cria_itens" on public.pedido_itens
    for insert to anon with check (true);


-- ---------------------------------------------------------------------
-- 7. SUPABASE REALTIME
-- Adiciona a tabela "pedidos" à publicação do Realtime. Sem isso,
-- INSERT e UPDATE não são enviados para as telas conectadas.
-- ---------------------------------------------------------------------
do $$
begin
    if not exists (
        select 1
        from pg_publication_tables
        where pubname = 'supabase_realtime'
          and schemaname = 'public'
          and tablename = 'pedidos'
    ) then
        alter publication supabase_realtime add table public.pedidos;
    end if;
end $$;


-- ---------------------------------------------------------------------
-- (OPCIONAL) Zerar os pedidos de teste e reiniciar a numeração em #1.
-- Descomente a linha abaixo e execute quando quiser limpar os testes.
-- ---------------------------------------------------------------------
-- truncate table public.pedido_itens, public.pedidos restart identity;
