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

| Função | Resumo | Notícias e eventos | Auditoria | Equipe |
|---|---|---|---|---|
| Superadministrador | sim | sim | sim | sim |
| Administrador | sim | sim | sim | não |
| Moderador | sim | sim | não | não |
| Suporte | sim | não | não | não |

O suporte ganha os chamados na Fase 11.

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
| Servidor online | teste de porta do Worker |
| Jogadores online | sem fonte até o servidor do jogo publicar (aparece "sem fonte") |
| Cadastros | `account.register` na auditoria do portal (só cadastros feitos no portal novo) |
| Notícias e eventos | tabelas do portal |
| Integração SQL | health checks dos dois bancos |

Fonte que falha aparece como "indisponível", nunca como zero.
