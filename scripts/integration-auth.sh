#!/usr/bin/env bash
# Teste de integração do cadastro, login e painel contra um SQL Server de LABORATÓRIO (CI ou docker local).
# Cria a réplica das tabelas do jogo, aplica as migrações do portal, cria os logins de permissão mínima
# com senhas aleatórias e roda a API com eles. Nunca aponte para o servidor de produção.
#
# Uso: SQLCMD="docker exec -i <container> /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P <senha> -C -b" \
#      SA_PASSWORD=<senha> SQL_HOST=127.0.0.1,1433 scripts/integration-auth.sh
set -euo pipefail
cd "$(dirname "$0")/.."

: "${SQLCMD:?defina SQLCMD}" "${SA_PASSWORD:?defina SA_PASSWORD}"
SQL_HOST="${SQL_HOST:-127.0.0.1,1433}"
API="http://127.0.0.1:5099"
rnd() { echo "Lab$(openssl rand -hex 10)!"; }
READ_PW=$(rnd); WRITE_PW=$(rnd); APP_PW=$(rnd); ADMIN_PW=$(rnd)
cs() { echo "Server=$SQL_HOST;Database=$1;User Id=$2;Password=$3;TrustServerCertificate=True"; }

$SQLCMD < database/scripts/dev/banco-jogo-teste.sql > /dev/null
PRISTON_ConnectionStrings__PortalDbDeploy="$(cs PortalDB sa "$SA_PASSWORD")" \
  dotnet run --project backend/src/Priston.DbMigrator -c Release -- --create-database > /dev/null
sed -e "s/<SENHA-LEITURA>/$READ_PW/" -e "s/<SENHA-ESCRITA>/$WRITE_PW/" -e "s/<SENHA-PORTAL>/$APP_PW/" \
  database/scripts/permissoes-portal.sql | $SQLCMD > /dev/null
sed -e "s/<SENHA-ADMIN>/$ADMIN_PW/" database/scripts/permissoes-admin-jogo.sql | $SQLCMD > /dev/null

GameDatabase__ReadConnectionString="$(cs accountdb portal_game_read "$READ_PW")" \
GameDatabase__WriteConnectionString="$(cs accountdb portal_game_write "$WRITE_PW")" \
GameDatabase__AdminConnectionString="$(cs accountdb portal_game_admin "$ADMIN_PW")" \
GameAdmin__VipPlans__2="VIP Ouro" \
PortalDatabase__ConnectionString="$(cs PortalDB portal_app "$APP_PW")" \
  dotnet run --project backend/src/Priston.Api -c Release --no-launch-profile --urls "$API" > /tmp/integration-api.log 2>&1 &
API_PID=$!
trap 'kill $API_PID 2>/dev/null || true' EXIT
for _ in $(seq 1 60); do curl -sf "$API/health/live" > /dev/null && break; sleep 2; done

fail=0
check() { # check <descrição> <status esperado> <método> <caminho> [corpo] [token]
  local args=(-s -o /tmp/integration-body -w '%{http_code}' -X "$3" -H 'Content-Type: application/json' -H "X-Forwarded-For: ${XFF:-198.51.100.20}")
  [ -n "${5:-}" ] && args+=(-d "$5")
  [ -n "${6:-}" ] && args+=(-H "Authorization: Bearer $6")
  local got; got=$(curl "${args[@]}" "$API$4")
  if [ "$got" = "$2" ]; then echo "ok   - $1"; else echo "FAIL - $1 (esperado $2, veio $got): $(cat /tmp/integration-body)"; fail=1; fi
}

