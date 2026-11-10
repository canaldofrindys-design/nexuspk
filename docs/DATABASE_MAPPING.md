# DATABASE_MAPPING.md — Portal Priston Tale (NexusPK)

Versão 1 · 10/11/2026 · Fonte: `schema.txt` (extração somente leitura) + código do site antigo em `C:\wwwroot`.

Legenda de status:
- **Confirmado**: a tabela e as colunas existem no schema e o uso foi visto no código atual.
- **Existe, semântica a confirmar**: a tabela existe e tem dados, mas falta confirmar o que cada linha significa.
- **Sem fonte**: não existe no banco; o portal vai criar no banco próprio.

## 1. Ambiente

| Item | Valor |
| --- | --- |
| SQL Server | 2025 (RTM) 17.0.1000.7, **Express Edition** (64-bit) |
| Sistema | Windows Server 2022 Standard |
| Instância | `.\SQLEXPRESS` |
| Contas do jogo hoje | 8 (soma das tabelas `*GameUser`) |

Consequências:
- **Express limita cada banco a 10 GB** e não tem SQL Server Agent. Backups e tarefas do portal rodam pelo Worker (Quartz.NET) e pelo Agendador de Tarefas do Windows.
- `ServerDB` e `UserDB` estão em compatibility level 100 (SQL 2008). As consultas do portal rodam no contexto do banco do site e acessam os bancos do jogo por nome de três partes (`[ServerDB].dbo.LevelTable`).
- Collations diferentes: `accountdb`, `KLVN Studio`, `QuestDB`, `TimeShopDB` usam `Latin1_General_CI_AS`; os demais `SQL_Latin1_General_CP1_CI_AS`. JOINs entre bancos por login/nick precisam de `COLLATE` explícito.
- **Quase nenhuma tabela do jogo tem chave primária ou índice** (são heaps): `*GameUser`, `LevelTable`, `BattlePoints`, `CharGameRecordLg`, `GameOnRecord`, `GameOutRecord`, `UL`, `CL`, `Accounts`. Consultas nelas sempre varrem a tabela inteira. Por isso o portal lê em lotes pelo Worker e serve tudo de cache. Criar índices nessas tabelas só com sua autorização.

## 2. Bancos

| Banco | Papel | Uso pelo portal |
| --- | --- | --- |
| `accountdb` | Contas do jogo (`<letra>GameUser`) e cadastro estilo coreano (`<letra>PersonalMember`, vazio) | Leitura; escrita só no cadastro e troca de senha |
| `ServerDB` | Configuração do servidor, `LevelTable`, `Vip`, aging, caixas | Leitura |
| `UserDB` | `BattlePoints`, `GameMasters`, `PremiumData`, `QuestData` | Leitura |
| `GameLogDB` | Logs de entrada/saída e de personagem | Leitura (fonte de online e histórico) |
| `ClanDB` | Clãs (`CL`, `UL`), Siege | Leitura |
| `PristonDB` | `TopLevel`, `PVP`, anti-cheat | Leitura |
| `CoinShopDB` / `TimeShopDB` | Saldo de coins e loja, sistemas Nexus | Leitura (fora do escopo inicial) |
| `QuestDB` | Quests e progresso | Leitura (fora do escopo inicial) |
| `SodDb` | Bellatra/SoD por mês | Leitura (fora do escopo inicial) |
| `KLVN Studio` | Banco do **site antigo** (contas web, logins, notícias, suporte) | Fonte de migração |
| `NexusPKAdmin` | Painel admin antigo, PIX, doações | Fonte de migração / fora do escopo inicial |

Bancos que existem no servidor e **não** foram extraídos: BattleEvents, BillingDb, BillingLogDb, EventDB, ItemDB, KLVN, LogadoPremiado, NexusMarket, Painel_Admin, PCRoom, PCRoomLog, rPTDB, SecurityDB, SoD2DB, StartUserDB. Se algum for usado pelo servidor do jogo, preciso do schema dele também.

