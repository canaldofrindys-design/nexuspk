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
check "sair"                           204 POST /api/v1/auth/logout '' "$TOKEN"
check "sessão revogada"                401 GET  /api/v1/auth/me '' "$TOKEN"

row=$($SQLCMD -h -1 -W -Q "SET NOCOUNT ON; SELECT userid+'|'+GPCode+'|'+inuse+'|'+Grade+'|'+BlockChk+'|'+DelChk FROM accountdb.dbo.IGameUser")
[ "$(echo "$row" | tr -d '\r' | head -1)" = "integ01|PTP-RUD001|0|U|0|0" ] && echo "ok   - linha no IGameUser no formato do jogo" || { echo "FAIL - linha no IGameUser: $row"; fail=1; }
if grep -q "senha123" /tmp/integration-api.log; then echo "FAIL - senha apareceu no log"; fail=1; else echo "ok   - senha fora do log"; fi
exit $fail
