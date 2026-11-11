# Segurança e desempenho (Fase 12)

Este documento descreve o que o portal faz para se proteger, o que foi testado e o que ainda depende da implantação (Fase 13).

## 1. Banco de dados

| Login SQL | Banco | Permissões | Uso |
|---|---|---|---|
| `portal_game_read` | jogo | só `SELECT` nas tabelas mapeadas | personagens, ranking, login |
| `portal_game_write` | `accountdb` | `SELECT`, `INSERT`, `UPDATE` em `<letra>GameUser` | cadastro e troca de senha. Vazio = desligado |
| `portal_app` | PortalDB | leitura e escrita só no banco do portal | sessões, conteúdo, chamados, auditoria |

- Todas as consultas usam parâmetros (Dapper). O único texto montado na consulta é o nome da tabela do shard, escolhido de uma lista fixa (`<letra>GameUser`), nunca vindo do usuário.
- As connection strings ficam em variáveis de ambiente do servidor. O frontend não tem nenhuma: ele só fala com a API.
- O SQL Server não deve ficar exposto à internet. Na VPS, a porta 1433 fica fechada no firewall e a API conecta por `127.0.0.1`.
- O banco do jogo não teve a estrutura alterada. Tudo que é do portal fica no PortalDB (migrações `0001` a `0006`).

**Limite conhecido:** o jogo grava a senha em texto em `GameUser.Passwd`, e o servidor compara assim. O portal não pode trocar esse formato sem mudar o servidor do jogo. O que o portal faz: compara em tempo constante, nunca grava a senha em outro lugar, nunca a escreve em log e nunca a devolve em nenhuma resposta.

## 2. Senhas, sessões e cookies

| Item | Como é |
|---|---|
| Senha da equipe | PBKDF2-SHA256, 600 mil iterações, sal de 16 bytes |
| Token de sessão (jogador e equipe) | 32 bytes aleatórios; o banco guarda só o SHA-256 |
| Links de e-mail (confirmação, nova senha) | só o SHA-256 no banco, uso único, 48 h e 30 min |
| Cookie do jogador | `HttpOnly`, `Secure` em produção, `SameSite=Lax`, caminho `/` |
| Cookie da equipe | `HttpOnly`, `Secure` em produção, `SameSite=Strict`, caminho `/admin` |
| Troca de senha | encerra as outras sessões |

O navegador nunca vê o token em JavaScript. O Next lê o cookie no servidor e repassa o token à API no cabeçalho `Authorization`.

## 3. Contas e recuperação

- Login errado e senha errada dão a mesma resposta. Conta bloqueada só é informada com a senha correta.
- 5 falhas por login ou 20 por IP bloqueiam novas tentativas por 15 minutos (`Accounts__MaxFailuresPerLogin`, `MaxFailuresPerIp`, `LockoutMinutes`).
- Recuperação: a resposta é sempre a mesma, exista a conta ou não. No máximo 3 e-mails por hora por conta.
- O e-mail de resposta do suporte leva só o link do chamado, nunca o texto.

## 4. Limite de requisições

| Regra | Limite | Onde |
|---|---|---|
| Geral | 120 por minuto por IP | toda a API |
| Sensível (`auth`) | 10 por minuto por IP, janela deslizante | login, cadastro, recuperação, troca de senha, login da equipe, abrir e responder chamado, lembretes de evento |

Chamadas internas do próprio Next (rede local) passam sem limite; o IP do jogador vai no `X-Forwarded-For`, e a API usa a última entrada, que é a que o IIS acrescentou.

## 5. CSRF e XSS

- Os formulários usam Server Actions do Next, que conferem o cabeçalho `Origin` contra o host. O cookie da equipe é `SameSite=Strict`.
- O React escapa todo texto. O texto de notícias e eventos é montado em elementos React (parágrafos, subtítulos, listas) a partir de texto puro; o portal não usa `dangerouslySetInnerHTML` em lugar nenhum.
- Cabeçalhos em todas as páginas (`frontend/next.config.ts`):

| Cabeçalho | Valor |
|---|---|
| `Content-Security-Policy` | `default-src 'self'`; scripts só do próprio site e do Turnstile da Cloudflare; `object-src 'none'`; `base-uri 'self'`; `form-action 'self'`; `frame-ancestors 'none'` |
| `Strict-Transport-Security` | `max-age=31536000; includeSubDomains` |
| `X-Content-Type-Options` | `nosniff` |
| `X-Frame-Options` | `DENY` |
| `Referrer-Policy` | `strict-origin-when-cross-origin` |
| `Cross-Origin-Opener-Policy` | `same-origin` |

O `script-src` ainda tem `'unsafe-inline'` porque o Next injeta scripts inline para a renderização em partes (streaming). Trocar por nonce obriga toda página a ser dinâmica e perde o cache das páginas estáticas. O restante da política continua bloqueando scripts de outros domínios, plugins e envio de formulário para fora.

Os anexos do suporte (`/painel/suporte/<id>/anexo/<anexo>` e `/admin/chamados/<id>/anexo/<anexo>`) têm política própria, `default-src 'none'; sandbox`: mesmo que alguém consiga enviar um arquivo disfarçado, o navegador não executa nada. Os anexos já são conferidos pela estrutura (PNG, JPEG, BMP), limpos de metadados e servidos só a quem pode ver a mensagem.

## 6. Autorização

