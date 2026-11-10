# Contas: cadastro, login e recuperação (Fase 5)

## Onde fica cada dado

| Dado | Onde | Observação |
|---|---|---|
| Login e senha do jogo | `[accountdb].[dbo].[<letra>GameUser]` (`userid`, `Passwd`) | Mesma tabela que o servidor do jogo usa. A senha fica em texto, porque é assim que o jogo compara (ver `DATABASE_MAPPING.md`, seção 4). |
| E-mail, confirmação, último login | `PortalDB.dbo.PlayerAccount` | O jogo não tem e-mail. Contas antigas ganham a linha no primeiro login pelo portal. |
| Sessões do site | `PortalDB.dbo.PlayerSession` | Só o SHA-256 do token. Validade padrão: 12 horas. |
| Links de confirmação e de nova senha | `PortalDB.dbo.OneTimeToken` | Só o SHA-256. Uso único. Nova senha: 30 min. Confirmação: 48 h. |
| Auditoria | `PortalDB.dbo.AuditLog` | Ações `account.*` com login e IP. Nunca senha, token ou cookie. |

## Fluxos

- **Cadastro**: valida login (4–16, letras/números/`_`/`-`), senha (6–16 letras e números, sem conter o login), e-mail único. Confere se o login existe no shard e grava a linha com `UPDLOCK, HOLDLOCK` numa transação (sem corrida entre dois cadastros iguais). Valores gravados iguais aos do site antigo: `GPCode='PTP-RUD001'`, `DisuseDay=2030-12-12`, `Grade='U'`, `inuse` e flags `'0'`, `Channel` = IP.
- **Login**: lê `Passwd`, `BlockChk`, `DelChk` com o login de leitura e compara em tempo constante. Senha errada e login inexistente dão a mesma resposta. Conta bloqueada só é informada com a senha correta. 5 falhas por login ou 20 por IP bloqueiam novas tentativas por 15 minutos.
- **Sessão**: token aleatório de 256 bits num cookie `nx_session` `httpOnly`, `SameSite=Lax`, `Secure` em produção. O navegador nunca fala com a API: o servidor do Next chama a API em `127.0.0.1` e repassa o token no cabeçalho `Authorization`.
- **Recuperação**: login + e-mail confirmado. A resposta é sempre a mesma, exista a conta ou não. No máximo 3 e-mails por hora por conta. Trocar a senha encerra todas as sessões.

## Diferença de comportamento em relação ao jogo

O `accountdb` usa collation `CI` (sem diferenciar maiúsculas). O jogo aceita a senha com qualquer caixa; o portal compara exatamente. É mais rígido, nunca mais permissivo.

## Configuração (variáveis de ambiente dos serviços)

| Variável | Para quê |
|---|---|
| `GameDatabase__ReadConnectionString` | Login `portal_game_read` |
| `GameDatabase__WriteConnectionString` | Login `portal_game_write`. **Vazio = cadastro e troca de senha desligados.** |
| `PortalDatabase__ConnectionString` | Login `portal_app` |
| `Accounts__PublicBaseUrl` | Endereço público do portal, para os links dos e-mails |
| `Smtp__Host`, `Smtp__Port`, `Smtp__UserName`, `Smtp__Password`, `Smtp__FromAddress` | Envio de e-mail. Sem `Host`, a recuperação por e-mail fica indisponível e o site avisa. |
| `Captcha__TurnstileSecret` (API) e `NEXT_PUBLIC_TURNSTILE_SITE_KEY` (site) | Cloudflare Turnstile. Vazio desliga o captcha. |

Os logins SQL e as permissões estão em `database/scripts/permissoes-portal.sql`:

- `portal_game_read`: `SELECT` nas 36 tabelas `<letra>GameUser`, em `ServerDB.dbo.LevelTable` e em `UserDB.dbo.GameMasters`.
- `portal_game_write`: `SELECT` e `INSERT` nas 36 tabelas `<letra>GameUser` e `UPDATE` **só da coluna `Passwd`**. Não apaga linhas, não mexe em `BlockChk` nem em nenhuma outra tabela.
- `portal_app`: leitura e escrita nas tabelas do `PortalDB`, sem permissão para alterar a estrutura nem acessar os bancos do jogo.

## Testes

- Unitários: `AuthServiceTests`, `AccountRulesTests` (cadastro, duplicidade, bloqueio, limite de tentativas, recuperação, token expirado).
- Integração: `scripts/integration-auth.sh` sobe a réplica das tabelas do jogo (`database/scripts/dev/banco-jogo-teste.sql`, gerada a partir do schema real) num SQL Server de laboratório, aplica as permissões acima e testa cadastro, duplicidade, login, sessão e logout. Roda no CI.

## Painel do jogador (Fase 6)

`GET /api/v1/player/panel` (exige sessão) junta, só com o login de leitura:

| No painel | Fonte | Observação |
|---|---|---|
| Situação e data de criação | `<letra>GameUser.BlockChk`, `RegistDay` | |
| Personagens e nível | `ServerDB.dbo.LevelTable` (`szID` = login) | Nível como o servidor grava hoje; sem EXP. |
| Clã | `ClanDB.dbo.UL` (`userid`, `ChName`, `ClanName`) | Ligado ao personagem pelo nome. |
| Abates, mortes e BP | `UserDB.dbo.BattlePoints` (`Account`, `Nick`) | |
| VIP | `ServerDB.dbo.Vip` (`AccountID`) | Mostra o plano ativo, ou o último que expirou. |
| Coins | `CoinShopDB.dbo.UserInfo` (`UserID`) | Só leitura. |
| Acessos recentes | `PortalDB.dbo.AuditLog` (`account.login`) | IP mostrado só pela metade (ex.: `203.0.*.*`). |

As datas do jogo (`RegistDay`, `DateStart`, `DateEnd`) são `datetime` sem fuso; o portal considera horário de Brasília.
Se o banco do jogo falhar, o painel abre do mesmo jeito e avisa que os dados do jogo estão indisponíveis.

Ações com sessão (limite de 10 por minuto por IP):

- `POST /api/v1/player/password`: exige a senha atual; grava no `GameUser` e encerra as outras sessões do site.
- `POST /api/v1/player/email`: cadastra ou troca o e-mail; a confirmação volta a ficar pendente e o link é enviado.
- `POST /api/v1/player/email/resend`: reenvia a confirmação (máximo 3 por hora).
- `POST /api/v1/player/sessions/revoke-others`: encerra as outras sessões.
