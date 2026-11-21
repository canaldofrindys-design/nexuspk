# Painel administrativo

Endereço: `/admin`. A equipe tem contas próprias no banco do portal (`dbo.StaffUser`, migração `0005`), separadas das contas de jogador e das contas do jogo.

## Primeiro acesso

Depois de aplicar as migrações, crie o superadministrador no próprio servidor:

```powershell
$env:PRISTON_ConnectionStrings__PortalDbDeploy = "<connection string de deploy>"
.\Priston.DbMigrator.exe --criar-superadmin <usuario>
```

A senha é digitada no terminal, sem eco, duas vezes. Ela não passa por linha de comando, arquivo, chat ou log. O comando recusa um usuário que já existe.

## Funções

| Função | Resumo, notícias, chamados | Conteúdo do site | Auditoria | Contas do jogo (ver, diagnóstico) | Banir, desbanir | Coins, VIP, Box Donate | Editores (lojas, drops, itens) | Logs de itens | Ligar / reiniciar | Reset de rankings | Configurar tabelas | Equipe |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Superadministrador | sim | sim | sim | sim | sim | sim | sim | sim | sim | sim | sim | sim |
| Administrador | sim | sim | sim | sim | sim | sim | sim | sim | não | não | não | não |
| Moderador | sim | não | não | sim | sim | não | não | sim | não | não | não | não |
| Suporte | sim (sem notícias) | não | não | sim | não | não | não | não | não | não | não | não |

A API confere a permissão em toda chamada (`Capability` em `StaffModels.cs`); o menu só esconde o que a função não pode usar.

## Funções do jogo no painel

Menus **Jogadores** e **Jogo** do `/admin`. Tudo usa o login SQL próprio `portal_game_admin` (ver "Ligar as funções do jogo" abaixo). Sem ele, as telas avisam que a função está desligada e nada é gravado.

| Tela | O que faz | Onde grava |
|---|---|---|
| **Contas** (`/admin/contas`) | Busca por login ou nome de personagem. A ficha mostra personagens, clã, VIP, coins, PvP, conta do portal, sessões, banimentos, histórico de ações e um **diagnóstico** (conta duplicada nos shards, banida, sem personagem, VIP vencido ou repetido, saldo negativo, e-mail não confirmado, GM etc.). | só leitura |
| Banir / desbanir | Banimento com motivo e prazo (1 a 3650 dias ou permanente). Pede a senha de quem bane, encerra as sessões do jogador no portal, e o Worker tira o banimento sozinho quando vence (a cada 5 minutos). Conta de GM só o superadministrador bane. | `BlockChk` na tabela `<letra>GameUser`; histórico em `PortalDB.dbo.GameBan` |
| Coins | Somar, tirar ou definir o saldo, com motivo. Nunca deixa negativo. Limite por ação para quem não é superadministrador (`GameAdmin:MaxCoinChange`, padrão 100000). | `CoinShopDB.dbo.UserInfo.UserCoin` |
| VIP | Dar VIP a um personagem da conta, estender (ou encurtar) e tirar (tirar pede senha). Limite de dias para quem não é superadministrador (`GameAdmin:MaxVipDays`, padrão 90). | `ServerDB.dbo.Vip` |
| **Ligar / reiniciar** (`/admin/servidor`) | Liga, desliga ou reinicia um programa do servidor do jogo ou todos (desliga na ordem inversa e liga na ordem cadastrada). Pede a palavra de confirmação (LIGAR, DESLIGAR, REINICIAR), motivo e senha. Uma ação por vez e 30 segundos de intervalo entre ações. | processos na VPS |
| **Rankings** (`/admin/rankings`) | Zera PvP semanal, SoD semanal, PvP geral ou pontos de clã do SoD. Pede a palavra ZERAR, motivo e senha. Antes de zerar, guarda uma cópia; cada cópia pode ser restaurada uma vez. | `UserDB.dbo.BattlePoints`, `ClanDB.dbo.CL.Cpoint`; cópias em `PortalDB.dbo.RankingResetBackup` |
| **Box Donate: enviar** | Monta a caixa para um personagem (até 64 itens, com aging e mix) e cria o pacote no sistema de donate do servidor (NexusPKAdmin.dbo.NexusDonatePackages e NexusDonateItems), igual à Coin Shop. O servidor do jogo monta os itens, avisa o jogador e entrega. A tela mostra se o distribuidor do jogo está respondendo e as últimas caixas com a situação. | `NexusPKAdmin` (nada a configurar) |
| Box Donate: preços, Loja de Skin (Coin Shop), Loja de minutos | Lista com busca e edição. Já vêm ligadas às tabelas que o servidor usa: preços e disponibilidade do NPC Box Donate em `NexusPKAdmin.dbo.NexusPixProducts` (centavos de real); Coin Shop com a galeria de skins em `CoinShopDB.dbo.ItemList` e loja de minutos em `TimeShopDB.dbo.ItemList` (preço; incluir e apagar itens, apagar pede senha). | já ligadas (dá para trocar em "Configurar") |
| Drops de monstros, Lojas NPC, Catálogo de itens | Consulta lida dos arquivos do servidor; edição só se uma tabela for configurada. | arquivos do servidor |
| Logs de itens | Consulta do log de itens do jogo (`ItemLogDB.dbo.IL`), mais novos primeiro, com busca por conta ou personagem. Só leitura. | já ligada |