check "cadastro cria conta"            201 POST /api/v1/auth/register '{"login":"integ01","password":"senha123","email":"integ01@example.com"}'
check "login repetido (outra caixa)"   409 POST /api/v1/auth/register '{"login":"INTEG01","password":"senha123","email":"outro@example.com"}'
check "e-mail repetido"                409 POST /api/v1/auth/register '{"login":"integ02","password":"senha123","email":"INTEG01@example.com"}'
check "senha errada"                   401 POST /api/v1/auth/login '{"login":"integ01","password":"errada99"}'
check "login correto"                  200 POST /api/v1/auth/login '{"login":"Integ01","password":"senha123"}'
TOKEN=$(python3 -c 'import json;print(json.load(open("/tmp/integration-body"))["token"])')
check "sessão válida"                  200 GET  /api/v1/auth/me '' "$TOKEN"
$SQLCMD -Q "SET NOCOUNT ON; INSERT INTO ServerDB.dbo.LevelTable (szID,Nick,Class,Level) VALUES ('integ01','Integ Char',3,77); INSERT INTO ServerDB.dbo.Vip (PlayerName,IdVip,DateStart,DateEnd,AccountID) VALUES ('Integ Char',1,'2026-01-01','2099-01-01','integ01'); INSERT INTO CoinShopDB.dbo.UserInfo (UserID,UserCoin,LastChange) VALUES ('integ01',10,GETDATE());" > /dev/null
check "painel com dados do jogo"       200 GET  /api/v1/player/panel '' "$TOKEN"
python3 - <<'PY' || fail=1
import json
d = json.load(open("/tmp/integration-body"))
assert d["gameDataAvailable"], "dados do jogo indisponíveis"
assert d["game"]["characters"][0]["name"] == "Integ Char", d["game"]["characters"]
assert d["game"]["vip"]["active"] and d["game"]["coins"] == 10, d["game"]
print("ok   - painel lê personagens, VIP e coins com o login de leitura")
PY
check "senha atual errada no painel"   400 POST /api/v1/player/password '{"currentPassword":"errada99","newPassword":"nova1234"}' "$TOKEN"
$SQLCMD -d PortalDB -Q "SET QUOTED_IDENTIFIER ON; SET NOCOUNT ON;
INSERT INTO dbo.News (Slug, Category, Title, Summary, Body, Status, PublishedUtc) VALUES
 ('integ-publicada','noticia',N'Publicada',N'r',N'b','published',DATEADD(hour,-1,SYSUTCDATETIME())),
 ('integ-rascunho','noticia',N'Rascunho',N'r',N'b','draft',NULL);
INSERT INTO dbo.GameEvent (Slug, Category, Name, Summary, Description, StartsUtc, Status) VALUES
 ('integ-evento','boss',N'Evento',N'r',N'd',DATEADD(day,1,SYSUTCDATETIME()),'published');" > /dev/null
check "lista de notícias"              200 GET  /api/v1/news
check "notícia publicada"              200 GET  /api/v1/news/integ-publicada
check "rascunho não é público"         404 GET  /api/v1/news/integ-rascunho
check "categoria inválida"             400 GET  "/api/v1/news?categoria=xyz"
check "agenda de eventos"              200 GET  /api/v1/events
check "calendário do mês"              200 GET  "/api/v1/events/calendar?ano=2026&mes=11"
check "arquivo .ics"                   200 GET  /api/v1/events/integ-evento/ics
check "lembrete sem sessão"            401 PUT  /api/v1/player/events/integ-evento/reminder
check "lembrete sem e-mail confirmado" 409 PUT  /api/v1/player/events/integ-evento/reminder '' "$TOKEN"
check "token de jogador no painel"      401 GET  /api/v1/admin/dashboard '' "$TOKEN"
STAFF_PW="Staff-$(openssl rand -hex 8)"
printf '%s\n%s\n' "$STAFF_PW" "$STAFF_PW" | PRISTON_ConnectionStrings__PortalDbDeploy="$(cs PortalDB sa "$SA_PASSWORD")" \
  dotnet run --project backend/src/Priston.DbMigrator -c Release -- --criar-superadmin integadmin > /dev/null
check "login da equipe errado"         401 POST /api/v1/admin/login '{"username":"integadmin","password":"errada-errada-1"}'
STAFF_TOKEN=$(curl -s -X POST -H 'Content-Type: application/json' -H 'X-Forwarded-For: 198.51.100.21' -d "{\"username\":\"integadmin\",\"password\":\"$STAFF_PW\"}" "$API/api/v1/admin/login" | python3 -c 'import json,sys; print(json.load(sys.stdin)["token"])')
check "painel: resumo"                 200 GET  /api/v1/admin/dashboard '' "$STAFF_TOKEN"
check "painel: criar notícia"          200 POST /api/v1/admin/news '{"slug":"integ-painel","category":"noticia","title":"T","summary":"R","body":"B","pinned":false,"status":"published"}' "$STAFF_TOKEN"
check "painel: notícia no site"        200 GET  /api/v1/news/integ-painel
check "painel: equipe"                 200 GET  /api/v1/admin/staff '' "$STAFF_TOKEN"
check "painel: auditoria"              200 GET  /api/v1/admin/audit '' "$STAFF_TOKEN"
# Suporte e status (Fase 11)
check "status sem endereço configurado" 200 GET  /api/v1/status
python3 -c 'import json; d=json.load(open("/tmp/integration-body")); assert d["availability"]=="unknown" and d["playersOnline"] is None and d["targets"]==[], d; print("ok   - status não inventa estado nem jogadores")' || fail=1
python3 - > /tmp/integration.png <<'PY'
import struct, sys, zlib
def chunk(t, d): return struct.pack(">I", len(d)) + t + d + struct.pack(">I", zlib.crc32(t + d))
sys.stdout.buffer.write(b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", 1, 1, 8, 2, 0, 0, 0))
  + chunk(b"tEXt", b"Author\x00SecretoGPS") + chunk(b"IDAT", zlib.compress(b"\x00\xff\x00\x00")) + chunk(b"IEND", b""))