## 3. Mapeamento por funcionalidade

| Funcionalidade | Origem real | Colunas | Status |
| --- | --- | --- | --- |
| Login do jogador | `[accountdb].dbo.[<1ª letra>GameUser]` (tabelas `0`–`9` e `A`–`Z`) | `userid` varchar(32), `Passwd` varchar(32), `BlockChk` char(1), `DelChk` char(1) | Confirmado. Ver seção 4 sobre a senha |
| Cadastro | Mesmo shard `<letra>GameUser` | `userid, Passwd, GPCode, RegistDay, DisuseDay, inuse, Grade, EventChk, SelectChk, BlockChk, SpecialChk, Credit, DelChk, Channel` (valores usados hoje: GPCode `PTP-RUD001`, DisuseDay `2030-12-12`, Grade `U`, demais `0`, Channel = IP) | Confirmado pelo código antigo; não há procedure de cadastro |
| Troca/recuperação de senha | `<letra>GameUser.Passwd` | `Passwd` | Confirmado pelo código antigo |
| Banimento | `<letra>GameUser.BlockChk`; histórico em `[accountdb].dbo.BlockUserList` e `[KLVN Studio].dbo.Ban` | `BlockChk`; `BlockUserList`: userid, AppDate, ReuseDate, Active, SReason… | Confirmado (BlockChk); BlockUserList está vazio |
| Último acesso da conta | `<letra>GameUser.LastDay`, `GameLogDB.GameOnRecord.LogonTime` | `LastDay`; `UserID, LogonTime, IP, ServerName` | Existe, semântica a confirmar |
| Personagens da conta | `[ServerDB].dbo.LevelTable` | `szID` (conta), `Nick`, `Class` int, `Level` int — **sem EXP** | Confirmado (usado pelo ranking antigo, 382 linhas) |
| EXP do personagem | `[GameLogDB].dbo.CharGameRecordLg` | `UserID, CharType, GameLevel, Exp` bigint, `GameMoney, EditDay` | Existe (215.945 linhas), semântica a confirmar |
| EXP (alternativa) | `[PristonDB].dbo.TopLevel` | `ID, NAME, CLASS, LVL, XP` (**int**, pode estourar em nível alto) | Existe (107 linhas), semântica a confirmar |
| Dados completos do personagem | Arquivos `.dat` em `PT_USERDATA_DIR` | binário | Fora do SQL; o portal **não** lê na v1 |
| Classe | `LevelTable.Class` | 1 Lutador, 2 Mecânico, 3 Arqueira, 4 Pikeman, 5 Atalanta, 6 Cavaleiro, 7 Mago, 8 Sacerdotisa, 9 Assassina, 10 Shaman, 11 Guerreira | Confirmado pelo site antigo |
| Clã do personagem | `[ClanDB].dbo.UL` | `userid, ChName, ChType, ChLv, ClanName, Permi, DelActive` | Confirmado (tabela usada pelo ASP do clã) |
| Clãs | `[ClanDB].dbo.CL` | `ClanName, ClanZang` (líder), `MemCnt, Cpoint, CWin, CFail, RegiDate, DelActive` | Confirmado |
| Ranking geral (nível) | `LevelTable` | `Level`, `Class` | Confirmado |
| Ranking geral (EXP) | `CharGameRecordLg` ou `TopLevel` | `Exp` / `XP` | Depende da confirmação acima |
| Ranking PvP | `[UserDB].dbo.BattlePoints` | `Nick, BPs, DPs, BPsSemanal, KillsPvP, MortesPvP, PontosPvP, IsReiPvP, SodSemanal` | Confirmado (usado hoje) |
| Excluir GMs do ranking | `[UserDB].dbo.GameMasters` | `Account, GameMasterLevel` | Confirmado |
| VIP | `[ServerDB].dbo.Vip` | `AccountID, PlayerName, IdVip, DateStart, DateEnd` | Confirmado |
| Jogadores online | `GameLogDB.GameOnRecord` x `GameOutRecord` por `SNo`; ou `<letra>GameUser.inuse` | `SNo, UserID, LogonTime / LogoutTime` | Existe, semântica a confirmar |
| Status do servidor (online/offline) | Teste de porta TCP do servidor de jogo (`GAME_SERVER_IP`/`PORT` do admin antigo) | — | Sem tabela; verificação ativa pelo Worker |
| Coins | `[CoinShopDB].dbo.UserInfo` | `UserID, UserCoin, LastChange` | Confirmado (fora do escopo inicial) |
| Histórico diário/semanal/mensal | — | — | **Sem fonte**: snapshots no banco do portal, a partir da data de implantação |
| Notícias, eventos, tickets, banners, downloads, notificações, auditoria, admins | `KLVN Studio` e `NexusPKAdmin` (site antigo) | — | Banco do portal; migração opcional dos dados antigos |
| Contas web (e-mail, senha do painel, palavra secreta) | `[KLVN Studio].dbo.Accounts` | `Username, Email, PasswordPainel, SecretWord, BanStats` | Migrar para o banco do portal |