### Tabelas configuradas (Box Donate, lojas, drops, itens, logs)

Os nomes dessas tabelas mudam de servidor para servidor e não estão no `DATABASE_MAPPING.md`, então o portal **não adivinha**: o superadministrador liga cada função a uma tabela uma vez, em **Jogo > (função) > Configurar**:

1. Escolha o banco e a tabela (a lista vem do próprio SQL Server, com as tabelas mais prováveis primeiro).
2. Marque as **colunas-chave** (as que identificam uma linha, por exemplo NPC + código do item), as **editáveis**, as de **busca** e, se houver, a coluna do **login da conta**.
3. Diga se a equipe pode incluir e apagar linhas.
4. Confirme com a sua senha.

Regras de proteção do editor:

- Só colunas que existem de verdade (conferidas em `sys.columns`) e só as marcadas como editáveis podem mudar.
- Cada valor é conferido pelo tipo da coluna (número no intervalo, data, texto no tamanho e só com caracteres que o banco aceita).
- Editar e apagar mexem em **exatamente uma** linha: se a chave achar mais de uma, nada muda e o painel pede mais colunas-chave. Incluir uma linha com chave repetida é recusado.
- Toda mudança grava na auditoria o valor de antes e o de depois.

### Ligar as funções do jogo (uma vez, na VPS)

Abra o PowerShell como administrador e rode:

```powershell
C:\NexusPortal\current\deploy\Enable-GameAdmin.ps1
```

O script cria o login `portal_game_admin` (leitura e escrita de dados nos bancos do jogo; não cria nem apaga tabelas), gera a senha ali mesmo, grava `GameDatabase:AdminConnectionString` no `C:\NexusPortal\config\portal.json` e reinicia a API e o Worker. Pode rodar de novo quando quiser (troca a senha).

Para a tela **Ligar / reiniciar**, informe os programas do servidor na ordem em que devem ligar:

```powershell
C:\NexusPortal\current\deploy\Enable-GameAdmin.ps1 -GameServerExe 'C:\ServidorPT\LoginServer.exe','C:\ServidorPT\GameServer.exe'
```

**Limite do Windows:** o painel roda como o serviço `NexusPortalApi`. Ele só consegue desligar um programa do jogo que ele mesmo ligou (ou que roda na mesma conta). Se o servidor do jogo foi aberto à mão na área de trabalho, a tela mostra "aberto por outra conta" e não desliga; feche-o uma vez à mão e ligue pelo painel, e daí em diante o painel controla.

Configuração opcional no `portal.json`:

```json
"GameAdmin": {
  "VipPlans": { "1": "VIP Bronze", "2": "VIP Prata", "3": "VIP Ouro" },
  "MaxCoinChange": 100000,
  "MaxVipDays": 90
},
"GameServerControl": {
  "Processes": [
    { "Key": "login", "Name": "Login Server", "ExecutablePath": "C:\\ServidorPT\\LoginServer.exe", "StartDelaySeconds": 5 },
    { "Key": "game", "Name": "Game Server", "ExecutablePath": "C:\\ServidorPT\\GameServer.exe" }
  ],
  "StopTimeoutSeconds": 30,
  "CooldownSeconds": 30
}
```

`VipPlans` só dá nome aos números de `IdVip`; sem ele, aparece "Plano 1", "Plano 2".

## Conteúdo do site, banners e imagens

- `/admin/site`: nome, slogan, texto da página **O Servidor** (`/servidor`), taxas, links da comunidade e redes sociais (só https; aparecem no rodapé, na home e em `/servidor`) e a página **Download** (`/download`: texto, requisitos e até 10 arquivos com link https ou do próprio site). Sem nada salvo, valem os valores de `ServerPresentation` do appsettings. Tabela `dbo.SiteSetting` (migração `0007`).
- `/admin/banners`: destaques do topo da home (até 5 ativos, pela ordem, com período opcional). Tabela `dbo.SiteBanner`.
- Imagem de notícia, evento e banner: pode ser enviada do computador (PNG ou JPG até 3 MB). O arquivo é conferido pela estrutura, os metadados (EXIF, textos) são removidos e ele fica em `dbo.MediaFile`, servido em `/midia/<id>` com cache longo.
- Notícias, eventos e banners podem ser excluídos (com confirmação). Evento publicado que está acontecendo tem o botão **Encerrar agora**, que grava o fim neste minuto.
- Tudo vai para a auditoria (`site.server.updated`, `site.downloads.updated`, `banner.*`, `media.uploaded`, `news.deleted`, `event.deleted`, `event.ended`).

Chamados e avisos de status: ver `docs/SUPORTE.md`.

## Segurança

- Senha da equipe: PBKDF2-SHA256 com 600 mil iterações e sal; mínimo de 12 caracteres, sem conter o usuário.
- Membro novo e senha redefinida pelo superadministrador são temporários: o painel só libera depois da troca.
- Cinco senhas erradas bloqueiam a conta por 15 minutos; além disso vale o limite por IP do login.
- Sessão de 8 horas em cookie `nx_staff` (httpOnly, SameSite=Strict, só em `/admin`). Trocar a senha encerra as outras sessões; mudar função ou desativar encerra todas as sessões do membro.
- Sempre resta pelo menos um superadministrador ativo, e ninguém muda a própria função nem se desativa.
- Toda operação da equipe vai para `dbo.AuditLog` (login, falha, bloqueio, notícia, evento, equipe e todas as ações no jogo, com motivo, valor de antes e de depois). Senhas e tokens nunca são gravados.
- Ações destrutivas no jogo (banir, tirar VIP, apagar linha, zerar ranking, restaurar cópia, ligar/desligar, configurar tabela) pedem a senha de novo. Cinco senhas erradas bloqueiam a conta por 15 minutos e encerram as sessões dela.
- Zerar ranking e ligar/desligar pedem também uma palavra de confirmação digitada.
- As ações sensíveis do jogo têm limite de 40 por minuto por IP.
- O painel escreve no banco do jogo só com o login `portal_game_admin`, separado dos logins do site, e nunca altera a estrutura das tabelas.

## Resumo (dashboard)

| Número | Fonte |
|---|---|
| Contas | soma das 36 tabelas `<letra>GameUser` (login de leitura) |
| Personagens | nomes distintos em `ServerDB.LevelTable` |
| Servidor online | teste de porta no login e nos canais (`GameServer:Targets`) |
| Jogadores online | sem fonte até o servidor do jogo publicar (aparece "sem fonte") |
| Cadastros | `account.register` na auditoria do portal (só cadastros feitos no portal novo) |
| Notícias e eventos | tabelas do portal |
| Chamados | `dbo.SupportTicket`: em andamento, esperando a equipe (última mensagem do jogador) e abertos em 24 horas |
| Integração SQL | health checks dos dois bancos |

Fonte que falha aparece como "indisponível", nunca como zero.
