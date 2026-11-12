# Instalação no Windows Server (Fase 13)

O portal novo é instalado **ao lado** do site atual, na mesma VPS, sem mexer nele:

| | Site atual | Portal novo |
|---|---|---|
| Pasta | `C:\wwwroot` | `C:\NexusPortal` |
| Site do IIS | o atual, sem alteração | `NexusPortal`, só com o subdomínio novo |
| Banco | `KLVN Studio`, `NexusPKAdmin` | `PortalDB` (novo) |
| Banco do jogo | lê e grava como hoje | lê com `portal_game_read`; cadastro e troca de senha com `portal_game_write` |

A troca do site antigo pelo novo é um passo separado, descrito em [`DEPLOYMENT.md`](DEPLOYMENT.md#troca-do-site-antigo-pelo-novo).

## Como fica no servidor

```
Navegador ──https──> IIS (site NexusPortal, porta 443, só o host do portal)
                      └─ módulo ASP.NET Core ──> Node (Next.js) em 127.0.0.1:<porta do IIS>
                                                    └─> API (serviço NexusPortalApi) em 127.0.0.1:5080
                                                          └─> SQL Server (.\SQLEXPRESS)
Serviço NexusPortalWorker: foto diária do ranking, status do servidor, lembretes, limpeza.
```

- A API e o Node escutam só em `127.0.0.1`. Nada novo fica aberto para a internet além do 443 (e do 80, que redireciona).
- O IIS inicia, reinicia e para o Node sozinho. Os dois serviços reiniciam sozinhos se caírem.

```
C:\NexusPortal\
  config\portal.json   connection strings e segredos (só administradores e os dois serviços leem)
  releases\<versão>\   cada versão publicada (api, worker, migrator, web, deploy)
  current\             junção para a versão no ar
  logs\                api-*.log, worker-*.log, backup.log, web\ (saída do Node)
  backups\             PortalDB_*.bak (14 dias)
```

## 1. Pré-requisitos

Faça num horário de pouco movimento: o Hosting Bundle reinicia o IIS, o que derruba o site atual por alguns segundos.

1. **IIS** já está instalado (o site atual usa).
2. **.NET 10 Hosting Bundle**: <https://dotnet.microsoft.com/download/dotnet/10.0> → "ASP.NET Core Runtime" → "Hosting Bundle". Instala o runtime e o módulo do IIS.
3. **Node.js 22 LTS** (instalador .msi, pasta padrão): <https://nodejs.org>.
4. **URL Rewrite 2.1** (redireciona http para https): <https://www.iis.net/downloads/microsoft/url-rewrite>.
5. **sqlcmd**: já vem com o SQL Server. Confira com `sqlcmd -?` num PowerShell novo.
6. **SQL Server em modo misto** (SQL Server e Windows). O portal usa logins SQL próprios. Confira:
   ```powershell
   sqlcmd -S .\SQLEXPRESS -E -C -Q "SELECT SERVERPROPERTY('IsIntegratedSecurityOnly')"
   ```
   `0` = modo misto (pronto). `1` = só Windows: mude em SSMS > propriedades do servidor > Segurança e reinicie o serviço do SQL.
7. **DNS**: um registro A do subdomínio do portal (ex.: `novo.seudominio.com`) apontando para o IP da VPS.
8. **Certificado** para esse subdomínio em "Computador Local > Pessoal". Com Let's Encrypt, o [win-acme](https://www.win-acme.com) emite e renova sozinho; escolha "Somente certificado" (sem criar site) e anote a impressão digital (thumbprint).

Rode tudo num **PowerShell como administrador**, numa conta do Windows que seja sysadmin no SQL Server. Se não for, acrescente `-SqlCredential (Get-Credential)` aos comandos: a senha é digitada na janela do Windows, nunca em chat.

## 2. Baixar o pacote

Cada versão é um `nexusportal-<versão>.zip` com um `.sha256` ao lado. Eles vêm de:

- **Releases** do repositório (tags `v*`), ou
- **Actions > Publicação no Windows > (execução) > Artifacts > nexusportal**.

Copie os dois arquivos para `C:\NexusPortal-pacotes` e desbloqueie (o Windows marca arquivos baixados):

```powershell
cd C:\NexusPortal-pacotes
Unblock-File .\nexusportal-*.zip
Expand-Archive .\nexusportal-<versão>.zip .\extraido
Set-ExecutionPolicy -Scope Process Bypass
```

## 3. Instalar

```powershell
.\extraido\deploy\Install-Portal.ps1 `
  -Package C:\NexusPortal-pacotes\nexusportal-<versão>.zip `
  -HostName novo.seudominio.com `
  -CertificateThumbprint <impressão digital>
```

Opções: `-SqlInstance` (padrão `.\SQLEXPRESS`), `-ApiPort` (padrão `5080`), `-Root` (padrão `C:\NexusPortal`), `-BackupTime` (padrão `04:00`).

O que o script faz, em ordem:

1. Confere os pré-requisitos e se o host já não está em outro site do IIS (se estiver, para sem mudar nada).
2. Cria os serviços `NexusPortalApi` e `NexusPortalWorker` com contas virtuais (`NT SERVICE\...`, sem senha).
3. Cria o pool e o site `NexusPortal` no IIS só com o host informado, e tira a query string do log do IIS.
4. Cria as pastas com permissões fechadas: só administradores e os dois serviços leem `config`. O site do IIS não lê os segredos.
5. Cria o `PortalDB` e aplica as migrações.
6. Cria os logins SQL com senhas aleatórias geradas na própria VPS e grava as connection strings em `config\portal.json`. Ninguém precisa ver ou digitar essas senhas.
7. Publica a versão, confere a API, os bancos e a página inicial.
8. Agenda o backup diário do `PortalDB`.
9. Se existir `C:\wwwroot`, liga ao site novo as pastas que o jogo e o launcher chamam (`ServerMain`, `Clan`, `ClanContent`, `updater`...), com os mesmos arquivos e o mesmo pool do site antigo. Detalhes em [`LIGACOES-DO-JOGO.md`](LIGACOES-DO-JOGO.md). Opções: `-LegacyRoot`, `-SkipLegacy`.

### O que muda no SQL Server

Nenhuma tabela do jogo é criada, alterada ou apagada. O script roda `database/scripts/permissoes-portal.sql`, que:

- cria os logins `portal_game_read`, `portal_game_write` e `portal_app`;
- cria o usuário `portal_game_read` em `accountdb`, `ServerDB`, `UserDB`, `ClanDB` e `CoinShopDB` com `SELECT` só nas tabelas que o portal lê;
- cria o usuário `portal_game_write` em `accountdb` com `SELECT`, `INSERT` e `UPDATE(Passwd)` nas tabelas `<letra>GameUser`;
- dá a `portal_app` leitura e escrita só no `PortalDB`.

Também dá `db_backupoperator` no `PortalDB` à conta SYSTEM, que roda a tarefa de backup.

## 4. Completar a configuração

Abra o Bloco de Notas **como administrador** e edite `C:\NexusPortal\config\portal.json`:

```json
"Smtp": {
  "Host": "smtp.seuprovedor.com", "Port": 587, "EnableSsl": true,
  "UserName": "...", "Password": "...", "FromAddress": "nao-responda@seudominio.com", "FromName": "NexusPK"
},
"Captcha": { "TurnstileSecret": "..." },
"GameServer": {
  "TimeoutMilliseconds": 2000,
  "Targets": [
    { "Key": "servidor", "Name": "Servidor do jogo", "Host": "127.0.0.1", "Port": 35002 }
  ]
}
```

- **Smtp**: sem ele, cadastro e login funcionam, mas confirmação de e-mail e recuperação de senha ficam desligadas (o site avisa).
- **Captcha**: a chave secreta do Cloudflare Turnstile. A chave pública vai no pacote (variável `TURNSTILE_SITE_KEY` do repositório no GitHub). Sem as duas, o captcha fica desligado.
- **GameServer.Targets**: as portas do servidor de login e de cada canal (as mesmas do `.ini` do servidor do jogo). Na VPS atual, o servidor do jogo (`open_the_server`) escuta só na 35002. A 32009 que o `include/status.php` do site antigo testava é do SQL Server, não do jogo. Para conferir as portas: `Get-NetTCPConnection -State Listen` com o nome do processo. Sem elas, a página de status mostra "Sem verificação configurada" em vez de inventar um estado.

Depois:

```powershell
Restart-Service NexusPortalApi, NexusPortalWorker
```

## 5. Primeiro superadministrador

```powershell
C:\NexusPortal\current\deploy\New-PortalAdmin.ps1 -UserName <usuario>
```

A senha é pedida no próprio console, sem aparecer na tela. Os outros membros da equipe são criados pelo painel em `/admin/equipe`.

## 6. Conferir

- [ ] `https://novo.seudominio.com` abre a página inicial; `http://` leva para `https://`.
- [ ] Cadastro de uma conta de teste e login no jogo com ela.
- [ ] Login no portal com uma conta antiga do jogo.
- [ ] Recuperação de senha chega por e-mail (com SMTP preenchido).
- [ ] `/status` mostra o servidor e os canais.
- [ ] `/admin/entrar` com o superadministrador.
- [ ] Do seu computador, `Test-NetConnection <ip da vps> -Port 5080` e `-Port 1433` falham (nada exposto).
- [ ] O site atual continua abrindo normalmente.

## Se algo der errado

| Sintoma | Onde olhar |
|---|---|
| Erro 502.5 / 500.30 | `C:\NexusPortal\logs\web\node_*.log` (Node não subiu) |
| Página abre, mas dados não | `C:\NexusPortal\logs\api-*.log`; `curl.exe http://127.0.0.1:5080/health/ready` |
| Ranking ou status parados | `C:\NexusPortal\logs\worker-*.log` |
| Serviço não sobe | Visualizador de Eventos > Logs do Windows > Aplicativo |

O instalador pode ser rodado de novo: ele mantém `portal.json` e os logins existentes.

O pacote é testado a cada mudança num Windows Server com IIS e SQL Server Express (`.github/workflows/windows.yml`): instalação, atualização, volta de versão, backup agendado e restauração.
