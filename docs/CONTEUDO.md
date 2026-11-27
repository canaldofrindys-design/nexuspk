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

## Banner da home: eventos com contagem regressiva

A abertura da home é um banner grande que começa pelos eventos principais e depois passa pelas três cenas de apresentação.
Cada evento mostra a contagem até o início (dias, horas, minutos e segundos) e, enquanto acontece, "Acontecendo agora" com a contagem até o fim.
No computador, a lateral lista os próximos eventos com a contagem de cada um; no celular eles entram como cenas do banner.
Com "reduzir movimento" ligado não há troca automática nem animação dos números (a contagem continua certa).

- **Horários fixos da semana e bosses**: `/admin/eventos`, quadro "Horários fixos da semana e bosses". Nome, tipo (Bless Castle, SoD, Hellsgate, Boss mundial, PvP ou evento), dias da semana, um ou mais horários de Brasília separados por vírgula (`01:00, 09:00, 17:00`, até 24 por dia), duração em minutos (padrão 60), mapa ou local (opcional) e descrição. Até 20 eventos. Sem nada salvo, vale o Bless Castle aos domingos, 16:00. Chave `schedule`, migração `0012`. O mesmo horário do Bless Castle vale para o card "Bless Castle" e para o "Líder do Bless Castle" da home. Os do tipo Boss mundial formam a página pública `/bosses` (contagem regressiva e aviso no navegador; os outros fixos aparecem embaixo). O aviso usa a Notification API, fica guardado no navegador do jogador e só dispara com a página aberta em alguma aba.
- **Mix List**: `/admin/mix`. Texto de abertura e uma linha por fórmula: tipo de mix (ex.: "Mix comum"), equipamento, nome, sheltoms e o que dá. Até 200 fórmulas. A página pública `/mix` agrupa pelo tipo de mix, filtra por equipamento e busca por texto; vazia, avisa que a lista está sendo publicada. Chave `mix`, migração `0013`; leitura pública em `GET /api/v1/site/mix`.
- **Aging**: a tabela e a calculadora de sheltoms do guia `/guias/aging` vêm de `frontend/features/guides/aging.ts`.
- **Agenda**: os eventos cadastrados em `/admin/eventos` que estão abertos ou começam nos próximos 7 dias entram junto.
- O banner mostra os três mais próximos (os que estão acontecendo primeiro).

## Jornada do Herói (centro da home)

A faixa do meio da home mostra o servidor ao vivo, em três fileiras:

1. **Drops raros da semana** (últimos 7 dias; passar o mouse ou tocar num item mostra quem pegou e, quando houver, de qual boss; sem drops, voltam os itens que levam aos guias):
   - **Automáticos, sem cadastro**: o portal lê `ItemLogDB.dbo.IL` (linhas com `Flag = 0`, "pegou do chão"; outro valor em `GameDatabase:ItemPickupFlag`), sem GMs e sem as contas escondidas do ranking. Raro é o item pego no máximo 3 vezes nos últimos 30 dias; ouro (grupo 5 do ItemCode) e poções (grupo 4) nunca contam. O nome exato vem dos arquivos do servidor do jogo (`C:\ServidorPT\GameServer\OpenItem\*.txt`, linha `*Name`; pasta em `GameServer:ItemsFolder`), ligados ao ItemCode pela sigla de `ItemDB.dbo.ItemType` (arquivo `CA182` = sigla CA, Costume, item 82). Sem os arquivos, aparece o nome do tipo com o número ("Axe nº 61"); sem o banco, o grupo e o código ("Arma #01-3D"). O log não diz de qual boss caiu: o portal procura o item nas listas de drop dos monstros (`C:\ServidorPT\GameServer\Monster\*.inf`, pasta em `GameServer:MonstersFolder`) e mostra o boss quando só 1 ou 2 monstros derrubam aquele item. Precisa de `GRANT SELECT` em `ItemLogDB.dbo.IL` e `ItemDB.dbo.ItemType` para o login `portal_game_read` (está em `database/scripts/permissoes-portal.sql`). `/admin/banners` mostra os itens pegos no mês e quais contam como raros (só conferência).
   - **Anunciados à mão** (opcional), no mesmo lugar: item, arte, boss, personagem e quando caiu. Chave `drops`, migração `0010`.
2. **Patentes PvP**: as mesmas do jogo, lidas do servidor (`UserDB.dbo.BattlePoints`: `PontosPvP` e `IsReiPvP`), com a regra que o cliente usa: menos de 100 pontos PvP, sem patente; a cada 100 pontos sobe uma (Prata de Elite, Ouro Mestre, Guardião de Elite, Mestre Guardião, Guerreiro Lendário, Guerreiro de Elite) e com 700 ou mais é Mestre Supremo. Quem tem `IsReiPvP = 1` é o Rei PvP. Os ícones são os emblemas do próprio cliente (`game\patente\emblems`, em `frontend/public/kit/patentes`). Cada patente mostra quantos jogadores tem e o de mais pontos nela; a patente também aparece no ranking PvP e na página do personagem.
3. **Cards ao vivo**: próximo boss (evento da agenda com categoria `boss`, com contagem regressiva), evento de agora ou o próximo, líder da arena (1º em BP), dono do Bless Castle com a contagem até a próxima guerra e o maior clã (mais membros).
