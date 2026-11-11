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
READ_PW=$(rnd); WRITE_PW=$(rnd); APP_PW=$(rnd)
cs() { echo "Server=$SQL_HOST;Database=$1;User Id=$2;Password=$3;TrustServerCertificate=True"; }

$SQLCMD < database/scripts/dev/banco-jogo-teste.sql > /dev/null
PRISTON_ConnectionStrings__PortalDbDeploy="$(cs PortalDB sa "$SA_PASSWORD")" \
  dotnet run --project backend/src/Priston.DbMigrator -c Release -- --create-database > /dev/null
sed -e "s/<SENHA-LEITURA>/$READ_PW/" -e "s/<SENHA-ESCRITA>/$WRITE_PW/" -e "s/<SENHA-PORTAL>/$APP_PW/" \
  database/scripts/permissoes-portal.sql | $SQLCMD > /dev/null

GameDatabase__ReadConnectionString="$(cs accountdb portal_game_read "$READ_PW")" \
GameDatabase__WriteConnectionString="$(cs accountdb portal_game_write "$WRITE_PW")" \
PortalDatabase__ConnectionString="$(cs PortalDB portal_app "$APP_PW")" \
  dotnet run --project backend/src/Priston.Api -c Release --no-launch-profile --urls "$API" > /tmp/integration-api.log 2>&1 &
API_PID=$!
trap 'kill $API_PID 2>/dev/null || true' EXIT
for _ in $(seq 1 60); do curl -sf "$API/health/live" > /dev/null && break; sleep 2; done

fail=0
check() { # check <descrição> <status esperado> <método> <caminho> [corpo] [token]
  local args=(-s -o /tmp/integration-body -w '%{http_code}' -X "$3" -H 'Content-Type: application/json' -H 'X-Forwarded-For: 198.51.100.20')
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
check "sair"                           204 POST /api/v1/auth/logout '' "$TOKEN"
check "sessão revogada"                401 GET  /api/v1/auth/me '' "$TOKEN"

row=$($SQLCMD -h -1 -W -Q "SET NOCOUNT ON; SELECT userid+'|'+GPCode+'|'+inuse+'|'+Grade+'|'+BlockChk+'|'+DelChk FROM accountdb.dbo.IGameUser")
[ "$(echo "$row" | tr -d '\r' | head -1)" = "integ01|PTP-RUD001|0|U|0|0" ] && echo "ok   - linha no IGameUser no formato do jogo" || { echo "FAIL - linha no IGameUser: $row"; fail=1; }
if grep -q -e "senha123" -e "$STAFF_PW" /tmp/integration-api.log; then echo "FAIL - senha apareceu no log"; fail=1; else echo "ok   - senha fora do log"; fi
exit $fail
