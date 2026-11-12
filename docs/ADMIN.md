# Painel administrativo (Fase 10)

Endereço: `/admin`. A equipe tem contas próprias no banco do portal (`dbo.StaffUser`, migração `0005`), separadas das contas de jogador e das contas do jogo.

## Primeiro acesso

Depois de aplicar as migrações, crie o superadministrador no próprio servidor:

```powershell
$env:PRISTON_ConnectionStrings__PortalDbDeploy = "<connection string de deploy>"
.\Priston.DbMigrator.exe --criar-superadmin <usuario>
```

A senha é digitada no terminal, sem eco, duas vezes. Ela não passa por linha de comando, arquivo, chat ou log. O comando recusa um usuário que já existe.

## Funções

| Função | Resumo | Notícias, eventos, banners e avisos de status | Conteúdo do site | Chamados | Auditoria | Equipe |
|---|---|---|---|---|---|---|
| Superadministrador | sim | sim | sim | sim | sim | sim |
| Administrador | sim | sim | sim | sim | sim | não |
| Moderador | sim | sim | não | sim | não | não |
| Suporte | sim | não | não | sim | não | não |

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
- Toda operação da equipe vai para `dbo.AuditLog` (login, falha, bloqueio, notícia, evento, equipe). Senhas e tokens nunca são gravados.
- O painel não escreve no banco do jogo.

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