## 4. Senha do jogo — conclusão da análise

- **Não há triggers no `accountdb`.** As procedures `usp_<letra>Login` comparam `Passwd = @PassWord` diretamente (e consultam `PersonalMember`, que está vazio, então não são usadas no fluxo atual).
- O site antigo grava a senha **como digitada** em `GameUser.Passwd` e em `[KLVN Studio].dbo.Accounts.Password`, e o login web compara texto.
- `Passwd` é `varchar(32)`, exatamente o tamanho de um MD5 em hexadecimal. **Se** o servidor do jogo calculasse MD5 antes de comparar, as contas criadas pelo site antigo não entrariam no jogo.
- **Confirmado pelos logs do servidor (10/11):** o servidor do jogo registra a senha digitada em texto no log `LogFile/(InfoUser)*.log` a cada login. A senha do jogo é **texto puro**, sem hash.
- Conclusão: a senha do jogo está em **texto puro**. O "hash" que você conhece provavelmente é o da senha do painel (`PasswordPainel`, bcrypt).
- Confirmação sem expor senhas: rodar a consulta da seção 6.

Implicação: o portal vai validar o login comparando com `Passwd` (em tempo constante, sem registrar o valor) e gravar `Passwd` no mesmo formato. Migrar para hash exige mudar o servidor do jogo; fica registrado como risco e decisão sua.

## 5. Decisões de integração

1. Acesso de leitura com um login SQL próprio do portal (`db_datareader` nos bancos do jogo). Escrita apenas em `<letra>GameUser` (INSERT no cadastro, UPDATE de `Passwd`) com um segundo login com permissão só nessas tabelas.
2. O nome da tabela de contas é montado a partir de uma lista fechada (`0`–`9`, `A`–`Z`) validada no código; o login nunca é concatenado no SQL.
3. Rankings, online e status são coletados pelo Worker em intervalos (sugestão: 5 min) e servidos do banco do portal + cache. Nada consulta as tabelas do jogo a cada visita.
4. A data de início dos snapshots aparece no site; nenhum histórico anterior será exibido.
5. O banco do portal será novo (ex.: `PortalDB`). `KLVN Studio` e `NexusPKAdmin` ficam intocados até a migração ser aprovada.

## 6. Consultas de confirmação (somente leitura, sem dados sensíveis)

