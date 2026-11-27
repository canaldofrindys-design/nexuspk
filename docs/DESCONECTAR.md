# Desconectar personagem travado

O jogador abre o painel (`/painel`, janela "Personagem travado") e pede para desconectar a própria conta. Serve para o caso
clássico: o jogo responde que a conta já está conectada, mas ninguém está jogando (queda de internet, cliente fechado à força).

## Por que precisa do servidor do jogo

Não existe no banco nenhum jeito de derrubar uma conta. Quem guarda "esta conta está no jogo" é a memória do servidor:

- `accountdb.<letra>GameUser.inuse` e `ClanDB.dbo.CT` não voltam a 0 quando o jogador sai (conferido em 11/11/2026), então
  mexer neles não solta ninguém;
- `GameLogDB.GameOnRecord`/`GameOutRecord` pararam em 2022;
- as procedures `usp_<letra>Login` não olham estado de conexão.

Por isso o portal só **grava o pedido** e o servidor do jogo **atende**, do mesmo jeito que já faz com o Box Donate
(`NexusPKAdmin.dbo.NexusDonatePackages` + sinal de vida em `NexusDonateRuntime`). Nada no banco do jogo muda.

Enquanto o servidor não tiver a parte dele, o botão aparece desligado no painel com o aviso "O servidor do jogo não está
atendendo pedidos de desconexão agora".

## Tabelas (PortalDB, migração `0013_game_disconnect.sql`)

`dbo.GameDisconnectRequest`, um pedido por linha:

| Coluna | Tipo | Quem grava | Significado |
|---|---|---|---|
| `Id` | bigint identity | portal | ordem dos pedidos |
| `GameLogin` | varchar(32) | portal | conta (`userid` do `<letra>GameUser`), já conferida pela sessão do painel |
| `RequestedUtc` | datetime2(0) | portal | hora do pedido, **UTC** |
| `RequestedIp` | varchar(45) | portal | IP de quem pediu (só para o suporte) |
| `Status` | varchar(16) | portal → servidor | `pending` → `done`, `not-online` ou `failed` |
| `ProcessedUtc` | datetime2(0) | servidor | hora da resposta, UTC (`SYSUTCDATETIME()`) |
| `ServerNote` | nvarchar(200) | servidor | opcional: texto curto para o suporte (ex.: "socket já fechado, removido da lista") |

`dbo.GameServerRuntime`, linha `Feature = 'disconnect'` (já criada pela migração):

| Coluna | Quem grava | Significado |
|---|---|---|
| `ProtocolVersion` | servidor | `1` (este documento) |
| `LastSeenUtc` | servidor | última vez que o servidor leu a fila, UTC |

O painel só liga o botão com `ProtocolVersion >= 1` e `LastSeenUtc` de menos de 2 minutos.

## O que o servidor do jogo precisa fazer (protocolo 1)

Numa thread separada (ou no mesmo laço do distribuidor de donate), a cada **5 segundos**:

```sql
-- 1) Sinal de vida
UPDATE PortalDB.dbo.GameServerRuntime
   SET ProtocolVersion = 1, LastSeenUtc = SYSUTCDATETIME()
 WHERE Feature = 'disconnect';

-- 2) Pedidos pendentes recentes (os mais antigos que 2 minutos o portal já mostra como expirados: ignore)
SELECT TOP (20) Id, GameLogin
  FROM PortalDB.dbo.GameDisconnectRequest
 WHERE Status = 'pending' AND RequestedUtc >= DATEADD(MINUTE, -2, SYSUTCDATETIME())
 ORDER BY Id;
```

Para cada pedido, **na thread principal do servidor** (onde a lista de jogadores é mexida; passe o login por uma fila):

1. Procure a conta na lista de jogadores conectados comparando o `szID`/`userid` **sem diferenciar maiúsculas**.
2. Achou: faça o mesmo que o servidor faz quando o jogador sai normalmente (salvar o personagem no `.dat`, liberar a troca,
   o clã e o que mais o logout normal libera) e feche a conexão. Se o socket já estiver morto e só a entrada da lista
   sobrou (o caso "travado"), remova a entrada do mesmo jeito. Resposta `done`.
3. Não achou: resposta `not-online` (a conta não está presa; o jogador já pode entrar).
4. Achou e não conseguiu (erro ao salvar, por exemplo): resposta `failed`, com o motivo em `ServerNote`. Não desconecte sem
   salvar.

E grave a resposta, sempre com a condição `Status = 'pending'` para nunca sobrescrever outra resposta:

```sql
UPDATE PortalDB.dbo.GameDisconnectRequest
   SET Status = ?, ProcessedUtc = SYSUTCDATETIME(), ServerNote = ?
 WHERE Id = ? AND Status = 'pending';
```

Observações:

- Use parâmetros (`?`) nas consultas, nunca o login concatenado no texto.
- Em servidor com mais de um processo (login, game, área), quem segura a conta é quem deve responder. Se o login server
  também guarda a conta, ele precisa soltar junto, senão o jogador continua recebendo "conta em uso".
- Se a conta estiver conectada de verdade (alguém jogando em outro PC), ela cai e o personagem é salvo. O painel avisa isso
  ao jogador antes do botão. Só o dono da conta (com a senha do painel) consegue pedir.

## Permissões

Se o servidor do jogo conecta com `sa` (como as páginas antigas do clã), não precisa fazer nada. Com um login próprio,
rode `database/scripts/permissoes-servidor-desconectar.sql` (no pacote: `deploy\sql\`) trocando `<LOGIN-DO-SERVIDOR>`: ele dá `SELECT` nas duas
tabelas e `UPDATE` só nas colunas de resposta e de sinal de vida.

## Limites do lado do portal

- Só a própria conta, pela sessão do painel. Nenhum dado de outra conta aparece.
- Um pedido pendente por vez e no máximo 5 por hora por conta; além disso, o limite geral de 10 requisições por minuto por IP.
- Cada pedido vai para `PortalDB.dbo.AuditLog` (`player.game.disconnect-requested`, com conta e IP).
- `GET /api/v1/player/disconnect` devolve a situação; `POST /api/v1/player/disconnect` grava o pedido (`202`), ou responde
  `409` (já há um pendente), `429` (limite por hora) ou `503` (servidor sem sinal).

## Testar depois que o servidor tiver a parte dele

1. Entre no jogo com uma conta de teste e feche o cliente pelo Gerenciador de Tarefas (a conta fica "em uso").
2. No painel do site, clique em "Desconectar minha conta do jogo". Em até 10 segundos a janela mostra "conta desconectada".
3. Entre no jogo de novo: deve entrar direto, com o personagem como estava no último salvamento.
4. Clique de novo com a conta fora do jogo: a resposta deve ser "a conta não estava presa no servidor".