PY
form() { # form <descrição> <status> <caminho> <token> [-F ...]
  local d=$1 want=$2 path=$3 tok=$4; shift 4
  local got; got=$(curl -s -o /tmp/integration-body -w '%{http_code}' -H 'X-Forwarded-For: 198.51.100.22' -H "Authorization: Bearer $tok" "$@" "$API$path")
  if [ "$got" = "$want" ]; then echo "ok   - $d"; else echo "FAIL - $d (esperado $want, veio $got): $(cat /tmp/integration-body)"; fail=1; fi
}
form "chamado: campos inválidos"        400 /api/v1/player/tickets "$TOKEN" -F category=xyz -F subject=oi -F body=
form "chamado: arquivo que não é imagem" 400 /api/v1/player/tickets "$TOKEN" -F category=conta -F subject="Teste integ" -F body="Descrição longa o bastante para passar." -F "files=@/etc/hostname;filename=x.png"
form "chamado: abrir com imagem"        201 /api/v1/player/tickets "$TOKEN" -F category=conta -F subject="Teste integ" -F body="Descrição longa o bastante para passar." -F "files=@/tmp/integration.png;filename=print.png"
TICKET=$(python3 -c 'import json;print(json.load(open("/tmp/integration-body"))["id"])')
check "chamado: lista do jogador"      200 GET  /api/v1/player/tickets '' "$TOKEN"
check "chamado: sem sessão"            401 GET  /api/v1/player/tickets
check "chamado: detalhe"               200 GET  "/api/v1/player/tickets/$TICKET" '' "$TOKEN"
ATT=$(python3 -c 'import json;print(json.load(open("/tmp/integration-body"))["messages"][0]["attachments"][0]["id"])')
curl -s -o /tmp/integration-att -D /tmp/integration-att-h -H "Authorization: Bearer $TOKEN" "$API/api/v1/player/tickets/$TICKET/attachments/$ATT"
if grep -qi "content-type: image/png" /tmp/integration-att-h && ! grep -q "Secreto" /tmp/integration-att; then echo "ok   - anexo volta como PNG sem o texto embutido"; else echo "FAIL - anexo"; fail=1; fi
check "fila da equipe"                 200 GET  "/api/v1/admin/tickets?aguardandoEquipe=true" '' "$STAFF_TOKEN"
form "equipe: nota interna"             204 "/api/v1/admin/tickets/$TICKET/messages" "$STAFF_TOKEN" -F body="nota-interna-integ" -F internal=true
form "equipe: resposta"                 204 "/api/v1/admin/tickets/$TICKET/messages" "$STAFF_TOKEN" -F body="resposta-integ" -F internal=false
check "chamado: jogador vê a resposta" 200 GET  "/api/v1/player/tickets/$TICKET" '' "$TOKEN"
if grep -q "resposta-integ" /tmp/integration-body && ! grep -q "nota-interna-integ" /tmp/integration-body; then echo "ok   - jogador vê a resposta e não a nota interna"; else echo "FAIL - nota interna vazou"; fail=1; fi
check "chamado: jogador encerra"       204 POST "/api/v1/player/tickets/$TICKET/close" '' "$TOKEN"
form "chamado: fechado não aceita resposta" 409 "/api/v1/player/tickets/$TICKET/messages" "$TOKEN" -F body="mais uma"
check "aviso de manutenção"            200 POST /api/v1/admin/notices '{"kind":"manutencao","title":"Reinício","body":"Texto","startsLocal":"2026-11-14T03:00","endsLocal":"2026-11-14T05:00","status":"published"}' "$STAFF_TOKEN"
# Conteúdo do site, banners, imagens, excluir e encerrar (proposta comercial)
check "site: padrão do appsettings"      200 GET  /api/v1/site
check "site: textos do servidor"         200 PUT  /api/v1/admin/site/server '{"name":"NexusPK","tagline":"PvP","about":"Texto","rates":[{"label":"EXP","value":"10x"}],"communityLinks":[{"label":"Discord","url":"https://discord.gg/integ"}]}' "$STAFF_TOKEN"
check "site: link de rede inseguro"      400 PUT  /api/v1/admin/site/server '{"name":"NexusPK","communityLinks":[{"label":"X","url":"javascript:alert(1)"}]}' "$STAFF_TOKEN"
check "site: downloads"                  200 PUT  /api/v1/admin/site/downloads '{"intro":"Baixe","requirements":"Windows","items":[{"name":"Cliente","url":"https://cdn.example.test/c.exe","size":"1 GB"}]}' "$STAFF_TOKEN"
check "site: sem sessão da equipe"       401 PUT  /api/v1/admin/site/downloads '{"items":[]}'
check "site: público atualizado"         200 GET  /api/v1/site
python3 -c 'import json; d=json.load(open("/tmp/integration-body")); assert d["server"]["communityLinks"][0]["label"]=="Discord" and d["downloads"]["items"][0]["name"]=="Cliente", d; print("ok   - site mostra redes e downloads salvos")' || fail=1
form "mídia: arquivo que não é imagem"   400 /api/v1/admin/media "$STAFF_TOKEN" -F "file=@/etc/hostname;filename=x.png"
form "mídia: enviar imagem"              200 /api/v1/admin/media "$STAFF_TOKEN" -F "file=@/tmp/integration.png;filename=banner.png"
MEDIA=$(python3 -c 'import json;print(json.load(open("/tmp/integration-body"))["id"])')
curl -s -o /tmp/integration-media -D /tmp/integration-media-h "$API/api/v1/media/$MEDIA"
if grep -qi "content-type: image/png" /tmp/integration-media-h && grep -qi "immutable" /tmp/integration-media-h && ! grep -q "Secreto" /tmp/integration-media; then echo "ok   - imagem pública sem metadados e com cache longo"; else echo "FAIL - imagem pública"; fail=1; fi
check "banner: criar"                    200 POST /api/v1/admin/banners "{\"title\":\"Banner integ\",\"imageUrl\":\"/midia/$MEDIA\",\"linkUrl\":\"/eventos\",\"sortOrder\":1,\"active\":true}" "$STAFF_TOKEN"
BANNER=$(python3 -c 'import json;print(json.load(open("/tmp/integration-body"))["id"])')
check "banner: na home"                  200 GET  /api/v1/home
python3 -c 'import json; d=json.load(open("/tmp/integration-body")); assert d["banners"][0]["title"]=="Banner integ" and d["server"]["communityLinks"], d; print("ok   - home traz banner e redes")' || fail=1
check "banner: excluir"                  200 DELETE "/api/v1/admin/banners/$BANNER" '' "$STAFF_TOKEN"
NEWS_ID=$($SQLCMD -h -1 -W -Q "SET NOCOUNT ON; SELECT Id FROM PortalDB.dbo.News WHERE Slug='integ-rascunho'" | tr -d '\r' | head -1)
check "notícia: excluir"                 200 DELETE "/api/v1/admin/news/$NEWS_ID" '' "$STAFF_TOKEN"
check "notícia: excluída some"           404 GET  "/api/v1/admin/news/$NEWS_ID" '' "$STAFF_TOKEN"
$SQLCMD -d PortalDB -Q "SET QUOTED_IDENTIFIER ON; SET NOCOUNT ON; INSERT INTO dbo.GameEvent (Slug, Category, Name, Summary, Description, StartsUtc, EndsUtc, Status) VALUES ('integ-agora','exp',N'Agora',N'r',N'd',DATEADD(hour,-1,SYSUTCDATETIME()),DATEADD(hour,2,SYSUTCDATETIME()),'published');" > /dev/null
EVENT_ID=$($SQLCMD -h -1 -W -Q "SET NOCOUNT ON; SELECT Id FROM PortalDB.dbo.GameEvent WHERE Slug='integ-agora'" | tr -d '\r' | head -1)
check "evento: encerrar agora"           200 POST "/api/v1/admin/events/$EVENT_ID/end" '' "$STAFF_TOKEN"
check "evento: já encerrado"             400 POST "/api/v1/admin/events/$EVENT_ID/end" '' "$STAFF_TOKEN"
check "evento: excluir"                  200 DELETE "/api/v1/admin/events/$EVENT_ID" '' "$STAFF_TOKEN"
# Segurança (Fase 12): entradas hostis não quebram nada nem vazam dados.
for q in "%27%20OR%201%3D1--" "%27%3BDROP%20TABLE%20dbo.News--" "%5B%5D%25_" "%3Cscript%3Ealert(1)%3C%2Fscript%3E"; do
  code=$(curl -s -o /tmp/integration-body -w '%{http_code}' -H 'X-Forwarded-For: 198.51.100.30' "$API/api/v1/characters?q=$q")
  if [ "$code" = "200" ] || [ "$code" = "400" ]; then echo "ok   - busca de personagem com entrada hostil ($code)"; else echo "FAIL - busca com '$q' respondeu $code"; fail=1; fi