```sql
-- A) A senha do jogo é texto ou MD5? Retorna só contagens.
SELECT COUNT(*) AS Contas,
       SUM(CASE WHEN LEN(Passwd)=32 AND Passwd NOT LIKE '%[^0-9a-fA-F]%' THEN 1 ELSE 0 END) AS ParecemMD5
FROM (
  SELECT Passwd FROM accountdb.dbo.AGameUser UNION ALL SELECT Passwd FROM accountdb.dbo.BGameUser
  UNION ALL SELECT Passwd FROM accountdb.dbo.CGameUser UNION ALL SELECT Passwd FROM accountdb.dbo.DGameUser
  UNION ALL SELECT Passwd FROM accountdb.dbo.EGameUser UNION ALL SELECT Passwd FROM accountdb.dbo.FGameUser
  UNION ALL SELECT Passwd FROM accountdb.dbo.GGameUser UNION ALL SELECT Passwd FROM accountdb.dbo.HGameUser
  UNION ALL SELECT Passwd FROM accountdb.dbo.IGameUser UNION ALL SELECT Passwd FROM accountdb.dbo.JGameUser
  UNION ALL SELECT Passwd FROM accountdb.dbo.KGameUser UNION ALL SELECT Passwd FROM accountdb.dbo.LGameUser
  UNION ALL SELECT Passwd FROM accountdb.dbo.MGameUser UNION ALL SELECT Passwd FROM accountdb.dbo.NGameUser
  UNION ALL SELECT Passwd FROM accountdb.dbo.OGameUser UNION ALL SELECT Passwd FROM accountdb.dbo.PGameUser
  UNION ALL SELECT Passwd FROM accountdb.dbo.QGameUser UNION ALL SELECT Passwd FROM accountdb.dbo.RGameUser
  UNION ALL SELECT Passwd FROM accountdb.dbo.SGameUser UNION ALL SELECT Passwd FROM accountdb.dbo.TGameUser
  UNION ALL SELECT Passwd FROM accountdb.dbo.UGameUser UNION ALL SELECT Passwd FROM accountdb.dbo.VGameUser
  UNION ALL SELECT Passwd FROM accountdb.dbo.WGameUser UNION ALL SELECT Passwd FROM accountdb.dbo.XGameUser
  UNION ALL SELECT Passwd FROM accountdb.dbo.YGameUser UNION ALL SELECT Passwd FROM accountdb.dbo.ZGameUser
  UNION ALL SELECT Passwd FROM accountdb.dbo.[0GameUser] UNION ALL SELECT Passwd FROM accountdb.dbo.[1GameUser]
  UNION ALL SELECT Passwd FROM accountdb.dbo.[2GameUser] UNION ALL SELECT Passwd FROM accountdb.dbo.[3GameUser]
  UNION ALL SELECT Passwd FROM accountdb.dbo.[4GameUser] UNION ALL SELECT Passwd FROM accountdb.dbo.[5GameUser]
  UNION ALL SELECT Passwd FROM accountdb.dbo.[6GameUser] UNION ALL SELECT Passwd FROM accountdb.dbo.[7GameUser]
  UNION ALL SELECT Passwd FROM accountdb.dbo.[8GameUser] UNION ALL SELECT Passwd FROM accountdb.dbo.[9GameUser]
) x;

-- B) Quando e como CharGameRecordLg é gravado? (sem login nem nome)
SELECT TOP 20 CharType, GameLevel, Exp, ServerName, RegistDay, EditDay, Active
FROM GameLogDB.dbo.CharGameRecordLg ORDER BY EditDay DESC;

-- C) As entradas e saídas fecham por sessão? (online = entrou e não saiu)
SELECT TOP 10 o.SNo, o.LogonTime, x.LogoutTime
FROM GameLogDB.dbo.GameOnRecord o
LEFT JOIN GameLogDB.dbo.GameOutRecord x ON x.SNo = o.SNo
ORDER BY o.LogonTime DESC;

-- D) TopLevel e LevelTable estão atualizadas?
SELECT MAX(EditDay) AS UltimaGravacaoLog FROM GameLogDB.dbo.CharGameRecordLg;
SELECT COUNT(*) AS Personagens, MAX(Level) AS MaiorNivel FROM ServerDB.dbo.LevelTable;
SELECT COUNT(*) AS Linhas, MAX(LVL) AS MaiorNivel, MAX(XP) AS MaiorXP FROM PristonDB.dbo.TopLevel;
```

## 7. Servidor do jogo (arquivos recebidos em 10/11)

