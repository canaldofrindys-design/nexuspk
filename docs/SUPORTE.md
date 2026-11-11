# Suporte e monitoramento (Fase 11)

## Central de suporte

Tudo fica no banco do portal (migração `0006`). O banco do jogo não é tocado.

| Tabela | Conteúdo |
|---|---|
| `dbo.SupportTicket` | Chamado: login do jogo (dono), categoria, assunto, situação, prioridade, atendente, quem escreveu por último |
| `dbo.SupportMessage` | Mensagens. `Internal = 1` é nota da equipe e nunca chega ao jogador |
| `dbo.SupportAttachment` | Imagens anexadas (até 3 MB cada, até 3 por mensagem), gravadas no banco já limpas |

### Jogador (`/painel/suporte`)

- Abre chamado escolhendo a categoria (conta, personagem, erro no jogo, denúncia, eventos, loja/coins, outro), assunto e descrição, com até 3 imagens.
- Acompanha a conversa, responde e encerra. Chamado fechado não recebe mensagem; o jogador abre outro.
- Cada conta pode ter até 3 chamados em andamento (aberto, em atendimento ou aguardando jogador).
- Quando a equipe responde, o jogador recebe um e-mail com o link do chamado, sem o texto da resposta, se o e-mail da conta estiver confirmado.
- O painel do jogador mostra os chamados em andamento; a lista marca "resposta nova".

### Equipe (`/admin/chamados`)

Permissão `ManageSupport`: superadministrador, administrador, moderador e suporte.

- Fila padrão "Esperando a equipe": chamados abertos ou em atendimento cuja última mensagem é do jogador, prioridade alta primeiro e o mais antigo antes.
- Filtros por situação, categoria, "só os meus" e login do jogador.
- No chamado: responder (padrão: "aguardando jogador"), nota interna, anexos, mudar situação, prioridade e atendente.
- O jogador vê o nome de exibição do atendente, nunca o usuário de login da equipe.

### Situações

| Situação | Quando |
|---|---|
| Aberto | Chamado novo, ou o jogador respondeu |
| Em atendimento | A equipe assumiu (continua assim quando o jogador responde) |
| Aguardando jogador | A equipe respondeu; para o jogador aparece "Respondido pela equipe" |
| Resolvido | A equipe considera resolvido; se o jogador responder, volta para "Aberto" |
| Fechado | Encerrado pelo jogador ou pela equipe |

### Imagens

O arquivo é conferido pela estrutura (PNG, JPEG ou BMP), não pela extensão. Antes de gravar, o portal remove EXIF, XMP, IPTC, comentários e textos do PNG (localização, câmera, nome do autor) e descarta qualquer dado depois do fim da imagem. Imagens acima de 10.000 px de lado são recusadas. O nome do arquivo é limpo e a extensão vem do tipo real.

Os anexos só saem pela rota do painel de quem pode ver a mensagem (`/painel/suporte/<n>/anexo/...` para o dono, `/admin/chamados/<n>/anexo/...` para a equipe), com `Content-Security-Policy: sandbox` e `nosniff`. Anexo de nota interna devolve 404 ao jogador.

### Auditoria

`ticket.created`, `ticket.player-replied`, `ticket.closed`, `ticket.replied`, `ticket.note`, `ticket.updated`. A auditoria guarda situação, prioridade, atendente e quantidade de anexos, nunca o texto das mensagens.

### Site antigo

O banco `KLVN` (site mais antigo) tem uma tabela `Tickets`. Os chamados antigos não foram importados; a importação só acontece se for pedida e com o schema confirmado.

## Monitoramento (`/status`)

### Fontes

| Item | Fonte |
|---|---|
| Login e canais online/offline | Conexão TCP na porta de cada endereço de `GameServer:Targets`, feita pela API (cache de 30 s) |
| Tempo de resposta | Duração dessa conexão |
| Histórico (24 h e 7 dias) | `dbo.ServerProbe`, gravado pelo Worker a cada minuto (`ServerProbeJob`), guardado por 30 dias |
| Manutenções e avisos | `dbo.ServerNotice`, cadastrados em `/admin/avisos` |
| Jogadores online | Sem fonte: o servidor do jogo não grava quem está conectado (`DATABASE_MAPPING.md`, seção 8). A página diz isso em vez de mostrar um número |

O histórico só existe a partir da primeira medição do Worker; a página mostra "Histórico medido desde ...". Hora sem medição aparece cinza e não conta no percentual.

Porta aberta quer dizer que o programa está aceitando conexões, não que o jogo esteja sem lentidão. A página diz isso.

### Configuração

Na API e no Worker (`appsettings.Production.json` ou variáveis de ambiente):

```json
"GameServer": {
  "TimeoutMilliseconds": 2000,
  "Targets": [
    { "Key": "login",   "Name": "Login",   "Host": "127.0.0.1", "Port": 0 },
    { "Key": "canal-1", "Name": "Canal 1", "Host": "127.0.0.1", "Port": 0 }
  ]
}
```

Troque `Port` pelas portas reais do login e de cada canal (as mesmas que o cliente do jogo usa para o login e para cada canal). A `Key` é a chave do histórico: não mude depois de ligar. Sem nenhum endereço, a página mostra "Sem verificação configurada" e o Worker não grava nada. `Host`/`Port` soltos (formato anterior) continuam valendo como um endereço só, chamado "Servidor".

### Avisos

Tipos: aviso e manutenção. Manutenção exige previsão de fim. Publicados aparecem no topo de `/status` enquanto estão ativos e em "Próximas manutenções e avisos" antes de começar. Publicar uma manutenção não desliga nada no servidor.
