# Publicação, backup e troca do site (Fase 13)

A primeira instalação está em [`INSTALLATION.md`](INSTALLATION.md). Aqui ficam a rotina de cada versão nova, a volta de versão, os backups e o plano para trocar o site antigo pelo novo.

## Gerar uma versão

| Como | Resultado |
|---|---|
| Criar a tag `vX.Y.Z` no GitHub (Releases > Draft a new release) | Release com `nexusportal-X.Y.Z.zip` e `.sha256`, depois de passar no teste de instalação |
| Actions > Publicação no Windows > Run workflow | Artefato `nexusportal` com o zip da `main` (fica 30 dias) |

O pacote é compilado num Windows do GitHub e não leva nenhum segredo.

## Publicar uma versão nova

1. Copie o zip e o `.sha256` para `C:\NexusPortal-pacotes` e rode `Unblock-File` no zip.
2. Num PowerShell como administrador:
   ```powershell
   Set-ExecutionPolicy -Scope Process Bypass
   C:\NexusPortal\current\deploy\Deploy-Portal.ps1 -Package C:\NexusPortal-pacotes\nexusportal-X.Y.Z.zip
   ```

O script:

1. confere o SHA-256 e extrai em `releases\<versão>_<data>`;
2. faz backup do `PortalDB` (`PortalDB_<data>_antes-<versão>.bak`) e confere o arquivo;
3. para o portal (o IIS mostra "O portal está sendo atualizado") e aplica as migrações novas;
4. troca `current` para a versão nova e liga tudo;
5. confere a API, os bancos e a página inicial. **Se não responder em 3 minutos, volta sozinho para a versão anterior.**

O portal fica fora do ar entre 20 e 60 segundos. O site antigo e o jogo não são afetados. Ficam guardadas a versão no ar, a anterior e as 3 mais novas.

## Voltar para a versão anterior

```powershell
C:\NexusPortal\current\deploy\Rollback-Portal.ps1
# ou uma versão específica (nome da pasta em C:\NexusPortal\releases):
C:\NexusPortal\current\deploy\Rollback-Portal.ps1 -Release 2026.11.01-abc1234_20261101120000
```

Volta só os arquivos. O banco fica como está: as migrações só acrescentam tabelas e colunas, então a versão anterior continua funcionando. Para voltar o banco também, restaure o backup `antes-<versão>` (abaixo), sabendo que o que foi gravado no portal depois dele se perde.

## Backups

| O quê | Quando | Onde | Guarda |
|---|---|---|---|
| `PortalDB` completo | todo dia às 04:00 (tarefa "NexusPortal - backup do PortalDB") | `C:\NexusPortal\backups` | 14 dias |
| `PortalDB` antes de cada versão | em cada `Deploy-Portal.ps1` | mesma pasta | 14 dias |

Registro de cada backup: `C:\NexusPortal\logs\backup.log`.

- **Cópia fora da VPS.** Um backup no mesmo disco não protege contra perda do servidor. Acrescente `-CopyTo` à tarefa agendada (Agendador de Tarefas > a tarefa > Ações), apontando para outro disco ou compartilhamento, ou sincronize a pasta `backups` com um serviço de nuvem.
- **Bancos do jogo.** Continuam com a rotina de backup que vocês já têm. O portal só faz backup do `PortalDB`.
- **Backup manual:** `C:\NexusPortal\current\deploy\Backup-PortalDb.ps1 -Verify`

### Restaurar

```powershell
C:\NexusPortal\current\deploy\Restore-PortalDb.ps1 -BackupFile C:\NexusPortal\backups\PortalDB_20261111_040000.bak
```

Pede confirmação, confere o arquivo, faz um backup de segurança do estado atual (`antes-de-restaurar`), para o portal, restaura e liga de novo. Teste uma restauração uma vez por mês: o teste do GitHub faz isso a cada versão, mas o servidor real é que importa.

## Acompanhar

| O quê | Como |
|---|---|
| Portal no ar | `curl.exe -s http://127.0.0.1:5080/health/ready` na VPS (bancos do portal e do jogo) |
| Erros da API | `C:\NexusPortal\logs\api-*.log` (30 dias) |
| Tarefas (ranking, status, lembretes) | `C:\NexusPortal\logs\worker-*.log` (30 dias) |
| Node | `C:\NexusPortal\logs\web\` |
| Ações da equipe | painel `/admin/auditoria` |
| Espaço do PortalDB | o SQL Server Express limita cada banco a 10 GB. Os anexos do suporte são o que mais cresce |

## Troca do site antigo pelo novo

A troca é só no IIS: os dois sites estão na mesma VPS, e o domínio principal passa de um site para o outro. Nada é apagado, e voltar leva um minuto.

### Antes da troca

- [ ] O portal novo rodou no subdomínio por pelo menos uma semana, com a equipe usando o painel e alguns jogadores testando.
- [ ] Login com contas antigas, cadastro, troca e recuperação de senha, painel, ranking, suporte e status conferidos.
- [ ] Decidido o que fazer com as contas web, notícias e chamados do `KLVN Studio` (migrar ou começar do zero). Os e-mails dessas contas permitiriam que jogadores antigos recuperassem a senha pelo portal.
- [ ] Certificado do domínio principal disponível para o site novo (o win-acme pode emitir um que cubra os dois nomes).
- [ ] Aviso publicado no site antigo e nas redes com dia e hora.
- [ ] Lista dos endereços antigos mais acessados (cadastro, ranking, download), para redirecionar depois.
- [ ] Pastas que o jogo e o launcher chamam ligadas ao portal (`Connect-LegacyPaths.ps1`, ver [`LIGACOES-DO-JOGO.md`](LIGACOES-DO-JOGO.md)) e conferidas pelo subdomínio: `http://<subdomínio>/updater/version.txt` e um clã dentro do jogo apontando para o subdomínio, se der para testar.

### Na hora (cerca de 15 minutos, num horário de pouco movimento)

1. Guarde a configuração do IIS:
   ```powershell
   & "$env:windir\system32\inetsrv\appcmd.exe" add backup "antes-da-troca"
   ```
2. No Gerenciador do IIS, no **site antigo**: Associações > remova as do domínio principal (`seudominio.com` e `www.seudominio.com`, 80 e 443). Acrescente `antigo.seudominio.com`, para a equipe ainda consultar o site antigo.
3. No site **NexusPortal**: Associações > acrescente `seudominio.com` e `www.seudominio.com` (80 e 443, com "Exigir Indicação de Nome de Servidor" marcado e o certificado do domínio).
4. Em `C:\NexusPortal\config\portal.json`, troque `Accounts.PublicBaseUrl` para `https://seudominio.com` (os links dos e-mails usam este endereço) e rode `Restart-Service NexusPortalApi, NexusPortalWorker`.
5. Confira em uma janela anônima: página inicial, login, cadastro e um e-mail de recuperação.

### Se precisar voltar

```powershell
& "$env:windir\system32\inetsrv\appcmd.exe" restore backup "antes-da-troca"
```

Volta todas as associações do IIS como estavam. Desfaça também o `PublicBaseUrl` e reinicie os serviços.

### Depois da troca

- Redirecionar os endereços antigos mais usados para as páginas novas (regras do URL Rewrite no site NexusPortal).
- Deixar o site antigo em `antigo.seudominio.com` por 30 dias e então desligá-lo. Os bancos `KLVN Studio` e `NexusPKAdmin` continuam intocados até vocês decidirem.
- O subdomínio de teste pode continuar apontando para o portal ou ser removido das associações.