- Não veio código-fonte nem executável; vieram `config.ini` (taxas: Exp=10, Gold=1, Coin=1), `NexusClasses.ini`, `NexusClassChange.ini`, `Bellatra.ini`, `BlessCastle.dat` e logs.
- Os logs citam `OnSever.cpp` e `NexusClassChange.inc`: o servidor é compilado a partir de um código-fonte customizado. Com esse código dá para confirmar quando o SQL é gravado.
- Logs em texto úteis para o portal: `(Death)` (nível e área por morte), `PK_LOG` (quem matou quem), `(Vip)`, `(ClassChange)`, `LogAdmin` (comandos de GM). Nenhum log registra EXP periodicamente.
- **Risco de segurança:** `(InfoUser)` grava login, senha em texto, IP e MAC de cada acesso. Recomendado: desativar essa linha no código do servidor, restringir a pasta `LogFile` e apagar os logs antigos.

## 8. Resultado das consultas de confirmação (10/11, 21:56)

| Fonte | Resultado | Conclusão |
| --- | --- | --- |
| `GameLogDB.CharGameRecordLg` | Última gravação em **09/06/2022**; nível e EXP repetidos | Dados antigos importados. **Não é atualizada** pelo servidor atual |
| `GameLogDB.GameOnRecord` x `GameOutRecord` | Últimas entradas em 09/06/2022, nenhuma com saída correspondente | **Não serve** para jogadores online |
| `ServerDB.LevelTable` | 382 personagens, maior nível 225, sem coluna de data | Usada pelo ranking antigo; falta confirmar se atualiza em tempo real |
| `PristonDB.TopLevel` | 107 linhas, maior nível 100, maior XP 1.987.960.702 (perto do limite de `int`) | Parece antiga; não usar |

Bancos restantes (schema 2): `KLVN` (site mais antigo: Accounts, Logout, Tickets), `NexusMarket` (mercado, ativo), `EventDB` (arena/eventos Nexus), `ItemDB`, `BattleEvents`, `LogadoPremiado`, `Painel_Admin`. Os demais estão vazios (Billing, PCRoom, SoD2DB, rPTDB, StartUserDB). **Nenhum guarda EXP nem status online.**

Conclusão:
- **EXP atual só existe nos arquivos `.dat` dos personagens.** Não há fonte SQL viva.
- **Jogadores online não têm fonte SQL viva.** O status do servidor será feito por teste de porta.
- Opções para EXP e online: (a) ranking só por nível na v1; (b) o servidor do jogo passar a gravar nível, EXP e online numa tabela (exige alterar o código-fonte do servidor); (c) o Worker ler os `.dat` (exige o formato do arquivo, que está no código-fonte).

## 9. Pendências

- [x] Resultado das consultas A–D (seção 8).
- [x] Schema dos bancos restantes (seção 8).
- [x] Fonte de EXP e online decidida em 10/11: v1 ranking por nível (LevelTable); depois o servidor do jogo grava nível, EXP e online numa tabela (opção b).
- [ ] Confirmar se `ServerDB.LevelTable` atualiza quando um personagem sobe de nível.
- [ ] Decidir se o portal novo migra contas web, notícias e tickets de `KLVN Studio`. Os e-mails de `[KLVN Studio].dbo.Accounts` permitiriam que contas antigas recuperem a senha sem passar pelo suporte.
- [ ] Confirmar o tamanho máximo de login e senha aceito pelo cliente do jogo (o portal usa 16; o banco aceita 32).
- [ ] Confirmar o fuso das datas gravadas pelo jogo (`RegistDay`, `Vip.DateStart/DateEnd`). O portal considera horário de Brasília.
- [ ] Confirmar se `ClanDB.dbo.UL.DelActive` marca membro que saiu do clã. Hoje o painel mostra o clã sem filtrar por essa coluna.
- [ ] Confirmar o significado de `BlockChk` e `DelChk` diferentes de `'0'` (o portal trata como bloqueada e apagada).