- A API confere a sessão e a permissão em cada chamada; o frontend esconder um botão não é proteção.
- Permissões da equipe: `ViewDashboard`, `ManageContent`, `ViewAudit`, `ManageStaff`, `ManageSupport` (detalhes em `ADMIN.md`).
- O jogador só vê os próprios chamados e anexos. Chamado de outra conta responde 404, sem dizer que existe.
- Notas internas e seus anexos nunca saem para o jogador.

## 7. Auditoria e logs

- `PortalDB.dbo.AuditLog` grava ações de conta, de equipe, de conteúdo e de suporte com usuário, IP e alvo. Nunca senha, token, cookie ou texto de mensagem.
- A API não liga o registro de requisições (`HttpLogging`), então corpo e cabeçalho `Authorization` não vão para o log. O teste de integração confere que nenhum token aparece no log.
- **IIS:** os links de e-mail trazem o token na URL (`/confirmar?token=...`, `/nova-senha?token=...`). O token é de uso único e vence logo, mas o IIS grava a query string no log por padrão. Na Fase 13, desmarque o campo `cs-uri-query` no log do site do portal (IIS > Logging > Select Fields).
- **Logs do servidor do jogo:** `LogFile\(InfoUser)*.log` guarda senhas em texto, IPs e MACs. Esses arquivos não devem ficar em pasta servida pelo IIS nem ser copiados para fora da VPS.

## 8. Dependências

- `npm audit --omit=dev`: 0 vulnerabilidades.
- Desenvolvimento: um aviso moderado no `minimatch` usado pelo `typescript-eslint` (só no lint, não vai para produção).
- NuGet (`dotnet list package --vulnerable --include-transitive`): nenhum. O CI agora falha se aparecer algum.

## 9. Testes de segurança

`scripts/integration-auth.sh` (67 verificações contra SQL Server real) inclui:

- valores hostis (`' OR 1=1--`, `<script>`, `%00`, caminhos com `..`) em busca, filtros e rotas: só 200, 400 ou 404, nunca 500;
- token de jogador na API da equipe e token inventado: 401;
- chamado de outra conta: 404;
- corpo de 12 MB: 413 com mensagem clara;
- cabeçalhos `nosniff`, `X-Frame-Options`, política do anexo e ausência do cabeçalho `Server`;
- 12 tentativas de login seguidas do mesmo IP: 429;
- nenhum erro 500 e nenhum token no log da API.

O e2e de Playwright roda as páginas com a CSP ligada e falha se o navegador registrar alguma violação.

## 10. Desempenho

### Medidas no laboratório

Um processo de cada, máquina de 4 núcleos, 20 conexões simultâneas por 10 s:

| Rota | req/s | mediana |
|---|---|---|
| API `/home` | ~9.200 | 1,6 ms |
| API `/rankings/level` | ~10.000 | 1,6 ms |
| API `/characters` | ~10.500 | 1,5 ms |
| API `/status` | ~9.200 | 1,7 ms |
| API `/news` | ~10.600 | 1,5 ms |
| Página `/` | 78 | 249 ms |
| Página `/ranking` | 95 | 202 ms |
| Página `/noticias` | 104 | 178 ms |
| Página `/status` | 46 | 432 ms |

A API responde do cache na memória (ranking 5 min, notícias e eventos 60 s, histórico de status 60 s, teste de porta 30 s). O gargalo é a renderização das páginas no Next. Para um servidor privado isso dá folga: 50 a 100 páginas por segundo são de 3 a 6 mil por minuto.

Se o tráfego crescer, a ordem recomendada é: (1) rodar 2 ou mais processos do Next atrás do IIS (ARR com balanceamento), (2) cachear as páginas públicas no próprio Next com `use cache` e invalidar nas ações do painel, (3) CDN na frente.

### O que já está feito

- Paginação em todas as listas (personagens, ranking, notícias, chamados, auditoria).
- Índices nas tabelas do portal para cada consulta de lista (ver migrações).
- Cache na API com `IMemoryCache` e tempo curto; cache do navegador de um ano (`immutable`) para os arquivos estáticos do Next.
- Compressão gzip das páginas e arquivos estáticos.
- Imagens com largura e altura e `loading="lazy"` fora da primeira dobra.
- O histórico de disponibilidade guarda 30 dias de testes de porta e apaga o resto.
- SignalR não foi usado: nada do portal precisa de atualização em tempo real; a página de status atualiza a cada minuto.

## 11. Na implantação (Fase 13)

O `Install-Portal.ps1` (ver `INSTALLATION.md`) já faz:

- site do IIS só com o host do portal, HTTPS com o certificado informado e redirecionamento de http (com URL Rewrite);
- log do IIS sem `cs-uri-query`;
- API e Node escutando só em `127.0.0.1`; nenhuma porta nova aberta;
- serviços com contas virtuais (`NT SERVICE\NexusPortalApi`, `NT SERVICE\NexusPortalWorker`), sem senha e sem privilégio de administrador;
- segredos em `C:\NexusPortal\config\portal.json`, legível só por administradores e pelos dois serviços (o site do IIS não lê);
- senhas dos logins SQL geradas na VPS, sem passar por chat ou pela tela;
- backup diário do PortalDB com retenção de 14 dias.

Fica com a equipe: manter a porta 1433 fechada no firewall da VPS, copiar os backups para fora do servidor e renovar o certificado (o win-acme renova sozinho).

Links do menu para páginas que ainda não existem (enciclopédia, download, guia) respondem 404 até as fases correspondentes.