done
for path in "/api/v1/characters/%27%20OR%201%3D1--" "/api/v1/news/%27%20OR%201%3D1--" "/api/v1/rankings/level?periodo=%27--&classe=-1&pagina=-5&porPagina=100000" "/api/v1/news?pagina=-1"; do
  code=$(curl -s -o /dev/null -w '%{http_code}' -H 'X-Forwarded-For: 198.51.100.30' "$API$path")
  case "$code" in 200|400|404) echo "ok   - $path responde $code";; *) echo "FAIL - $path respondeu $code"; fail=1;; esac
done
check "fila com login hostil"          200 GET  "/api/v1/admin/tickets?situacao=todos&login=%27%20OR%201%3D1--" '' "$STAFF_TOKEN"
python3 -c 'import json; d=json.load(open("/tmp/integration-body")); assert d["total"]==0, d; print("ok   - filtro hostil não devolve chamados de outros")' || fail=1
check "token de jogador na fila"       401 GET  /api/v1/admin/tickets '' "$TOKEN"
check "token inventado"                401 GET  /api/v1/player/panel '' "abc.def.ghi"
check "chamado de outro id"            404 GET  /api/v1/player/tickets/999999 '' "$TOKEN"
head -c 12000000 /dev/zero > /tmp/integration-big.bin
form "corpo acima do limite"            413 /api/v1/player/tickets "$TOKEN" -F category=conta -F subject="Grande demais" -F body="Descrição longa o bastante para passar." -F "files=@/tmp/integration-big.bin;filename=a.png"
hdr=$(curl -s -D - -o /dev/null "$API/api/v1/news")
for h in "x-content-type-options: nosniff" "x-frame-options: deny" "content-security-policy: default-src 'none'"; do
  echo "$hdr" | tr 'A-Z' 'a-z' | grep -q "$h" && echo "ok   - cabeçalho $h" || { echo "FAIL - falta cabeçalho $h"; fail=1; }
