# Notícias e eventos (Fase 8)

Tudo fica no banco do portal (`PortalDB`, migração `0004_news_events.sql`). Nada aqui é lido do banco do jogo, e nada é executado dentro do jogo.

## Notícias (`dbo.News`)

- Categorias: `noticia`, `atualizacao`, `manutencao`, `evento`.
- Só aparece com `Status = 'published'` e `PublishedUtc` no passado. Uma data futura agenda a publicação.
- `Pinned = 1` mantém a notícia no topo da lista.
- `Slug` é o endereço (`/noticias/<slug>`): letras minúsculas, números e hífen.

## Eventos (`dbo.GameEvent`)

- Categorias: `exp`, `drop`, `boss`, `pvp`, `invasao`, `especial`, `comemorativo`, `gm`.
- `Status`: `draft` (oculto), `published` ou `cancelled` (continua visível, marcado como cancelado).
- Situação calculada pelo horário: agendado, em andamento, encerrado. Evento sem `EndsUtc` fica "em andamento" por 1 hora.
- `ExecutionMode` só aceita `manual`: o evento é conduzido pela equipe. Um modo automático só será criado se o servidor oferecer um comando ou API confiável para isso.
- Datas gravadas em UTC; o site mostra em horário de Brasília.
- Página do evento: contagem regressiva, regulamento, premiação, arquivo `.ics` e lembrete por e-mail.

## Lembretes (`dbo.GameEventReminder`)

O jogador logado, com e-mail confirmado, pede o lembrete na página do evento. O Worker verifica a cada 5 minutos e envia um e-mail para quem pediu, cerca de 1 hora antes do início. Cada lembrete é enviado uma vez.

## Formato do texto

Texto simples, sem HTML (qualquer marcação aparece como texto):

- linha em branco separa parágrafos;
- `## ` no começo da linha cria um subtítulo;
- linhas começando com `- ` viram lista.

## Cadastro

O painel administrativo (Fase 10) vai cadastrar e editar notícias e eventos. Até lá não há conteúdo publicado: o site mostra "Nenhuma notícia publicada" e "Nenhum evento agendado".
