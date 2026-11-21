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

Pelo painel da equipe em `/admin/noticias` e `/admin/eventos` (funções moderador, administrador e superadministrador). Os horários do formulário são de Brasília. O site mostra a alteração na hora. Ver `docs/ADMIN.md`.

## Jornada do Herói (centro da home)

A faixa do meio da home mostra o servidor ao vivo, em três fileiras:

1. **Drops raros da semana** (últimos 7 dias; passar o mouse ou tocar num item mostra quem pegou e, quando houver, de qual boss; sem drops, voltam os itens que levam aos guias):
   - **Automáticos, sem cadastro**: o portal lê `ItemLogDB.dbo.IL` (linhas com `Flag = 0`, "pegou do chão"; outro valor em `GameDatabase:ItemPickupFlag`), sem GMs e sem as contas escondidas do ranking. Raro é o item pego no máximo 3 vezes nos últimos 30 dias; ouro (grupo 5 do ItemCode) e poções (grupo 4) nunca contam. O nome vem de `ItemDB.dbo.ItemType` (nome do tipo pelo grupo+tipo do ItemCode, com o número do item: "Machado nº 61"); sem acesso, aparece o grupo e o código ("Arma #01-3D"). O log não diz de qual boss caiu. Precisa de `GRANT SELECT` em `ItemLogDB.dbo.IL` e `ItemDB.dbo.ItemType` para o login `portal_game_read` (está em `database/scripts/permissoes-portal.sql`). `/admin/banners` mostra os itens pegos no mês e quais contam como raros (só conferência).
   - **Anunciados à mão** (opcional), no mesmo lugar: item, arte, boss, personagem e quando caiu. Chave `drops`, migração `0010`.
2. **Elos PvP**: calculados sozinhos pelo ranking de BP (já sem equipe e contas escondidas). Pela posição: Grão-Mestre é o 1% do topo, Mestre até 4%, Diamante até 10%, Platina até 20%, Ouro até 40%, Prata até 70% e o resto Bronze. O 1º do ranking é sempre Grão-Mestre; empate em BP fica no mesmo elo. Cada elo mostra quantos jogadores tem e o melhor dele.
3. **Cards ao vivo**: próximo boss (evento da agenda com categoria `boss`, com contagem regressiva), evento de agora ou o próximo, líder da arena (1º em BP), dono do Bless Castle com a contagem até a próxima guerra e o maior clã (mais membros).