done
echo "$hdr" | grep -qi "^server:" && { echo "FAIL - cabeçalho Server exposto"; fail=1; } || echo "ok   - sem cabeçalho Server"
limited=0
for _ in $(seq 1 12); do
  c=$(curl -s -o /dev/null -w '%{http_code}' -X POST -H 'Content-Type: application/json' -H 'X-Forwarded-For: 198.51.100.40' -d '{"login":"naoexiste","password":"errada99"}' "$API/api/v1/auth/login")
  [ "$c" = "429" ] && limited=1
done
[ $limited = 1 ] && echo "ok   - limite de tentativas de login por IP" || { echo "FAIL - login sem limite por IP"; fail=1; }
check "sair"                           204 POST /api/v1/auth/logout '' "$TOKEN"
check "sessão revogada"                401 GET  /api/v1/auth/me '' "$TOKEN"
# Painel da equipe: contas, coins, VIP, banimento, rankings e editores do jogo (login portal_game_admin)
GA="/api/v1/admin"
check "contas: busca"                  200 GET  "$GA/accounts?q=integ" '' "$STAFF_TOKEN"
python3 -c 'import json; d=json.load(open("/tmp/integration-body")); assert any(h["login"]=="integ01" for h in d["data"]), d; print("ok   - busca acha a conta do jogo")' || fail=1
check "contas: busca por personagem"   200 GET  "$GA/accounts?q=Integ%20Ch" '' "$STAFF_TOKEN"
check "contas: detalhe e diagnóstico"  200 GET  "$GA/accounts/integ01" '' "$STAFF_TOKEN"
python3 -c 'import json; d=json.load(open("/tmp/integration-body"))["data"]; assert d["coins"]==10 and d["characters"][0]["name"]=="Integ Char" and d["vips"] and d["diagnostics"], d; print("ok   - detalhe traz coins, personagem, VIP e diagnóstico")' || fail=1
check "contas: inexistente"            404 GET  "$GA/accounts/naoexiste99" '' "$STAFF_TOKEN"
check "contas: token de jogador"       401 GET  "$GA/accounts?q=integ" '' "$TOKEN"
check "coins: sem motivo"              400 POST "$GA/accounts/integ01/coins" '{"mode":"add","amount":5,"reason":""}' "$STAFF_TOKEN"
check "coins: somar"                   200 POST "$GA/accounts/integ01/coins" '{"mode":"add","amount":5,"reason":"teste de integração"}' "$STAFF_TOKEN"
check "coins: tirar demais"            400 POST "$GA/accounts/integ01/coins" '{"mode":"remove","amount":999,"reason":"teste de integração"}' "$STAFF_TOKEN"
check "coins: definir"                 200 POST "$GA/accounts/integ01/coins" '{"mode":"set","amount":42,"reason":"teste de integração"}' "$STAFF_TOKEN"
coins=$($SQLCMD -h -1 -W -Q "SET NOCOUNT ON; SELECT UserCoin FROM CoinShopDB.dbo.UserInfo WHERE UserID='integ01'" | tr -d '\r' | head -1)
[ "$coins" = "42" ] && echo "ok   - coins gravados no UserInfo" || { echo "FAIL - coins no UserInfo: $coins"; fail=1; }
check "vip: adicionar"                 200 POST "$GA/accounts/integ01/vip" '{"plan":2,"character":"Integ Char","days":7,"reason":"teste de integração"}' "$STAFF_TOKEN"
check "vip: personagem de outra conta" 400 POST "$GA/accounts/integ01/vip" '{"plan":2,"character":"Ninguem","days":7,"reason":"teste de integração"}' "$STAFF_TOKEN"
check "contas: detalhe com 2 VIPs"     200 GET  "$GA/accounts/integ01" '' "$STAFF_TOKEN"
VIPROW=$(python3 -c 'import json; v=[x for x in json.load(open("/tmp/integration-body"))["data"]["vips"] if x["plan"]==2][0]; print(json.dumps({"playerName":v["playerName"],"plan":v["plan"],"startLocal":v["startLocal"],"endLocal":v["endLocal"]}))')
check "vip: estender"                  200 POST "$GA/accounts/integ01/vip/extend" "{\"row\":$VIPROW,\"days\":3,\"reason\":\"teste de integração\"}" "$STAFF_TOKEN"
check "vip: linha antiga já mudou"     409 POST "$GA/accounts/integ01/vip/extend" "{\"row\":$VIPROW,\"days\":3,\"reason\":\"teste de integração\"}" "$STAFF_TOKEN"
check "contas: detalhe após estender"  200 GET  "$GA/accounts/integ01" '' "$STAFF_TOKEN"
VIPROW=$(python3 -c 'import json; v=[x for x in json.load(open("/tmp/integration-body"))["data"]["vips"] if x["plan"]==2][0]; print(json.dumps({"playerName":v["playerName"],"plan":v["plan"],"startLocal":v["startLocal"],"endLocal":v["endLocal"]}))')
check "vip: tirar com senha errada"    400 POST "$GA/accounts/integ01/vip/remove" "{\"row\":$VIPROW,\"reason\":\"teste de integração\",\"password\":\"errada-errada\"}" "$STAFF_TOKEN"
check "vip: tirar"                     200 POST "$GA/accounts/integ01/vip/remove" "{\"row\":$VIPROW,\"reason\":\"teste de integração\",\"password\":\"$STAFF_PW\"}" "$STAFF_TOKEN"
vips=$($SQLCMD -h -1 -W -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM ServerDB.dbo.Vip WHERE AccountID='integ01'" | tr -d '\r' | head -1)
[ "$vips" = "1" ] && echo "ok   - só o VIP original ficou" || { echo "FAIL - VIPs restantes: $vips"; fail=1; }
check "ban: sem senha"                 400 POST "$GA/accounts/integ01/ban" '{"reason":"teste de integração","days":3}' "$STAFF_TOKEN"
check "ban: banir 3 dias"              200 POST "$GA/accounts/integ01/ban" "{\"reason\":\"teste de integração\",\"days\":3,\"password\":\"$STAFF_PW\"}" "$STAFF_TOKEN"
blk=$($SQLCMD -h -1 -W -Q "SET NOCOUNT ON; SELECT BlockChk FROM accountdb.dbo.IGameUser WHERE userid='integ01'" | tr -d '\r' | head -1)
[ "$blk" = "1" ] && echo "ok   - BlockChk = 1 no jogo" || { echo "FAIL - BlockChk: $blk"; fail=1; }
XFF=198.51.100.24 check "ban: login do jogador bloqueado" 403 POST /api/v1/auth/login '{"login":"integ01","password":"senha123"}'
check "ban: desbanir"                  200 POST "$GA/accounts/integ01/unban" '{"reason":"teste de integração"}' "$STAFF_TOKEN"
check "ban: histórico no detalhe"      200 GET  "$GA/accounts/integ01" '' "$STAFF_TOKEN"
python3 -c 'import json; d=json.load(open("/tmp/integration-body"))["data"]; assert not d["blocked"] and d["bans"] and d["activity"], d; print("ok   - desbanido, com histórico e auditoria")' || fail=1
# Rankings: zera com cópia e restaura
$SQLCMD -Q "SET NOCOUNT ON; INSERT INTO UserDB.dbo.BattlePoints (Account,Nick,BPs,DPs,Class,BPsSemanal,KillsPvP,MortesPvP,PontosPvP,IsReiPvP,SodSemanal) VALUES ('integ01','Integ Char',100,5,3,77,9,2,50,0,33);" > /dev/null
check "rankings: tela"                 200 GET  "$GA/rankings" '' "$STAFF_TOKEN"
check "rankings: palavra errada"       400 POST "$GA/rankings/reset" "{\"kind\":\"pvp-semanal\",\"confirm\":\"zerar?\",\"reason\":\"teste de integração\",\"password\":\"$STAFF_PW\"}" "$STAFF_TOKEN"
check "rankings: zerar PvP semanal"    200 POST "$GA/rankings/reset" "{\"kind\":\"pvp-semanal\",\"confirm\":\"ZERAR\",\"reason\":\"teste de integração\",\"password\":\"$STAFF_PW\"}" "$STAFF_TOKEN"
bp=$($SQLCMD -h -1 -W -Q "SET NOCOUNT ON; SELECT CAST(BPsSemanal AS varchar)+'|'+CAST(BPs AS varchar) FROM UserDB.dbo.BattlePoints WHERE Account='integ01'" | tr -d '\r' | head -1)
[ "$bp" = "0|100" ] && echo "ok   - só o PvP semanal foi zerado" || { echo "FAIL - BattlePoints depois de zerar: $bp"; fail=1; }
BACKUP=$($SQLCMD -h -1 -W -Q "SET NOCOUNT ON; SELECT MAX(Id) FROM PortalDB.dbo.RankingResetBackup" | tr -d '\r' | head -1)
check "rankings: restaurar cópia"      200 POST "$GA/rankings/backups/$BACKUP/restore" "{\"reason\":\"teste de integração\",\"password\":\"$STAFF_PW\"}" "$STAFF_TOKEN"
bp=$($SQLCMD -h -1 -W -Q "SET NOCOUNT ON; SELECT BPsSemanal FROM UserDB.dbo.BattlePoints WHERE Account='integ01'" | tr -d '\r' | head -1)
[ "$bp" = "77" ] && echo "ok   - PvP semanal restaurado" || { echo "FAIL - BPsSemanal restaurado: $bp"; fail=1; }
check "rankings: restaurar de novo"    409 POST "$GA/rankings/backups/$BACKUP/restore" "{\"reason\":\"teste de integração\",\"password\":\"$STAFF_PW\"}" "$STAFF_TOKEN"
check "servidor: sem processos"        200 GET  "$GA/server" '' "$STAFF_TOKEN"
# Editor genérico: liga a "Loja NPC" a uma tabela de laboratório e edita uma linha
$SQLCMD -Q "SET NOCOUNT ON; CREATE TABLE ServerDB.dbo.IntegLojaNpc (NpcId int NOT NULL, ItemCode varchar(20) NOT NULL, Preco int NOT NULL, Nome varchar(40) NULL, Criado datetime NULL);
INSERT INTO ServerDB.dbo.IntegLojaNpc VALUES (1,'WA101',100,'Espada',GETDATE()),(1,'WA102',200,'Machado [x]%',GETDATE());" > /dev/null
check "jogo: funções"                  200 GET  "$GA/game-tables" '' "$STAFF_TOKEN"
check "jogo: não configurada"          409 GET  "$GA/game-tables/npc-shops" '' "$STAFF_TOKEN"
check "config: tabelas do banco"       200 GET  "$GA/game-tables/npc-shops/config/tables?banco=ServerDB" '' "$STAFF_TOKEN"
check "config: colunas"                200 GET  "$GA/game-tables/npc-shops/config/columns?banco=ServerDB&tabela=IntegLojaNpc" '' "$STAFF_TOKEN"
check "config: coluna inventada"       400 PUT  "$GA/game-tables/npc-shops/config" "{\"database\":\"ServerDB\",\"table\":\"IntegLojaNpc\",\"keyColumns\":[\"NpcId\",\"Inventada\"],\"editableColumns\":[\"Preco\"],\"searchColumns\":[],\"allowInsert\":true,\"allowDelete\":true,\"newestFirst\":false,\"password\":\"$STAFF_PW\"}" "$STAFF_TOKEN"
check "config: tabela hostil"          400 PUT  "$GA/game-tables/npc-shops/config" "{\"database\":\"ServerDB\",\"table\":\"x];DROP TABLE dbo.Vip--\",\"keyColumns\":[\"NpcId\"],\"editableColumns\":[],\"searchColumns\":[],\"allowInsert\":false,\"allowDelete\":false,\"newestFirst\":false,\"password\":\"$STAFF_PW\"}" "$STAFF_TOKEN"
check "config: salvar"                 200 PUT  "$GA/game-tables/npc-shops/config" "{\"database\":\"ServerDB\",\"table\":\"IntegLojaNpc\",\"keyColumns\":[\"NpcId\",\"ItemCode\"],\"editableColumns\":[\"Preco\",\"Nome\"],\"searchColumns\":[\"ItemCode\",\"Nome\"],\"allowInsert\":true,\"allowDelete\":true,\"newestFirst\":false,\"password\":\"$STAFF_PW\"}" "$STAFF_TOKEN"
check "jogo: listar linhas"            200 GET  "$GA/game-tables/npc-shops" '' "$STAFF_TOKEN"
python3 -c 'import json; d=json.load(open("/tmp/integration-body"))["data"]; assert len(d["rows"])==2 and not d["hasMore"], d; print("ok   - editor lista as 2 linhas")' || fail=1
check "jogo: busca com % e ["          200 GET  "$GA/game-tables/npc-shops?f.Nome=Machado%20%5Bx%5D%25" '' "$STAFF_TOKEN"
python3 -c 'import json; d=json.load(open("/tmp/integration-body"))["data"]; assert len(d["rows"])==1 and d["rows"][0]["ItemCode"]=="WA102", d; print("ok   - busca pelo começo do nome, com [ e %")' || fail=1
check "jogo: % não é curinga"          200 GET  "$GA/game-tables/npc-shops?f.Nome=%25" '' "$STAFF_TOKEN"
python3 -c 'import json; d=json.load(open("/tmp/integration-body"))["data"]; assert len(d["rows"])==0, d; print("ok   - % digitado é texto, não curinga")' || fail=1
check "jogo: editar preço"             200 PUT  "$GA/game-tables/npc-shops/rows" '{"keys":{"NpcId":"1","ItemCode":"WA101"},"values":{"Preco":"150"},"reason":"teste de integração"}' "$STAFF_TOKEN"
check "jogo: preço não numérico"       400 PUT  "$GA/game-tables/npc-shops/rows" '{"keys":{"NpcId":"1","ItemCode":"WA101"},"values":{"Preco":"abc"},"reason":"teste de integração"}' "$STAFF_TOKEN"
check "jogo: coluna não liberada"      400 PUT  "$GA/game-tables/npc-shops/rows" '{"keys":{"NpcId":"1","ItemCode":"WA101"},"values":{"Criado":"2026-01-01T00:00:00"},"reason":"teste de integração"}' "$STAFF_TOKEN"
check "jogo: chave que não existe"     409 PUT  "$GA/game-tables/npc-shops/rows" '{"keys":{"NpcId":"9","ItemCode":"ZZ"},"values":{"Preco":"1"},"reason":"teste de integração"}' "$STAFF_TOKEN"
check "jogo: inserir"                  200 POST "$GA/game-tables/npc-shops/rows" '{"values":{"NpcId":"1","ItemCode":"WA103","Preco":"300","Nome":"Arco"},"reason":"teste de integração"}' "$STAFF_TOKEN"
check "jogo: chave repetida (2 linhas)" 409 POST "$GA/game-tables/npc-shops/rows" '{"values":{"NpcId":"1","ItemCode":"WA103","Preco":"300","Nome":"Arco"},"reason":"teste de integração"}' "$STAFF_TOKEN"
check "jogo: apagar sem senha"         400 POST "$GA/game-tables/npc-shops/rows/delete" '{"keys":{"NpcId":"1","ItemCode":"WA102"},"reason":"teste de integração"}' "$STAFF_TOKEN"
check "jogo: apagar"                   200 POST "$GA/game-tables/npc-shops/rows/delete" "{\"keys\":{\"NpcId\":\"1\",\"ItemCode\":\"WA102\"},\"reason\":\"teste de integração\",\"password\":\"$STAFF_PW\"}" "$STAFF_TOKEN"
loja=$($SQLCMD -h -1 -W -Q "SET NOCOUNT ON; SELECT STRING_AGG(ItemCode+':'+CAST(Preco AS varchar), ',') WITHIN GROUP (ORDER BY ItemCode) FROM ServerDB.dbo.IntegLojaNpc" | tr -d '\r' | head -1)
[ "$loja" = "WA101:150,WA103:300" ] && echo "ok   - tabela do jogo com as mudanças certas" || { echo "FAIL - tabela de laboratório: $loja"; fail=1; }
check "config: remover"                200 POST "$GA/game-tables/npc-shops/config/remove" "{\"password\":\"$STAFF_PW\",\"reason\":\"teste de integração\"}" "$STAFF_TOKEN"
check "auditoria do jogo"              200 GET  "$GA/audit" '' "$STAFF_TOKEN"
grep -q "game-account" /tmp/integration-body && echo "ok   - ações nas contas estão na auditoria" || { echo "FAIL - auditoria sem ações do jogo"; fail=1; }

row=$($SQLCMD -h -1 -W -Q "SET NOCOUNT ON; SELECT userid+'|'+GPCode+'|'+inuse+'|'+Grade+'|'+BlockChk+'|'+DelChk FROM accountdb.dbo.IGameUser")
[ "$(echo "$row" | tr -d '\r' | head -1)" = "integ01|PTP-RUD001|0|U|0|0" ] && echo "ok   - linha no IGameUser no formato do jogo" || { echo "FAIL - linha no IGameUser: $row"; fail=1; }
if grep -q -e "HTTP/1.1 500\|responded 500\| 500 in " /tmp/integration-api.log; then echo "FAIL - a API respondeu 500 em algum teste"; fail=1; else echo "ok   - nenhum erro 500 no log da API"; fi
if grep -q -e "$TOKEN" -e "$STAFF_TOKEN" /tmp/integration-api.log; then echo "FAIL - token apareceu no log"; fail=1; else echo "ok   - tokens fora do log"; fi
if grep -q -e "senha123" -e "$STAFF_PW" /tmp/integration-api.log; then echo "FAIL - senha apareceu no log"; fail=1; else echo "ok   - senha fora do log"; fi
exit $fail
